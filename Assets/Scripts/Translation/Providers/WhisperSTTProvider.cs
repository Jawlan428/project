using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Translation.Providers
{
    /// <summary>
    /// Speech-to-Text via OpenAI Whisper API.
    ///
    /// Supports all four project languages out of the box:
    ///   English (en-US), Hebrew (he-IL), Arabic (ar-SA), Thai (th-TH)
    ///
    /// Setup:
    ///   1. Get an API key from https://platform.openai.com/api-keys
    ///   2. Paste it into TranslationManager → Whisper Api Key in the Inspector
    ///   3. Whisper has no additional per-language config — it auto-detects or uses the hint.
    ///
    /// Cost: ~$0.006 per minute of audio (as of 2025).
    /// Audio limit: max 25 MB per request (the 5-second default segment is ~160 KB).
    /// </summary>
    public class WhisperSTTProvider : ISpeechToTextProvider
    {
        public string ProviderName => "OpenAI Whisper";

        private const string Endpoint = "https://api.openai.com/v1/audio/transcriptions";

        private readonly string _apiKey;

        public WhisperSTTProvider(string apiKey)
        {
            _apiKey = apiKey;
        }

        public IEnumerator Transcribe(byte[] audioWavBytes, string localeCode,
            Action<string> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                onError?.Invoke("Whisper STT: API key not configured. Set it in TranslationManager → Whisper Api Key.");
                yield break;
            }

            if (audioWavBytes == null || audioWavBytes.Length < 44) // minimum WAV header size
            {
                onError?.Invoke("Whisper STT: Audio data is empty or too short.");
                yield break;
            }

            // Extract ISO 639-1 code from BCP-47 locale (e.g. "th-TH" → "th")
            string langCode = localeCode.Contains("-")
                ? localeCode.Substring(0, localeCode.IndexOf('-'))
                : localeCode;

            var form = new WWWForm();
            form.AddBinaryData("file", audioWavBytes, "audio.wav", "audio/wav");
            form.AddField("model", "whisper-1");
            form.AddField("language", langCode);
            form.AddField("response_format", "json");

            using var req = UnityWebRequest.Post(Endpoint, form);
            req.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            req.timeout = 30;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Whisper STT: {req.responseCode} — {req.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<WhisperResponse>(req.downloadHandler.text);
            if (response != null && !string.IsNullOrWhiteSpace(response.text))
                onSuccess?.Invoke(response.text.Trim());
            else
                onError?.Invoke($"Whisper STT: Empty response — {req.downloadHandler.text}");
        }

        [Serializable]
        private class WhisperResponse
        {
            public string text;
        }
    }
}
