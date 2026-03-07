using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Translation.Providers
{
    /// <summary>
    /// Text translation via LibreTranslate — free, open-source REST API.
    ///
    /// Supports all four project languages:
    ///   English (en), Hebrew (he), Arabic (ar), Thai (th)
    ///
    /// Setup options:
    ///
    ///   Option A — Public instance (easiest, rate-limited):
    ///     Endpoint: https://libretranslate.com/translate
    ///     Get a free API key at https://libretranslate.com/
    ///
    ///   Option B — Self-hosted (free, unlimited, recommended for production):
    ///     docker run -ti --rm -p 5000:5000 libretranslate/libretranslate
    ///     Endpoint: http://localhost:5000/translate
    ///     ApiKey:   (leave empty for self-hosted)
    ///
    ///   Option C — Argos translate mirror (completely free, no key needed):
    ///     Endpoint: https://translate.argosopentech.com/translate
    ///
    /// Note: Hebrew (he) and Thai (th) support varies by instance.
    /// The self-hosted option with --load-only en,he,ar,th is recommended.
    /// </summary>
    public class LibreTranslateProvider : ITranslationProvider
    {
        public string ProviderName => "LibreTranslate";

        private readonly string _endpoint;
        private readonly string _apiKey;

        public LibreTranslateProvider(string endpoint, string apiKey = "")
        {
            _endpoint = string.IsNullOrWhiteSpace(endpoint)
                ? "https://libretranslate.com/translate"
                : endpoint;
            _apiKey = apiKey ?? "";
        }

        public IEnumerator Translate(string text, string sourceLangCode, string targetLangCode,
            Action<string> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                onSuccess?.Invoke("");
                yield break;
            }

            var body = new TranslateRequest
            {
                q       = text,
                source  = sourceLangCode,
                target  = targetLangCode,
                api_key = _apiKey,
                format  = "text"
            };

            byte[] bodyBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

            using var req = new UnityWebRequest(_endpoint, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 20;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"LibreTranslate: {req.responseCode} — {req.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<TranslateResponse>(req.downloadHandler.text);
            if (response != null && !string.IsNullOrWhiteSpace(response.translatedText))
                onSuccess?.Invoke(response.translatedText);
            else
                onError?.Invoke($"LibreTranslate: Empty response — {req.downloadHandler.text}");
        }

        [Serializable]
        private class TranslateRequest
        {
            public string q;
            public string source;
            public string target;
            public string api_key;
            public string format;
        }

        [Serializable]
        private class TranslateResponse
        {
            public string translatedText;
        }
    }
}
