"""Tests for Piper."""

import io
import wave
from pathlib import Path

from piper import PiperVoice
from piper.phonemize_espeak import EspeakPhonemizer

_DIR = Path(__file__).parent
_TESTS_DIR = _DIR
_TEST_VOICE = _TESTS_DIR / "test_voice.onnx"


def test_load_voice() -> None:
    """Test loading a voice that generates silence."""
    voice = PiperVoice.load(_TEST_VOICE)
    assert voice.config.sample_rate == 22050
    assert voice.config.num_symbols == 256
    assert voice.config.num_speakers == 1
    assert voice.config.phoneme_type == "espeak"
    assert voice.config.espeak_voice == "en-us"


def test_phonemize_synthesize() -> None:
    """Test phonemizing and synthesizing."""
    voice = PiperVoice.load(_TEST_VOICE)
    phonemes = voice.phonemize("Test 1. Test 2.")
    assert phonemes == [
        # Test 1.
        ["t", "ˈ", "ɛ", "s", "t", " ", "w", "ˈ", "ʌ", "n", "."],
        # Test 2.
        ["t", "ˈ", "ɛ", "s", "t", " ", "t", "ˈ", "u", "ː", "."],
    ]

    phoneme_ids = [voice.phonemes_to_ids(ps) for ps in phonemes]

    # Test 1.
    assert phoneme_ids[0] == [
        1,  # BOS
        0,
        32,
        0,  # PAD
        120,
        0,
        61,
        0,
        31,
        0,
        32,
        0,
        3,
        0,
        35,
        0,
        120,
        0,
        102,
        0,
        26,
        0,
        10,
        0,
        2,  # EOS
    ]

    # Test 2.
    assert phoneme_ids[1] == [
        1,  # BOS
        0,
        32,
        0,  # PAD
        120,
        0,
        61,
        0,
        31,
        0,
        32,
        0,
        3,
        0,
        32,
        0,
        120,
        0,
        33,
        0,
        122,
        0,
        10,
        0,
        2,  # EOS
    ]

    audio_array = voice.phoneme_ids_to_audio(phoneme_ids[0])
    assert len(audio_array) == voice.config.sample_rate  # 1 second of silence
    assert not any(audio_array)


def test_language_switch_flags_removed() -> None:
    """Test that (language) switch (flags) are removed."""
    phonemizer = EspeakPhonemizer()
    phonemes = phonemizer.phonemize("ar", "test")
    assert phonemes == [["t", "ˈ", "ɛ", "s", "t"]]


def test_synthesize() -> None:
    """Test streaming text to audio synthesis."""
    voice = PiperVoice.load(_TEST_VOICE)
    audio_chunks = list(voice.synthesize("This is a test. This is another test."))

    # One chunk per sentence
    assert len(audio_chunks) == 2
    for chunk in audio_chunks:
        sample_rate = chunk.sample_rate
        assert sample_rate == voice.config.sample_rate
        assert chunk.sample_width == 2
        assert chunk.sample_channels == 1

        # Verify 1 second of silence
        assert len(chunk.audio_float_array) == sample_rate
        assert not any(chunk.audio_float_array)

        assert len(chunk.audio_int16_array) == sample_rate
        assert not any(chunk.audio_int16_array)

        assert len(chunk.audio_int16_bytes) == sample_rate * 2
        assert not any(chunk.audio_int16_bytes)


def test_synthesize_wav() -> None:
    """Test text to audio synthesis with WAV output."""
    voice = PiperVoice.load(_TEST_VOICE)

    with io.BytesIO() as wav_io:
        wav_output: wave.Wave_write = wave.open(wav_io, "wb")
        with wav_output:
            voice.synthesize_wav("This is a test. This is another test.", wav_output)

        wav_io.seek(0)
        wav_input: wave.Wave_read = wave.open(wav_io, "rb")
        with wav_input:
            assert wav_input.getframerate() == voice.config.sample_rate
            assert wav_input.getsampwidth() == 2
            assert wav_input.getnchannels() == 1

            # Verify 2 seconds of silence (1 per sentence)
            audio_data = wav_input.readframes(wav_input.getnframes())
            assert (
                len(audio_data)
                == voice.config.sample_rate * wav_input.getsampwidth() * 2
            )
            assert not any(audio_data)


def test_ar_tashkeel() -> None:
    """Test Arabic diacritization."""
    voice = PiperVoice.load(_TEST_VOICE)
    voice.config.espeak_voice = "ar"

    phonemes_with_diacritics = "bismˌi ʔalllˈahi ʔarrrˈaħmanˌi ʔarrrˈaħiːm"
    phonemes_without_diacritics = "bˈismillˌaːh ʔˈarɹaħmˌaːn ʔarrˈaħiːm"

    # Diacritization is enabled by default
    phonemes = voice.phonemize("بسم الله الرحمن الرحيم")
    assert phonemes_with_diacritics == "".join(phonemes[0])

    # Disable diacritization
    voice.use_tashkeel = False
    phonemes = voice.phonemize("بسم الله الرحمن الرحيم")
    assert phonemes_without_diacritics == "".join(phonemes[0])


def test_raw_phonemes() -> None:
    """Test [[ phonemes block ]]."""
    voice = PiperVoice.load(_TEST_VOICE)
    phonemes = voice.phonemize("I am the [[ bˈætmæn ]] not [[bɹˈuːs wˈe‍ɪn]]")
    phonemes_str = "".join("".join(ps) for ps in phonemes)
    assert phonemes_str == "aɪɐm ðə bˈætmæn nˈɑːt bɹˈuːs wˈe‍ɪn"
