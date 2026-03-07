using TMPro;
using Translation;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Binds a TranslationEntry to a transcript row prefab.
    ///
    /// Assign this component to a row prefab and wire the text fields.
    /// The TranslationTabletPage will call Bind() when a new translation arrives.
    ///
    /// If no prefab is assigned, TranslationTabletPage builds rows procedurally —
    /// this component is only needed when you want a custom-designed row.
    /// </summary>
    public class TranslationEntryRowUI : MonoBehaviour
    {
        [Header("Row Text Fields")]
        [SerializeField] private TMP_Text speakerAndTimeText;
        [SerializeField] private TMP_Text originalText;
        [SerializeField] private TMP_Text translatedText;

        [Header("Language Tag (optional)")]
        [SerializeField] private TMP_Text langTagText;

        [Header("Background (colour changes with severity/language)")]
        [SerializeField] private Image background;

        // ── Public API ────────────────────────────────────────────────────────

        public void Bind(Translation.TranslationEntry entry)
        {
            if (speakerAndTimeText != null)
                speakerAndTimeText.text =
                    $"[{entry.timestampUtc.ToLocalTime():HH:mm:ss}]  {entry.speakerName}";

            if (originalText != null)
                originalText.text =
                    $"{entry.sourceLanguage.ToDisplayName()}: {entry.originalText}";

            if (translatedText != null)
                translatedText.text =
                    $"{entry.targetLanguage.ToDisplayName()}: {entry.translatedText}";

            if (langTagText != null)
                langTagText.text =
                    $"{entry.sourceLanguage.ToShortLabel()} → {entry.targetLanguage.ToShortLabel()}";

            if (background != null)
                background.color = GetRowColor(entry.targetLanguage);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Color GetRowColor(Translation.TranslationLanguage lang) => lang switch
        {
            Translation.TranslationLanguage.Hebrew  => new Color(0.10f, 0.18f, 0.32f, 0.88f),
            Translation.TranslationLanguage.Arabic  => new Color(0.16f, 0.12f, 0.28f, 0.88f),
            Translation.TranslationLanguage.Thai    => new Color(0.08f, 0.22f, 0.18f, 0.88f),
            _                                       => new Color(0.10f, 0.16f, 0.26f, 0.85f)
        };
    }
}
