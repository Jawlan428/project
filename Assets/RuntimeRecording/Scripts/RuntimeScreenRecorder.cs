using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RuntimeRecording
{
    /// <summary>
    /// Records the Game view to a JPG frame sequence and (optionally) merges it with audio into an MP4 using ffmpeg.
    /// Video capture uses ReadPixels (CPU). For VR/high-res this can be heavy; keep resolution/FPS reasonable.
    /// </summary>
    public sealed class RuntimeScreenRecorder : MonoBehaviour
    {
        public enum OutputLocation
        {
            PersistentDataPath = 0,
            Desktop = 1,
        }

        [Header("Capture Source")]
        [Tooltip("Camera used for capture. If null, will try Camera.main at runtime.")]
        public Camera captureCamera;

        [Header("Video")]
        [Min(1)] public int fps = 30;
        [Min(16)] public int width = 1280;
        [Min(16)] public int height = 720;
        [Tooltip("JPG quality (1..100). Higher = larger files.")]
        [Range(1, 100)] public int jpgQuality = 80;

        [Header("Audio")]
        public bool recordAudio = true;
        [Tooltip("Audio listener to tap. If null, will try FindObjectOfType<AudioListener>().")]
        public AudioListener audioListener;

        [Header("Output")]
        public OutputLocation outputLocation = OutputLocation.Desktop;
        [Tooltip("Subfolder name under the chosen output location.")]
        public string folderName = "UnityRecordings";
        [Tooltip("If enabled, tries to produce a single MP4 by calling ffmpeg on Stop.")]
        public bool mergeToMp4WithFfmpeg = true;
        [Tooltip("Optional: absolute path to ffmpeg.exe. If empty, will try 'ffmpeg' from PATH.")]
        public string ffmpegPathOverride = "";
        [Tooltip("If true, deletes frame JPGs after MP4 is produced.")]
        public bool deleteFramesAfterMp4 = false;

        public bool IsRecording => _isRecording;
        public string CurrentOutputDirectory => _sessionDir;

        private bool _isRecording;
        private string _sessionDir;
        private string _framesDir;
        private string _audioPath;
        private string _mp4Path;

        private RenderTexture _rt;
        private Texture2D _readbackTex;
        private WaitForEndOfFrame _waitForEndOfFrame;
        private float _nextCaptureTime;
        private int _frameIndex;

        private RuntimeWavRecorder _wavRecorder;

        private void Awake()
        {
            _waitForEndOfFrame = new WaitForEndOfFrame();
        }

        private void OnDisable()
        {
            if (_isRecording)
                StopRecording();
        }

        public void StartRecording()
        {
            if (_isRecording)
                return;

            if (captureCamera == null)
                captureCamera = Camera.main;

            if (captureCamera == null)
            {
                UnityEngine.Debug.LogError("[RuntimeScreenRecorder] No capture camera assigned, and Camera.main was not found.");
                return;
            }

            var root = GetRootOutputDirectory();
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _sessionDir = Path.Combine(root, folderName, $"Recording_{stamp}");
            _framesDir = Path.Combine(_sessionDir, "frames");
            Directory.CreateDirectory(_framesDir);

            _audioPath = Path.Combine(_sessionDir, "audio.wav");
            _mp4Path = Path.Combine(_sessionDir, $"Recording_{stamp}.mp4");

            AllocateCaptureTextures();

            _frameIndex = 0;
            _nextCaptureTime = Time.unscaledTime;
            _isRecording = true;

            if (recordAudio)
            {
                if (audioListener == null)
                    audioListener = FindFirstObjectByType<AudioListener>();

                if (audioListener == null)
                {
                    UnityEngine.Debug.LogWarning("[RuntimeScreenRecorder] recordAudio is enabled but no AudioListener was found. Audio will be skipped.");
                }
                else
                {
                    _wavRecorder = audioListener.gameObject.GetComponent<RuntimeWavRecorder>();
                    if (_wavRecorder == null)
                        _wavRecorder = audioListener.gameObject.AddComponent<RuntimeWavRecorder>();

                    _wavRecorder.Begin(_audioPath);
                }
            }

            StartCoroutine(CaptureLoop());
            UnityEngine.Debug.Log($"[RuntimeScreenRecorder] Recording STARTED. Output: {_sessionDir}");
        }

        public void StopRecording()
        {
            if (!_isRecording)
                return;

            _isRecording = false;

            _wavRecorder?.End();
            _wavRecorder = null;

            ReleaseCaptureTextures();

            UnityEngine.Debug.Log($"[RuntimeScreenRecorder] Recording STOPPED. Output: {_sessionDir}");

            if (mergeToMp4WithFfmpeg)
                _ = TryMergeToMp4Async();
        }

        private IEnumerator CaptureLoop()
        {
            while (_isRecording)
            {
                // Throttle to target FPS using unscaled time so it keeps working during timescale changes.
                if (Time.unscaledTime < _nextCaptureTime)
                {
                    yield return null;
                    continue;
                }

                _nextCaptureTime += 1f / Mathf.Max(1, fps);

                yield return _waitForEndOfFrame;

                if (!_isRecording)
                    yield break;

                CaptureFrame();
            }
        }

        private void CaptureFrame()
        {
            if (_rt == null || _readbackTex == null)
                return;

            var prevTarget = captureCamera.targetTexture;
            var prevActive = RenderTexture.active;

            try
            {
                captureCamera.targetTexture = _rt;
                captureCamera.Render();

                RenderTexture.active = _rt;
                _readbackTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                _readbackTex.Apply(false, false);

                var jpg = _readbackTex.EncodeToJPG(jpgQuality);
                var framePath = Path.Combine(_framesDir, $"frame_{_frameIndex:D06}.jpg");
                _frameIndex++;

                // File I/O off main thread.
                Task.Run(() => File.WriteAllBytes(framePath, jpg));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                StopRecording();
            }
            finally
            {
                captureCamera.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
            }
        }

        private void AllocateCaptureTextures()
        {
            ReleaseCaptureTextures();

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            _rt.Create();

            _readbackTex = new Texture2D(width, height, TextureFormat.RGB24, false);
        }

        private void ReleaseCaptureTextures()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }

            if (_readbackTex != null)
            {
                Destroy(_readbackTex);
                _readbackTex = null;
            }
        }

        private string GetRootOutputDirectory()
        {
            if (outputLocation == OutputLocation.Desktop)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                try
                {
                    return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                }
                catch
                {
                    // fall through
                }
#endif
                // Desktop isn't available on mobile/Quest. Fall back.
                return Application.persistentDataPath;
            }

            return Application.persistentDataPath;
        }

        private async Task TryMergeToMp4Async()
        {
            // If audio was requested but couldn't start, just skip merging.
            if (recordAudio && !File.Exists(_audioPath))
            {
                UnityEngine.Debug.LogWarning("[RuntimeScreenRecorder] Audio file not found; skipping MP4 merge.");
                return;
            }

            // Wait a moment for async frame writes to finish.
            await Task.Delay(500);

            var ffmpegExe = string.IsNullOrWhiteSpace(ffmpegPathOverride) ? "ffmpeg" : ffmpegPathOverride;

            var inputPattern = Path.Combine(_framesDir, "frame_%06d.jpg");
            var args = recordAudio
                ? $"-y -framerate {fps} -i \"{inputPattern}\" -i \"{_audioPath}\" -c:v libx264 -pix_fmt yuv420p -c:a aac \"{_mp4Path}\""
                : $"-y -framerate {fps} -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{_mp4Path}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = _sessionDir
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    UnityEngine.Debug.LogWarning("[RuntimeScreenRecorder] Failed to start ffmpeg. Is it installed / on PATH?");
                    return;
                }

                var stderr = await proc.StandardError.ReadToEndAsync();
                var stdout = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode != 0)
                {
                    UnityEngine.Debug.LogWarning($"[RuntimeScreenRecorder] ffmpeg failed (exit {proc.ExitCode}).\n{stderr}\n{stdout}");
                    return;
                }

                UnityEngine.Debug.Log($"[RuntimeScreenRecorder] MP4 created: {_mp4Path}");

                if (deleteFramesAfterMp4)
                {
                    try
                    {
                        Directory.Delete(_framesDir, true);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[RuntimeScreenRecorder] Could not delete frames folder: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RuntimeScreenRecorder] MP4 merge skipped (ffmpeg missing or failed): {e.Message}");
            }
        }
    }
}


