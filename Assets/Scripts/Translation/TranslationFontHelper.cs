using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Translation
{
    /// <summary>
    /// Manages TMP font assets and RTL settings for each supported language.
    ///
    /// Font loading priority:
    ///   1. Pre-made TMP Font Asset in  Resources/TranslationFonts/<Language>.asset
    ///      (best quality — create these from Noto fonts, see instructions below)
    ///   2. OS / system font found at runtime (works on Windows + Quest Android)
    ///   3. TMP default font (English only — other languages show boxes)
    ///
    /// ── How to add proper fonts (recommended) ──────────────────────────────────
    ///  1. Download from https://fonts.google.com/noto :
    ///       • Noto Naskh Arabic   →  Arabic
    ///       • Noto Sans Hebrew    →  Hebrew
    ///       • Noto Sans Thai      →  Thai
    ///  2. Import the .ttf files into Unity.
    ///  3. Open  Window > TextMeshPro > Font Asset Creator
    ///       Source Font File  = the .ttf
    ///       Sampling Point Size = 32
    ///       Padding = 5
    ///       Packing Method = Optimum
    ///       Atlas Resolution = 2048 × 2048
    ///       Character Set = Unicode Range (Hex)
    ///         Arabic:  0600-06FF,0750-077F
    ///         Hebrew:  0590-05FF,FB1D-FB4F
    ///         Thai:    0E00-0E7F
    ///       Click "Generate Font Atlas", then "Save".
    ///  4. Move the generated .asset files to  Assets/Resources/TranslationFonts/
    ///       Assets/Resources/TranslationFonts/Arabic.asset
    ///       Assets/Resources/TranslationFonts/Hebrew.asset
    ///       Assets/Resources/TranslationFonts/Thai.asset
    ///  The system will pick them up automatically on next Play.
    /// ───────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class TranslationFontHelper
    {
        // ── Resource paths (relative to Resources/) ───────────────────────────
        // Checked in order — first match wins.
        // Add your folder name here if it differs from the defaults.
        private static readonly string[] FontFolders =
            { "TranslationFonts", "translate", "Fonts/Translation", "Fonts" };

        private static readonly Dictionary<TranslationLanguage, string[]> ResourceNames =
            new Dictionary<TranslationLanguage, string[]>
        {
            { TranslationLanguage.Arabic, new[] { "Arabic",  "NotoNaskhArabic-Regular",  "Noto_Naskh_Arabic",  "NotoArabic" } },
            { TranslationLanguage.Hebrew, new[] { "Hebrew",  "NotoSansHebrew-Regular",   "Noto_Sans_Hebrew",   "NotoHebrew" } },
            { TranslationLanguage.Thai,   new[] { "Thai",    "NotoSansThai-Regular",     "Noto_Sans_Thai",     "NotoThai"   } },
        };

        // ── OS / system font fallbacks (Windows + Android) ────────────────────
        private static readonly Dictionary<TranslationLanguage, string[]> OsFontCandidates =
            new Dictionary<TranslationLanguage, string[]>
        {
            {
                TranslationLanguage.Arabic,
                new[] { "Arial Unicode MS", "Segoe UI Historic", "Times New Roman" }
            },
            {
                TranslationLanguage.Hebrew,
                new[] { "Arial Unicode MS", "David", "Times New Roman", "Arial" }
            },
            {
                TranslationLanguage.Thai,
                new[] { "Arial Unicode MS", "Tahoma", "Leelawadee UI", "Leelawadee" }
            }
        };

        // ── RTL languages ──────────────────────────────────────────────────────
        public static bool IsRTL(TranslationLanguage lang) =>
            lang == TranslationLanguage.Hebrew || lang == TranslationLanguage.Arabic;

        // ── Cache ──────────────────────────────────────────────────────────────
        private static readonly Dictionary<TranslationLanguage, TMP_FontAsset> Cache =
            new Dictionary<TranslationLanguage, TMP_FontAsset>();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Apply font + direction to a text that contains a Latin label prefix
        /// (e.g. "Hebrew: שלום"). isRightToLeftText is kept false so the Latin
        /// prefix is not reversed; Unicode Bidi handles the RTL characters inside.
        /// </summary>
        public static void Apply(TMP_Text text, TranslationLanguage language)
        {
            if (text == null) return;

            text.isRightToLeftText = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var font = GetFont(language);
            if (font != null) text.font = font;
        }

        /// <summary>
        /// Apply font + direction to a content-only text (no Latin prefix).
        /// RTL languages (Arabic, Hebrew) get isRightToLeftText = true and
        /// right-alignment so the text flows correctly from right to left.
        /// </summary>
        public static void ApplyContent(TMP_Text text, TranslationLanguage language)
        {
            if (text == null) return;

            bool rtl = IsRTL(language);
            text.isRightToLeftText = rtl;
            text.alignment = rtl
                ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.MidlineLeft;

            var font = GetFont(language);
            if (font != null) text.font = font;
        }

        // ── Font loading ───────────────────────────────────────────────────────

        private static TMP_FontAsset GetFont(TranslationLanguage lang)
        {
            if (lang == TranslationLanguage.English) return null;

            if (Cache.TryGetValue(lang, out var cached) && cached != null)
                return cached;

            // 1. Try pre-made TMP font asset from Resources (multiple folder + name combos)
            if (ResourceNames.TryGetValue(lang, out var names))
            {
                foreach (var folder in FontFolders)
                {
                    foreach (var name in names)
                    {
                        string path = $"{folder}/{name}";
                        var asset = Resources.Load<TMP_FontAsset>(path);
                        if (asset != null)
                        {
                            Cache[lang] = asset;
                            Debug.Log($"[TranslationFont] Loaded {lang} font from Resources/{path}");
                            return asset;
                        }
                    }
                }
            }

            // 2. Try to build a TMP font asset from an OS / system font
            var builtFont = TryBuildFromOSFont(lang);
            if (builtFont != null)
            {
                Cache[lang] = builtFont;
                return builtFont;
            }

            // 3. No suitable font found — caller will keep the default TMP font
            Debug.LogWarning(
                $"[TranslationFont] No font found for {lang}. " +
                $"Characters may appear as □. " +
                $"Follow the font setup instructions in TranslationFontHelper.cs.");
            Cache[lang] = null;
            return null;
        }

        private static TMP_FontAsset TryBuildFromOSFont(TranslationLanguage lang)
        {
            if (!OsFontCandidates.TryGetValue(lang, out var candidates)) return null;

            var installed = new HashSet<string>(Font.GetOSInstalledFontNames());

            foreach (string name in candidates)
            {
                if (!installed.Contains(name)) continue;

                try
                {
                    Font osFont = Font.CreateDynamicFontFromOSFont(name, 32);
                    if (osFont == null) continue;

                    // Single-parameter overload avoids the GlyphRenderMode / TextCore
                    // namespace dependency that varies across TMP / Unity versions.
                    var tmpFont = TMP_FontAsset.CreateFontAsset(osFont);
                    if (tmpFont == null) continue;

                    // Allow new characters to be added to the atlas on demand.
                    tmpFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;

                    Debug.Log($"[TranslationFont] Built {lang} font from OS font: {name}");
                    return tmpFont;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TranslationFont] Failed to build font '{name}': {ex.Message}");
                }
            }

            return null;
        }
    }
}
