using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Translation.Providers
{
    /// <summary>
    /// Text translation via MyMemory API — completely free, no API key required.
    ///
    /// Endpoint: https://api.mymemory.translated.net/get
    /// Free limit: 5 000 words / day (enough for any VR meeting session).
    ///
    /// Supports all four project languages:
    ///   English (en), Hebrew (he), Arabic (ar), Thai (th)
    ///
    /// No setup needed — works out of the box on Quest and PC.
    /// </summary>
    public class MyMemoryTranslateProvider : ITranslationProvider
    {
        public string ProviderName => "MyMemory (free)";

        private const string BaseUrl = "https://api.mymemory.translated.net/get";

        public IEnumerator Translate(string text, string sourceLangCode, string targetLangCode,
            Action<string> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                onSuccess?.Invoke("");
                yield break;
            }

            // MyMemory uses "en|ar" pair format
            string langPair  = $"{sourceLangCode}|{targetLangCode}";
            string encodedQ  = UnityWebRequest.EscapeURL(text);
            string url       = $"{BaseUrl}?q={encodedQ}&langpair={langPair}";

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", "UnityVRFarmApp/1.0");
            req.timeout = 15;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"MyMemory: {req.responseCode} — {req.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<MyMemoryResponse>(req.downloadHandler.text);

            if (response?.responseData != null &&
                !string.IsNullOrWhiteSpace(response.responseData.translatedText))
            {
                // MyMemory returns "QUERY LENGTH LIMIT EXCEEDED" as translatedText on abuse
                if (response.responseData.translatedText.StartsWith("QUERY LENGTH"))
                {
                    onError?.Invoke("MyMemory: query too long — shorten the input text.");
                    yield break;
                }

                onSuccess?.Invoke(response.responseData.translatedText);
            }
            else
            {
                onError?.Invoke($"MyMemory: empty response — {req.downloadHandler.text}");
            }
        }

        // ── JSON shapes ───────────────────────────────────────────────────────

        [Serializable]
        private class MyMemoryResponse
        {
            public ResponseData responseData;
            public int          responseStatus;
        }

        [Serializable]
        private class ResponseData
        {
            public string translatedText;
            public float  match;
        }
    }
}
