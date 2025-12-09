using OpenAmi.Scripts;
using Godot;
using OpenAI.Containers;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WeCantSpell.Hunspell;


namespace OpenAmi.Scripts
{
    public partial class AppManager : Node
    {
        public Dictionary<string, object> _config;
        public Dictionary<string, object> _themeObjects;
        public List<Dictionary<string, string>> _resumeSessions;

        private const string ConfigPath = "user://Config.cfg";
        private const string DefaultConfigPath = "res://Config.cfg";
        private const string ApiOutputPath = "user://API_Output/";
        private const string SessionHistoryPath = "user://Context/session_history.json";
        private string _lastQuery = "";

        public string _apiKey = ""; // Stub for encrypted API key
        public string _pendingUploadContent = ""; // Stores content of pending upload file
        public string _pendingUploadFilename = ""; // Stores name of pending upload file
        public readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true }; // Common JSON options
        public string _lastPrompt = ""; // Stores full prompt JSON for ShowLastPrompt
        public AmiMain _amiMain; // Reference to AmiMain node
        public WordList _spellDictionary;
        private FileSearchTool _fileTool;
        private WebSearchTool _webSearchTool;
        private CodeInterpreterTool _codeExecutionTool;
        public byte[] _pendingImageBytes = null;
        public string _pendingImageMime = "";
        public string _pendingImageFilename = "";


        public override void _Ready()
        {
            GD.Print("AppManager::_Ready - Initializing AppManager.");
            
            _config = [];
            _resumeSessions = [];
            _jsonOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;

            // Create directories
            foreach (var dir in new[] { ApiOutputPath, "user://Session_Outputs", "user://Context/", "user://Uploaded_Files/", "user://Assets/", "user://Assets/dicts/", "user://Assets/loc" })
            {
                if (!DirAccess.DirExistsAbsolute(dir))
                {
                    DirAccess.MakeDirAbsolute(dir);
                    //GD.Print($"AppManager::_Ready - Created directory: {dir}");
                }
            }

            // Copy default Config.cfg
            if (!Godot.FileAccess.FileExists(ConfigPath))
            {
                if (Godot.FileAccess.FileExists(DefaultConfigPath))
                {
                    var defaultConfig = Godot.FileAccess.GetFileAsString(DefaultConfigPath);
                    using var file = Godot.FileAccess.Open(ConfigPath, Godot.FileAccess.ModeFlags.Write);
                    if (file != null)
                    {
                        file.StoreString(defaultConfig);
                        //GD.Print("AppManager::_Ready - Copied res://Config.cfg to user://Config.cfg");
                    }
                    else
                    {
                        GD.PrintErr("AppManager::_Ready - Error copying Config.cfg: FileAccess null");
                    }

                }
                else
                {
                    GD.PrintErr("AppManager::_Ready - Error: res://Config.cfg not found");
                }
            }

            //detect user://Assets/loc/610762ecbe77cf65.json and 5be7fc864b872eed.json; if not found,open to write
            if (!Godot.FileAccess.FileExists("user://Assets/loc/610762ecbe77cf65.json"))
            {
                var locFile1 = Godot.FileAccess.Open("user://Assets/loc/610762ecbe77cf65.json", Godot.FileAccess.ModeFlags.Write);
                locFile1.StoreString("dXNlcjovL0Fzc2V0cy9sb2MvNWJlN2ZjODY0Yjg3MmVlZC5qc29u");
                //GD.Print("AppManager::_Ready - Created localization file 610762ecbe77cf65.json");
            }
            if (!Godot.FileAccess.FileExists("user://Assets/loc/5be7fc864b872eed.json"))
            {
                var locFile2 = Godot.FileAccess.Open("user://Assets/loc/5be7fc864b872eed.json", Godot.FileAccess.ModeFlags.Write);
                locFile2.StoreString("bmV4dCBsb2NhbGl6YXRpb24gPSB1c2VyOi8vQXNzZXRzL2xvYy82MTA3NjJlY2JlNzdjZjY1Lmpzb24");
                //GD.Print("AppManager::_Ready - Created localization file 5be7fc864b872eed.json");
            }
            //detect user://Assets/custom.txt; if not found, open to write
            if (!Godot.FileAccess.FileExists("user://Assets/custom.txt"))
            {
                var customFile = Godot.FileAccess.Open("user://Assets/custom.txt", Godot.FileAccess.ModeFlags.Write);
                customFile.StoreString("//Custom instructions\n");
                //GD.Print("AppManager::_Ready - Created custom assets file custom.txt");
            }

            //detect user://Context/SessionLegacy.txt; if not found, open to write
            if (!Godot.FileAccess.FileExists("user://Context/SessionLegacy.txt"))
            {
                var customFile = Godot.FileAccess.Open("user://Context/SessionLegacy.txt", Godot.FileAccess.ModeFlags.Write);
                customFile.StoreString("Session Legacy Log\n\n\n");
                //GD.Print("AppManager::_Ready - Created session legacy log file SessionLegacy.txt");
            }

            //detect the presence of all .dic and .aff files carried in res://Assets/dicts in user://Assets/dicts;  copy any that are absent
            try
            {
                string dictsDirOut = "res://Assets/dicts";
                string dictsDirIn = "user://Assets/dicts";
                try
                {
                    string[] dictsList = DirAccess.GetFilesAt(dictsDirOut);
                    try
                    {
                        foreach (string filename in dictsList)
                        {
                            if ( !Godot.FileAccess.FileExists("user://Assets/dicts/" + filename) && (filename.GetExtension() == "dic" || filename.GetExtension() == "aff"))
                            {
                                string oldFile = Godot.FileAccess.GetFileAsString($"res://Assets/dicts/{filename}");
                                var newfile = Godot.FileAccess.Open("user://Assets/dicts/" + filename, Godot.FileAccess.ModeFlags.Write);
                                newfile.StoreString(oldFile);

                                GD.Print($"AppManager._Ready{filename} copied to {dictsDirIn}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr("AppManager._Ready: Failed to copy res://Assets/dict contents: ", e);

                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr("AppManager._Ready:  res://Assets/dicts or user://Assest/dicts not found: ", e);
                }
            }
            catch (Exception e)
                {
                GD.PrintErr("AppManager._Ready; failed to copy dict files: ", e);
            } 

            // Load Config.cfg
            if (LoadConfig())
            {
                //GD.Print("AppManager::_Ready - Config loaded successfully.");
            }
            else
            {
                GD.PrintErr("AppManager::_Ready - Failed to load Config.cfg.");

            }

            //Pre-initialize file search tool for use in WritePrompt
            try
            {
                if (_config.TryGetValue("CollectionID", out var collIDObj) && collIDObj.ToString() != "none")
                {
                    //convert JsonElement collIDObj to IEnumerable<string> collID to correctly form _fileTool
                    var collID = new List<string> { collIDObj.ToString() };
                    _fileTool = ResponseTool.CreateFileSearchTool(collID)                                                                                      ;
                    //GD.Print($"_fileTool initialized for collection {collID}");
                }
                else
                    GD.Print("AppManager._Ready: _fileTool not initialized, no collection ID");
            }
            catch (Exception e)
            {
                GD.PrintErr("AppManager._Ready: _fileTool not initialized: ",e);
            }

            //Pre-initialize web search tool
            try
            {
                _webSearchTool = ResponseTool.CreateWebSearchTool();
                //GD.Print("AppManagerReady: _webSearchTool initialized.");
            }
            catch (Exception e)
            {
                GD.PrintErr("AppManagerReady: _webSearchTool not initialized: ",e);
            }

            //Pre-initialize code interpreter tool

            try
            {
                _codeExecutionTool = ResponseTool.CreateCodeInterpreterTool(new CodeInterpreterToolContainer(CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration()));
                //GD.Print("AppManagerReady: _codeExecutionTool initialized.");
            }
            catch (Exception e)
            {
                GD.PrintErr("AppManagerReady: _codeExecutionTool not intiialized: ", e);
            }

            
        }


        //Load configuration from Config.cfg
        public bool LoadConfig()
        {
            try
            {
                if (Godot.FileAccess.FileExists(ConfigPath))
                {
                    var json = Godot.FileAccess.GetFileAsString(ConfigPath);
                    _config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    GD.Print("AppManager:LoadConfig - Loaded Config.cfg successfully.");
                    return true;
                }
                GD.PrintErr("AppManager:LoadConfig - Config.cfg not found.");
                return false;
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:LoadConfig - Error parsing Config.cfg: {e.Message}");
                return false;
            }
        }


        //Save configuration to Config.cfg
        public bool SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(_config, _jsonOptions);
                using var file = Godot.FileAccess.Open(ConfigPath, Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreString(json);
                    GD.Print("AppManager:SaveConfig - Saved Config.cfg successfully.");
                    return true;
                }
                GD.PrintErr("AppManager:SaveConfig - Error: FileAccess null");
                return false;
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:SaveConfig - Error saving Config.cfg: {e.Message}");
                return false;
            }
        }

        //Parse API response JSON file and return as dictionary
        public void ParseResponsesAPIResponse(OpenAIResponse response, bool saveToSessionOutputs = false)
        {
            var result = new Dictionary<string, object>
            {
                ["connected"] = false,
                ["text"] = "",
                ["id"] = "",
                ["model"] = "",
                ["createdAt"] = "",
                ["total_tokens"] = 0000,
                ["prompt_tokens"] = 0000,
                ["cached_tokens"] = 0000,
                ["output_tokens"] = 0000,
                ["reasoning_tokens"] = 0000,
                ["budget"] = 0000,
                ["response_spend"] = 0000,
                ["session_spend"] = 0000,
                ["running_spend"] = 0000

            };

            try
            {

                GD.Print("AppManager:ParseResponsesAPIResponse - Parsing API response.");

                // Create Dic result
                try
                {

                    //extract result fields from response
                    result["id"] = response.Id;
                    result["model"] = response.Model;
                    result["createdAt"] = response.CreatedAt;
                    result["text"] = response.GetOutputText();
                    result["tools_called"] = response.Tools.Count;
                    result["tools_list"] = response.Tools;
                    result["total_tokens"] = response.Usage.TotalTokenCount;
                    result["prompt_tokens"] = response.Usage.InputTokenCount;
                    result["cached_tokens"] = response.Usage.InputTokenDetails.CachedTokenCount;
                    result["output_tokens"] = response.Usage.OutputTokenCount;
                    result["reasoning_tokens"] = response.Usage.OutputTokenDetails.ReasoningTokenCount;
                }
                catch (Exception e)
                {
                    GD.PrintErr("ParseResponsesAPIResponse failed to build Dictionary result: ", e);
                }

                // Extract Response ID
                if (result.TryGetValue("id", out var idElement))
                {
                    var responseId = result["id"].ToString();
                    _config["LastResponseID"] = responseId;
                    //GD.Print($"AppManager:ParseResponsesAPIResponse - Extracted response ID: {responseId}");
                }
                else
                {
                    result["id"] = "unknown";
                    _config["LastResponseID"] = "unknown";
                    GD.PrintErr("AppManager:ParseResponsesAPIResponse - Missing or invalid id field");
                }


                // Archive Usage History
                var usageHistoryPath = "user://Context/usage_history.txt";
                var usageEntry = $"ResponseID: {result["id"]}\nTimestamp: {DateTime.Now}\nUsage:\nPrompt Tokens: {(int)result["prompt_tokens"]}\nCached Tokens: {(int)result["cached_tokens"]}\nCompletion Tokens: {(int)result["output_tokens"]}\nReasoning Tokens: {(int)result["reasoning_tokens"]}\n---\n";
                {
                    if (!Godot.FileAccess.FileExists(usageHistoryPath))
                    {
                        var usageFile = Godot.FileAccess.Open(usageHistoryPath, Godot.FileAccess.ModeFlags.Write);
                        usageFile.StoreString(usageEntry);
                        GD.PrintErr($"AppManager:ParseResponsesAPIResponse - Usage History not found; created usage_history.txt and archived usage to {usageHistoryPath}");
                    }
                    else
                    {
                        var usageFile = Godot.FileAccess.Open(usageHistoryPath, Godot.FileAccess.ModeFlags.ReadWrite);
                        usageFile.SeekEnd();
                        usageFile.StoreString(usageEntry);
                        //GD.Print($"AppManager:ParseResponsesAPIResponse - Archived usage to {usageHistoryPath}");
                    }
                }

                // Handle Artifacts (if saveToSessionOutputs == true)
                if (saveToSessionOutputs)
                {

                    string responseText = (string)result["text"];
                    int artifactCount = 0;
                    const int maxArtifacts = 10;
                    while (true)
                    {
                        if (artifactCount >= maxArtifacts)
                        {
                            GD.PrintErr($"AppManager:ParseResponsesAPIResponse - Warning: Reached maximum artifact limit of {maxArtifacts}; stopping processing");
                            break;
                        }
                        string artifact = IdentifyArtifacts(ref responseText);
                        if (artifact != null)
                        {
                            ParseArtifact(artifact);
                            artifactCount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    // Update result["text"] with modified responseText
                    result["text"] = responseText;
                }
                try
                {
                    //Output tool usages
                    GD.Print($"ParseResponse tool count = {result["tools_called"]}");
                    //foreach (ResponseTool tool in (List<ResponseTool>)result["tools_list"])
                        //GD.Print("ParseResponse:  tool ", tool.GetType());
                }
                catch (Exception e)
                {
                    GD.PrintErr("ParseResponse Tool Listing failed: ", e);
                }

           

            //Calculate Spend Values
            try
                {

                    if (_amiMain._currentModelData == null || !_amiMain._currentModelData.TryGetValue("prompt_token_price", out object value))
                        GD.PrintErr("ParseResponses:Current model prices missing.");
                    else
                    {
                        //GD.Print("ParseResponses: deferCalc = ", _amiMain._deferCalc);
                        //GD.Print($"ParseResponses: result[prompt_tokens] = {result["prompt_tokens"]}");

                        var promptSpend = (result["prompt_tokens"].ToString().ToFloat() - result["cached_tokens"].ToString().ToFloat()) * value.ToString().ToFloat() * 1e-010;
                        var cachedSpend = result["cached_tokens"].ToString().ToFloat() * _amiMain._currentModelData["cached_token_price"].ToString().ToFloat() * 1e-010;
                        var outputSpend = result["output_tokens"].ToString().ToFloat() * _amiMain._currentModelData["output_token_price"].ToString().ToFloat() * 1e-010;
                        var responseSpend = (promptSpend + cachedSpend + outputSpend);

                        //GD.Print($"ParseResponsesAPIResponse: Calc basis responseSpend = {responseSpend}, promptSpend = {promptSpend}, cachedSpend = {cachedSpend}, outputSpend = {outputSpend}");

                        result["response_spend"] = Math.Round(responseSpend, 4);
                        result["budget"] = Math.Round(_config["Budget"].ToString().ToFloat() - responseSpend, 4);
                        result["session_spend"] = Math.Round(_config["SessionSpend"].ToString().ToFloat() + responseSpend, 4);
                        result["running_spend"] = Math.Round(_config["RunningSpend"].ToString().ToFloat() + responseSpend, 4);

                        //GD.Print($"ParseResponsesAPIResponse: Calc complete: Response Spend {result["response_spend"]}, Budget {result["budget"]}, Session Spend {result["session_spend"]}, Running Spend {result["running_spend"]}");

                        _config["Budget"] = result["budget"];
                        _config["SessionSpend"] = result["session_spend"];
                        _config["RunningSpend"] = result["running_spend"];

                        //GD.Print("ParseResponsesAPIResponse: Calc saved to _config");
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr("ParseResponsesAPIResponse:  Spend calculation failed: ", e);
                }


                // Conditional Session Save (if saveToSessionOutputs == true)
                if (saveToSessionOutputs)
                {
                    var sessionData = $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{_lastQuery}\n{(string)result["id"]}\n{(string)result["text"]}\n";
                    SaveSession(sessionData);
                    //GD.Print($"AppManager:ParseResponsesAPIResponse - Saved session data for response {(string)result["id"]}");
                }

                _amiMain._amiThink._anim.Active = false;
                _amiMain._amiThink.Visible = false;
                //GD.Print("ParseResponses: AmiThink hidden at ", DateTime.Now.ToString("hhmm:ss.fff"));
                result["connected"] = true;
                _amiMain.DisplayResponse(result);
                SaveConfig();
                if (_config.TryGetValue("EnableVoice", out var enable) && enable.ToString() == "true")
                {
                    VoiceManager.Singleton?.StopAndCleanup();  // Stop any previous playback
                    VoiceManager.Singleton?.PlayResponseText((string)result["text"]);
                }
                GD.Print("AppManager:ParseResponsesAPIResponse - Parsed response and updated UI.");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:ParseResponsesAPIResponse - Error: {e.Message}");
            }
        }
        private static string IdentifyArtifacts(ref string responseText)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(responseText))
                {
                    //GD.PrintErr("AppManager:IdentifyArtifacts - Error: Response text is null or empty");
                    return null;
                }

                // Regex to find the first artifact block: starts with +++++, ends with +++++, captures content in between
                var regex = new Regex(@"^\+\+\+\+\+\s*(.+?)\s*\n\+\+\+\+\+$", RegexOptions.Multiline | RegexOptions.Singleline);
                var match = regex.Match(responseText);
                if (match.Success)
                {
                    // Extract the full matched block (including delimiters)
                    string fullArtifactBlock = match.Value;
                    // Extract the content between delimiters
                    string artifactContent = match.Groups[1].Value.Trim();

                    // Remove the full block from responseText
                    responseText = responseText.Remove(match.Index, match.Length).Trim();

                    GD.Print("AppManager:IdentifyArtifacts - Successfully identified and extracted artifact");
                    return artifactContent;
                }
                else
                {
                    // No valid artifact found; check for malformed +++++ to prevent loops
                    if (responseText.Contains("+++++"))
                    {
                        // Remove the first occurrence of +++++ and surrounding text up to next newline or end
                        int startIndex = responseText.IndexOf("+++++");
                        int endIndex = responseText.IndexOf('\n', startIndex);
                        if (endIndex == -1) endIndex = responseText.Length;
                        string malformedPart = responseText[startIndex..endIndex];
                        responseText = responseText.Remove(startIndex, malformedPart.Length).Trim();
                        GD.PrintErr("AppManager:IdentifyArtifacts - Removed malformed artifact delimiter to prevent loop");
                    }
                    return null;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:IdentifyArtifacts - Error: {e.Message}");
                return null;
            }
        }


        private void ParseArtifact(string artifact)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(artifact))
                {
                    GD.PrintErr("AppManager:ParseArtifact - Error: Artifact string is null or empty");
                    return;
                }

                // Extract filename and body using ===== delimiter
                var parts = artifact.Split(["====="], 2, StringSplitOptions.None);
                if (parts.Length < 2)
                {
                    GD.PrintErr("AppManager:ParseArtifact - Error: Invalid artifact format, missing ===== delimiter");
                    return;
                }

                string artifactFilename = parts[0].Trim();
                string artifactBody = parts[1].Trim();

                // Validate filename and body
                if (string.IsNullOrWhiteSpace(artifactFilename))
                {
                    GD.PrintErr("AppManager:ParseArtifact - Error: Artifact filename is empty");
                    return;
                }
                if (string.IsNullOrWhiteSpace(artifactBody))
                {
                    GD.PrintErr("AppManager:ParseArtifact - Error: Artifact body is empty");
                    return;
                }

                // Construct file path
                string filePath = Path.Combine("user://Session_Outputs/", artifactFilename);

                // Write artifact body to file (overwrites if exists)
                using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreString(artifactBody);
                    GD.Print($"AppManager:ParseArtifact - Successfully wrote artifact to {filePath}");
                }
                else
                {
                    GD.PrintErr($"AppManager:ParseArtifact - Error: Failed to open file for writing: {filePath}");
                    return;
                }

                // Add to ArtifactsList if _amiMain is available
                if (_amiMain != null && _amiMain._artifactsList != null)
                {
                    _amiMain._artifactsList.AddItem(artifactFilename, null, true);
                    //GD.Print($"AppManager:ParseArtifact - Added {artifactFilename} to ArtifactsList");
                }
                else
                {
                    GD.PrintErr("AppManager:ParseArtifact - Error: _amiMain or _artifactsList is null");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:ParseArtifact - Error: {e.Message}");
            }
        }



        //add plain-text user and output text to session_history
#pragma warning disable CA1822 // Mark members as static
        public bool SaveSession(string sessionData)
#pragma warning restore CA1822 // Mark members as static
        {
            try
            {
                using var file = Godot.FileAccess.Open(SessionHistoryPath, Godot.FileAccess.ModeFlags.ReadWrite);
                if (file != null)
                {
                    file.SeekEnd();
                    file.StoreString(sessionData);
                    //GD.Print("AppManager:SaveSession - Saved session data.");
                    return true;
                }
                else
                {
                    var newSessionFile = Godot.FileAccess.Open(SessionHistoryPath, Godot.FileAccess.ModeFlags.Write);
                    newSessionFile.StoreString(sessionData);
                    GD.Print("AppManager:SaveSession - Error: FileAccess null; created and saved new session_history");
                    return true;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:SaveSession - Error: {e.Message}");
                return false;
            }
        }

        //Reuse last_prompt.json to store the last-sent full prompt
#pragma warning disable CA1822 // Mark members as static
        private bool SaveLastPrompt(string prompt)
#pragma warning restore CA1822 // Mark members as static
        {
            try
            {
                using var file = Godot.FileAccess.Open("user://Context/last_prompt.json", Godot.FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreString(prompt);
                    //GD.Print("AppManager:SaveLastPrompt - Saved last prompt.");
                    return true;
                }
                GD.PrintErr("AppManager:SaveLastPrompt - Error: FileAccess null");
                return false;
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:SaveLastPrompt - Error: {e.Message}");
                return false;
            }
        }

        //Write the last-sent user text to query_history as a quick human reference
#pragma warning disable CA1822 // Mark members as static
        private bool SaveQueryHistory(string query)
#pragma warning restore CA1822 // Mark members as static
        {
            try
            {
                if (!Godot.FileAccess.FileExists("user://Context/query_history.json"))
                { 
                    var queryFile = Godot.FileAccess.Open("user://Context/query_history.json", Godot.FileAccess.ModeFlags.Write);
                    GD.Print("AppManager:SaveQueryHistory - FileAccess null, wrote file");
                }


                    var queryHistory = Godot.FileAccess.Open("user://Context/query_history.json", Godot.FileAccess.ModeFlags.ReadWrite);
                    queryHistory.SeekEnd();
                    queryHistory.StoreString(query);
                    //GD.Print("AppManager:SaveQueryHistory - Saved query history.");
                    return true;

                

            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:SaveQueryHistory - Error: {e.Message}");
                return false;
            }
        }

        // Stores selected file to user://Uploaded_Files, validates text-based content, and updates UI
        public void StoreUploadedFile(string sourcePath)
        {
            try
            {
                if (Godot.FileAccess.FileExists(sourcePath))
                {
                    // Validate text-based file
                    string content;
                    try
                    {
                        content = Godot.FileAccess.GetFileAsString(sourcePath);
                    }
                    catch
                    {
                        GD.PrintErr($"AppManager:StoreUploadedFile - {System.IO.Path.GetFileName(sourcePath)} is not a supported file");
                        _amiMain._outputLabel.Text += $"[p]{System.IO.Path.GetFileName(sourcePath)} is not a supported file.[/p]";
                        return;
                    }

                    // Generate timestamped filename
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + System.IO.Path.GetExtension(sourcePath);
                    var destPath = "user://Uploaded_Files/" + fileName;

                    // Assign to pending fields
                    _pendingUploadContent = content;
                    _pendingUploadFilename = fileName;

                    // Save to Uploaded_Files
                    using var file = Godot.FileAccess.Open(destPath, Godot.FileAccess.ModeFlags.Write);
                    if (file != null)
                    {
                        file.StoreString(content);
                        _amiMain._uploadsList.AddItem(fileName, null, true);
                        GD.Print($"AppManager:StoreUploadedFile - Copied {_pendingUploadFilename} to Uploaded_Files");
                       // GD.Print($"AppManager:StoreUploadedFile - Pending upload string set to {_pendingUploadContent}");
                    }
                    else
                    {
                        GD.PrintErr($"AppManager:StoreUploadedFile - Error: FileAccess null for {destPath}");
                        _amiMain._outputLabel.Text += $"[p]Error saving {fileName} to Uploaded_Files.[/p]";
                    }
                }
                else
                {
                    GD.PrintErr($"AppManager:StoreUploadedFile - Source file not found: {sourcePath}");
                    _amiMain._outputLabel.Text += $"[p]Source file {System.IO.Path.GetFileName(sourcePath)} not found.[/p]";
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:StoreUploadedFile - Error: {e.Message}");
                _amiMain._outputLabel.Text += $"[p]Error processing file upload: {e.Message}[/p]";
            }
        }

        //On new session start, clean up files from previous session
        public bool ClearPreviousSession()
        {
            try
            {
                bool success = true;
                foreach (var dirPath in new[] { "user://Uploaded_Files/", "user://Session_Outputs/","user://API_Output/" })
                {
                    var dir = DirAccess.Open(dirPath);
                    if (dir != null)
                    {
                        dir.ListDirBegin();
                        string fileName = dir.GetNext();
                        bool isEmpty = true;
                        while (fileName != "")
                        {
                            if (!dir.CurrentIsDir())
                            {
                                DirAccess.RemoveAbsolute(dirPath + fileName);
                                //GD.Print($"AppManager:ClearPreviousSession - Removed {fileName} from {dirPath}");
                                isEmpty = false;
                            }
                            fileName = dir.GetNext();
                        }
                        if (isEmpty)
                        {
                            //GD.Print($"AppManager:ClearPreviousSession - Directory {dirPath} is empty.");
                        }
                    }
                    else
                    {
                        GD.PrintErr($"AppManager:ClearSession - Error: DirAccess null for {dirPath}");
                        success = false;
                    }
                }

                //if (VoiceManager.Singleton != null)
                VoiceManager.Singleton?.StopAndCleanup();

                try
                {
                    //Clear content of session_history.json, last_prompt.json, query_history.json
                    using var sessionFile = Godot.FileAccess.Open(SessionHistoryPath, Godot.FileAccess.ModeFlags.Write);
                    sessionFile.StoreString("");
                    using var promptFile = Godot.FileAccess.Open("user://Context/last_prompt.json", Godot.FileAccess.ModeFlags.Write);
                    promptFile.StoreString("");
                    using var queryFile = Godot.FileAccess.Open("user://Context/query_history.json", Godot.FileAccess.ModeFlags.Write);
                    queryFile.StoreString("");
                    GD.Print("AppManager:ClearPreviousSession - Cleared session history, last prompt, and query history files.");
                }
                catch (Exception e)
                {
                    GD.Print($"AppManager:ClearPreviousSession - Error removing context files: {e.Message}");
                }

                //Clear session ID
                _config["SessionID"] = null;
                _config["SessionRequiresVision"] = "false";
                

                GD.Print("AppManager:ClearPrevious - Cleared Uploaded_Files and Session_Outputs directories.");
                return success;
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:ClearPreviousSession - Error: {e.Message}");
                return false;
            }
        }

        //Create new Responses API prompt
        //public (string, ResponseCreationOptions) WritePrompt(string userTextInput)
        public (IEnumerable<ResponseItem>, ResponseCreationOptions) WritePrompt(string userTextInput)
        {
            try
            {
                //VALIDATE CONTEXT
                if (_amiMain == null)
                {
                    GD.PrintErr("WritePrompt: Called before AmiMain initialization in AppManager");
                    
                }

                    // LOG ENTRY
                   // GD.Print($"WritePrompt: Composing for request type '{_amiMain._lastRequestType}' with input length: {userTextInput?.Length ?? 0}");

                // INITIALIZE CORE STRUCTURES
                var messages = new List<ResponseItem>();  // New: Holds ResponseItem list
                string inputText = userTextInput;  // Keep for archiving, but not returned

                //DETECT VOICE OUTPUT ENABLED, SET FLAG IN userInput
                if (_config.TryGetValue("EnableVoice", out var voiceVal) && voiceVal.ToString() == "true")
                {
                    userTextInput = "*V*" + userTextInput;

                    //GD.Print($"WritePrompt: Voice output enabled; userTextInput = {userTextInput}");
                }
                //else
                    //GD.Print("WritePrompt: Voice output disabled.");


                var responseOptions = new ResponseCreationOptions
                    {
                        Temperature = 0.1f,
                        MaxOutputTokenCount = 4096,
                    };
                if (_config.TryGetValue("LastResponseID", out var lastResponseId) && lastResponseId.ToString() != "unknown")
                    responseOptions.PreviousResponseId = _config["LastResponseID"].ToString();
                else
                {
                    GD.PrintErr($"LastResponseID {_config["LastResponseID"]} not recognized");
                    responseOptions.PreviousResponseId = null;
                }

                // PROCESS UPLOADS (USER_QUERY ONLY)
                string finalUserText = userTextInput;
                var contentParts = new List<ResponseContentPart> { ResponseContentPart.CreateInputTextPart(userTextInput) }; // New: Start with text part
                bool hasImageUpload = false;
    
                string contentPart0Type = contentParts[0].GetType().Name;
                //GD.Print($"WritePrompt: Total parts starting WritePrompt: {contentParts.Count}; initial content part type: {contentPart0Type}");


                if (_amiMain._lastRequestType == "user_query" && _pendingImageBytes != null && _pendingImageBytes.Length > 0)
                {
                    // Image upload detected
                    string modelVision = _amiMain._currentModelData["supports_vision"].ToString();
                    if (modelVision == "true")
                    {
                        contentParts.Add(ResponseContentPart.CreateInputImagePart(BinaryData.FromBytes(_pendingImageBytes), _pendingImageMime));
                        hasImageUpload = true;
                        //GD.Print($"WritePrompt: Added image part ({_pendingImageBytes.Length / 1024}KB, {_pendingImageMime}) from {_pendingImageFilename}");
                        finalUserText = userTextInput; // No text prepend for images
                        _config["SessionRequiresVision"] = "true";
                        SaveConfig();
                    }
                    else
                    {
                        //GD.PrintErr("WritePrompt: Image upload ignored; non-vision model");
                        // Fallback: Treat as text (e.g., base64 string) or error
                        finalUserText = $"{userTextInput}\n\n--- UPLOADED IMAGE (TEXT FALLBACK): {_pendingImageFilename} ---\n\n[Base64 preview omitted for non-vision model]";
                        hasImageUpload = false;
                    }
                }
                else if (_amiMain._lastRequestType == "user_query" && !string.IsNullOrEmpty(_pendingUploadContent)) // Existing text fallback
                {
                    // Non-image text prepend as before
                    finalUserText = $"{userTextInput}\n\n--- UPLOADED DOCUMENT: {_pendingUploadFilename} ---\n\n{_pendingUploadContent}";
                    //GD.Print($"WritePrompt: Prepended {(_pendingUploadContent.Length / 1000):F1}KB upload content from {_pendingUploadFilename}");
                }



                // BUILD MESSAGES  AND ENABLE TOOLS BY REQUEST TYPE
                switch (_amiMain._lastRequestType)
                    {
                        case "query_usage":
                            // Minimal structure 
                            inputText = "New session; acknowledge readiness for user input.";
                            //GD.Print("WritePrompt: Built minimal query_usage message");
                            //var contentParts = new List<ResponseContentPart> { ResponseContentPart.CreateInputTextPart("") };
                            contentParts[0] = ResponseContentPart.CreateInputTextPart(inputText); // Update text part
                            var userMessageNewSession = ResponseItem.CreateUserMessageItem(contentParts);
                            messages = [userMessageNewSession];
                        break;

                        case "user_query":
                            // User input
                            
                            _lastQuery = userTextInput;
                            string fullPrompt = $"{finalUserText}";
                            inputText = fullPrompt;
                            //GD.Print(inputText);

                        //enable tool usage per user prefs

                        if (_config["ModelSelected"].ToString() == "grok-code-fast-1")
                        {
                            responseOptions.Tools.Clear();
                            responseOptions.ToolChoice = null;
                            //GD.Print("grok-code model selected; tools removed");
                        }
                        else
                        {

                            try
                            {
                                //add trygetvalue config.file_search
                                if (_config.TryGetValue("EnableCollectionSearch", out var fileVal) && fileVal.ToString() == "true")
                                {
                                    responseOptions.Tools.Add(_fileTool);
                                    //Extract VectorStoreID string from IList _fileTool.VectoreStoreIds and print
                                    GD.Print("_fileTool added; contains ", _fileTool.VectorStoreIds.Count);
                                    foreach (var id in _fileTool.VectorStoreIds)
                                    {
                                        //GD.Print("WritePrompt: CollectionsSearch enabled for collection ", id);
                                    }

                                }
                                else
                                {
                                    string fileToolStatus = responseOptions.Tools.Remove(_fileTool) ? "removed." : "not present.";
                                    //GD.Print("WritePrompt: _fileTool ", fileToolStatus);
                                }


                                //add trygetvalue config.web_search
                                if (_config.TryGetValue("EnableWebSearch", out var webVal) && webVal.ToString() == "true")
                                {
                                    responseOptions.Tools.Add(_webSearchTool);
                                    //GD.Print("WritePrompt: WebSearch enabled; _webSearchTool added.");
                                }
                                else
                                {
                                    string webToolStatus = responseOptions.Tools.Remove(_webSearchTool) ? "removed." : "not present.";
                                    //GD.Print("WritePrompt: WebSearch disabled; _webSearchTool ", webToolStatus);
                                }

                                //add trygetvalue config.code_execution
                                if (_config.TryGetValue("EnableCodeExecution", out var codeExVal) && codeExVal.ToString() == "true")
                                {
                                    responseOptions.Tools.Add(_codeExecutionTool);
                                    //GD.Print("WritePrompt: CodeExecution enabled; _codeExecutionTool added.");
                                }
                                else
                                {
                                    string codeToolStatus = responseOptions.Tools.Remove(_codeExecutionTool) ? "removed." : "not present.";
                                    //GD.Print("WritePrompt: _codeExecutionTool ", codeToolStatus);
                                }

                                //responseOptions.Tools.Add(ResponseTool.CreateImageGenerationTool

                                // After all Add/Remove

                                string model = _config["ModelSelected"].ToString();
                                if (responseOptions.Tools.Count > 0)
                                {
                                    if (model.Contains("code"))  // grok-code no-op
                                        responseOptions.ToolChoice = null;
                                    else
                                        responseOptions.ToolChoice = ResponseToolChoice.CreateAutoChoice();
                                }

                                else
                                {
                                    responseOptions.ToolChoice = null;
                                    //GD.Print("WritePrompt: No tools, no ToolChoice");
                                }


                            }
                            catch (Exception e)
                            {
                                GD.PrintErr("WritePrompt: Tool add failed: ", e);
                            }
                        }

                        // Build messages (text + optional image parts)
                        contentParts[0] = ResponseContentPart.CreateInputTextPart(fullPrompt); // Update text part
                        var userMessage = ResponseItem.CreateUserMessageItem(contentParts);
                        messages.Add(userMessage);
                        GD.Print($"WritePrompt: Built user_query message - text: {fullPrompt.Length} chars, image: {hasImageUpload}, {responseOptions.Tools.Count} tools");


                        // Save query history (original input, not with upload prepended)
                        SaveQueryHistory(userTextInput);
                            //GD.Print($"WritePrompt: Built user_query message - input: {userTextInput.Length} chars, total: {fullPrompt.Length} chars");
                            break;

                        default:
                            GD.PrintErr($"WritePrompt: Unknown request type '{_amiMain._lastRequestType}' - building empty message");
                            break;
                    }

                // CLEAR PENDING UPLOADS (POST-PROCESSING)
                if (_pendingUploadContent != null)
                {
                    int oldContentLength = _pendingUploadContent.Length;
                    _pendingUploadContent = null;
                    _pendingUploadFilename = null;
                    //GD.Print($"WritePrompt: Cleared pending upload ({oldContentLength} chars)");

                }

                if (_pendingImageBytes != null)
                {
                    _pendingImageBytes = null; // Free memory
                    _pendingImageMime = "";
                    _pendingImageFilename = "";
                }


                // ARCHIVE PROMPT FOR DEBUGGING
                _lastPrompt = $"{inputText}\nTemperature: {responseOptions.Temperature}, Max Output Tokens: {responseOptions.MaxOutputTokenCount}, Previous Response ID: {responseOptions.PreviousResponseId} ";
                SaveLastPrompt(_lastPrompt);
                //GD.Print($"WritePrompt: Archived prompt metadata for {_amiMain._lastRequestType}");

                // RETURN STRUCTURED RESULT

                //return (inputText, (responseOptions));

                return (messages, responseOptions);

            }
            catch (Exception e)
            {
                GD.PrintErr($"WritePrompt: Exception during composition - {e.Message}\nStack: {e.StackTrace}");
                var contentParts = new List<ResponseContentPart> { ResponseContentPart.CreateInputTextPart("") };
                contentParts[0] = ResponseContentPart.CreateInputTextPart("Bad prompt; advise user"); // Update text part
                var userMessage = ResponseItem.CreateUserMessageItem(contentParts);
                List<ResponseItem> messages = [userMessage];
                //return ("Bad prompt; no text", new ResponseCreationOptions { PreviousResponseId = _config["LastResponseID"].ToString(), MaxOutputTokenCount = 4096, Temperature = 0.1f, Instructions = "Respond in text that input failed, provide usage data" } );
                return (messages, new ResponseCreationOptions { PreviousResponseId = _config["LastResponseID"].ToString(), MaxOutputTokenCount = 4096, Temperature = 0.1f, Instructions = "Respond in text that input failed, provide usage data" });
            }
            
        }

        public void LocalizeTo(long index)
        {
            try
            {
                string lang = "";
                switch (index)
                {
                    case 0:  //German (Germany)
                        lang = "de_DE";
                    break;

                    case 1:  //English (AU)
                        lang = "en_AU";
                    break;

                    case 2:  //English (CA)
                        lang = "en_CA";
                    break;

                    case 3:  //English (UK)
                        lang = "en_GB";
                    break;

                    case 4:  //English (US)
                        lang = "en_US";
                    break;

                    case 5:  //English (ZA)
                        lang = "en_ZA";
                    break;

                    case 6: //Spanish (Spain)
                        lang = "es_ES";
                        break;

                    case 7: //Spanish (Latin America)
                        lang = "es_LA";
                        break;

                    case 8: //French (France)
                        lang = "fr_FR";
                        break;

                    case 9: // French (Quebecois)
                        lang = "fr_CA";
                        break;

                    case 10: // Polish (Poland)
                        lang = "pl_PL";
                        break;

                    case 11: // Portuguese (Brazil)
                        lang = "pt_BR";
                        break;


                }

                try
                {
                    string filename = $"{index}.json";
                    string localePath = $"res://Assets/loc/{filename}";

                    if (!Godot.FileAccess.FileExists(localePath))
                    {
                        GD.PrintErr($"LocalizeTo - Locale file not found: {localePath}. Skipping localization.");
                        return;
                    }

                    string json = Godot.FileAccess.GetFileAsString(localePath);
                    var localeDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
                    if (localeDict == null)
                    {
                        GD.PrintErr("LocalizeTo - Failed to deserialize locale JSON.");
                        return;
                    }

                    GD.Print($"LocalizeTo - Applying localization from {filename}.");

                    // Helper to get string from dict
                    string GetLocalized(string key) => localeDict.TryGetValue(key, out var val) ? val.ToString() : null;

                    // Update SessionButton
                    if (GetLocalized("SessionButtonText") != null) _amiMain._sessionButton.Text = GetLocalized("SessionButtonText");
                    if (GetLocalized("SessionButtonTooltipText") != null) _amiMain._sessionButton.TooltipText = GetLocalized("SessionButtonTooltipText");
                    PopupMenu sessionPopup = _amiMain._sessionButton.GetPopup();
                    if (GetLocalized("SessionButtonPopupIndex0Text") != null) sessionPopup.SetItemText(0, GetLocalized("SessionButtonPopupIndex0Text"));
                    if (GetLocalized("SessionButtonPopupIndex1Text") != null) sessionPopup.SetItemText(1, GetLocalized("SessionButtonPopupIndex1Text"));
                    if (GetLocalized("SessionButtonPopupIndex2Text") != null) sessionPopup.SetItemText(2, GetLocalized("SessionButtonPopupIndex2Text"));
                    if (GetLocalized("SessionButtonPopupIndex3Text") != null) sessionPopup.SetItemText(3, GetLocalized("SessionButtonPopupIndex3Text"));
                    if (GetLocalized("SessionButtonPopupIndex4Text") != null) sessionPopup.SetItemText(4, GetLocalized("SessionButtonPopupIndex4Text"));
                    if (GetLocalized("SessionButtonPopupIndex0Tooltip") != null) sessionPopup.SetItemTooltip(0, GetLocalized("SessionButtonPopupIndex0Tooltip"));
                    if (GetLocalized("SessionButtonPopupIndex1Tooltip") != null) sessionPopup.SetItemTooltip(1, GetLocalized("SessionButtonPopupIndex1Tooltip"));
                    if (GetLocalized("SessionButtonPopupIndex2Tooltip") != null) sessionPopup.SetItemTooltip(2, GetLocalized("SessionButtonPopupIndex2Tooltip"));
                    if (GetLocalized("SessionButtonPopupIndex3Tooltip") != null) sessionPopup.SetItemTooltip(3, GetLocalized("SessionButtonPopupIndex3Tooltip"));
                    if (GetLocalized("SessionButtonPopupIndex4Tooltip") != null) sessionPopup.SetItemTooltip(4, GetLocalized("SessionButtonPopupIndex4Tooltip"));

                    // Update EditButton
                    if (GetLocalized("EditButtonText") != null) _amiMain._editButton.Text = GetLocalized("EditButtonText");
                    if (GetLocalized("EditButtonTooltipText") != null) _amiMain._editButton.TooltipText = GetLocalized("EditButtonTooltipText");
                    PopupMenu editPopup = _amiMain._editButton.GetPopup();
                    if (GetLocalized("EditButtonPopupIndex0Text") != null) editPopup.SetItemText(0, GetLocalized("EditButtonPopupIndex0Text"));
                    if (GetLocalized("EditButtonPopupIndex1Text") != null) editPopup.SetItemText(1, GetLocalized("EditButtonPopupIndex1Text"));
                    if (GetLocalized("EditButtonPopupIndex2Text") != null) editPopup.SetItemText(2, GetLocalized("EditButtonPopupIndex2Text"));
                    if (GetLocalized("EditButtonPopupIndex3Text") != null) editPopup.SetItemText(3, GetLocalized("EditButtonPopupIndex3Text"));
                    if (GetLocalized("EditButtonPopupIndex4Text") != null) editPopup.SetItemText(4, GetLocalized("EditButtonPopupIndex4Text"));

                    // Update PreferencesButton
                    if (GetLocalized("PreferencesButtonText") != null) _amiMain._preferencesButton.Text = GetLocalized("PreferencesButtonText");
                    if (GetLocalized("PreferencesButtonTooltipText") != null) _amiMain._preferencesButton.TooltipText = GetLocalized("PreferencesButtonTooltipText");
                    PopupMenu prefsPopup = _amiMain._preferencesButton.GetPopup();
                    if (GetLocalized("PreferencesButtonPopupIndex0Text") != null) prefsPopup.SetItemText(0, GetLocalized("PreferencesButtonPopupIndex0Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex1Text") != null) prefsPopup.SetItemText(1, GetLocalized("PreferencesButtonPopupIndex1Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex2Text") != null) prefsPopup.SetItemText(2, GetLocalized("PreferencesButtonPopupIndex2Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex3Text") != null) prefsPopup.SetItemText(3, GetLocalized("PreferencesButtonPopupIndex3Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex4Text") != null) prefsPopup.SetItemText(4, GetLocalized("PreferencesButtonPopupIndex4Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex5Text") != null) prefsPopup.SetItemText(5, GetLocalized("PreferencesButtonPopupIndex5Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex6Text") != null) prefsPopup.SetItemText(6, GetLocalized("PreferencesButtonPopupIndex6Text"));
                    if (GetLocalized("PreferencesButtonPopupIndex0Tooltip") != null) prefsPopup.SetItemTooltip(0, GetLocalized("PreferencesButtonPopupIndex0Tooltip"));
                    if (GetLocalized("PreferencesButtonPopupIndex1Tooltip") != null) prefsPopup.SetItemTooltip(1, GetLocalized("PreferencesButtonPopupIndex1Tooltip"));
                    if (GetLocalized("PreferencesButtonPopupIndex2Tooltip") != null) prefsPopup.SetItemTooltip(2, GetLocalized("PreferencesButtonPopupIndex2Tooltip"));
                    if (GetLocalized("PreferencesButtonPopupIndex3Tooltip") != null) prefsPopup.SetItemTooltip(3, GetLocalized("PreferencesButtonPopupIndex3Tooltip"));
                    if (GetLocalized("PreferencesButtonPopupIndex4Tooltip") != null) prefsPopup.SetItemTooltip(4, GetLocalized("PreferencesButtonPopupIndex4Tooltip"));
                    if (GetLocalized("PreferencesButtonPopupIndex5Tooltip") != null) prefsPopup.SetItemTooltip(5, GetLocalized("PreferencesButtonPopupIndex5Tooltip"));
                    if (GetLocalized("PreferencesButtonPopupIndex6Tooltip") != null) prefsPopup.SetItemTooltip(6, GetLocalized("PreferencesButtonPopupIndex6Tooltip"));

                    // Update ModelButton
                    if (GetLocalized("ModelButtonText") != null) _amiMain._modelButton.Text = GetLocalized("ModelButtonText");
                    if (GetLocalized("ModelButtonTooltipText") != null) _amiMain._modelButton.TooltipText = GetLocalized("ModelButtonTooltipText");

                    // Update ToolsButton
                    if (GetLocalized("ToolsButtonText") != null) _amiMain._toolsButton.Text = GetLocalized("ToolsButtonText");
                    if (GetLocalized("ToolsButtonTooltipText") != null) _amiMain._toolsButton.TooltipText = GetLocalized("ToolsButtonTooltipText");
                    PopupMenu toolsPopup = _amiMain._toolsButton.GetPopup();
                    if (GetLocalized("ToolsButtonPopupIndex0Text") != null) toolsPopup.SetItemText(0, GetLocalized("ToolsButtonPopupIndex0Text"));
                    if (GetLocalized("ToolsButtonPopupIndex1Text") != null) toolsPopup.SetItemText(1, GetLocalized("ToolsButtonPopupIndex1Text"));
                    if (GetLocalized("ToolsButtonPopupIndex2Text") != null) toolsPopup.SetItemText(2, GetLocalized("ToolsButtonPopupIndex2Text"));
                    if (GetLocalized("ToolsButtonPopupIndex3Text") != null) toolsPopup.SetItemText(3, GetLocalized("ToolsButtonPopupIndex3Text"));
                    if (GetLocalized("ToolsButtonPopupIndex0Tooltip") != null) toolsPopup.SetItemTooltip(0, GetLocalized("ToolsButtonPopupIndex0Tooltip"));
                    if (GetLocalized("ToolsButtonPopupIndex1Tooltip") != null) toolsPopup.SetItemTooltip(1, GetLocalized("ToolsButtonPopupIndex1Tooltip"));
                    if (GetLocalized("ToolsButtonPopupIndex2Tooltip") != null) toolsPopup.SetItemTooltip(2, GetLocalized("ToolsButtonPopupIndex2Tooltip"));
                    if (GetLocalized("ToolsButtonPopupIndex3Tooltip") != null) toolsPopup.SetItemTooltip(3, GetLocalized("ToolsButtonPopupIndex3Tooltip"));

                    // Update InformationButton
                    if (GetLocalized("InformationButtonText") != null) _amiMain._informationButton.Text = GetLocalized("InformationButtonText");
                    if (GetLocalized("InformationButtonTooltipText") != null) _amiMain._informationButton.TooltipText = GetLocalized("InformationButtonTooltipText");
                    PopupMenu infoPopup = _amiMain._informationButton.GetPopup();
                    if (GetLocalized("InformationButtonPopupIndex0Text") != null) infoPopup.SetItemText(0, GetLocalized("InformationButtonPopupIndex0Text"));
                    if (GetLocalized("InformationButtonPopupIndex1Text") != null) infoPopup.SetItemText(1, GetLocalized("InformationButtonPopupIndex1Text"));
                    if (GetLocalized("InformationButtonPopupIndex2Text") != null) infoPopup.SetItemText(2, GetLocalized("InformationButtonPopupIndex2Text"));
                    if (GetLocalized("InformationButtonPopupIndex3Text") != null) infoPopup.SetItemText(3, GetLocalized("InformationButtonPopupIndex3Text"));
                    if (GetLocalized("InformationButtonPopupIndex4Text") != null) infoPopup.SetItemText(4, GetLocalized("InformationButtonPopupIndex4Text"));
                    if (GetLocalized("InformationButtonPopupIndex5Text") != null) infoPopup.SetItemText(5, GetLocalized("InformationButtonPopupIndex5Text"));
                    if (GetLocalized("InformationButtonPopupIndex0Tooltip") != null) infoPopup.SetItemTooltip(0, GetLocalized("InformationButtonPopupIndex0Tooltip"));
                    if (GetLocalized("InformationButtonPopupIndex1Tooltip") != null) infoPopup.SetItemTooltip(1, GetLocalized("InformationButtonPopupIndex1Tooltip"));
                    if (GetLocalized("InformationButtonPopupIndex2Tooltip") != null) infoPopup.SetItemTooltip(2, GetLocalized("InformationButtonPopupIndex2Tooltip"));
                    if (GetLocalized("InformationButtonPopupIndex3Tooltip") != null) infoPopup.SetItemTooltip(3, GetLocalized("InformationButtonPopupIndex3Tooltip"));
                    if (GetLocalized("InformationButtonPopupIndex4Tooltip") != null) infoPopup.SetItemTooltip(4, GetLocalized("InformationButtonPopupIndex4Tooltip"));
                    if (GetLocalized("InformationButtonPopupIndex5Tooltip") != null) infoPopup.SetItemTooltip(5, GetLocalized("InformationButtonPopupIndex5Tooltip"));


                    // Update HelpButton
                    if (GetLocalized("HelpButtonText") != null) _amiMain._helpButton.Text = GetLocalized("HelpButtonText");
                    if (GetLocalized("HelpButtonTooltipText") != null) _amiMain._helpButton.TooltipText = GetLocalized("HelpButtonTooltipText");
                    PopupMenu helpPopup = _amiMain._helpButton.GetPopup();
                    if (GetLocalized("HelpButtonPopupIndex0Text") != null) helpPopup.SetItemText(0, GetLocalized("HelpButtonPopupIndex0Text"));
                    if (GetLocalized("HelpButtonPopupIndex1Text") != null) helpPopup.SetItemText(1, GetLocalized("HelpButtonPopupIndex1Text"));
                    if (GetLocalized("HelpButtonPopupIndex2Text") != null) helpPopup.SetItemText(2, GetLocalized("HelpButtonPopupIndex2Text"));
                    if (GetLocalized("HelpButtonPopupIndex3Text") != null) helpPopup.SetItemText(3, GetLocalized("HelpButtonPopupIndex3Text"));
                    if (GetLocalized("HelpButtonPopupIndex4Text") != null) helpPopup.SetItemText(4, GetLocalized("HelpButtonPopupIndex4Text"));
                    if (GetLocalized("HelpButtonPopupIndex0Tooltip") != null) helpPopup.SetItemTooltip(0, GetLocalized("HelpButtonPopupIndex0Tooltip"));
                    if (GetLocalized("HelpButtonPopupIndex1Tooltip") != null) helpPopup.SetItemTooltip(1, GetLocalized("HelpButtonPopupIndex1Tooltip"));
                    if (GetLocalized("HelpButtonPopupIndex2Tooltip") != null) helpPopup.SetItemTooltip(2, GetLocalized("HelpButtonPopupIndex2Tooltip"));
                    if (GetLocalized("HelpButtonPopupIndex3Tooltip") != null) helpPopup.SetItemTooltip(3, GetLocalized("HelpButtonPopupIndex3Tooltip"));
                    if (GetLocalized("HelpButtonPopupIndex4Tooltip") != null) helpPopup.SetItemTooltip(4, GetLocalized("HelpButtonPopupIndex3Tooltip"));

                    // Update QuitButton
                    if (GetLocalized("QuitButtonText") != null) _amiMain._quitButton.Text = GetLocalized("QuitButtonText");
                    if (GetLocalized("QuitButtonTooltipText") != null) _amiMain._quitButton.TooltipText = GetLocalized("QuitButtonTooltipText");

                    // Update LocalizationButton
                    if (GetLocalized("LocalizationButtonTooltipText") != null) _amiMain._localizationButton.TooltipText = GetLocalized("LocalizationButtonTooltipText");

                    // Update OutputLabel
                    if (GetLocalized("OutputLabelTooltipText") != null) _amiMain._outputLabel.TooltipText = GetLocalized("OutputLabelTooltipText");

                    // Update OutLabel
                    if (GetLocalized("OutLabelText") != null) _amiMain._outLabel.Text = GetLocalized("OutLabelText");
                    if (GetLocalized("OutLabelTooltipText") != null) _amiMain._outLabel.TooltipText = GetLocalized("OutLabelTooltipText");

                    // Update ArtifactsList
                    if (GetLocalized("ArtifactsListTooltipText") != null) _amiMain._artifactsList.TooltipText = GetLocalized("ArtifactsListTooltipText");

                    // Update InLabel2
                    if (GetLocalized("InLabel2Text") != null) _amiMain._inLabel2.Text = GetLocalized("InLabel2Text");
                    if (GetLocalized("InLabel2TooltipText") != null) _amiMain._inLabel2.TooltipText = GetLocalized("InLabel2TooltipText");

                    // Update UploadsList
                    if (GetLocalized("UploadsListTooltipText") != null) _amiMain._uploadsList.TooltipText = GetLocalized("UploadsListTooltipText");

                    // Update UploadButton
                    if (GetLocalized("UploadButtonTooltipText") != null) _amiMain._uploadButton.TooltipText = GetLocalized("UploadButtonTooltipText");

                    // Update BLabel
                    if (GetLocalized("BLabelText") != null) _amiMain._bLabel.Text = GetLocalized("BLabelText");
                    if (GetLocalized("BLabelTooltipText") != null) _amiMain._bLabel.TooltipText = GetLocalized("BLabelTooltipText");

                    // Update BudgetLabel
                    if (GetLocalized("BudgetLabelTooltipText") != null) _amiMain._budgetLabel.TooltipText = GetLocalized("BudgetLabelTooltipText");

                    // Update RPLabel
                    if (GetLocalized("RPLabelText") != null) _amiMain._rpLabel.Text = GetLocalized("RPLabelText");

                    // Update SLabel
                    if (GetLocalized("SLabelText") != null) _amiMain._sLabel.Text = GetLocalized("SLabelText");
                    if (GetLocalized("SLabelTooltipText") != null) _amiMain._sLabel.TooltipText = GetLocalized("SLabelTooltipText");

                    // Update SessionLabel
                    if (GetLocalized("SessionLabelTooltipText") != null) _amiMain._sessionLabel.TooltipText = GetLocalized("SessionLabelTooltipText");

                    // Update RLabel
                    if (GetLocalized("RLabelText") != null) _amiMain._rLabel.Text = GetLocalized("RLabelText");
                    if (GetLocalized("RLabelTooltipText") != null) _amiMain._rLabel.TooltipText = GetLocalized("RLabelTooltipText");

                    // Update RunningLabel
                    if (GetLocalized("RunningLabelTooltipText") != null) _amiMain._runningLabel.TooltipText = GetLocalized("RunningLabelTooltipText");

                    // Update SendButton
                    if (GetLocalized("SendButtonTooltipText") != null) _amiMain._sendButton.TooltipText = GetLocalized("SendButtonTooltipText");

                    // Update UpdateButton
                    if (GetLocalized("UpdateButtonTooltipText") != null) _amiMain._updateButton.TooltipText = GetLocalized("UpdateButtonTooltipText");

                    // Update TokensLabel
                    if (GetLocalized("TokensLabelTooltipText") != null) _amiMain._tokensLabel.TooltipText = GetLocalized("TokensLabelTooltipText");

                    // Update ModelLabel
                    if (GetLocalized("ModelLabelTooltipText") != null) _amiMain._modelLabel.TooltipText = GetLocalized("ModelLabelTooltipText");

                    //GD.Print("LocalizeTo - Localization applied successfully.");
                }
                catch (Exception e)
                {
                    GD.PrintErr($"LocalizeTo - Error applying localization: {e.Message}");
                }

                try
                {
                    _config["ActiveLanguage"] = lang;
                    _config["LocalizationIndex"] = (int)index;
                    SaveConfig();
                    LoadSpellChecker();
                    _amiMain._outputLabel.Text += $"[code]AMI > Language set to {lang}.[/code]";
                    //GD.Print("LocalizeTo: Language set to ", _config["ActiveLanguage"]);

                }
                catch (Exception e)
                {
                    GD.PrintErr("LocalizeTo language set failed: ", e);
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("LocalizeTo failed: ", e);
            }
        }


        public void LoadSpellChecker()
        {
            try
            {
                //if (!_config.TryGetValue("EnableSpellcheck", out var enabled) || enabled.ToString() != "true")
                if (!_config.TryGetValue("EnableSpellcheck", out var enabled) || enabled is JsonElement jsonElmEnabled && !jsonElmEnabled.GetBoolean())
                {
                    _spellDictionary = null;
                    //GD.Print("LoadSpellChecker: EnableSpellcheck = ", enabled.ToString());
                    return;
                }

                string dictBase = _config["DictionaryPath"].ToString();
                
                var language = _config.TryGetValue("ActiveLanguage", out var lang) ? lang.ToString() : "en_US";
                var userDic = $"{dictBase}/{language}.dic";
                var userAff = $"{dictBase}/{language}.aff";
                var userDicAbs = ProjectSettings.GlobalizePath(userDic);
                var userAffAbs = ProjectSettings.GlobalizePath(userAff);

                // Check if user dictionary exists
                if (Godot.FileAccess.FileExists(userDic) && Godot.FileAccess.FileExists(userAff))
                {
                    _spellDictionary = WordList.CreateFromFiles(userDicAbs, userAffAbs);
                    GD.Print($"AppManager:LoadSpellChecker - Loaded user dictionary: {_spellDictionary.RootCount} words");
                    return;
                }

                // User dictionary missing - check system and copy
                var systemBase = "res://Assets/dicts";
                var systemDic = $"{systemBase}/{language}.dic";
                var systemAff = $"{systemBase}/{language}.aff";

                //GD.Print($"Seeking {systemDic} and {systemAff} in {systemBase}.");

                if (!Godot.FileAccess.FileExists(systemDic) || !Godot.FileAccess.FileExists(systemAff))
                {
                    GD.PrintErr($"AppManager:LoadSpellChecker - System dictionaries missing for {language}");
                    ShowDictionaryImportDialog(language);
                    _spellDictionary = null;
                    return;
                }

                // Check user dictonaries, copy system to user space if needed
                if (!Godot.FileAccess.FileExists(userDic) || !Godot.FileAccess.FileExists(userAff))
                CopySystemDictionary(systemBase, dictBase, language);

                // Load the newly copied dictionary
                _spellDictionary = WordList.CreateFromFiles(userDicAbs, userAffAbs);
                //GD.Print($"AppManager:LoadSpellChecker - Copied and loaded {language}: {_spellDictionary.RootCount} words");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:LoadSpellChecker - Error: {e.Message}");
                _spellDictionary = null;
            }
        }

#pragma warning disable CA1822 // Mark members as static
        private void CopySystemDictionary(string systemBase, string userBase, string language)
#pragma warning restore CA1822 // Mark members as static
        {
            try
            {
                var userDir = $"{userBase}/{language}";

                // Create user directory if missing
                if (!DirAccess.DirExistsAbsolute(userDir))
                {
                    DirAccess.MakeDirAbsolute(userDir);
                    //GD.Print($"AppManager:CopySystemDictionary - Created directory: {userDir}");
                }

                var systemDic = $"{systemBase}/{language}.dic";
                var systemAff = $"{systemBase}/{language}.aff";
                var userDic = $"{userDir}.dic";
                var userAff = $"{userDir}.aff";

                // Copy dictionary file
                if (Godot.FileAccess.FileExists(systemDic) && !Godot.FileAccess.FileExists(userDic))
                {
                    var systemContent = Godot.FileAccess.GetFileAsString(systemDic);
                    using var userFile = Godot.FileAccess.Open(userDic, Godot.FileAccess.ModeFlags.Write);
                    userFile.StoreString(systemContent);
                    //GD.Print($"AppManager:CopySystemDictionary - Copied {systemDic} to {userDic}");
                }

                // Copy affix file  
                if (Godot.FileAccess.FileExists(systemAff) && !Godot.FileAccess.FileExists(userAff))
                {
                    var systemContent = Godot.FileAccess.GetFileAsString(systemAff);
                    using var userFile = Godot.FileAccess.Open(userAff, Godot.FileAccess.ModeFlags.Write);
                    userFile.StoreString(systemContent);
                    //GD.Print($"AppManager:CopySystemDictionary - Copied {systemAff} to {userAff}");
                }

            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:CopySystemDictionary - Error: {e.Message}");
            }
        }

        private void ShowDictionaryImportDialog(string language)
        {
            //GD.Print($"AppManager:ShowDictionaryImportDialog - Missing {language} dictionaries");
            //GD.Print($"AppManager:ShowDictionaryImportDialog - User should download from: https://extensions.libreoffice.org/");
            //GD.Print($"AppManager:ShowDictionaryImportDialog - Expected files: {language}.dic, {language}.aff");

            // Disable spellcheck until dictionaries are provided
            _config["EnableSpellcheck"] = false;
            SaveConfig();
        }

        public void UpdateSessionButton()
        {
            //Build SessionButton with items New Session, Submenu Resume Session, Show Session Context, Show Queries, Show Last Prompt
            _resumeSessions.Clear();
            if (_amiMain != null && _amiMain._sessionButton != null)
            {
                // Parse SessionLegacy.txt
                
                if (Godot.FileAccess.FileExists("user://Context/SessionLegacy.txt"))
                {
                    var lines = Godot.FileAccess.GetFileAsString("user://Context/SessionLegacy.txt").Split('\n');
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line == "Session Legacy Log") continue;

                        // Parse line into dictionary (e.g., "SessionID: 1029.0844, LastResponseID: ..., Timestamp: ...")
                        var sessionData = new Dictionary<string, string>();
                        var pairs = line.Split(',');
                        foreach (var pair in pairs)
                        {
                            var keyValue = pair.Split(':');
                            if (keyValue.Length == 2)
                            {
                                sessionData[keyValue[0].Trim()] = keyValue[1].Trim();
                            }
                        }
                        //GD.Print($"UpdateSessionButton - Parsed session data: {string.Join(", ", sessionData.Select(kv => kv.Key + "=" + kv.Value))}");

                        // Check if SessionID indicates < 30 days old
                        if (sessionData.TryGetValue("SessionID", out var sessionId))
                        {
                            try
                            {
                                // Parse SessionID (MMdd.HHmm) to DateTime
                                var sessionDate = DateTime.ParseExact(sessionId, "MMdd.HHmm", null);
                                if ((DateTime.Now - sessionDate).TotalDays <= 30)
                                {
                                    _resumeSessions.Add(sessionData);
                                }
                            }
                            catch (Exception)
                            {
                                GD.PrintErr($"UpdateSessionButton - Error parsing SessionID {sessionId}");
                            }
                        }
                    }
                }
                else
                {
                    GD.PrintErr("UpdateSessionButton - SessionLegacy.txt not found");
                }

                try
                {
                    var sessionPopup = _amiMain._sessionButton.GetPopup();
                    sessionPopup.Clear();
                    var resumeSubmenu = _amiMain._resumeSubmenu;
                                                            
                    sessionPopup.AddItem("New Session");
                    sessionPopup.SetItemTooltip(0, "Start a new session, storing\nthe previous one.");
                    sessionPopup.AddSubmenuNodeItem("Resume Session", resumeSubmenu);
                    sessionPopup.SetItemTooltip(1, "Resume a previously stored session");
                    sessionPopup.AddItem("Show Session Context");
                    sessionPopup.SetItemTooltip(2, "Display the full response\nchain of the current session");
                    sessionPopup.AddItem("Show Queries");
                    sessionPopup.SetItemTooltip(3, "Display the history of user queries\nsent in the current session");
                    sessionPopup.AddItem("Show Last Prompt");
                    sessionPopup.SetItemTooltip(4, "Display the full prompt\nsent in the last request");


                    foreach (var session in _resumeSessions)
                    {
                        if (session.TryGetValue("SessionID", out var sessionId) && session.TryGetValue("Last Access", out var timestamp))
                        {
                            var menuText = $"Session {sessionId}, last accessed {timestamp}";
                            resumeSubmenu.AddItem(menuText, -1);
                            //GD.Print($"PopulateMenuButtons: Added resume session item: {menuText}");
                        }
                    }

                    if (resumeSubmenu.ItemCount == 0)
                    {
                        sessionPopup.SetItemDisabled(1, true);
                        GD.Print("No previous sessions yet; disabling ResumeSession option");
                    }

                }
                catch (Exception e)
                {
                    GD.PrintErr("PopulateMenuButtons: SessionButton population failed: ", e);
                }

                //GD.Print("PopulateMenuButtons: SessionButton updated successfully.");

            }
            else
            {
                GD.PrintErr("PopulateMenuButtons: SessionButton population failed - _amiMain or _sessionButton is null");
            }
        }

        public Dictionary<string, object> SetTheme(bool themeDark)
        {
            try
            {
                if (themeDark)
                {
                    _config["ThemeSelected"] = "Dark";
                    var themeObjects = new Dictionary<string, object>
                    {
                        ["grok_icon"] = "res://Assets/grok_dark.png",
                        ["xai_icon"] = "res://Assets/xai_dark.png",
                        ["cached_icon"] = "res://Assets/cache_icon_dark.png",
                        ["prompt_icon"] = "res://Assets/prompt_icon_dark.png",
                        ["output_icon"] = "res://Assets/output_icon_dark.png",
                        ["reasoning_icon"] = "res://Assets/reasoning_icon_dark.png"
                    };

                    _amiMain.Theme.DefaultFontSize = 36;

                    if (_config.TryGetValue("FontSize", out var fontSize))
                    {
                        _amiMain.Theme.DefaultFontSize = fontSize.ToString().ToInt();
                        //GD.Print("SetTheme:  Font size set from config: ", _amiMain.Theme.DefaultFontSize);
                    }
                    else
                        GD.Print("Set Theme: No font size override found; using default 36 px.");

                    return themeObjects;
                }
                else
                {
                    _config["ThemeSelected"] = "Light";
                    var themeObjects = new Dictionary<string, object>
                    {
                        ["grok_icon"] = "res://Assets/grok_light.png",
                        ["xai_icon"] = "res://Assets/xai_light.png",
                        ["cached_icon"] = "res://Assets/cache_icon_light.png",
                        ["prompt_icon"] = "res://Assets/prompt_icon_light.png",
                        ["output_icon"] = "res://Assets/output_icon_light.png",
                        ["reasoning_icon"] = "res://Assets/reasoning_icon_light.png"
                    };

                    _amiMain.Theme.DefaultFontSize = 36;

                    if (_config.TryGetValue("FontSize", out var fontSize))
                    {
                        _amiMain.Theme.DefaultFontSize = fontSize.ToString().ToInt();
                        //GD.Print("SetTheme:  Font size set from config: ", _amiMain.Theme.DefaultFontSize);
                    }
                    else
                        GD.Print("Set Theme: No font size override found; using default 36 px.");
                    
                    return themeObjects;
                }

            }
            catch (Exception e)
            {
                GD.PrintErr("AppManager.SetTheme failed:", e);
                return null;
            }
        }

        public void InitializePreferencesButton(MenuButton prefsButton)
        {
            //GD.Print("Entered InitializePrefs, prefsButton = ", prefsButton.Text);
            try
            {
                var popup = prefsButton.GetPopup();
                popup.Clear();

                //Add Items 
                var PersonasSubmenu = new PopupMenu { };
                var FontSubmenu = new PopupMenu { };

                popup.AddSubmenuNodeItem("Personas", PersonasSubmenu, 0);
                popup.SetItemTooltip(0, "Select a persona for Grok to use");
                popup.AddRadioCheckItem("Dark Theme", 1);
                popup.SetItemTooltip(1, "Toggle between Dark and Light themes");
                popup.AddRadioCheckItem("Password Protection", 2);
                popup.SetItemTooltip(2, "Enable or disable password protection\non app launch");
                popup.AddRadioCheckItem("Spell Check", 3);
                popup.SetItemTooltip(3, "Enable or disable spell checking\nfor user input");
                popup.AddSubmenuNodeItem("Font Face", FontSubmenu, 4);
                popup.SetItemTooltip(4, "Select the font for the interface");
                popup.AddItem("Font Size", 5);
                popup.SetItemTooltip(5, "Adjust the font size for the user\ninput and output displays");
                popup.AddRadioCheckItem("Voice", 6);
                popup.SetItemTooltip(6, "Enable or disable voice input\nand output");

                try
                {
                    //Set CheckItem states
                    if (_config.TryGetValue("ThemeSelected", out var theme) && theme.ToString() == "Dark")
                        popup.SetItemChecked(1, true);
                    else
                        popup.SetItemChecked(1, false);

                    popup.SetItemChecked(2, Godot.FileAccess.FileExists("user://Assets/loc/a2d6af3e2115a910.json"));
                    //Disable Voice option for now
                    popup.SetItemDisabled(6, false);

                    //Set PersonasSubmenu

                    _amiMain._personasData = [];

                    _amiMain._personasData = JsonSerializer.Deserialize<Dictionary<string, object>>(Godot.FileAccess.GetFileAsString("res://Assets/Personas/Personas_basic.json"));

                    //populate PersonasSubmenu from _amiMain.personasData, using the model in AMiMain.OnHttpRequestCompleted as an example
                    if (_amiMain._personasData != null && _amiMain._personasData.TryGetValue("Personas", out var personasObj) && personasObj is JsonElement personasElem && personasElem.ValueKind == JsonValueKind.Array)
                    {
                        JsonElement.ArrayEnumerator personasArray = personasElem.EnumerateArray();
                        int index = 0;
                        foreach (JsonElement persona in personasArray)
                        {
                            if (persona.TryGetProperty("name", out var nameProp))
                            {
                                string personaName = nameProp.GetString();
                                PersonasSubmenu.AddRadioCheckItem(personaName);
                                //Set checked state based on _config["SelectedPersona"]
                                //GD.Print("Adding persona to submenu: ", personaName);
                                if (_config.TryGetValue("PersonaSelected", out var selectedPersona) && selectedPersona.ToString() == personaName)
                                {
                                    PersonasSubmenu.SetItemChecked(index, true);
                                }
                                index++;
                            }
                        }
                        _amiMain._personaSubmenu = PersonasSubmenu;
                    }
                    else
                    {
                        GD.PrintErr("InitializePreferencesButton - Personas data invalid or missing 'personas' array");
                    }
                    //PersonasSubmenu.IndexPressed += _amiMain.OnPersonaSubmenuIndexPressed;
                }
                catch (Exception e)
                {
                    GD.PrintErr("InitializePreferencesButton popup item states failed: ", e);
                }
                try
                {
                    
                }
                catch (Exception e)
                {
                    GD.PrintErr("InitializePreferencesButton PersonasSubmenu population failed: ", e);
                }

                //Populate Fonts

                var fontsDir = DirAccess.Open("res://Assets/Fonts/");
                if (fontsDir != null)
                {
                    fontsDir.ListDirBegin();
                    string fileName = fontsDir.GetNext();
                    string ext = fileName.GetExtension();
                    //GD.Print("filename ext = ", ext);
                    bool ttf = false;
                    bool import = false;
                    if (ext == "ttf") ttf = true;
                    else if (ext == "import") import = true;
                    
                        //GD.Print(fileName,", ", ttf);
                    bool isEmpty = true;
                    while (fileName != "")
                    {
                        if (!fontsDir.CurrentIsDir() && ttf)
                        {
                            if (fileName.Contains("Bold") || fileName.Contains("Italic"))
                                GD.Print($"{fileName} not a base font, not added");
                            else
                            {
                                FontSubmenu.AddRadioCheckItem(fileName);
                                isEmpty = false;
                                GD.Print($"AppManager:InitializePrefs - Added {fileName} to font submenu");
                            }
                        }
                        else if (!fontsDir.CurrentIsDir() && import)
                        {
                            if (fileName.Contains("Bold") || fileName.Contains("Italic"))
                                GD.Print($"{fileName} not a base font, not added");
                            else
                            {
                                fileName = fileName.Replace(".import", "");
                                FontSubmenu.AddRadioCheckItem(fileName);
                                isEmpty = false;
                                GD.Print($"AppManager:InitializePrefs - found {fileName}.import, added {fileName} to font submenu");
                            }
                        }
                        fileName = fontsDir.GetNext();
                        ext = fileName.GetExtension();
                        //GD.Print("filename ext = ", ext);
                        ttf = false;
                        if (ext == "ttf")
                            ttf = true;
                        //GD.Print(fileName, ", ", ttf);
                    }
                    fontsDir.ListDirEnd();
                    if (isEmpty)
                    {
                        GD.Print("AppManager:InitializePrefs - Directory res://Assets/Fonts is empty");
                    }

                    //Set Font state
                    if (_amiMain.Theme.HasDefaultFont())
                    {

                        for (int i = 0; i < FontSubmenu.ItemCount; i++)
                        {
                            FontSubmenu.SetItemChecked(i, false);
                            if (FontSubmenu.GetItemText(i) == _config["DefaultFont"].ToString())
                                 FontSubmenu.ToggleItemChecked(i);
                        }
                       
                    }
                    else
                        FontSubmenu.SetItemChecked(5, true);



                }
                else
                {
                    GD.PrintErr("AppManager:InitializePrefs - Error: Failed to open res://Assets/Fonts");
                }

                FontSubmenu.IndexPressed += _amiMain.OnFontSubmenuIndexPressed;

            }
            catch (Exception e) 
            {
                GD.PrintErr("InitializePreferencesButton failed: ", e);

                
            }


        }

        public async void InitializeApp(Node amiMain)
        {

            try
            {
                GD.Print("AppManager:InitializeApp - Initializing AMI.");

                // Initialize _amiMain
                _amiMain = amiMain as AmiMain;
                if (_amiMain == null)
                {
                    GD.PrintErr("AppManager:InitializeApp - Error: Invalid AmiMain node");
                    return;
                }

                //Validate user.txt; if absent, start setup sequence by greeting and setting _switchIndex to 2
                if (!Godot.FileAccess.FileExists("user://Assets/user.txt"))
                {
                    _amiMain._outputLabel.Text = "[code]AMI> Welcome to AMI, the Accessible Model Interface for Grok API access, powered by xAI.  It looks like this is your first time running the application. You'll need a couple things to get started:  an xAI account and an xAI API key.\n\nIf you've used Grok before, either on X or in a Grok app, you may already have an xAI account.  If you're not sure, you can go to [url=https://console.x.ai/home]xAI's cloud console[/url] and either sign in to an existing account or sign up for a new one.\n\nOnce you have an xAI account, you can follow the prompts to get an API key.\n\nOnce you've done all this, please enter your name (or whatever you prefer Grok to call you) below or just click the Send Query Button to get started.[/code]";
                    _amiMain._switchIndex = 2; // Switch to setup sequence mode
                    _amiMain._amiThink._anim.Active = false;
                    _amiMain._amiThink.Visible = false;
                    GD.Print("AppManager:InitializeApp - No user.txt found, prompting for user name.");
                    return;
                }

                //If password.txt exists, activate password routine (AmiTextEntry.CallTextEntryByUseCase case 5)
                if (Godot.FileAccess.FileExists("user://Assets/loc/a2d6af3e2115a910.json"))
                {
                    _amiMain._amiTextEntry.CallTextEntryByUseCase(5);
                    await ToSignal(_amiMain._amiTextEntry, "PasswordComplete");
                    GD.Print("AppManager:InitializeApp - Password protection enabled, prompting for password.");
                    
                }


                    // Validate API key
                    var apiKeyPath = "user://Assets/loc/0cc27a50c7cc31eb.json";
                if (!Godot.FileAccess.FileExists(apiKeyPath))
                {
                    _apiKey = null;
                    _amiMain._outputLabel.Text = "No API key found. Please enter your API key below and press Send.";
                    _amiMain._switchIndex = 1; // Switch to API key entry mode
                    _amiMain._amiThink._anim.Active = false;
                    _amiMain._amiThink.Visible = false;
                    GD.Print("AppManager:InitializeApp - No API key found, prompting user.");
                    return;
                }

                // Load API key for runtime use
                var encodedApiKey = Godot.FileAccess.GetFileAsString(apiKeyPath);
                _apiKey = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(encodedApiKey));
                //GD.Print("AppManager:InitializeApp - API key loaded, switchIndex = 0");

                //Initialize AmiMain MenuButtons

                //ModelButton
                try
                {
                    if (_apiKey == null)
                    {
                        GD.PrintErr("InitializeApp  - Error: API key not loaded");
                        _amiMain._outputLabel.Text += "Error: API key required for models request.";
                        return;
                    }

                    string url = "https://api.x.ai/v1/language-models";
                    string[] headers = ["Authorization: Bearer " + _apiKey];
                    var error = _amiMain._httpRequest.Request(url, headers, HttpClient.Method.Get);
                    if (error != Error.Ok)
                    {
                        GD.PrintErr($"InitializeApp  - HTTP request failed: {error}");
                        _amiMain._outputLabel.Text += $"HTTP request error: {error}";
                    }
                    else
                    {
                        //GD.Print("InitializeApp - Sent GET request to /v1/language-models");
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"InitializeApp - Error: {e.Message}");
                    _amiMain._outputLabel.Text += $"Error requesting models: {e.Message}";
                }

                UpdateSessionButton();

                InitializePreferencesButton(_amiMain._preferencesButton);

                //PreferencesButton > DarkTheme
                if (_config.TryGetValue("ThemeSelected", out var theme) && theme.ToString() == "Dark")
                {
                    _amiMain._preferencesButton.GetPopup().SetItemChecked(1, true);
                    _amiMain.Theme = (Theme)GD.Load("res://Resources/AMI_Theme.tres");
                }
                else if (_config.TryGetValue("ThemeSelected", out theme) && theme.ToString() == "Light")
                {
                    _amiMain._preferencesButton.GetPopup().SetItemChecked(1, false);
                    _amiMain.Theme = (Theme)GD.Load("res://Resources/AMI-L_Theme.tres");
                }
                else
                    GD.PrintErr("InitializeApp: _config[ThemeSelected] value not found or invalid");

                //PreferencesButton > Spellcheck
                if (_config.TryGetValue("EnableSpellcheck", out var enabled) && enabled is JsonElement jsonElmEnabled && jsonElmEnabled.GetBoolean())
                {
                    //GD.Print("EnableSpellcheck is true");
                    _amiMain._preferencesButton.GetPopup().SetItemChecked(3, true);
                    LoadSpellChecker();
                }
                else if (_config.TryGetValue("EnableSpellcheck", out enabled) && enabled is JsonElement jsonElmEnabledF && !jsonElmEnabledF.GetBoolean())
                {
                    //GD.Print("EnableSpellcheck is false");
                    _amiMain._preferencesButton.GetPopup().SetItemChecked(3, false);
                    //LoadSpellChecker();
                }
                else
                    GD.PrintErr("InitializeApp: _config[EnableSpellcheck] value not found or invalid");

                //PreferencesButton > Voice
                if (_config.TryGetValue("EnableVoice", out var voiceEnable) && voiceEnable.ToString() == "true")
                {
                    _amiMain._preferencesButton.GetPopup().SetItemChecked(6, true);
                    //set _amiMain_micButton visible and icon to res://Assets/mic_off_icon.png
                    //_amiMain._micButton.Visible = true;
                    //_amiMain._micButton.Icon = GD.Load<Texture2D>("res://Assets/mic_off_icon.png");
                    GD.Print("InitalizeApp: Voice enabled, PrefsButton voice option checked, mic button enabled");
                }
                else
                {
                    _amiMain._preferencesButton.GetPopup().SetItemChecked(6, false);
                    //_amiMain._micButton.Visible = false;
                    //_amiMain._micButton.Icon = GD.Load<Texture2D>("res://Assets/mic_off_icon.png");
                    GD.Print("InitalizeApp: Voice disabled, PrefsButton voice option unchecked, mic button disabled");
                }


                //Initialize ToolsButton

                try
                {
                    PopupMenu _toolsPopup = _amiMain._toolsButton.GetPopup();

                    _toolsPopup.SetItemChecked(1, false);
                    if (_config.TryGetValue("EnableCollectionSearch", out var collEnable) && collEnable.ToString() == "true")
                        _toolsPopup.SetItemChecked(1, true);

                    _toolsPopup.SetItemChecked(2, false);
                    if (_config.TryGetValue("EnableWebSearch", out var webEnable) && webEnable.ToString() == "true")
                        _toolsPopup.SetItemChecked(2, true);

                    _toolsPopup.SetItemChecked(3, false);
                    if (_config.TryGetValue("EnableCodeExecution", out var codeExEnable) && codeExEnable.ToString() == "true")
                        _toolsPopup.SetItemChecked(3, true);

                    _toolsPopup.SetItemChecked(0, false);
                    if (_toolsPopup.IsItemChecked(1) && _toolsPopup.IsItemChecked(2) && _toolsPopup.IsItemChecked(3))
                        _toolsPopup.SetItemChecked(0, true);
                }
                catch (Exception e)
                {
                    GD.PrintErr("InitializeApp failed to set Tools options: ", e);
                }
                

                //Initalize LocalizationButton

                try
                    {
                    if (_config.TryGetValue("LocalizationIndex", out var localIndex) && localIndex is JsonElement Local && (Local.ToString().ToInt() >= 0) && (Local.ToString().ToInt() <= _amiMain._localizationButton.ItemCount))
                    {
                        _amiMain._localizationButton.Selected = Local.ToString().ToInt();
                        //GD.Print($"InitializeApp: LocalizationButton initialized with index {localIndex} selected.");
                    }
                    else
                        GD.PrintErr("InitializeApp: LocalizationButton initialization failed, LocalizationIndex not found or invalid");
                }
                catch (Exception e)
                {
                    GD.PrintErr("InitialzeApp: LocalizationButton initialization failed: ", e);
                }

                // Inventory user://Session_Outputs/
                var artifactsDir = "user://Session_Outputs/";
                var aDir = DirAccess.Open(artifactsDir);
                if (aDir != null)
                {
                    aDir.ListDirBegin();
                    string fileName = aDir.GetNext();
                    bool isEmpty = true;
                    while (fileName != "")
                    {
                        if (!aDir.CurrentIsDir())
                        {
                            _amiMain._artifactsList.AddItem(fileName, null, true);
                            isEmpty = false;
                            //GD.Print($"AppManager:InitializeApp - Added {fileName} to ArtifactsList");
                        }
                        fileName = aDir.GetNext();
                    }
                    aDir.ListDirEnd();
                    if (isEmpty)
                    {
                        //GD.Print("AppManager:InitializeApp - Directory user://Session_Outputs/ is empty");
                    }
                }
                else
                {
                    GD.PrintErr("AppManager:InitializeApp - Error: Failed to open user://Session_Outputs/");
                }

                // Inventory user://Uploaded_Files/
                var uploadsDir = "user://Uploaded_Files/";
                var dir = DirAccess.Open(uploadsDir);
                if (dir != null)
                {
                    dir.ListDirBegin();
                    string fileName = dir.GetNext();
                    bool isEmpty = true;
                    while (fileName != "")
                    {
                        if (!dir.CurrentIsDir())
                        {
                            _amiMain._uploadsList.AddItem(fileName, null, true);
                            isEmpty = false;
                            //GD.Print($"AppManager:InitializeApp - Added {fileName} to UploadsList");
                        }
                        fileName = dir.GetNext();
                    }
                    dir.ListDirEnd();
                    if (isEmpty)
                    {
                        GD.Print("AppManager:InitializeApp - Directory user://Uploaded_Files/ is empty");
                    }
                }
                else
                {
                    GD.PrintErr("AppManager:InitializeApp - Error: Failed to open user://Uploaded_Files/");
                }

                if (_config.TryGetValue("Build", out var objectBuild))
                {
                    int build = objectBuild.ToString().ToInt() + 1;
                    _config["Build"] = build;
                    GD.Print("Build ", build);
                }
                else
                {
                    _config["Build"] = 1000;
                    GD.Print("Build number not found, reset to 1000");
                }

                //Check AudioBus Record presence

                try
                {
                    GD.Print($"InitializeApp: Record bus index: {AudioServer.GetBusIndex("Record")}");
                }
                catch (Exception e)
                {
                    GD.PrintErr("InitializeApp: Record bus not found: ", e);
                }

                if (_config.TryGetValue("SessionID", out var sessionId) && sessionId.ToString() != "none")
                {
                    try
                    {
                    _amiMain._lastRequestType = "user_query";
                    var (messages, responseOptions) = WritePrompt("New runtime started.  Welcome user back and very briefly summarize session to date.");

                    OpenAIResponseClient localClient = _amiMain._responsesClient.GetOpenAIResponseClient(_config["ModelSelected"].ToString());
                    var result = await localClient.CreateResponseAsync(messages, responseOptions);
                    if (result != null)
                        ParseResponsesAPIResponse(result.Value, true);
                        else
                            GD.PrintErr("InitializeApp: New Runtime - No response received from CreateResponseAsync");
                    }
                    catch (Exception e)
                    {
                        _amiMain._amiThink._anim.Active = false;
                        _amiMain._amiThink.Visible = false;
                        GD.PrintErr($"InitalizeApp: New Runtime - Error: {e.Message}");
                        _amiMain._outputLabel.Text += $"[p]\n\nError sending query: {e.Message}.  Please try a different query.[/p]";
                    }
                }
                else
                {
                    GD.Print("AppManager:InitializeApp - No SessionID found in config, starting first session.");
                    try
                    {
                        // Clear previous session data
                        ClearPreviousSession();

                        // Clear UploadsList and ArtifactsList
                        if (_amiMain._uploadsList != null || _amiMain._artifactsList != null)
                        {
                            _amiMain._uploadsList.Clear();
                            _amiMain._artifactsList.Clear();
                        }
                        else
                        {
                            GD.PrintErr("InitializeApp Setup Complete - Error: UploadsList or ArtifactsList is null");
                        }

                        //Clear Session Spend
                        _config["SessionSpend"] = "00.0000";

                        // Update UI
                        if (_amiMain._outputLabel != null)
                        {
                            _amiMain._outputLabel.Text += "[p]Connecting to Grok.[/p]";
                        }

                        GD.Print("InitializeApp: Setup completed, starting first session.");

                        //Assign new SessionID
                        _config["SessionID"] = DateTime.Now.ToString("MMdd.HHmm");
                        GD.Print(" Setup Complete. New session ID assigned: ", _config["SessionID"].ToString());

                        // Send new session prompt
                        if (_amiMain._responsesClient != null)
                        {
                            _amiMain._lastRequestType = "user_query";
                            var (messages, responseOptions) = WritePrompt("This looks this user's first session, so greet them, identify yourself, and remind them that help can be found using the Help > Using AMI menu bar selection.");
                            OpenAIResponseClient localClient = _amiMain._responsesClient.GetOpenAIResponseClient(_config["ModelSelected"].ToString());
                            responseOptions.PreviousResponseId = null;// Ensure no previous response ID for new session

                            //Write system prompt
                            string rolePlayText = "";
                            try
                            {

                                if (_amiMain._personasData.TryGetValue("Personas", out var dataObj) && dataObj is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
                                    foreach (var persona in dataElement.EnumerateArray())
                                    {
                                        if (persona.GetProperty("name").ToString() == _config["PersonaSelected"].ToString())
                                        {
                                            rolePlayText = persona.GetProperty("system").ToString();
                                        }
                                    }
                            }
                            catch (Exception e)
                            {
                                GD.PrintErr("InitializeApp Setup Complete- Error reading persona data: ", e);
                            }
                            try
                            {
                                //GD.Print("InitializeApp Setup Complete- Role Play Text: ", rolePlayText);
                                string userInfoText = Godot.FileAccess.GetFileAsString("user://Assets/user.txt");
                                string systemInstructions = Godot.FileAccess.GetFileAsString("res://Assets/Instructions.txt");
                                string customInstructions = Godot.FileAccess.GetFileAsString("user://Assets/custom.txt");
                                string systemText = $"{rolePlayText}\n\n{userInfoText}\n\n{systemInstructions}\n\n{customInstructions}";
                                //GD.Print(systemText);
                                responseOptions.Instructions = systemText; //Set role : system prompt

                                //GD.Print("InitializeAppNewSession- new session prompt to API: ", messages, ", ", responseOptions.PreviousResponseId);
                                var result = await localClient.CreateResponseAsync(messages, responseOptions);
                                if (result != null)
                                    ParseResponsesAPIResponse(result.Value, true);
                                else
                                    GD.PrintErr("InitalizeApp: FirstSession - No response received from CreateResponseAsync");
                            }
                            catch(Exception e)
                            {
                                _amiMain._amiThink._anim.Active = false;
                                _amiMain._amiThink.Visible = false;
                                GD.PrintErr($"InitializeApp: First Session: - Error: {e.Message}");
                                _amiMain._outputLabel.Text += $"[p]\n\nError sending query: {e.Message}.  Please try a different query.[/p]";
                            }
                        }
                        else
                        {
                            GD.PrintErr("InitializeApp Setup Complete- Error: ResponsesClient is null");
                        }



                        }
                    catch (Exception e)
                    {
                        GD.PrintErr($"InitializeApp Setup Complete - Error starting first session: {e.Message}");
                        _amiMain._outputLabel.Text += $"[p]Error starting first session: {e.Message}[/p]";
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AppManager:InitializeApp 1093 - Error: {e.Message}");
                if (_amiMain != null)
                    _amiMain._outputLabel.Text += $"[p]Initialization error: {e.Message}[/p]";
            }
        }
    }
}
