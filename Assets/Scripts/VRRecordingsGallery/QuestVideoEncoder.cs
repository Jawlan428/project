using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace VRRecordings
{
    /// <summary>
    /// Handles video encoding on Quest/Android devices using Unity's native capabilities.
    /// Since ffmpeg is not available on Quest, this provides alternative approaches:
    /// 1. Direct frame capture to video (using Android MediaCodec via Unity)
    /// 2. Saving frames + audio for later PC encoding
    /// 3. Integration with platform-specific recording APIs
    /// </summary>
    public class QuestVideoEncoder : MonoBehaviour
    {
        [Header("Encoder Settings")]
        [SerializeField] private int targetFps = 30;
        
        // These are exposed for future encoder configuration
#pragma warning disable CS0414 // Value is assigned but never used - reserved for future encoder implementation
        [SerializeField] private int videoBitrate = 8000000; // 8 Mbps
        [SerializeField] private int audioBitrate = 192000;  // 192 kbps

        [Header("Output")]
        [SerializeField] private string outputFolderName = "QuestRecordings";
#pragma warning restore CS0414

        // Events
        public event Action<string> OnEncodingComplete;
        public event Action<string> OnEncodingError;
#pragma warning disable CS0067 // Event is never used - reserved for progress reporting implementation
        public event Action<float> OnEncodingProgress;
#pragma warning restore CS0067

        private bool isEncoding;
        private string currentOutputPath;

        /// <summary>
        /// Gets the recordings folder path appropriate for the current platform
        /// </summary>
        public static string GetRecordingsFolder(string folderName = "QuestRecordings")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Quest/Android, use persistent data path
            return Path.Combine(Application.persistentDataPath, folderName);
#else
            // On Editor/Desktop, use project folder or desktop
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                return Path.Combine(projectRoot, folderName);
            }
            return Path.Combine(Application.persistentDataPath, folderName);
#endif
        }

        /// <summary>
        /// Creates a new recording session folder and returns the path
        /// </summary>
        public static string CreateRecordingSession(string folderName = "QuestRecordings")
        {
            string baseFolder = GetRecordingsFolder(folderName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sessionFolder = Path.Combine(baseFolder, $"Recording_{timestamp}");
            
            Directory.CreateDirectory(sessionFolder);
            Debug.Log($"[QuestVideoEncoder] Created session folder: {sessionFolder}");
            
            return sessionFolder;
        }

        /// <summary>
        /// Gets the expected output MP4 path for a session folder
        /// </summary>
        public static string GetVideoOutputPath(string sessionFolder)
        {
            string sessionName = Path.GetFileName(sessionFolder);
            return Path.Combine(sessionFolder, $"{sessionName}.mp4");
        }

        /// <summary>
        /// Checks if Quest native recording is available
        /// </summary>
        public static bool IsNativeRecordingAvailable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Check if we can access Android's MediaRecorder
            try
            {
                using (AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = buildVersion.GetStatic<int>("SDK_INT");
                    // MediaRecorder with surface input requires API level 21+
                    return sdkInt >= 21;
                }
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Converts frames folder + audio WAV to MP4 using platform-appropriate method
        /// On Quest: Uses Android MediaCodec (if available) or keeps frames for PC encoding
        /// On Desktop: Uses ffmpeg (handled by QuestFrameSequenceRecorder)
        /// </summary>
        public void EncodeFramesToVideo(string sessionFolder)
        {
            if (isEncoding)
            {
                Debug.LogWarning("[QuestVideoEncoder] Already encoding");
                return;
            }

            StartCoroutine(EncodeRoutine(sessionFolder));
        }

        private IEnumerator EncodeRoutine(string sessionFolder)
        {
            isEncoding = true;
            string framesFolder = Path.Combine(sessionFolder, "frames");
            string audioPath = Path.Combine(sessionFolder, "audio.wav");
            string outputPath = GetVideoOutputPath(sessionFolder);
            currentOutputPath = outputPath;

            // Check if frames exist
            if (!Directory.Exists(framesFolder))
            {
                OnEncodingError?.Invoke("Frames folder not found");
                isEncoding = false;
                yield break;
            }

            string[] frameFiles = Directory.GetFiles(framesFolder, "*.jpg");
            if (frameFiles.Length == 0)
            {
                OnEncodingError?.Invoke("No frame files found");
                isEncoding = false;
                yield break;
            }

            Debug.Log($"[QuestVideoEncoder] Starting encoding: {frameFiles.Length} frames");

#if UNITY_ANDROID && !UNITY_EDITOR
            // On Quest: Try to use Unity's experimental video encoder or Android APIs
            // For now, we'll create a manifest file for PC encoding
            yield return CreateEncodingManifest(sessionFolder, frameFiles.Length, File.Exists(audioPath));
            
            // Notify that frames are ready for viewing/encoding
            Debug.Log($"[QuestVideoEncoder] Frames saved. For best quality, encode on PC using:");
            Debug.Log($"  ffmpeg -y -framerate {targetFps} -i \"{Path.Combine(framesFolder, "frame_%06d.jpg")}\" " +
                     (File.Exists(audioPath) ? $"-i \"{audioPath}\" " : "") +
                     $"-c:v libx264 -pix_fmt yuv420p -c:a aac \"{outputPath}\"");
            
            // As a fallback, create a simple video using Unity's capabilities
            // Note: Unity doesn't have built-in video encoding, so we either need:
            // 1. A native plugin (like NatCorder)
            // 2. Encode on PC
            // 3. Use Meta's recording API (captures the entire view)
            
            OnEncodingComplete?.Invoke(sessionFolder); // Return session folder, not MP4
#else
            // On Desktop, encoding is handled by QuestFrameSequenceRecorder using ffmpeg
            OnEncodingComplete?.Invoke(outputPath);
#endif
            isEncoding = false;
        }

        /// <summary>
        /// Creates a manifest file with encoding instructions
        /// </summary>
        private IEnumerator CreateEncodingManifest(string sessionFolder, int frameCount, bool hasAudio)
        {
            string manifestPath = Path.Combine(sessionFolder, "encoding_manifest.json");
            
            var manifest = new EncodingManifest
            {
                frameCount = frameCount,
                fps = targetFps,
                hasAudio = hasAudio,
                createdDate = DateTime.Now.ToString("o"),
                platform = "Quest",
                ffmpegCommand = GenerateFfmpegCommand(sessionFolder, hasAudio)
            };

            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(manifestPath, json);
            
            yield return null;
        }

        private string GenerateFfmpegCommand(string sessionFolder, bool hasAudio)
        {
            string framesPath = Path.Combine(sessionFolder, "frames", "frame_%06d.jpg");
            string audioPath = Path.Combine(sessionFolder, "audio.wav");
            string outputPath = GetVideoOutputPath(sessionFolder);

            if (hasAudio)
            {
                return $"ffmpeg -y -framerate {targetFps} -i \"{framesPath}\" -i \"{audioPath}\" " +
                       $"-c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -c:a aac -b:a 192k \"{outputPath}\"";
            }
            else
            {
                return $"ffmpeg -y -framerate {targetFps} -i \"{framesPath}\" " +
                       $"-c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p \"{outputPath}\"";
            }
        }

        [Serializable]
        private class EncodingManifest
        {
            public int frameCount;
            public int fps;
            public bool hasAudio;
            public string createdDate;
            public string platform;
            public string ffmpegCommand;
        }

        /// <summary>
        /// Requests Quest/Meta passthrough recording permission if needed
        /// </summary>
        public static void RequestRecordingPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Request RECORD_AUDIO permission for microphone access
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            }
            
            // Request WRITE_EXTERNAL_STORAGE for older Android versions (not needed on Quest usually)
            // Quest uses app-specific storage which doesn't require this permission
#endif
        }

        /// <summary>
        /// Lists all recording sessions in the recordings folder
        /// </summary>
        public static string[] GetAllRecordingSessions(string folderName = "QuestRecordings")
        {
            string baseFolder = GetRecordingsFolder(folderName);
            
            if (!Directory.Exists(baseFolder))
            {
                return new string[0];
            }

            return Directory.GetDirectories(baseFolder, "Recording_*");
        }

        /// <summary>
        /// Finds the video file in a recording session (MP4 or frames folder)
        /// </summary>
        public static string FindVideoInSession(string sessionFolder)
        {
            if (!Directory.Exists(sessionFolder))
                return null;

            // First, look for MP4 file
            string[] mp4Files = Directory.GetFiles(sessionFolder, "*.mp4");
            if (mp4Files.Length > 0)
                return mp4Files[0];

            // Check for WebM or MOV
            string[] webmFiles = Directory.GetFiles(sessionFolder, "*.webm");
            if (webmFiles.Length > 0)
                return webmFiles[0];

            string[] movFiles = Directory.GetFiles(sessionFolder, "*.mov");
            if (movFiles.Length > 0)
                return movFiles[0];

            return null;
        }

        public bool IsEncoding => isEncoding;
        public string CurrentOutputPath => currentOutputPath;
    }
}

