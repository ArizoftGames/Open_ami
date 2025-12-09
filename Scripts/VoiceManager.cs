using OpenAmi.Scripts;
using Godot;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;



namespace OpenAmi.Scripts
{
    public partial class VoiceManager : Node
    {

        public static VoiceManager Singleton { get; private set; }
        private AudioStreamPlayer _audioPlayer;
        public bool _isPlaying = false;
        private string _piperModelPath = ProjectSettings.GlobalizePath("res://External/piper131/mv2.onnx");
        private string _piperConfigPath = ProjectSettings.GlobalizePath("res://External/piper131/mv2.onnx.json");
        private string _outputDir = ProjectSettings.GlobalizePath("user://API_Output/");
        private AppManager _appManager;
        private AudioStreamMicrophone _micStream;
        private AudioEffectCapture _captureEffect;
        private bool _isRecording = false;
        private string _transcriptionBuffer = "";
        private float _vadThreshold = 0.01f;  // Configurable later
        private float _pauseTimeout = 1.0f;   // 2000ms as requested
        private double _lastSpeechTime = 0;
        private Timer _pauseTimer;
        public string _piperDir;

        public override void _Ready()
        {
            _appManager = GetNode<AppManager>("/root/AppManager");
            
            //_amiMain = _appManager._amiMain;
            Singleton = this;

            _piperDir = Path.Combine(ProjectSettings.GlobalizePath("user://"), "piper");

            if (!DirAccess.DirExistsAbsolute(_outputDir))
            {
                DirAccess.MakeDirAbsolute(_outputDir);
            }
            _audioPlayer = new AudioStreamPlayer();
            AddChild(_audioPlayer);
            _audioPlayer.Finished += OnAudioFinished;
        }

        //Utility for copying directory structure

        public void CopyPiperRecursively(string srcRes, string dst)
        {
            var dir = DirAccess.Open(srcRes);
            if (dir == null)
            {
                GD.PrintErr($"Failed to open {srcRes}");
                return;
            } 
            
            dir.ListDirBegin();
                string fileName = dir.GetNext();
            while (fileName != "")
            {
                string srcPath = srcRes + "/" + fileName;
                string dstPath = Path.Combine(dst, fileName);
                if (dir.CurrentIsDir())
                {
                    Directory.CreateDirectory(dstPath);
                    CopyPiperRecursively(srcPath, dstPath);
                }
                else
                {
                    using var file = Godot.FileAccess.Open(srcPath, Godot.FileAccess.ModeFlags.Read);
                    if (file != null)
                    {
                        byte[] data = file.GetBuffer((long)file.GetLength());
                        File.WriteAllBytes(dstPath, data);
                        // GD.Print($"Copied {fileName} to {dstPath}");  // Uncomment for verbose logging
                    }
                    else
                    {
                        GD.PrintErr($"Failed to read {srcPath} from PCK");
                    }
                }

                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }

        //Divide output text into chunks by sentence for rendering by piper
        private static List<string> ChunkTextForVoice(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            // Split on sentence endings (. ! ?) followed by space, keeping punctuation
            var chunks = Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
                .ToList();

            return chunks.Count > 0 ? chunks : [text]; // Fallback to full text if no splits
        }


        private async Task<string> RenderChunkToWavAsync(string chunk, int index)
        {
            string tempInputPath = Path.Combine(_outputDir, $"chunk_{index}_input.txt");
            File.WriteAllText(tempInputPath, chunk);
            string globalTempPath = ProjectSettings.GlobalizePath(tempInputPath);
            string wavPath = Path.Combine(_outputDir, $"chunk_{index}.wav");
            //switch to assign speakers based on persona
            int spkr = _appManager._config["PersonaSelected"].ToString() switch
            {
                "Evie" => 0,
                "Teacher" => 6,
                "Assistant" => 15,
                "Collaborator" => 14,
                _ => 14,
            };


            //GD.Print($"Voice Manager: {globalTempPath} contains {File.ReadAllText(globalTempPath)}");
            //GD.Print($"VoiceManager: Rendering chunk {index} to WAV at {wavPath}");
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-m piper --input-file {globalTempPath} --model mv2.onnx -s {spkr} --config mv2.onnx.json --output_file {wavPath}",
                //WorkingDirectory = ProjectSettings.GlobalizePath("res://External/piper131"),
                WorkingDirectory = _piperDir,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            //GD.Print($"VoiceManager: Piper called with {psi.FileName} {psi.Arguments} for chunk {index}");
            try
            {
            using var process = Process.Start(psi);
            if (process == null)
            {
                GD.PrintErr($"VoiceManager: Failed to start Piper process for chunk {index}");
                return null;
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || !File.Exists(wavPath))
            {
                string error = await process.StandardError.ReadToEndAsync();
                GD.PrintErr($"VoiceManager: Piper render failed for chunk {index} (exit {process.ExitCode}): {error}");
                return null;
            }

            }
            catch (Exception e)
            {
                GD.PrintErr($"Piper failed: {e.Message}");
                _appManager._amiMain._outputLabel.CallDeferred("set_text", "\n\n[code}AMI> Voice function failed. \n\nVoice function requires Python 3.9+ and piper-tts. Open command prompt and run: pip install piper-tts.[/code]");
                return null;  // Skip chunk
            }
            return wavPath;
        }

        public async void PlayResponseText(string fullText)
        {
            //GD.Print("VoiceManager: PlayResponseText called with text length: " + fullText?.Length ?? "0");
            if (string.IsNullOrWhiteSpace(fullText) || _isPlaying)
            {
                //GD.Print("VoiceManager: Skipping playback - empty text or already playing");
                return;
            }

            var chunks = ChunkTextForVoice(fullText);
            //GD.Print($"VoiceManager: Chunked text into {chunks.Count} chunks");
            if (chunks.Count == 0)
            {
                //GD.Print("VoiceManager: No chunks to play");
                return;
            }

            _isPlaying = true;
            GD.Print("VoiceManager: Starting playback orchestration");
            await HandleVoicePlaybackAsync(chunks);
            _isPlaying = false;
            GD.Print("VoiceManager: Playback complete, resetting _isPlaying");
        }
        private async Task HandleVoicePlaybackAsync(List<string> chunks)
        {
            //GD.Print($"VoiceManager: Handling playback for {chunks.Count} chunks");

            Task<string> nextRenderTask = null;  // Task to hold the next render  

            for (int i = 0; i < chunks.Count; i++)
            {
                string wavPath;
                if (i == 0)
                {
                    // First chunk: render synchronously  
                    wavPath = await RenderChunkToWavAsync(chunks[i], i);
                }
                else
                {
                    // Subsequent chunks: await the pre-started render task  
                    wavPath = await nextRenderTask;
                }

                //GD.Print($"VoiceManager: Render complete for chunk {i}, loading WAV: {wavPath}");
                AudioStreamWav stream = AudioStreamWav.LoadFromFile(wavPath);
                if (stream == null)
                {
                    GD.PrintErr($"VoiceManager: Failed to load WAV for chunk {i}");
                    await CleanupWavsAsync();
                    return;
                }

                _audioPlayer.Stream = stream;
                //GD.Print($"VoiceManager: Playing chunk {i}");
                _audioPlayer.Play();

                // Start rendering the next chunk while this one plays (if not the last)  
                if (i < chunks.Count - 1)
                {
                    nextRenderTask = RenderChunkToWavAsync(chunks[i + 1], i + 1);
                }

                await ToSignal(_audioPlayer, "finished");
                //GD.Print($"VoiceManager: Finished playing chunk {i}");
            }

            //GD.Print("VoiceManager: All chunks played, cleaning up WAVs");
            await CleanupWavsAsync();
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task CleanupWavsAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            //GD.Print("VoiceManager: Starting WAV cleanup");
            var dir = DirAccess.Open(_outputDir);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                int deletedCount = 0;
                while (fileName != "")
                {
                    //if (!dir.CurrentIsDir() && fileName.EndsWith(".wav"))
                    if (!dir.CurrentIsDir())
                    {
                        string fullPath = Path.Combine(_outputDir, fileName);
                        if (fileName.EndsWith(".wav") || (fileName.EndsWith(".txt") && fileName.Contains("chunk")))
                        {
                            DirAccess.RemoveAbsolute(fullPath);
                            deletedCount++;
                            //GD.Print($"VoiceManager: Deleted {fileName}");
                        }
                    }
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
                //GD.Print($"VoiceManager: Cleanup complete, deleted {deletedCount} WAV files");
            }
            else
            {
                GD.PrintErr("VoiceManager: Failed to open output directory for cleanup");
            }
        }
        private void OnAudioFinished()
        {
            //GD.Print("VoiceManager: Audio finished signal received");
            // No action needed here; HandleVoicePlaybackAsync awaits the signal directly
        }

        // Stop playback, reset flags, dispose STT components, and cleanup.
        // Extended for Vosk and audio resources.
        public void StopAndCleanup()
        {
            GD.Print("VoiceManager: StopAndCleanup called.");
            if (_audioPlayer.Playing)
            {
                _audioPlayer.Stop();
                GD.Print("VoiceManager: Audio playback stopped.");
            }
            _isPlaying = false;
            GD.Print("VoiceManager: _isPlaying reset to false.");

            // Cleanup WAVs 
            Task.Run(() => CleanupWavsAsync());
            GD.Print("VoiceManager: WAV cleanup initiated.");
        }

        //private void CleanupSTT()
//        {
//            _micPlayer?.Stop(); _micPlayer = null;
//            _recognizer?.Dispose();
//            _voskModel?.Dispose();
//            _captureEffect?.ClearBuffer();
//            _captureEffect = null;
//            _pauseTimer?.Stop();
//            _amiThink?.QueueFree();
//            GD.Print("VoiceManager: STT cleaned up.");
//        }

//        Initialize STT components: Load Vosk model, create recognizer, set up audio capture effect on "Record" bus.
//        Called on-demand when mic is first enabled to avoid unnecessary load.
//        public void InitializeSTT()
//        {
//            GD.Print("VoiceManager: InitializeSTT called - Starting STT setup.");
//            if (_voskModel != null)
//            {
//                GD.Print("VoiceManager: STT already initialized.");
//                return;
//            }
//            try
//            {
//                Load AmiThink as node
//                Singleton.AddChild(_amiThink = _amiMain._amiThink);

//                Load Vosk model from config path
//                string modelPath = ProjectSettings.GlobalizePath(_appManager._config.TryGetValue("STTModelPath", out var path) ? path.ToString() : "res://Assets/Models/vosk-model-small-en-us-0.15");
//                GD.Print($"VoiceManager: Loading Vosk model from {modelPath}");
//                _voskModel = new Model(modelPath);
//                GD.Print("VoiceManager: Vosk model loaded successfully.");

//                Create Vosk recognizer with 16kHz sample rate
//               _recognizer = new VoskRecognizer(_voskModel, 16000.0f);
//                _recognizer.SetMaxAlternatives(0);  // Single best result for speed
//                _recognizer.SetWords(true);  // Include word timings if needed
//                GD.Print("VoiceManager: VoskRecognizer created with 16kHz, max alternatives 0, words enabled.");

//                Set up audio capture effect on "Record" bus for real - time mic input

//               _micPlayer = new AudioStreamPlayer();
//               Singleton.AddChild(_micPlayer);
//               _micPlayer.Stream = new AudioStreamMicrophone();
//                _micPlayer.Bus = "Record";
//                _micPlayer.VolumeLinear = 1.0f;  // Full volume for capture
//                AudioServer.SetBusVolumeLinear(1, 0f); // Mute output bus to avoid feedback
//                int recordBusIndex = AudioServer.GetBusIndex("Record");
//                GD.Print($"Record bus index: {recordBusIndex}");
//                if (recordBusIndex == -1)
//                    {
//                        GD.PrintErr("VoiceManager: 'Record' bus not found - ensure it's created in AudioServer.");
//                        return;
//                    }
//                _captureEffect = new AudioEffectCapture();
//                AudioServer.AddBusEffect(recordBusIndex, _captureEffect);
//                GD.Print("VoiceManager: AudioEffectCapture added to 'Record' bus for mic input.");

//                Set up pause timer for VAD(2 - second timeout)

//               _pauseTimer = new Timer
//               {
//                   OneShot = true,
//                   WaitTime = _pauseTimeout
//               };
//               _pauseTimer.Timeout += () => OnPauseTimeout();
//               AddChild(_pauseTimer);
//                GD.Print("VoiceManager: Pause timer set up with 2-second timeout.");

//                GD.Print("VoiceManager: InitializeSTT completed successfully.");
//            }
//            catch (Exception e)
//            {
//                GD.PrintErr($"VoiceManager: InitializeSTT failed - {e.Message}");
//                Dispose partial components on failure
//               _recognizer?.Dispose();
//        _voskModel?.Dispose();
//    }
//}

//Toggle recording state: Start if off, stop if on. Checks EnableVoice config.
//        public void ToggleRecording()
//{
//    GD.Print("VoiceManager: ToggleRecording called.");

//    if (_appManager._config["EnableVoice"].ToString() != "true" || !_micOn)
//    {
//        CleanupSTT();
//        GD.Print("VoiceManager: ToggleRecording skipped - EnableVoice is false or _micOn is false");
//        return;
//    }

//    if (_isRecording)
//    {
//        GD.Print("VoiceManager: Stopping recording.");
//        Task.Run(() => StopRecording());
//    }
//    else
//    {
//        GD.Print("VoiceManager: Starting recording.");
//        StartRecording();
//    }
//}

//Start recording: Clear buffers, reset VAD timer, set recording flag.
//         Called when mic is toggled on.
//        private void StartRecording()
//{
//    GD.Print("VoiceManager: StartRecording called.");
//    if (_isRecording)
//    {
//        GD.Print("VoiceManager: Already recording, skipping.");
//        return;
//    }

//    _micPlayer.Play();
//    _isRecording = true;
//    _transcriptionBuffer = "";  // Clear any previous transcription
//    _captureEffect.ClearBuffer();  // Clear audio buffer for fresh start
//    _lastSpeechTime = 0;  // Reset speech timer for VAD
//    GD.Print("VoiceManager: Recording started, buffers cleared, VAD reset.");
//}

//Stop recording: Reset flag, clear buffers, finalize transcription if any.
//         Called when mic is toggled off or pause timeout.
//        private async Task StopRecording()
//{
//    GD.Print("VoiceManager: StopRecording called.");
//    if (!_isRecording)
//    {
//        GD.Print("VoiceManager: Not recording, skipping.");
//        return;
//    }

//    _isRecording = false;
//    _pauseTimer.Stop();  // Stop any pending pause timer
//    _micPlayer.Stop();
//    _captureEffect.ClearBuffer();  // Clear remaining audio
//    Finalize any pending transcription(if buffer has data)
//            if (!string.IsNullOrEmpty(_transcriptionBuffer))
//    {
//        GD.Print($"VoiceManager: Finalizing transcription: {_transcriptionBuffer}");
//        await ParseJsonResult(_transcriptionBuffer);  // Send the final result
//        _transcriptionBuffer = "";
//    }
//    GD.Print("VoiceManager: Recording stopped, buffers cleared.");
//    if (_micOn) StartRecording();  // Add this for back-to-back phrases
//}

//Process audio buffers in real-time: Check for speech via VAD, feed to Vosk, handle partials.
//         Runs every frame for streaming STT.
//        public override void _Process(double delta)
//{
//    base._Process(delta);
//    if (!_isRecording)
//    {
//        return;  // Skip if not recording
//    }

//    Get audio frames from capture effect(2048 frames ~0.125s at 16kHz)
//            Vector2[] audioFrames = _captureEffect.GetBuffer(2048);
//    GD.Print($"VoiceManager: _Process - Got {audioFrames.Length} frames from capture.");

//    if (audioFrames.Length > 0)
//    {
//        Convert frames to 16 - bit PCM byte[] for Vosk(mono, little - endian)

//       byte[] buffer = new byte[audioFrames.Length * 2];  // 2 bytes per frame
//                for (int i = 0; i < audioFrames.Length; i++)
//            {
//                short sample = (short)(audioFrames[i].X * 32767f);  // Scale float to short, use X for mono
//                buffer[2 * i] = (byte)sample;          // Low byte
//                buffer[2 * i + 1] = (byte)(sample >> 8); // High byte
//            }
//        int bytesRead = buffer.Length;
//        GD.Print($"VoiceManager: _Process - Converted to {bytesRead} bytes for Vosk.");

//        Simple VAD: Calculate RMS energy on shorts
//                float energy = 0f;
//        for (int i = 0; i < bytesRead; i += 2)
//        {
//            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
//            energy += sample * sample;
//        }
//        energy = Mathf.Sqrt(energy / (bytesRead / 2)) / 32768f;  // Normalize
//        GD.Print($"VoiceManager: _Process - VAD energy: {energy}");

//        Check for speech


//       double currentTime = Time.GetTicksMsec() / 1000.0;
//                if (energy > _vadThreshold)
//            {
//                _lastSpeechTime = currentTime;
//                _pauseTimer.Stop();
//                GD.Print("VoiceManager: _Process - Speech detected, resetting pause timer.");
//            }
//            else if (currentTime - _lastSpeechTime > 0.1f)
//            {
//                _pauseTimer.Start(_pauseTimeout);
//                GD.Print("VoiceManager: _Process - Silence detected, starting pause timer.");
//            }


//        if (energy > _vadThreshold)
//        {
//            _lastSpeechTime = (double)Time.GetTimeDictFromSystem()["elapsed"];
//            _pauseTimer.Stop();
//            GD.Print("VoiceManager: _Process - Speech detected, resetting pause timer.");
//        }
//        else if (((double)Time.GetTimeDictFromSystem()["elapsed"] - _lastSpeechTime) > 0.1f)
//        {
//            _pauseTimer.Start(_pauseTimeout);
//            GD.Print("VoiceManager: _Process - Silence detected, starting pause timer.");
//        }

//        Feed buffer to Vosk
//                if (_recognizer.AcceptWaveform(buffer, bytesRead))
//        {
//            string resultJson = _recognizer.Result();
//            GD.Print($"VoiceManager: _Process - Final result: {resultJson}");
//            Task.Run(() => ParseJsonResult(resultJson));
//            GD.Print("VoiceManager: called ParseJsonResult");
//        }
//        else
//        {
//            string partialJson = _recognizer.PartialResult();
//            GD.Print($"VoiceManager: _Process - Partial result: {partialJson}");
//        }
//    }
//}

//Handle pause timeout: Stop recording and finalize transcription.
//         Called when 2s of silence detected.
//        private void OnPauseTimeout()
//{
//    try
//    {
//        GD.Print("VoiceManager: OnPauseTimeout called - 1s silence detected.");
//        Task.Run(() => StopRecording());  // This will finalize and send
//    }
//    catch
//    {
//        GD.PrintErr("VoiceManger: OnPauseTimeout > StopRecording failed");
//    }
//}

//Parse Vosk JSON result, extract text, and replicate send logic from OnSendButtonPressed case 0.
//         Accumulates transcription and triggers query send on final results.
//        private async Task ParseJsonResult(string json)
//{
//    GD.Print($"VoiceManager: ParseJsonResult called with JSON: {json}");
//    try
//    {
//        Parse JSON to extract text
//       var doc = JsonDocument.Parse(json);
//        string text = doc.RootElement.GetProperty("text").GetString();
//        GD.Print($"VoiceManager: Extracted text: '{text}'");

//        if (!string.IsNullOrEmpty(text))
//        {
//            _transcriptionBuffer += text + " ";  // Accumulate for full phrase
//            GD.Print($"VoiceManager: Accumulated buffer: '{_transcriptionBuffer.Trim()}'");

//            Replicate OnSendButtonPressed case 0 logic for sending query

//            1.Check vision support
//                    if (_appManager._amiMain._currentModelData.TryGetValue("supports_vision", out var visionSupport) &&
//                        visionSupport.ToString() == "false" &&
//                        _appManager._config["SessionRequiresVision"].ToString() == "true")
//                    {
//                        GD.PrintErr("VoiceManager: Vision model required but not supported - skipping send.");
//                        _appManager._amiMain._outputLabel.Text += "[code]AMI> Error: Current model does not support vision inputs. Please select a different model or start a new session.[/code]";
//                        return;
//                    }

//                2.Set request type and prepare prompt
//                    IEnumerable<ResponseItem> messages;
//                ResponseCreationOptions responseOptions;
//                try
//                {
//                    _appManager._amiMain._lastRequestType = "user_query";
//                    _appManager._amiMain._userInputTextEdit.CallDeferred("set_text", _transcriptionBuffer.Trim());
//                    (messages, responseOptions) = _appManager.WritePrompt(_transcriptionBuffer.Trim());
//                    GD.Print("VoiceManager: Prompt written, starting think animation.");
//                }
//                catch (Exception e)
//                {
//                    GD.PrintErr($"VoiceManager: WritePrompt failed - {e.Message}");
//                    return;
//                }

//                3.Start think animation
//                    try
//                {
//                    if (_appManager._amiMain._amiThink != null)
//                    {
//                        _appManager._amiMain._amiThink.CallDeferred("set_visible", true);
//                        _appManager._amiMain._amiThink._anim.CallDeferred("set_active", true);
//                        _appManager._amiMain._amiThink._anim.CallDeferred("play", "think");
//                    }
//                }
//                catch (Exception e)
//                {
//                    GD.PrintErr($"VoiceManager: Starting think animation failed - {e.Message}");
//                }

//                4.Get client and send async
//                    OpenAIResponseClient localClient = _appManager._amiMain._responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
//                var result = await localClient.CreateResponseAsync(messages, responseOptions);
//                if (result != null)
//                {
//                    _appManager.ParseResponsesAPIResponse(result.Value, true);
//                }
//                else
//                {
//                    GD.PrintErr("VoiceManager: No response from CreateResponseAsync.");
//                }

//                5.Clear buffer and input
//               _transcriptionBuffer = "";
//                _appManager._amiMain._userInputTextEdit.Text = "";
//                GD.Print("VoiceManager: Send completed, buffers cleared.");
//            }
//        }
//            catch (Exception e)
//    {
//        GD.PrintErr($"VoiceManager: ParseJsonResult failed - {e.Message}");
//    }
//}



    }
}