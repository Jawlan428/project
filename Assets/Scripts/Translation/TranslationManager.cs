using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Translation.Providers;
using UnityEngine;

namespace Translation
{
    /// <summary>Which translation backend to use.</summary>
    public enum TranslationBackend
    {
        /// <summary>Google Translate unofficial endpoint — free, no key, best quality. Recommended.</summary>
        Google,
        /// <summary>Free, no API key, 5 000 words/day limit. Quality varies for short phrases.</summary>
        MyMemory,
        /// <summary>Requires a running LibreTranslate instance or public endpoint.</summary>
        LibreTranslate
    }

    /// <summary>
    /// Core controller for the VR Meeting Translation System.
    ///
    /// Responsibilities:
    ///   • Captures microphone audio with energy-based voice-activity detection
    ///   • Sends audio segments to the STT provider (Whisper)
    ///   • Sends transcripts to the translation provider (LibreTranslate)
    ///   • Maintains an in-memory history of TranslationEntry records
    ///   • Fires events consumed by SubtitleUIController and TranslationTabletPage
    ///   • Logs every translation to EventLogger (→ History tab) and AuditLogger
    ///
    /// Modular design:
    ///   Swap STT or translation backends by calling SetSTTProvider() / SetTranslationProvider()
    ///   or by implementing ISpeechToTextProvider / ITranslationProvider and assigning at Awake.
    ///
    /// Voice translation is NOT implemented here yet — the system is intentionally subtitle-only.
    /// Add a voice synthesis provider later without touching this class.
    /// </summary>
    public class TranslationManager : MonoBehaviour
    {
        public static TranslationManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("STT Provider — OpenAI Whisper")]
        [Tooltip("Get a key at https://platform.openai.com/api-keys")]
        [SerializeField] private string whisperApiKey = "";

        [Header("Translation Provider")]
        [Tooltip("Google = best quality, free, no key (recommended).\nMyMemory = free, no key, limited quality.\nLibreTranslate = self-hosted or public instance.")]
        [SerializeField] private TranslationBackend translationBackend = TranslationBackend.Google;

        [Tooltip("Only used when Translation Backend = LibreTranslate.\n" +
                 "Public: https://libretranslate.com/translate\n" +
                 "Free mirror: https://translate.argosopentech.com/translate\n" +
                 "Self-hosted: http://localhost:5000/translate")]
        [SerializeField] private string libreTranslateEndpoint = "https://translate.argosopentech.com/translate";
        [Tooltip("Leave empty for self-hosted or the free Argos mirror")]
        [SerializeField] private string libreTranslateApiKey = "";

        [Header("Language")]
        [SerializeField] private TranslationLanguage sourceLanguage = TranslationLanguage.English;
        [SerializeField] private TranslationLanguage targetLanguage = TranslationLanguage.Hebrew;

        [Header("Microphone / VAD")]
        [Tooltip("Max duration of one recording segment in seconds")]
        [SerializeField] [Range(3f, 15f)] private float segmentMaxDuration = 6f;
        [Tooltip("RMS energy level above which speech is detected (0 = silent, 1 = max). Lower = more sensitive.")]
        [SerializeField] [Range(0.001f, 0.1f)] private float voiceThreshold = 0.005f;
        [Tooltip("Seconds of continuous silence that ends the current segment")]
        [SerializeField] [Range(0.5f, 3f)] private float silenceToEndSegment = 1.2f;
        [Tooltip("Sample rate for microphone capture — 16000 is standard for STT")]
        [SerializeField] private int micSampleRate = 16000;

        [Header("Features")]
        [SerializeField] private bool showSubtitlesAboveAvatars = true;
        [SerializeField] private bool saveTranslatedTranscript  = true;
        [SerializeField] [Range(10, 500)] private int maxHistoryEntries = 200;

        // ── Public state ──────────────────────────────────────────────────────

        public TranslationLanguage SourceLanguage
        {
            get => sourceLanguage;
            private set { sourceLanguage = value; OnLanguageChanged?.Invoke(sourceLanguage, targetLanguage); }
        }

        public TranslationLanguage TargetLanguage
        {
            get => targetLanguage;
            private set { targetLanguage = value; OnLanguageChanged?.Invoke(sourceLanguage, targetLanguage); }
        }

        public bool IsListening          { get; private set; }
        public bool IsProcessing         { get; private set; }

        public bool ShowSubtitlesAboveAvatars
        {
            get => showSubtitlesAboveAvatars;
            set { showSubtitlesAboveAvatars = value; OnSubtitleToggle?.Invoke(value); }
        }

        public bool SaveTranslatedTranscript
        {
            get => saveTranslatedTranscript;
            set => saveTranslatedTranscript = value;
        }

        public IReadOnlyList<TranslationEntry> History => _history;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when a new translation entry is fully processed.</summary>
        public event Action<TranslationEntry>                           OnTranslationReceived;

        /// <summary>Fired with a human-readable status message for the UI.</summary>
        public event Action<string> OnStatusChanged;

        /// <summary>Fired when an error occurs — stays visible until cleared.</summary>
        public event Action<string> OnErrorChanged;

        /// <summary>Fired when listening starts or stops.</summary>
        public event Action<bool>                                       OnListeningChanged;

        /// <summary>Fired when subtitles are toggled on/off.</summary>
        public event Action<bool>                                       OnSubtitleToggle;

        /// <summary>Fired when either source or target language changes.</summary>
        public event Action<TranslationLanguage, TranslationLanguage>   OnLanguageChanged;

        // ── Private ───────────────────────────────────────────────────────────

        private ISpeechToTextProvider          _sttProvider;
        private ITranslationProvider           _translationProvider;
        private readonly List<TranslationEntry> _history = new List<TranslationEntry>();

        private AudioClip  _micClip;
        private string     _micDevice;
        private Coroutine  _listenCoroutine;

        // Prevents "Listening…" from overwriting error messages for 6 seconds
        private float _errorExpireTime = -1f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _sttProvider         = new WhisperSTTProvider(whisperApiKey);
            _translationProvider = BuildTranslationProvider();
        }

        private ITranslationProvider BuildTranslationProvider()
        {
            return translationBackend switch
            {
                TranslationBackend.LibreTranslate => new LibreTranslateProvider(libreTranslateEndpoint, libreTranslateApiKey),
                TranslationBackend.MyMemory       => new MyMemoryTranslateProvider(),
                _                                 => new GoogleTranslateProvider()
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopListening();
        }

        // ── Public API — configuration ────────────────────────────────────────

        public void SetSourceLanguage(TranslationLanguage lang) => SourceLanguage = lang;
        public void SetTargetLanguage(TranslationLanguage lang) => TargetLanguage = lang;

        /// <summary>Replace the STT backend at runtime (e.g. switch to Azure).</summary>
        public void SetSTTProvider(ISpeechToTextProvider provider)
        {
            _sttProvider = provider;
            OnStatusChanged?.Invoke($"STT provider: {provider?.ProviderName ?? "None"}");
        }

        /// <summary>Replace the translation backend at runtime.</summary>
        public void SetTranslationProvider(ITranslationProvider provider)
        {
            _translationProvider = provider;
            OnStatusChanged?.Invoke($"Translation provider: {provider?.ProviderName ?? "None"}");
        }

        public void SetWhisperApiKey(string key)
        {
            whisperApiKey = key;
            _sttProvider  = new WhisperSTTProvider(key);
        }

        public void SetLibreTranslateEndpoint(string endpoint, string apiKey = "")
        {
            libreTranslateEndpoint = endpoint;
            libreTranslateApiKey   = apiKey;
            translationBackend     = TranslationBackend.LibreTranslate;
            _translationProvider   = new LibreTranslateProvider(endpoint, apiKey);
        }

        /// <summary>Switch to the free MyMemory backend at runtime.</summary>
        public void UseMyMemoryProvider()
        {
            translationBackend   = TranslationBackend.MyMemory;
            _translationProvider = new MyMemoryTranslateProvider();
            OnStatusChanged?.Invoke("Translation backend: MyMemory (free)");
        }

        // ── Public API — listening ────────────────────────────────────────────

        /// <summary>Live microphone RMS level 0–1. Updated every frame while listening.</summary>
        public float MicLevel { get; private set; }

        /// <summary>Start microphone capture + VAD loop.</summary>
        public void StartListening()
        {
            if (IsListening) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Request microphone permission on Android / Quest before starting
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    UnityEngine.Android.Permission.Microphone))
            {
                OnStatusChanged?.Invoke("Requesting microphone permission…");
                UnityEngine.Android.Permission.RequestUserPermission(
                    UnityEngine.Android.Permission.Microphone);
                StartCoroutine(WaitForMicPermissionThenStart());
                return;
            }
#endif
            StartListeningInternal();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator WaitForMicPermissionThenStart()
        {
            // Poll until permission is granted or denied (max 10 s)
            float waited = 0f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                       UnityEngine.Android.Permission.Microphone) && waited < 10f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
            }

            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    UnityEngine.Android.Permission.Microphone))
            {
                StartListeningInternal();
            }
            else
            {
                OnStatusChanged?.Invoke("Microphone permission denied — cannot listen");
            }
        }
#endif

        private void StartListeningInternal()
        {
            if (Microphone.devices.Length == 0)
            {
                OnStatusChanged?.Invoke("No microphone detected — check device settings");
                return;
            }

            _micDevice = Microphone.devices[0];
            IsListening = true;
            OnListeningChanged?.Invoke(true);
            OnStatusChanged?.Invoke($"Listening…  (mic: {_micDevice})");
            _listenCoroutine = StartCoroutine(ListenLoop());
        }

        /// <summary>Stop microphone capture.</summary>
        public void StopListening()
        {
            if (!IsListening) return;

            IsListening = false;
            OnListeningChanged?.Invoke(false);
            OnStatusChanged?.Invoke("Stopped");

            if (_listenCoroutine != null) { StopCoroutine(_listenCoroutine); _listenCoroutine = null; }
            if (Microphone.IsRecording(_micDevice)) Microphone.End(_micDevice);
            if (_micClip != null) { Destroy(_micClip); _micClip = null; }
        }

        public void ToggleListening()
        {
            if (IsListening) StopListening();
            else StartListening();
        }

        // ── Public API — manual text submission ───────────────────────────────

        /// <summary>
        /// Submit text directly for translation (bypasses STT).
        /// Useful for typed messages or text received from other participants via network.
        /// </summary>
        public void SubmitText(string text, string speakerName = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            StartCoroutine(TranslateAndPublish(text, speakerName ?? GetLocalPlayerName()));
        }

        // ── Public API — history ──────────────────────────────────────────────

        public void ClearHistory() => _history.Clear();

        /// <summary>Save the full transcript to a .txt file on the Desktop.</summary>
        public void SaveHistoryToFile()
        {
            if (_history.Count == 0)
            {
                OnStatusChanged?.Invoke("Nothing to save — transcript is empty");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"VR Meeting Translation Transcript");
                sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                sb.AppendLine();

                foreach (var e in _history)
                {
                    sb.AppendLine($"[{e.timestampUtc.ToLocalTime():HH:mm:ss}]  {e.speakerName}");
                    sb.AppendLine($"  {e.sourceLanguage.ToDisplayName()}: {e.originalText}");
                    sb.AppendLine($"  {e.targetLanguage.ToDisplayName()}: {e.translatedText}");
                    sb.AppendLine();
                }

                string dir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "MeetingTranscripts");
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, $"transcript_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                OnStatusChanged?.Invoke($"Saved → {Path.GetFileName(path)}");
                SmartFarm.EventLogger.LogEvent($"Translation transcript saved: {path}");
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke($"Save failed: {ex.Message}");
            }
        }

        // ── Listen loop (VAD → segment → STT → translate) ─────────────────────

        private IEnumerator ListenLoop()
        {
            while (IsListening)
            {
                // Start a new microphone segment
                int clipDuration = Mathf.CeilToInt(segmentMaxDuration) + 2;
                _micClip  = Microphone.Start(_micDevice, false, clipDuration, micSampleRate);

                // Wait for mic to initialise
                float waitTime = 0f;
                while (Microphone.GetPosition(_micDevice) <= 0 && waitTime < 2f)
                {
                    yield return null;
                    waitTime += Time.unscaledDeltaTime;
                }

                float elapsed    = 0f;
                float silenceAcc = 0f;
                bool  hadVoice   = false;

                // VAD loop — poll every 100ms
                while (elapsed < segmentMaxDuration && IsListening)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    elapsed += 0.1f;

                    float rms = GetMicRMSLevel();

                    if (rms > voiceThreshold)
                    {
                        hadVoice   = true;
                        silenceAcc = 0f;
                    }
                    else if (hadVoice)
                    {
                        silenceAcc += 0.1f;
                        if (silenceAcc >= silenceToEndSegment)
                            break; // natural pause → send this segment
                    }
                }

                int capturedSamples = Microphone.GetPosition(_micDevice);
                Microphone.End(_micDevice);

                // Only process segments that contain actual speech
                bool hasEnoughAudio = capturedSamples > micSampleRate * 0.4f; // > 0.4 s
                if (hadVoice && hasEnoughAudio && _micClip != null)
                {
                    IsProcessing = true;
                    OnStatusChanged?.Invoke("Processing speech…");

                    float[] samples = new float[capturedSamples];
                    _micClip.GetData(samples, 0);
                    byte[] wavBytes = PcmToWav(samples, micSampleRate, 1);

                    yield return StartCoroutine(TranscribeSegment(wavBytes));

                    IsProcessing = false;
                }

                if (_micClip != null) { Destroy(_micClip); _micClip = null; }

                // Don't overwrite recent error messages
                if (IsListening && Time.realtimeSinceStartup > _errorExpireTime)
                    OnStatusChanged?.Invoke("Listening…");
            }
        }

        private float GetMicRMSLevel()
        {
            if (_micClip == null) { MicLevel = 0f; return 0f; }
            int pos = Microphone.GetPosition(_micDevice);
            if (pos <= 0) { MicLevel = 0f; return 0f; }

            int sampleWindow = Mathf.Min(pos, 512);
            int startSample  = Mathf.Max(0, pos - sampleWindow);
            float[] buf = new float[sampleWindow];
            _micClip.GetData(buf, startSample);

            float sum = 0f;
            for (int i = 0; i < buf.Length; i++) sum += buf[i] * buf[i];
            MicLevel = Mathf.Sqrt(sum / buf.Length);
            return MicLevel;
        }

        // ── STT + Translation pipeline ────────────────────────────────────────

        private IEnumerator TranscribeSegment(byte[] wavBytes)
        {
            if (_sttProvider == null) yield break;

            string transcript = null;
            string sttError   = null;

            yield return StartCoroutine(
                _sttProvider.Transcribe(wavBytes, sourceLanguage.ToLocaleCode(),
                    t   => transcript = t,
                    err => sttError   = err));

            if (!string.IsNullOrEmpty(sttError))
            {
                string errMsg = $"STT error: {sttError}";
                _errorExpireTime = Time.realtimeSinceStartup + 8f;
                OnStatusChanged?.Invoke(errMsg);
                OnErrorChanged?.Invoke(errMsg);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(transcript))
                yield return StartCoroutine(TranslateAndPublish(transcript, GetLocalPlayerName()));
        }

        private IEnumerator TranslateAndPublish(string text, string speakerName)
        {
            string translated = text; // default: pass-through when languages match
            string xlError    = null;

            if (sourceLanguage != targetLanguage && _translationProvider != null)
            {
                yield return StartCoroutine(
                    _translationProvider.Translate(
                        text,
                        sourceLanguage.ToLanguageCode(),
                        targetLanguage.ToLanguageCode(),
                        t   => translated = t,
                        err => xlError    = err));
            }

            if (!string.IsNullOrEmpty(xlError))
            {
                string errMsg = $"Translation error: {xlError}";
                _errorExpireTime = Time.realtimeSinceStartup + 8f;
                OnStatusChanged?.Invoke(errMsg);
                OnErrorChanged?.Invoke(errMsg);
                yield break;
            }

            // Build and store the entry
            var entry = new TranslationEntry
            {
                id             = Guid.NewGuid().ToString("N"),
                timestampUtc   = DateTime.UtcNow,
                speakerName    = speakerName,
                originalText   = text,
                translatedText = translated,
                sourceLanguage = sourceLanguage,
                targetLanguage = targetLanguage,
                savedToDisk    = saveTranslatedTranscript
            };

            _history.Insert(0, entry);
            if (_history.Count > maxHistoryEntries)
                _history.RemoveRange(maxHistoryEntries, _history.Count - maxHistoryEntries);

            OnTranslationReceived?.Invoke(entry);
            OnStatusChanged?.Invoke("Translation received");
            OnErrorChanged?.Invoke(""); // clear any previous error

            // Log to SmartFarm EventLogger → populates History tab live feed
            string logMsg =
                $"[Translation] {speakerName} " +
                $"({sourceLanguage.ToShortLabel()}→{targetLanguage.ToShortLabel()}): " +
                $"\"{translated}\"";
            SmartFarm.EventLogger.LogEvent(logMsg);

            // Log full detail to AuditLogger
            if (AuditLogger.Instance != null)
            {
                AuditLogger.Instance.Log(
                    AuditEventType.FARM_EVENT,
                    metaJson:
                        $"{{\"event\":\"translation\"," +
                        $"\"speaker\":\"{EscapeJson(speakerName)}\"," +
                        $"\"sourceLang\":\"{sourceLanguage.ToLanguageCode()}\"," +
                        $"\"targetLang\":\"{targetLanguage.ToLanguageCode()}\"," +
                        $"\"original\":\"{EscapeJson(text)}\"," +
                        $"\"translated\":\"{EscapeJson(translated)}\"}}"
                );
            }
        }

        // ── WAV encoder ───────────────────────────────────────────────────────

        /// <summary>Converts Unity float PCM samples to a standard WAV byte array.</summary>
        private static byte[] PcmToWav(float[] samples, int sampleRate, int channels)
        {
            int sampleCount = samples.Length;
            int byteCount   = sampleCount * 2; // 16-bit = 2 bytes per sample

            using var ms = new MemoryStream(44 + byteCount);
            using var bw = new BinaryWriter(ms);

            // RIFF header
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + byteCount);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);                       // PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2);      // byte rate
            bw.Write((short)(channels * 2));           // block align
            bw.Write((short)16);                      // bits per sample

            // data chunk
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(byteCount);
            foreach (float s in samples)
                bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));

            return ms.ToArray();
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static string GetLocalPlayerName()
        {
            // Reflection-based lookup — avoids a hard compile-time dependency on
            // XRINetworkGameManager which lives in a separate SDK assembly.
            try
            {
                System.Type xriType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    xriType = asm.GetType("XRINetworkGameManager");
                    if (xriType != null) break;
                }

                if (xriType != null)
                {
                    var nameProp = xriType.GetProperty("LocalPlayerName",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Static);

                    if (nameProp != null)
                    {
                        var nameVar = nameProp.GetValue(null);
                        if (nameVar != null)
                        {
                            var valueProp = nameVar.GetType().GetProperty("Value");
                            if (valueProp != null)
                            {
                                string name = valueProp.GetValue(nameVar) as string;
                                if (!string.IsNullOrEmpty(name)) return name;
                            }
                        }
                    }
                }
            }
            catch { }

            return SystemInfo.deviceName ?? "Player";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n",  "\\n")
                    .Replace("\r",  "\\r");
        }
    }
}
