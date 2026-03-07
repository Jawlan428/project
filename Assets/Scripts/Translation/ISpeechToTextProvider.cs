using System;
using System.Collections;

namespace Translation
{
    /// <summary>
    /// Interface for any Speech-to-Text backend.
    /// Implemented as a coroutine so it works with Unity's main-thread requirement.
    ///
    /// Current implementations:
    ///   - WhisperSTTProvider  (OpenAI Whisper API — requires API key)
    ///
    /// Future implementations can be added without changing the TranslationManager:
    ///   - AzureSpeechProvider
    ///   - GoogleCloudSTTProvider
    ///   - LocalWhisperProvider  (on-device, no API key needed)
    /// </summary>
    public interface ISpeechToTextProvider
    {
        /// <summary>Human-readable name shown in diagnostics / settings.</summary>
        string ProviderName { get; }

        /// <summary>
        /// Transcribe raw PCM audio (WAV format) to text.
        /// </summary>
        /// <param name="audioWavBytes">Complete WAV file bytes (header + 16-bit PCM).</param>
        /// <param name="localeCode">BCP-47 locale hint, e.g. "en-US", "th-TH".</param>
        /// <param name="onSuccess">Called with the transcribed text on success.</param>
        /// <param name="onError">Called with an error message on failure.</param>
        IEnumerator Transcribe(byte[] audioWavBytes, string localeCode,
            Action<string> onSuccess, Action<string> onError);
    }
}
