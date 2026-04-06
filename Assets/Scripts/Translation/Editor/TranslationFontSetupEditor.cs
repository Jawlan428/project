using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Translation.Editor
{
    /// <summary>
    /// Sets up Arabic / Hebrew / Thai fonts for the Translation System.
    ///
    /// Menu: Tools > Smart Farm > Setup Translation Fonts
    ///
    /// Two-layer approach for maximum reliability:
    ///   Layer 1 — Creates TMP Font Assets in Resources/TranslationFonts/
    ///             (loaded at runtime by TranslationFontHelper)
    ///   Layer 2 — Adds the same fonts as fallbacks on LiberationSans SDF
    ///             (the project's default TMP font) so any TMP text component
    ///             automatically renders Arabic/Hebrew/Thai characters even
    ///             without explicit font assignment.
    /// </summary>
    public static class TranslationFontSetupEditor
    {
        private const string OutputFolder  = "Assets/Resources/TranslationFonts";
        private const string MainFontPath  = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static readonly (string lang, string assetName, string[] keywords)[] Targets =
        {
            ("Arabic", "Arabic", new[] { "arabic", "naskh"  }),
            ("Hebrew", "Hebrew", new[] { "hebrew"           }),
            ("Thai",   "Thai",   new[] { "thai"             }),
        };

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Smart Farm/Setup Translation Fonts")]
        public static void SetupFonts()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[TranslationFont] Stop Play mode first.");
                return;
            }

            EnsureOutputFolder();

            var mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
            if (mainFont == null)
                Debug.LogWarning("[TranslationFont] Could not load LiberationSans SDF — fallback layer skipped.");

            int created = 0, alreadyExist = 0, missing = 0, fallbacksAdded = 0;

            foreach (var (lang, assetName, keywords) in Targets)
            {
                string destPath = $"{OutputFolder}/{assetName}.asset";

                // ── Find the source TTF in Assets ────────────────────────────
                Font sourceFont = FindFont(keywords, lang);
                if (sourceFont == null)
                {
                    Debug.LogWarning(
                        $"[TranslationFont] ✗ {lang}: .ttf not found in Assets.\n" +
                        $"  Make sure  {GetExpectedName(lang)}  is imported into Assets/Fonts/.");
                    missing++;
                    continue;
                }

                // ── Layer 1: Create / update the Resources asset ─────────────
                TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destPath);

                if (tmpFont == null)
                {
                    tmpFont = BuildAndSaveFontAsset(sourceFont, destPath, lang);
                    if (tmpFont != null) created++;
                    else { missing++; continue; }
                }
                else
                {
                    Debug.Log($"[TranslationFont] ✓ {lang} already exists at {destPath}");
                    alreadyExist++;
                }

                // ── Layer 2: Add as fallback to LiberationSans SDF ───────────
                if (mainFont != null && tmpFont != null)
                {
                    if (AddFallback(mainFont, tmpFont, lang))
                        fallbacksAdded++;
                }
            }

            if (mainFont != null)
            {
                EditorUtility.SetDirty(mainFont);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();

            // ── Summary ───────────────────────────────────────────────────────
            string msg =
                $"Created  : {created}\n" +
                $"Already present : {alreadyExist}\n" +
                $"Fallbacks added to LiberationSans SDF : {fallbacksAdded}\n" +
                (missing > 0
                    ? $"\n{missing} font(s) NOT FOUND.\n" +
                      "Make sure NotoNaskhArabic-Regular.ttf, NotoSansHebrew-Regular.ttf\n" +
                      "and NotoSansThai-Regular.ttf are inside any Assets subfolder."
                    : "\nAll fonts are ready. Press Play to test.");

            Debug.Log("[TranslationFont] Setup complete:\n" + msg);
            EditorUtility.DisplayDialog("Translation Font Setup", msg, "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Smart Farm/Check Translation Font Status")]
        public static void CheckStatus()
        {
            string report = "";
            foreach (var (lang, assetName, _) in Targets)
            {
                string destPath = $"{OutputFolder}/{assetName}.asset";
                bool exists = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destPath) != null;
                report += $"{(exists ? "✓" : "✗")} {lang}: {(exists ? destPath : "MISSING")}\n";
            }

            // Check fallbacks on main font
            var mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
            if (mainFont != null && mainFont.fallbackFontAssetTable != null)
            {
                report += $"\nLiberationSans SDF fallbacks: {mainFont.fallbackFontAssetTable.Count}";
            }

            Debug.Log("[TranslationFont] Status:\n" + report);
            EditorUtility.DisplayDialog("Translation Font Status", report.Trim(), "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Build a TMP Font Asset and save it correctly with sub-assets
        // ─────────────────────────────────────────────────────────────────────
        private static TMP_FontAsset BuildAndSaveFontAsset(Font sourceFont, string destPath, string lang)
        {
            try
            {
                // Create dynamic font asset (characters populated on first use)
                var tmpFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (tmpFont == null)
                {
                    Debug.LogError($"[TranslationFont] CreateFontAsset returned null for {lang}.");
                    return null;
                }

                tmpFont.name = lang;
                tmpFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;

                // Save main asset
                AssetDatabase.CreateAsset(tmpFont, destPath);

                // IMPORTANT: save atlas textures and material as sub-assets.
                // Without this step the asset appears empty / broken after reimport.
                if (tmpFont.atlasTextures != null)
                {
                    foreach (var tex in tmpFont.atlasTextures)
                    {
                        if (tex == null) continue;
                        tex.name = $"{lang} Atlas";
                        AssetDatabase.AddObjectToAsset(tex, destPath);
                    }
                }

                if (tmpFont.material != null)
                {
                    tmpFont.material.name = $"{lang} Material";
                    AssetDatabase.AddObjectToAsset(tmpFont.material, destPath);
                }

                EditorUtility.SetDirty(tmpFont);
                AssetDatabase.SaveAssets();

                Debug.Log($"[TranslationFont] ✓ Created {lang} font asset → {destPath}");
                return tmpFont;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TranslationFont] Error creating {lang} font asset: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Add a font as a fallback on the target font (if not already present)
        // ─────────────────────────────────────────────────────────────────────
        private static bool AddFallback(TMP_FontAsset target, TMP_FontAsset fallback, string lang)
        {
            if (target.fallbackFontAssetTable == null)
                target.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

            // Check if already in the list
            foreach (var f in target.fallbackFontAssetTable)
                if (f != null && f.name == fallback.name) return false;

            target.fallbackFontAssetTable.Add(fallback);
            Debug.Log($"[TranslationFont] Added {lang} as fallback on LiberationSans SDF.");
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Find a Unity Font asset whose filename matches any of the keywords
        // ─────────────────────────────────────────────────────────────────────
        private static Font FindFont(string[] keywords, string lang)
        {
            var guids = AssetDatabase.FindAssets("t:Font");
            foreach (var guid in guids)
            {
                string path     = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                foreach (var kw in keywords)
                {
                    if (!fileName.Contains(kw)) continue;
                    var f = AssetDatabase.LoadAssetAtPath<Font>(path);
                    if (f != null)
                    {
                        Debug.Log($"[TranslationFont] Found {lang} TTF: {path}");
                        return f;
                    }
                }
            }
            return null;
        }

        private static void EnsureOutputFolder()
        {
            string full = Path.Combine(Application.dataPath, "Resources/TranslationFonts");
            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full);
                AssetDatabase.Refresh();
                Debug.Log($"[TranslationFont] Created folder: {OutputFolder}");
            }
        }

        private static string GetExpectedName(string lang) => lang switch
        {
            "Arabic" => "NotoNaskhArabic-Regular.ttf",
            "Hebrew" => "NotoSansHebrew-Regular.ttf",
            "Thai"   => "NotoSansThai-Regular.ttf",
            _        => $"Noto{lang}-Regular.ttf"
        };
    }
}
