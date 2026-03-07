using System;

namespace Translation
{
    /// <summary>
    /// A single translation record — one speech segment fully processed.
    /// Stored in TranslationManager.History and serialised to the transcript file.
    /// </summary>
    [Serializable]
    public class TranslationEntry
    {
        /// <summary>Unique identifier for this entry.</summary>
        public string id;

        /// <summary>UTC time when the translation was received.</summary>
        public DateTime timestampUtc;

        /// <summary>Display name of the speaker (from XRINetworkGameManager or fallback).</summary>
        public string speakerName;

        /// <summary>Raw transcript text from the STT provider.</summary>
        public string originalText;

        /// <summary>Translated text from the translation provider.</summary>
        public string translatedText;

        /// <summary>Language the speaker was recognised as speaking.</summary>
        public TranslationLanguage sourceLanguage;

        /// <summary>Language the text was translated into.</summary>
        public TranslationLanguage targetLanguage;

        /// <summary>True when this entry has been persisted to disk.</summary>
        public bool savedToDisk;
    }
}
