using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace RuntimeRecording
{
    /// <summary>
    /// Quest/Android-friendly runtime video recorder:
    /// - Captures video frames and audio, automatically creates MP4 video files
    /// - On Windows/Editor: Creates MP4 video file directly using ffmpeg
    /// - On Quest/Android: Saves frames + audio (can be merged to MP4 on PC, or use native encoder plugin)
    /// - Supports game audio + microphone mixing
    /// </summary>
    public sealed class QuestFrameSequenceRecorder : MonoBehaviour
    {
        [Header("Video")]
        [Min(1)] public int fps = 24;
        [Min(64)] public int width = 1024;
        [Min(64)] public int height = 1024;
        [Range(1, 100)] public int jpgQuality = 80;

        [Header("Audio (optional)")]
        public bool recordAudio = false;
        [Tooltip("If null, audio will be captured from the same GameObject this script is on (via OnAudioFilterRead).")]
        public AudioSource audioSourceForCapture;
        
        [Header("Microphone (mixed with game audio)")]
        [Tooltip("If enabled, microphone audio will be mixed with game audio. Requires microphone permission on Android.")]
        public bool recordMicrophone = true;
        [Tooltip("Microphone device name. Leave empty for default microphone.")]
        public string microphoneDevice = "";
        [Range(0f, 2f)]
        [Tooltip("Microphone volume multiplier (0 = silent, 1 = normal, 2 = double).")]
        public float microphoneVolume = 1f;
        [Range(0f, 2f)]
        [Tooltip("Game audio volume multiplier (0 = silent, 1 = normal, 2 = double).")]
        public float gameAudioVolume = 1f;

        [Header("Output")]
        public string folderName = "QuestRecordings";
        [Tooltip("Optional override for the root output directory on desktop/editor. Ignored on Android/Quest builds.")]
        public string outputRootOverride = "";

        [Header("MP4 Output")]
        [Tooltip("If enabled, automatically merges frames + audio into a single MP4 video file on Stop. On Windows/Editor uses ffmpeg. On Quest/Android, frames are saved and can be merged on PC.")]
        public bool createMp4Video = true;
        [Tooltip("Optional: absolute path to ffmpeg.exe. If empty, will try 'ffmpeg' from PATH (Windows/Editor only).")]
        public string ffmpegPathOverride = "";
        [Tooltip("If true, deletes individual frame files after MP4 video is created (Windows/Editor only).")]
        public bool deleteFramesAfterMp4 = true;

        public bool IsRecording => _isRecording;
        public string SessionDirectory => _sessionDir;
        public string FramesDirectory => _framesDir;
        public string WavPath => _wavPath;

        private bool _isRecording;
        private string _sessionDir;
        private string _framesDir;
        private string _wavPath;
        private string _mp4Path;

        private RenderTexture _rt;
        private Texture2D _readbackTex;
        private WaitForEndOfFrame _eof;
        private float _nextCaptureTime;
        private int _frameIndex;

        private RuntimePcmWavWriter _wavWriter;
        private int _audioSampleRate;
        private int _audioChannels;
        private bool _audioReady;
        
        // Microphone capture
        private AudioClip _microphoneClip;
        private int _microphoneLastPosition;
        private float[] _microphoneBuffer;
        private float[] _gameAudioBuffer;
        private float[] _mixedBuffer;

        private void Awake()
        {
            _eof = new WaitForEndOfFrame();
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

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var root = GetRootOutputDirectory();
            _sessionDir = Path.Combine(root, folderName, $"Recording_{stamp}");
            _framesDir = Path.Combine(_sessionDir, "frames");
            Directory.CreateDirectory(_framesDir);

            _wavPath = Path.Combine(_sessionDir, "audio.wav");
            _mp4Path = Path.Combine(_sessionDir, $"Recording_{stamp}.mp4");

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _rt.Create();

            _readbackTex = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);

            _frameIndex = 0;
            _nextCaptureTime = Time.unscaledTime;
            _isRecording = true;

            SetupAudioIfNeeded();

            StartCoroutine(CaptureLoop());
            UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Recording started. Output: {_sessionDir}");
        }

        public void StopRecording()
        {
            if (!_isRecording)
                return;

            _isRecording = false;
            StopAllCoroutines();

            try { _wavWriter?.Dispose(); } catch { /* ignore */ }
            _wavWriter = null;

            // Stop microphone
            if (_microphoneClip != null && Microphone.IsRecording(_microphoneClip.name))
            {
                Microphone.End(_microphoneClip.name);
                Destroy(_microphoneClip);
                _microphoneClip = null;
            }

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

            UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Recording stopped. Output: {_sessionDir}");

            // Automatically create MP4 video file
            if (createMp4Video)
            {
#if !UNITY_ANDROID || UNITY_EDITOR
                // On Windows/Editor: Merge frames + audio to MP4 using ffmpeg
                TryMergeToMp4FireAndForget();
#else
                // On Quest/Android: Log instructions for merging on PC
                UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Video recording complete! To create MP4 video file:");
                UnityEngine.Debug.Log($"1. Pull folder from device: adb pull {_sessionDir}");
                UnityEngine.Debug.Log($"2. Run: ffmpeg -y -framerate {fps} -i \"{Path.Combine(_sessionDir, "frames", "frame_%06d.jpg")}\" -i \"{_wavPath}\" -c:v libx264 -pix_fmt yuv420p -c:a aac \"{_mp4Path}\"");
                UnityEngine.Debug.Log($"Or use the frames + audio.wav files in: {_sessionDir}");
#endif
            }
            else
            {
                UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Frames and audio saved to: {_sessionDir}");
            }
        }

        private void SetupAudioIfNeeded()
        {
            _audioReady = false;
            _audioSampleRate = AudioSettings.outputSampleRate;

            // Unity typically mixes to stereo, but OnAudioFilterRead receives channel count.
            // We'll discover channels on first callback and create the writer there.
            if (!recordAudio)
                return;

            // Start microphone if requested
            if (recordMicrophone)
            {
                try
                {
                    string deviceName = string.IsNullOrEmpty(microphoneDevice) ? null : microphoneDevice;
                    int minFreq, maxFreq;
                    Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
                    int micFreq = Mathf.Clamp(_audioSampleRate, minFreq, maxFreq > 0 ? maxFreq : _audioSampleRate);
                    
                    _microphoneClip = Microphone.Start(deviceName, true, 10, micFreq);
                    if (_microphoneClip == null)
                    {
                        UnityEngine.Debug.LogWarning("[QuestFrameSequenceRecorder] Failed to start microphone. Microphone permission may be required on Android.");
                        recordMicrophone = false;
                    }
                    else
                    {
                        _microphoneLastPosition = 0;
                        UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Microphone started: {(_microphoneClip.name)}");
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[QuestFrameSequenceRecorder] Microphone setup failed: {e.Message}");
                    recordMicrophone = false;
                }
            }

            // Ensure an AudioSource exists so OnAudioFilterRead runs on this component.
            // This captures the game's mixed audio output.
            if (audioSourceForCapture == null)
                audioSourceForCapture = GetComponent<AudioSource>();

            if (audioSourceForCapture == null)
                audioSourceForCapture = gameObject.AddComponent<AudioSource>();

            // Keep it silent; we only need the callback.
            audioSourceForCapture.playOnAwake = false;
            audioSourceForCapture.loop = true;
            audioSourceForCapture.volume = 0f;

            if (!audioSourceForCapture.isPlaying)
                audioSourceForCapture.Play();
        }

        private IEnumerator CaptureLoop()
        {
            while (_isRecording)
            {
                yield return _eof;

                if (Time.unscaledTime < _nextCaptureTime)
                    continue;

                _nextCaptureTime += 1f / Mathf.Max(1, fps);

                try
                {
                    CaptureFrame();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[QuestFrameSequenceRecorder] Capture failed: {e.Message}");
                }
            }
        }

        private string GetRootOutputDirectory()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Quest/Android cannot write to Windows paths; use device storage.
            return Application.persistentDataPath;
#else
            // In Editor/desktop builds, default to project root (e.g. C:\Users\ASUS\My project)
            // so it's easy to find outputs next to the project.
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            var candidate =
                !string.IsNullOrWhiteSpace(outputRootOverride) ? outputRootOverride :
                !string.IsNullOrWhiteSpace(projectRoot) ? projectRoot :
                Application.persistentDataPath;

            try
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch
            {
                return Application.persistentDataPath;
            }
#endif
        }

        private void CaptureFrame()
        {
            // Capture the screen into a RT. This is supported on most Unity runtimes including Android/Quest.
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);

            var prev = RenderTexture.active;
            RenderTexture.active = _rt;

            _readbackTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            _readbackTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            RenderTexture.active = prev;

            byte[] jpg = ImageConversion.EncodeToJPG(_readbackTex, jpgQuality);
            var path = Path.Combine(_framesDir, $"frame_{_frameIndex:D6}.jpg");
            File.WriteAllBytes(path, jpg);
            _frameIndex++;
        }

        // Captures the AudioSource output on this GameObject and mixes it with microphone audio.
        // For game mix you may prefer attaching this script to an always-on AudioListener object.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isRecording || !recordAudio)
                return;

            if (!_audioReady)
            {
                _audioChannels = Mathf.Max(1, channels);
                _wavWriter = new RuntimePcmWavWriter(_wavPath, _audioSampleRate, _audioChannels);
                _audioReady = true;
            }
            
            // Ensure buffers are the right size (in case data.Length changes, though it shouldn't)
            if (_gameAudioBuffer == null || _gameAudioBuffer.Length != data.Length)
            {
                _gameAudioBuffer = new float[data.Length];
                _microphoneBuffer = new float[data.Length];
                _mixedBuffer = new float[data.Length];
            }

            // Store game audio (apply volume)
            for (int i = 0; i < data.Length; i++)
            {
                _gameAudioBuffer[i] = data[i] * gameAudioVolume;
            }

            // Read and mix microphone audio if enabled
            if (recordMicrophone && _microphoneClip != null && Microphone.IsRecording(_microphoneClip.name))
            {
                int micPosition = Microphone.GetPosition(_microphoneClip.name);
                if (micPosition < 0)
                    micPosition = 0;

                int sampleCount = data.Length;
                int micSamplesAvailable = micPosition - _microphoneLastPosition;
                
                if (micSamplesAvailable < 0)
                    micSamplesAvailable += _microphoneClip.samples;

                if (micSamplesAvailable > 0)
                {
                    // Read microphone data (handle circular buffer wrap-around)
                    int samplesToRead = Mathf.Min(sampleCount, micSamplesAvailable);
                    
                    if (_microphoneLastPosition + samplesToRead <= _microphoneClip.samples)
                    {
                        // Simple case: no wrap-around
                        _microphoneClip.GetData(_microphoneBuffer, _microphoneLastPosition);
                        _microphoneLastPosition = (_microphoneLastPosition + samplesToRead) % _microphoneClip.samples;
                    }
                    else
                    {
                        // Wrap-around case: read in two parts
                        int firstPart = _microphoneClip.samples - _microphoneLastPosition;
                        int secondPart = samplesToRead - firstPart;
                        
                        float[] firstChunk = new float[firstPart];
                        float[] secondChunk = new float[secondPart];
                        
                        _microphoneClip.GetData(firstChunk, _microphoneLastPosition);
                        _microphoneClip.GetData(secondChunk, 0);
                        
                        Array.Copy(firstChunk, 0, _microphoneBuffer, 0, firstPart);
                        Array.Copy(secondChunk, 0, _microphoneBuffer, firstPart, secondPart);
                        
                        // Zero out the rest if we read fewer samples than requested
                        if (samplesToRead < sampleCount)
                        {
                            Array.Clear(_microphoneBuffer, samplesToRead, sampleCount - samplesToRead);
                        }
                        
                        _microphoneLastPosition = secondPart;
                    }
                }
                else
                {
                    // No new microphone data, use silence
                    Array.Clear(_microphoneBuffer, 0, _microphoneBuffer.Length);
                }
            }
            else
            {
                // Microphone not active, use silence
                Array.Clear(_microphoneBuffer, 0, _microphoneBuffer.Length);
            }

            // Mix game audio + microphone (apply microphone volume)
            for (int i = 0; i < data.Length; i++)
            {
                _mixedBuffer[i] = _gameAudioBuffer[i] + (_microphoneBuffer[i] * microphoneVolume);
                // Clamp to prevent clipping
                _mixedBuffer[i] = Mathf.Clamp(_mixedBuffer[i], -1f, 1f);
            }

            // Write mixed audio to WAV
            _wavWriter?.WriteInterleavedFloatSamples(_mixedBuffer, _mixedBuffer.Length);
        }

#if !UNITY_ANDROID || UNITY_EDITOR
        private async void TryMergeToMp4FireAndForget()
        {
            try
            {
                await TryMergeToMp4Async();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[QuestFrameSequenceRecorder] MP4 merge skipped (ffmpeg missing or failed): {e.Message}");
            }
        }

        private async Task TryMergeToMp4Async()
        {
            // Wait a moment for last frame writes to finish
            await Task.Delay(500);
            
            // If audio was requested but couldn't start, just skip audio input.
            bool hasAudio = recordAudio && File.Exists(_wavPath) && new FileInfo(_wavPath).Length > 0;

            // Wait a moment for last frame writes to finish.
            await Task.Delay(250);

            // Find ffmpeg executable
            string ffmpegExe = FindFfmpegExecutable();
            if (string.IsNullOrEmpty(ffmpegExe))
            {
                UnityEngine.Debug.LogError("[QuestFrameSequenceRecorder] ❌ ffmpeg not found! MP4 video creation requires ffmpeg.");
                UnityEngine.Debug.LogError("📥 To install ffmpeg:");
                UnityEngine.Debug.LogError("   1. Download from: https://ffmpeg.org/download.html");
                UnityEngine.Debug.LogError("   2. Extract to a folder (e.g., C:\\ffmpeg)");
                UnityEngine.Debug.LogError("   3. Add to PATH, OR set 'Ffmpeg Path Override' in Inspector to: C:\\ffmpeg\\bin\\ffmpeg.exe");
                UnityEngine.Debug.LogError($"   4. Your frames + audio are saved in: {_sessionDir}");
                UnityEngine.Debug.LogError($"   5. You can manually merge with: ffmpeg -y -framerate {fps} -i \"{Path.Combine(_framesDir, "frame_%06d.jpg")}\" {(hasAudio ? $"-i \"{_wavPath}\" " : "")}-c:v libx264 -pix_fmt yuv420p {(hasAudio ? "-c:a aac " : "")}\"{_mp4Path}\"");
                return;
            }

            var inputPattern = Path.Combine(_framesDir, "frame_%06d.jpg");

            var args = hasAudio
                ? $"-y -framerate {fps} -i \"{inputPattern}\" -i \"{_wavPath}\" -c:v libx264 -pix_fmt yuv420p -c:a aac \"{_mp4Path}\""
                : $"-y -framerate {fps} -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{_mp4Path}\"";

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
                UnityEngine.Debug.LogError("[QuestFrameSequenceRecorder] Failed to start ffmpeg process. Check if the path is correct.");
                return;
            }

            var stderrTask = proc.StandardError.ReadToEndAsync();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            await Task.WhenAll(stderrTask, stdoutTask, WaitForExitAsyncCompat(proc));

            var stderr = stderrTask.Result;
            var stdout = stdoutTask.Result;

            if (proc.ExitCode != 0)
            {
                UnityEngine.Debug.LogWarning($"[QuestFrameSequenceRecorder] ffmpeg failed (exit {proc.ExitCode}).\n{stderr}\n{stdout}");
                return;
            }

            UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] ✅ Video file created: {_mp4Path}");

            if (deleteFramesAfterMp4)
            {
                try 
                { 
                    Directory.Delete(_framesDir, true);
                    UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Cleaned up frame files (MP4 video saved).");
                }
                catch (Exception e) 
                { 
                    UnityEngine.Debug.LogWarning($"[QuestFrameSequenceRecorder] Could not delete frames folder: {e.Message}"); 
                }
            }
            
            // Also delete WAV if MP4 was created successfully (audio is now in MP4)
            if (hasAudio && deleteFramesAfterMp4)
            {
                try
                {
                    if (File.Exists(_wavPath))
                        File.Delete(_wavPath);
                }
                catch { /* ignore */ }
            }
        }

        private static Task WaitForExitAsyncCompat(Process proc)
        {
            if (proc.HasExited)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            proc.EnableRaisingEvents = true;

            EventHandler handler = null;
            handler = (_, __) =>
            {
                proc.Exited -= handler;
                tcs.TrySetResult(true);
            };
            proc.Exited += handler;

            if (proc.HasExited)
            {
                proc.Exited -= handler;
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private string FindFfmpegExecutable()
        {
            // If override path is set, use it
            if (!string.IsNullOrWhiteSpace(ffmpegPathOverride))
            {
                if (File.Exists(ffmpegPathOverride))
                    return ffmpegPathOverride;
                
                // Try adding .exe if not present
                string withExe = ffmpegPathOverride + ".exe";
                if (File.Exists(withExe))
                    return withExe;
                
                UnityEngine.Debug.LogWarning($"[QuestFrameSequenceRecorder] ffmpeg path override not found: {ffmpegPathOverride}");
            }

            // Try common Windows locations (including Desktop where user extracted it)
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string[] commonPaths = {
                Path.Combine(desktopPath, "ffmpeg-8.0.1", "bin", "ffmpeg.exe"),
                Path.Combine(desktopPath, "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(desktopPath, "ffmpeg-8.0.1", "ffmpeg.exe"),
                Path.Combine(desktopPath, "ffmpeg", "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe")
            };

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Try finding in Desktop folder recursively (in case structure is different)
            try
            {
                string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktopFolder))
                {
                    var ffmpegFiles = Directory.GetFiles(desktopFolder, "ffmpeg.exe", SearchOption.AllDirectories);
                    if (ffmpegFiles.Length > 0)
                    {
                        UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Auto-detected ffmpeg at: {ffmpegFiles[0]}");
                        return ffmpegFiles[0];
                    }
                }
            }
            catch { /* ignore */ }

            // Try finding in PATH
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "ffmpeg",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    
                    if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        string firstPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(firstPath) && File.Exists(firstPath))
                        {
                            UnityEngine.Debug.Log($"[QuestFrameSequenceRecorder] Auto-detected ffmpeg in PATH: {firstPath.Trim()}");
                            return firstPath.Trim();
                        }
                    }
                }
            }
            catch { /* ignore */ }

            return null;
        }
#endif
    }
}


