using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Translation.Providers
{
    /// <summary>
    /// Text translation via Google Translate (unofficial free endpoint).
    ///
    /// Uses the same API that powers translate.google.com.
    /// No API key required. No rate limits for normal use.
    /// Excellent translation quality for all four project languages:
    ///   English (en), Hebrew (he), Arabic (ar), Thai (th)
    ///
    /// Endpoint: https://translate.googleapis.com/translate_a/single
    ///
    /// Note: This is an unofficial/undocumented endpoint. Google does not
    /// guarantee its availability, but it has been stable for many years.
    /// For production deployments, consider the official Cloud Translation API.
    /// </summary>
    public class GoogleTranslateProvider : ITranslationProvider
    {
        public string ProviderName => "Google Translate (free)";

        private const string BaseUrl =
            "https://translate.googleapis.com/translate_a/single" +
            "?client=gtx&dt=t";

        public IEnumerator Translate(string text, string sourceLangCode, string targetLangCode,
            Action<string> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                onSuccess?.Invoke("");
                yield break;
            }

            string url = $"{BaseUrl}&sl={sourceLangCode}&tl={targetLangCode}" +
                         $"&q={UnityWebRequest.EscapeURL(text)}";

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent",
                "Mozilla/5.0 (compatible; UnityVRFarmApp/1.0)");
            req.timeout = 15;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Google Translate: {req.responseCode} — {req.error}");
                yield break;
            }

            string json = req.downloadHandler.text;

            // Response: [[["translated","original",null,null,10]],null,"ar"]
            // Parse the first translation segment
            string translated = ParseGoogleResponse(json);

            if (!string.IsNullOrWhiteSpace(translated))
                onSuccess?.Invoke(translated);
            else
                onError?.Invoke($"Google Translate: could not parse response — {json}");
        }

        /// <summary>
        /// Minimal parser for Google's nested JSON array response.
        /// Extracts and concatenates all translated segments.
        /// </summary>
        private static string ParseGoogleResponse(string json)
        {
            try
            {
                // Structure: [[[seg1,orig1],[seg2,orig2],...],null,"src"]
                // We walk character-by-character to extract the first string
                // inside each inner array without a full JSON library.
                var result = new System.Text.StringBuilder();
                int depth = 0;
                bool inString = false;
                bool escape   = false;

                var current = new System.Text.StringBuilder();
                bool collectingTranslation = false;

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];

                    if (escape) { escape = false; if (inString) current.Append(c); continue; }
                    if (c == '\\') { escape = true; if (inString) current.Append(c); continue; }

                    if (c == '"')
                    {
                        inString = !inString;
                        if (!inString && collectingTranslation)
                        {
                            // Finished reading a translation segment
                            if (current.Length > 0)
                                result.Append(current);
                            current.Clear();
                            collectingTranslation = false;
                        }
                        continue;
                    }

                    if (inString) { current.Append(c); continue; }

                    if (c == '[')
                    {
                        depth++;
                        // depth 3 = we are inside [[[ ... ]]]
                        // The very next string token at depth 3 is the translation
                        if (depth == 3) collectingTranslation = true;
                        continue;
                    }

                    if (c == ']')
                    {
                        depth--;
                        collectingTranslation = false;
                        continue;
                    }
                }

                string finalResult = result.ToString().Trim();
                return string.IsNullOrEmpty(finalResult) ? null : finalResult;
            }
            catch
            {
                return null;
            }
        }
    }
}
