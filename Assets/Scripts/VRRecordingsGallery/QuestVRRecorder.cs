using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace VRRecordings
{
    /// <summary>
    /// VR-compatible video recorder that works on both PC and Quest.
    /// On Quest: Saves frames + audio to persistentDataPath for later playback or PC conversion.
    /// On PC: Can optionally use FFmpeg to create MP4 immediately.
    /// </summary>
    public class QuestVRRecorder : MonoBehaviour
    {
        [Header("Recording Settings")]
        [Tooltip("Camera to record from. If null, uses main camera.")]
        public Camera recordingCamera;
        
        [Tooltip("Frame rate for recording")]
        [Range(10, 60)]
        public int frameRate = 24;
        
        [Tooltip("Recording resolution width")]
        public int resolutionWidth = 1280;
        
        [Tooltip("Recording resolution height")]
        public int resolutionHeight = 720;
        
        [Header("Audio Settings")]
        [Tooltip("Record microphone audio")]
        public bool recordMicrophone = true;
        
        [Tooltip("Microphone device name (empty = auto-detect)")]
        public string microphoneDevice = "";
        
        [Range(0.5f, 3f)]
        public float microphoneGain = 1.5f;
        
        [Header("Output Settings")]
        [Tooltip("Folder name for recordings")]
        public string recordingsFolderName = "QuestRecordings";
        
        [Tooltip("JPEG quality (0-100)")]
        [Range(50, 100)]
        public int jpegQuality = 85;
        
        [Header("Quest-Specific")]
        [Tooltip("On Quest, reduce resolution for better performance")]
        public bool autoReduceResolutionOnQuest = true;
        
        [Tooltip("Quest resolution multiplier (0.5 = half resolution)")]
        [Range(0.25f, 1f)]
        public float questResolutionScale = 0.75f;

        [Header("Status (Read Only)")]
        public bool isRecording = false;
        public bool isProcessing = false;
        public int recordedFrameCount = 0;
        public float recordingDuration = 0f;

        // Events
        public event Action OnRecordingStarted;
        public event Action<string> OnRecordingStopped; // path to recording folder
        public event Action<string> OnRecordingError;

        // Internal
        private string currentRecordingPath;
        private RenderTexture renderTexture;
        private Texture2D captureTexture;
        private float captureInterval;
        private float nextCaptureTime;
        private float recordingStartTime;
        
        // Audio
        private AudioClip micClip;
        private List<float> micSamples = new List<float>();
        private int lastMicPosition = 0;
        private int micSampleRate = 44100;
        private bool hasMicrophone = false;
        
        // Frame queue for async saving
        private Queue<FrameData> frameQueue = new Queue<FrameData>();
        private bool isSavingFrames = false;
        
        private struct FrameData
        {
            public byte[] jpegData;
            public int frameNumber;
        }

        private void Awake()
        {
            // Auto-find camera
            if (recordingCamera == null)
            {
                recordingCamera = Camera.main;
            }
            
            // Adjust resolution for Quest
#if UNITY_ANDROID && !UNITY_EDITOR
            if (autoReduceResolutionOnQuest)
            {
                resolutionWidth = Mathf.RoundToInt(resolutionWidth * questResolutionScale);
                resolutionHeight = Mathf.RoundToInt(resolutionHeight * questResolutionScale);
                Debug.Log($"[QuestVRRecorder] Quest mode: Resolution adjusted to {resolutionWidth}x{resolutionHeight}");
            }
#endif
            
            captureInterval = 1f / frameRate;
            
            // Detect microphone
            DetectMicrophone();
        }

        private void DetectMicrophone()
        {
            if (!recordMicrophone) return;
            
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[QuestVRRecorder] No microphone detected");
                hasMicrophone = false;
                return;
            }
            
            Debug.Log("[QuestVRRecorder] Available microphones:");
            foreach (string device in devices)
            {
                Debug.Log($"  - {device}");
            }
            
            // Auto-select microphone
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                // On Quest, prefer the built-in mic
#if UNITY_ANDROID && !UNITY_EDITOR
                microphoneDevice = devices[0]; // Quest usually has one mic
#else
                // On PC, try to find a real microphone (not virtual)
                foreach (string device in devices)
                {
                    string lower = device.ToLower();
                    if (!lower.Contains("virtual") && !lower.Contains("cable"))
                    {
                        microphoneDevice = device;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(microphoneDevice))
                    microphoneDevice = devices[0];
#endif
            }
            
            hasMicrophone = true;
            Debug.Log($"[QuestVRRecorder] Selected microphone: {microphoneDevice}");
        }

        private void Update()
        {
            if (isRecording)
            {
                recordingDuration = Time.time - recordingStartTime;
                
                // Capture frames at specified interval
                if (Time.time >= nextCaptureTime)
                {
                    CaptureFrame();
                    nextCaptureTime = Time.time + captureInterval;
                }
                
                // Collect microphone samples
                if (hasMicrophone && micClip != null)
                {
                    CollectMicSamples();
                }
            }
        }

        /// <summary>
        /// Starts recording
        /// </summary>
        public void StartRecording()
        {
            if (isRecording || isProcessing)
            {
                Debug.LogWarning("[QuestVRRecorder] Already recording or processing");
                return;
            }
            
            if (recordingCamera == null)
            {
                Debug.LogError("[QuestVRRecorder] No recording camera assigned!");
                OnRecordingError?.Invoke("No recording camera assigned");
                return;
            }
            
            Debug.Log("[QuestVRRecorder] Starting recording...");
            
            // Create recording folder
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string basePath = GetRecordingsBasePath();
            currentRecordingPath = Path.Combine(basePath, $"Recording_{timestamp}");
            
            try
            {
                // Ensure base directory exists
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                    Debug.Log($"[QuestVRRecorder] Created base directory: {basePath}");
                }
                
                Directory.CreateDirectory(currentRecordingPath);
                Debug.Log($"[QuestVRRecorder] ✅ Recording folder created: {currentRecordingPath}");
                Debug.Log($"[QuestVRRecorder] Base path: {basePath}");
                Debug.Log($"[QuestVRRecorder] Full path exists: {Directory.Exists(currentRecordingPath)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestVRRecorder] Failed to create recording folder: {e.Message}");
                Debug.LogError($"[QuestVRRecorder] Base path was: {basePath}");
                OnRecordingError?.Invoke($"Failed to create folder: {e.Message}");
                return;
            }
            
            // Setup render texture
            renderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            
            captureTexture = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
            
            // Start microphone
            if (hasMicrophone && recordMicrophone)
            {
                StartMicrophone();
            }
            
            // Reset state
            recordedFrameCount = 0;
            frameQueue.Clear();
            micSamples.Clear();
            recordingStartTime = Time.time;
            nextCaptureTime = Time.time;
            
            isRecording = true;
            
            // Start frame saving coroutine
            StartCoroutine(SaveFramesCoroutine());
            
            OnRecordingStarted?.Invoke();
            Debug.Log("[QuestVRRecorder] Recording started!");
        }

        /// <summary>
        /// Stops recording and saves files
        /// </summary>
        public void StopRecording()
        {
            if (!isRecording)
            {
                Debug.LogWarning("[QuestVRRecorder] Not recording");
                return;
            }
            
            Debug.Log("[QuestVRRecorder] Stopping recording...");
            isRecording = false;
            isProcessing = true;
            
            // Stop microphone
            if (hasMicrophone && micClip != null)
            {
                Microphone.End(microphoneDevice);
                CollectMicSamples(); // Get remaining samples
            }
            
            // Start finalization
            StartCoroutine(FinalizeRecordingCoroutine());
        }

        private void CaptureFrame()
        {
            if (recordingCamera == null) return;
            
            // Store original target
            RenderTexture originalTarget = recordingCamera.targetTexture;
            
            // Render to our texture
            recordingCamera.targetTexture = renderTexture;
            recordingCamera.Render();
            recordingCamera.targetTexture = originalTarget;
            
            // Read pixels
            RenderTexture.active = renderTexture;
            captureTexture.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
            captureTexture.Apply();
            RenderTexture.active = null;
            
            // Encode to JPEG
            byte[] jpegData = captureTexture.EncodeToJPG(jpegQuality);
            
            // Queue for async saving
            frameQueue.Enqueue(new FrameData 
            { 
                jpegData = jpegData, 
                frameNumber = recordedFrameCount 
            });
            
            recordedFrameCount++;
        }

        private IEnumerator SaveFramesCoroutine()
        {
            isSavingFrames = true;
            
            while (isRecording || frameQueue.Count > 0)
            {
                if (frameQueue.Count > 0)
                {
                    FrameData frame = frameQueue.Dequeue();
                    string framePath = Path.Combine(currentRecordingPath, $"frame_{frame.frameNumber:D6}.jpg");
                    
                    try
                    {
                        File.WriteAllBytes(framePath, frame.jpegData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[QuestVRRecorder] Error saving frame: {e.Message}");
                    }
                }
                else
                {
                    yield return null;
                }
                
                // Yield periodically to avoid blocking
                if (frameQueue.Count > 0 && frameQueue.Count % 5 == 0)
                    yield return null;
            }
            
            isSavingFrames = false;
        }

        private void StartMicrophone()
        {
            try
            {
                // Get microphone capabilities
                int minFreq, maxFreq;
                Microphone.GetDeviceCaps(microphoneDevice, out minFreq, out maxFreq);
                
                // Choose sample rate
                micSampleRate = (maxFreq > 0) ? Mathf.Min(maxFreq, 44100) : 44100;
                
                // Start recording (10 second loop buffer)
                micClip = Microphone.Start(microphoneDevice, true, 10, micSampleRate);
                lastMicPosition = 0;
                
                Debug.Log($"[QuestVRRecorder] Microphone started: {microphoneDevice} @ {micSampleRate}Hz");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestVRRecorder] Failed to start microphone: {e.Message}");
                hasMicrophone = false;
            }
        }

        private void CollectMicSamples()
        {
            if (micClip == null) return;
            
            int currentPosition = Microphone.GetPosition(microphoneDevice);
            if (currentPosition < 0) return;
            
            int samplesToRead;
            if (currentPosition >= lastMicPosition)
            {
                samplesToRead = currentPosition - lastMicPosition;
            }
            else
            {
                // Wrapped around
                samplesToRead = (micClip.samples - lastMicPosition) + currentPosition;
            }
            
            if (samplesToRead > 0)
            {
                float[] buffer = new float[samplesToRead];
                micClip.GetData(buffer, lastMicPosition);
                
                // Apply gain
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = Mathf.Clamp(buffer[i] * microphoneGain, -1f, 1f);
                }
                
                micSamples.AddRange(buffer);
                lastMicPosition = currentPosition;
            }
        }

        private IEnumerator FinalizeRecordingCoroutine()
        {
            Debug.Log("[QuestVRRecorder] Finalizing recording...");
            
            // Wait for all frames to be saved
            while (isSavingFrames || frameQueue.Count > 0)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            // Save audio
            if (micSamples.Count > 0)
            {
                string audioPath = Path.Combine(currentRecordingPath, "audio.wav");
                SaveWavFile(audioPath, micSamples.ToArray(), micSampleRate);
                Debug.Log($"[QuestVRRecorder] Audio saved: {micSamples.Count} samples");
            }
            
            // Save metadata
            SaveMetadata();
            
            // Create marker file to indicate recording is complete
            string markerPath = Path.Combine(currentRecordingPath, "encoding_complete.marker");
            try
            {
                File.WriteAllText(markerPath, DateTime.Now.ToString("o"));
                Debug.Log($"[QuestVRRecorder] ✅ Created completion marker: {markerPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestVRRecorder] Failed to create marker: {e.Message}");
            }
            
            // Verify recording folder exists and has files
            if (Directory.Exists(currentRecordingPath))
            {
                string[] allFiles = Directory.GetFiles(currentRecordingPath);
                string[] frameFiles = Directory.GetFiles(currentRecordingPath, "frame_*.jpg");
                Debug.Log($"[QuestVRRecorder] Recording folder verification:");
                Debug.Log($"[QuestVRRecorder]   - Total files: {allFiles.Length}");
                Debug.Log($"[QuestVRRecorder]   - Frame files: {frameFiles.Length}");
                Debug.Log($"[QuestVRRecorder]   - Folder path: {currentRecordingPath}");
            }
            else
            {
                Debug.LogError($"[QuestVRRecorder] ❌ Recording folder does not exist: {currentRecordingPath}");
            }
            
            // Cleanup
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }
            
            if (captureTexture != null)
            {
                Destroy(captureTexture);
                captureTexture = null;
            }
            
            isProcessing = false;
            
            Debug.Log($"[QuestVRRecorder] Recording complete! {recordedFrameCount} frames saved to: {currentRecordingPath}");
            
            // Try to create MP4 on PC
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            yield return StartCoroutine(TryCreateMP4Coroutine());
#endif
            
            OnRecordingStopped?.Invoke(currentRecordingPath);
        }

        private void SaveMetadata()
        {
            string metadataPath = Path.Combine(currentRecordingPath, "metadata.txt");
            
            string metadata = $"Recording Metadata\n" +
                             $"==================\n" +
                             $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"Duration: {recordingDuration:F1} seconds\n" +
                             $"Frame Count: {recordedFrameCount}\n" +
                             $"Frame Rate: {frameRate}\n" +
                             $"Resolution: {resolutionWidth}x{resolutionHeight}\n" +
                             $"Audio Samples: {micSamples.Count}\n" +
                             $"Audio Sample Rate: {micSampleRate}\n" +
                             $"Platform: {Application.platform}\n";
            
            File.WriteAllText(metadataPath, metadata);
        }

        private void SaveWavFile(string path, float[] samples, int sampleRate)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    int channels = 1;
                    int bitsPerSample = 16;
                    int byteRate = sampleRate * channels * bitsPerSample / 8;
                    int blockAlign = channels * bitsPerSample / 8;
                    int dataSize = samples.Length * blockAlign;
                    
                    // RIFF header
                    writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                    writer.Write(36 + dataSize);
                    writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                    
                    // fmt chunk
                    writer.Write(new char[] { 'f', 'm', 't', ' ' });
                    writer.Write(16); // chunk size
                    writer.Write((short)1); // PCM
                    writer.Write((short)channels);
                    writer.Write(sampleRate);
                    writer.Write(byteRate);
                    writer.Write((short)blockAlign);
                    writer.Write((short)bitsPerSample);
                    
                    // data chunk
                    writer.Write(new char[] { 'd', 'a', 't', 'a' });
                    writer.Write(dataSize);
                    
                    // Write samples as 16-bit
                    foreach (float sample in samples)
                    {
                        short s = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
                        writer.Write(s);
                    }
                }
                
                Debug.Log($"[QuestVRRecorder] WAV saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestVRRecorder] Error saving WAV: {e.Message}");
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        private IEnumerator TryCreateMP4Coroutine()
        {
            // Look for FFmpeg
            string ffmpegPath = Path.Combine(Application.dataPath, "ffmpeg-8.0.1-essentials_build", "bin", "ffmpeg.exe");
            
            if (!File.Exists(ffmpegPath))
            {
                Debug.Log("[QuestVRRecorder] FFmpeg not found - MP4 not created. Use the fix script to convert frames to MP4.");
                yield break;
            }
            
            Debug.Log("[QuestVRRecorder] Creating MP4 with FFmpeg...");
            
            string audioPath = Path.Combine(currentRecordingPath, "audio.wav");
            string outputPath = Path.Combine(currentRecordingPath, "meeting.mp4");
            
            string arguments;
            if (File.Exists(audioPath))
            {
                arguments = $"-y -framerate {frameRate} -i \"{currentRecordingPath}/frame_%06d.jpg\" " +
                           $"-i \"{audioPath}\" " +
                           $"-c:v libx264 -profile:v baseline -level 3.1 -preset fast -crf 23 -pix_fmt yuv420p " +
                           $"-c:a aac -b:a 128k -movflags +faststart -shortest \"{outputPath}\"";
            }
            else
            {
                arguments = $"-y -framerate {frameRate} -i \"{currentRecordingPath}/frame_%06d.jpg\" " +
                           $"-c:v libx264 -profile:v baseline -level 3.1 -preset fast -crf 23 -pix_fmt yuv420p " +
                           $"-movflags +faststart \"{outputPath}\"";
            }
            
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            System.Diagnostics.Process process = null;
            try
            {
                process = System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestVRRecorder] FFmpeg error: {e.Message}");
                yield break;
            }
            
            while (process != null && !process.HasExited)
            {
                yield return new WaitForSeconds(0.5f);
            }
            
            if (process != null && process.ExitCode == 0 && File.Exists(outputPath))
            {
                Debug.Log($"[QuestVRRecorder] ✅ MP4 created: {outputPath}");
            }
            else
            {
                string error = process?.StandardError.ReadToEnd() ?? "Unknown error";
                Debug.LogWarning($"[QuestVRRecorder] FFmpeg failed: {error}");
            }
            
            process?.Dispose();
        }
#endif

        /// <summary>
        /// Gets the base path for recordings based on platform
        /// </summary>
        public string GetRecordingsBasePath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Quest: Use persistent data path
            return Path.Combine(Application.persistentDataPath, recordingsFolderName);
#else
            // PC/Editor: Use Desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return Path.Combine(desktopPath, "MeetingRecordings");
#endif
        }

        /// <summary>
        /// Gets the path of the last recording
        /// </summary>
        public string GetLastRecordingPath()
        {
            return currentRecordingPath;
        }

        private void OnDestroy()
        {
            if (isRecording)
            {
                StopRecording();
            }
            
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            
            if (captureTexture != null)
            {
                Destroy(captureTexture);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test Start Recording")]
        private void TestStartRecording()
        {
            StartRecording();
        }

        [ContextMenu("Test Stop Recording")]
        private void TestStopRecording()
        {
            StopRecording();
        }
#endif
    }
}

