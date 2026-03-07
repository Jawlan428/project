using System;
using System.Collections;

namespace Translation
{
    /// <summary>
    /// Interface for any text-translation backend.
    /// Implemented as a coroutine so it works with Unity's main-thread requirement.
    ///
    /// Current implementations:
    ///   - LibreTranslateProvider  (free / self-hostable REST API)
    ///
    /// Future implementations can be added without changing the TranslationManager:
    ///   - DeepLProvider
    ///   - GoogleCloudTranslateProvider
    ///   - AzureTranslatorProvider
    /// </summary>
    public interface ITranslationProvider
    {
        /// <summary>Human-readable name shown in diagnostics / settings.</summary>
        string ProviderName { get; }

        /// <summary>
        /// Translate a plain-text string between two languages.
        /// </summary>
        /// <param name="text">Source text to translate.</param>
        /// <param name="sourceLangCode">ISO 639-1 source code, e.g. "en".</param>
        /// <param name="targetLangCode">ISO 639-1 target code, e.g. "th".</param>
        /// <param name="onSuccess">Called with the translated text on success.</param>
        /// <param name="onError">Called with an error message on failure.</param>
        IEnumerator Translate(string text, string sourceLangCode, string targetLangCode,
            Action<string> onSuccess, Action<string> onError);
    }
}
