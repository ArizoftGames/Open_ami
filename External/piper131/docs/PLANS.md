# Plans

Plans for future Piper development.

## C API

A C++ library with a C API named `libpiper` that supports loading voices and synthesizing audio in chunks. This library would depend on:

* `onnxruntime`
* `espeak-ng`

High level design:

* A `piper_context` represents a Piper TTS session
* One or more voices can be loaded into a context
* Functions exist for audio synthesis, phonemization, and phoneme/id mapping

Audio synthesis is chunk-based, with one chunk per sentence. The synthesis function is called multiple times, returning the next chunk each time. Some kind of session should be used to track the details and allow for aborting synthesis early.

A high-level WAV synthesis function should also exist, which simply writes a WAV file with all audio to an output file (or buffer?).
