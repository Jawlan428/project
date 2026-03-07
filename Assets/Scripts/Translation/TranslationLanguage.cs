namespace Translation
{
    /// <summary>
    /// All languages supported by the VR Meeting Translation System.
    /// Add new languages here — language codes are defined in the extension methods below.
    /// </summary>
    public enum TranslationLanguage
    {
        English,
        Hebrew,
        Arabic,
        Thai
    }

    public static class TranslationLanguageExtensions
    {
        /// <summary>Human-readable display name shown in the tablet UI.</summary>
        public static string ToDisplayName(this TranslationLanguage lang) => lang switch
        {
            TranslationLanguage.English => "English",
            TranslationLanguage.Hebrew  => "Hebrew (עברית)",
            TranslationLanguage.Arabic  => "Arabic (العربية)",
            TranslationLanguage.Thai    => "Thai (ภาษาไทย)",
            _                           => lang.ToString()
        };

        /// <summary>Short two/three-letter label for compact tablet buttons.</summary>
        public static string ToShortLabel(this TranslationLanguage lang) => lang switch
        {
            TranslationLanguage.English => "EN",
            TranslationLanguage.Hebrew  => "HE",
            TranslationLanguage.Arabic  => "AR",
            TranslationLanguage.Thai    => "TH",
            _                           => lang.ToString().Substring(0, 2).ToUpper()
        };

        /// <summary>ISO 639-1 code used by LibreTranslate and Google Translate.</summary>
        public static string ToLanguageCode(this TranslationLanguage lang) => lang switch
        {
            TranslationLanguage.English => "en",
            TranslationLanguage.Hebrew  => "he",
            TranslationLanguage.Arabic  => "ar",
            TranslationLanguage.Thai    => "th",
            _                           => "en"
        };

        /// <summary>BCP-47 locale code used by Whisper STT and speech recognition APIs.</summary>
        public static string ToLocaleCode(this TranslationLanguage lang) => lang switch
        {
            TranslationLanguage.English => "en-US",
            TranslationLanguage.Hebrew  => "he-IL",
            TranslationLanguage.Arabic  => "ar-SA",
            TranslationLanguage.Thai    => "th-TH",
            _                           => "en-US"
        };

        /// <summary>All supported languages in a fixed order (used to build UI button rows).</summary>
        public static readonly TranslationLanguage[] All =
        {
            TranslationLanguage.English,
            TranslationLanguage.Hebrew,
            TranslationLanguage.Arabic,
            TranslationLanguage.Thai
        };
    }
}
