using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System;
using Debug = UnityEngine.Debug;

public class MeetingVideoRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    public Camera recordingCamera;
    public int frameRate = 30;
    public int resolutionWidth = 1280;
    public int resolutionHeight = 720;

    [Header("Audio Settings")]
    [Tooltip("Capture system audio (ALL sounds including other participants' Vivox voices)")]
    public bool recordSystemAudio = true;      // ✅ captures what plays through speakers (other participants)
    
    [Tooltip("Capture YOUR voice via microphone - REQUIRED for host voice")]
    public bool recordMicrophone = true;       // ✅ captures YOUR voice (host)

    [Tooltip("Also capture Unity game audio separately (backup if system audio fails)")]
    public bool recordGameAudio = true;        // Backup: Unity-only audio
    public GameAudioCapture gameAudioCapture;  // auto-found if null
    public int audioSampleRate = 44100;
    public string microphoneDevice = "";
    [Range(0.1f, 5f)]
    public float microphoneGain = 1f;

    [Header("Output Settings")]
    public bool autoCreateMP4 = true;
    public bool deleteFramesAfterMP4 = true;

    [Header("UI Settings")]
    public bool showUI = true;

    [Header("Status")]
    public bool isRecording = false;
    public bool isProcessing = false;
    private bool isStartingRecording = false;

    private string videoFolderPath;
    private string currentSessionPath;
    private string ffmpegPath;
    private RenderTexture renderTexture;
    private Texture2D screenShot;
    private int frameCount = 0;
    private float captureInterval;
    private float nextCaptureTime;
    private float recordingStartTime;
    private string processingStatus = "";

    // mic
    private AudioClip micClip;
    private bool hasMicrophone = false;
    private int lastMicPosition = 0;
    private readonly System.Collections.Generic.List<float> micSamples = new System.Collections.Generic.List<float>(1024 * 10);
    private float[] micReadBuffer;
    private int micSampleRate = 0;
    private int micChannels = 1;

    // System audio capture (for Vivox + all system sounds)
    private SystemAudioCapture systemAudioCapture;

    // UI textures
    private GUIStyle buttonStyle;
    private GUIStyle statusStyle;
    private GUIStyle pathStyle;
    private Texture2D redTex;
    private Texture2D greenTex;
    private Texture2D darkTex;
    private Texture2D blueTex;

    void Start()
    {
        // ffmpeg path (adjust if needed)
        ffmpegPath = Path.Combine(Application.dataPath, "ffmpeg-8.0.1-essentials_build", "bin", "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError("FFmpeg not found at: " + ffmpegPath);
            autoCreateMP4 = false;
        }
        else
        {
            Debug.Log("FFmpeg found: " + ffmpegPath);
        }

        // Save videos to Desktop/MeetingRecordings
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        videoFolderPath = Path.Combine(desktopPath, "MeetingRecordings");
        if (!Directory.Exists(videoFolderPath))
            Directory.CreateDirectory(videoFolderPath);

        Debug.Log("Videos will be saved to: " + videoFolderPath);

        captureInterval = 1f / frameRate;
        FindRecordingCamera();

        // Setup system audio capture for Vivox + all system sounds
        if (recordSystemAudio)
            EnsureSystemAudioCapture();

        // find or attach Unity game audio capture (optional backup)
        if (recordGameAudio)
            EnsureGameAudioCapture();

        // microphone detection - find the best microphone
        if (Microphone.devices.Length > 0)
        {
            hasMicrophone = true;

            Debug.Log("🎤 Available microphones:");
            foreach (string device in Microphone.devices)
            {
                Debug.Log($"   - {device}");
            }

            // If no device specified, try to find the best one
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                // Priority: Microphone Array > any non-virtual mic > first available
                foreach (string device in Microphone.devices)
                {
                    string lower = device.ToLower();
                    // Prefer Microphone Array (built-in laptop mic)
                    if (lower.Contains("microphone array"))
                    {
                        microphoneDevice = device;
                        break;
                    }
                    // Skip virtual devices (Oculus, etc.) - they might conflict with VR
                    if (lower.Contains("virtual") || lower.Contains("oculus") || lower.Contains("cable"))
                    {
                        continue;
                    }
                    // Use first real microphone found
                    if (string.IsNullOrEmpty(microphoneDevice))
                    {
                        microphoneDevice = device;
                    }
                }
                
                // Fallback to first device if nothing else found
                if (string.IsNullOrEmpty(microphoneDevice))
                {
                    microphoneDevice = Microphone.devices[0];
                }
            }
            
            Debug.Log($"🎤 Selected microphone: {microphoneDevice}");
        }
        else
        {
            hasMicrophone = false;
            Debug.LogWarning("No microphone device detected.");
        }

        redTex = MakeTexture(new Color(0.8f, 0.2f, 0.2f, 0.9f));
        greenTex = MakeTexture(new Color(0.2f, 0.7f, 0.3f, 0.9f));
        darkTex = MakeTexture(new Color(0.1f, 0.1f, 0.1f, 0.85f));
        blueTex = MakeTexture(new Color(0.2f, 0.4f, 0.8f, 0.9f));
    }

    void FindRecordingCamera()
    {
        if (recordingCamera != null) return;

        recordingCamera = Camera.main;
        if (recordingCamera == null)
        {
            foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    recordingCamera = cam;
                    break;
                }
            }
        }

        if (recordingCamera != null)
            Debug.Log("Recording camera: " + recordingCamera.name);
    }

    void EnsureGameAudioCapture()
    {
        if (gameAudioCapture != null) return;

        gameAudioCapture = FindFirstObjectByType<GameAudioCapture>();
        if (gameAudioCapture != null) return;

        AudioListener listener = FindFirstObjectByType<AudioListener>();

        if (listener == null && recordingCamera != null)
            listener = recordingCamera.GetComponent<AudioListener>();

        if (listener == null && recordingCamera != null)
            listener = recordingCamera.gameObject.AddComponent<AudioListener>();

        if (listener != null)
            gameAudioCapture = listener.GetComponent<GameAudioCapture>() ??
                               listener.gameObject.AddComponent<GameAudioCapture>();

        if (gameAudioCapture == null)
            Debug.LogWarning("No AudioListener found. Game audio capture will be silent.");
    }

    void EnsureSystemAudioCapture()
    {
        if (systemAudioCapture != null) return;

        systemAudioCapture = FindFirstObjectByType<SystemAudioCapture>();
        if (systemAudioCapture != null)
        {
            Debug.Log("🔊 SystemAudioCapture found - ALL system audio will be recorded (including Vivox)");
            return;
        }

        // Create SystemAudioCapture if not found
        systemAudioCapture = gameObject.AddComponent<SystemAudioCapture>();
        Debug.Log("🔊 Created SystemAudioCapture - ALL audio including Vivox voices will be captured");
    }

    Texture2D MakeTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    void Update()
    {
        if (isRecording && Time.time >= nextCaptureTime)
        {
            CaptureFrame();
            nextCaptureTime = Time.time + captureInterval;
        }

        // Capture microphone samples continuously
        if (isRecording && recordMicrophone && micClip != null)
        {
            bool isRecordingMic = false;
            try
            {
                isRecordingMic = Microphone.IsRecording(microphoneDevice) || Microphone.IsRecording(null);
            }
            catch { }

            if (isRecordingMic)
            {
                CaptureMicSamples();
            }
        }
    }

    void OnGUI()
    {
        if (!showUI) return;

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            statusStyle.normal.textColor = Color.white;

            pathStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            pathStyle.normal.textColor = Color.yellow;
        }

        float buttonWidth = 220;
        float buttonHeight = 60;
        float x = 170;
        float y = 80;

        GUI.DrawTexture(new Rect(x - 10, y - 10, buttonWidth + 20, 220), darkTex);

        if (isProcessing)
        {
            if (processingStatus.Contains("SUCCESS"))
            {
                buttonStyle.normal.background = greenTex;
                buttonStyle.hover.background = greenTex;
                GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "✅ DONE", buttonStyle);
            }
            else if (processingStatus.Contains("ERROR") || processingStatus.Contains("Failed"))
            {
                buttonStyle.normal.background = redTex;
                buttonStyle.hover.background = redTex;
                GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "❌ ERROR", buttonStyle);
            }
            else
            {
                buttonStyle.normal.background = blueTex;
                buttonStyle.hover.background = blueTex;
                GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "⏳ PROCESSING...", buttonStyle);
            }

            GUI.Label(new Rect(x, y + buttonHeight + 10, buttonWidth, 60), processingStatus, statusStyle);
        }
        else if (isRecording)
        {
            buttonStyle.normal.background = redTex;
            buttonStyle.hover.background = redTex;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "⏹ STOP", buttonStyle))
                StopRecording();

            float elapsed = Time.time - recordingStartTime;
            int minutes = Mathf.FloorToInt(elapsed / 60);
            int seconds = Mathf.FloorToInt(elapsed % 60);
            string dot = (Mathf.FloorToInt(Time.time * 2) % 2 == 0) ? "🔴" : "⚫";

            GUI.Label(new Rect(x, y + buttonHeight + 10, buttonWidth, 25),
                $"{dot} REC {minutes:00}:{seconds:00}", statusStyle);
            GUI.Label(new Rect(x, y + buttonHeight + 35, buttonWidth, 25),
                frameCount + " frames", statusStyle);

            if (recordSystemAudio && systemAudioCapture != null && systemAudioCapture.IsCapturing)
                GUI.Label(new Rect(x, y + buttonHeight + 60, buttonWidth, 25), "🔊 Participants: ON", statusStyle);

            if (recordMicrophone && hasMicrophone)
                GUI.Label(new Rect(x, y + buttonHeight + 85, buttonWidth, 25), "🎤 Your voice: ON", statusStyle);

            if (recordGameAudio && gameAudioCapture != null)
                GUI.Label(new Rect(x, y + buttonHeight + 110, buttonWidth, 25), "🎮 Game: ON", statusStyle);
        }
        else
        {
            buttonStyle.normal.background = greenTex;
            buttonStyle.hover.background = greenTex;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "⏺ RECORD", buttonStyle))
                StartRecording();

            GUI.Label(new Rect(x, y + buttonHeight + 10, buttonWidth, 25), "Ready to record", statusStyle);

            if (recordSystemAudio)
                GUI.Label(new Rect(x, y + buttonHeight + 35, buttonWidth, 25), "🔊 Participants: ON", statusStyle);

            if (recordMicrophone)
                GUI.Label(new Rect(x, y + buttonHeight + 60, buttonWidth, 25),
                    hasMicrophone ? "🎤 Your voice: ON" : "🎤 Mic NOT found!", statusStyle);

            if (recordGameAudio)
                GUI.Label(new Rect(x, y + buttonHeight + 85, buttonWidth, 25), "🎮 Game audio: ON", statusStyle);

            if (autoCreateMP4)
                GUI.Label(new Rect(x, y + buttonHeight + 110, buttonWidth, 25), "🎬 Auto MP4: ON", statusStyle);

            string camName = recordingCamera != null ? recordingCamera.name : "None!";
            GUI.Label(new Rect(x, y + buttonHeight + 135, buttonWidth, 50), "Cam: " + camName, pathStyle);
        }
    }

    public void StartRecording()
    {
        if (isRecording || isProcessing || isStartingRecording) return;
        StartCoroutine(StartRecordingRoutine());
    }

    IEnumerator StartRecordingRoutine()
    {
        isStartingRecording = true;

        FindRecordingCamera();
        if (recordingCamera == null)
        {
            Debug.LogError("No recording camera found!");
            isStartingRecording = false;
            yield break;
        }

        string sessionName = "Recording_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        currentSessionPath = Path.Combine(videoFolderPath, sessionName);
        Directory.CreateDirectory(currentSessionPath);

        renderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        screenShot = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);

        // Start microphone capture for YOUR voice (host)
        if (recordMicrophone && hasMicrophone)
        {
            // Get device capabilities
            Microphone.GetDeviceCaps(microphoneDevice, out int minRate, out int maxRate);
            
            // Choose sample rate - prefer 44100, but respect device limits
            int sampleRateToUse = audioSampleRate;
            if (maxRate > 0)
            {
                sampleRateToUse = Mathf.Clamp(sampleRateToUse, minRate, maxRate);
            }
            
            Debug.Log($"🎤 Starting microphone: {microphoneDevice} @ {sampleRateToUse}Hz");

            // Stop any existing recording on this device first
            if (Microphone.IsRecording(microphoneDevice))
            {
                Microphone.End(microphoneDevice);
                yield return new WaitForSeconds(0.1f);
            }

            // Start recording with a shorter buffer (60 seconds, loop)
            micClip = Microphone.Start(microphoneDevice, true, 60, sampleRateToUse);
            
            if (micClip == null)
            {
                Debug.LogError($"🎤 Failed to start microphone: {microphoneDevice}");
                // Try with null (default device)
                Debug.Log("🎤 Trying default microphone...");
                micClip = Microphone.Start(null, true, 60, sampleRateToUse);
            }

            if (micClip != null)
            {
                micSampleRate = micClip.frequency;
                micChannels = micClip.channels;
                lastMicPosition = 0;
                micSamples.Clear();

                // Wait for microphone to start producing samples (up to 3 seconds)
                float startTime = Time.realtimeSinceStartup;
                while (Microphone.GetPosition(microphoneDevice) <= 0)
                {
                    if (Time.realtimeSinceStartup - startTime > 3f)
                    {
                        Debug.LogWarning("🎤 Microphone slow to start, continuing anyway...");
                        break;
                    }
                    yield return null;
                }
                
                Debug.Log($"🎤 Microphone recording started! (Channels: {micChannels}, Rate: {micSampleRate})");
            }
            else
            {
                Debug.LogError("🎤 Could not start any microphone!");
                hasMicrophone = false;
            }
        }

        // Start system audio capture (captures ALL audio including Vivox)
        if (recordSystemAudio)
        {
            EnsureSystemAudioCapture();

            if (systemAudioCapture != null)
            {
                string systemAudioPath = Path.Combine(currentSessionPath, "system.wav");
                if (systemAudioCapture.StartCapture(systemAudioPath))
                {
                    Debug.Log("🔊 System audio capture started (includes Vivox voices)");
                }
                else
                {
                    Debug.LogWarning("Failed to start system audio capture. See setup instructions.");
                }
            }
        }

        // Start capturing Unity game audio (optional backup)
        if (recordGameAudio)
        {
            EnsureGameAudioCapture();

            if (gameAudioCapture != null)
            {
                gameAudioCapture.StartCapture();
                Debug.Log("🔊 Unity game audio capture started");
            }
            else
            {
                Debug.LogWarning("GameAudioCapture not found. Game audio will NOT be recorded.");
            }
        }

        frameCount = 0;
        nextCaptureTime = Time.time;
        recordingStartTime = Time.time;
        isRecording = true;
        isStartingRecording = false;

        Debug.Log("🔴 Recording started: " + currentSessionPath);
    }


    public void StopRecording()
    {
        if (!isRecording && !isStartingRecording) return;
        isRecording = false;
        isStartingRecording = false;

        string systemAudioPath = null;
        string audioPath = null;
        string micPath = null;

        // Stop system audio capture first (captures Vivox + all system sounds)
        if (recordSystemAudio && systemAudioCapture != null && systemAudioCapture.IsCapturing)
        {
            systemAudioPath = systemAudioCapture.StopCapture();
            if (!string.IsNullOrEmpty(systemAudioPath) && File.Exists(systemAudioPath))
            {
                Debug.Log("🔊 System audio saved: " + systemAudioPath);
            }
            else
            {
                Debug.LogWarning("System audio capture produced no output.");
                systemAudioPath = null;
            }
        }

        // Stop and save Unity game audio (backup)
        if (recordGameAudio && gameAudioCapture != null)
        {
            float[] gameSamples = gameAudioCapture.StopCapture();
            if (gameSamples != null && gameSamples.Length > 0)
            {
                audioPath = Path.Combine(currentSessionPath, "game.wav");
                SaveWavFromSamples(gameSamples, gameAudioCapture.SampleRate, gameAudioCapture.Channels, audioPath, 1f);
                Debug.Log("🔊 Game audio saved: " + audioPath);
            }
            else
            {
                Debug.LogWarning("Game audio samples were empty.");
            }
        }

        // Stop microphone and save YOUR voice
        if (recordMicrophone && micClip != null)
        {
            // Capture any remaining samples
            CaptureMicSamples();
            
            // Get final position
            int position = 0;
            try
            {
                position = Microphone.GetPosition(microphoneDevice);
            }
            catch { }
            
            // Stop microphone
            try
            {
                Microphone.End(microphoneDevice);
            }
            catch { }

            if (position <= 0 && lastMicPosition > 0)
                position = lastMicPosition;

            Debug.Log($"🎤 Microphone captured {micSamples.Count} samples");

            if (micSamples.Count > 0)
            {
                micPath = Path.Combine(currentSessionPath, "mic.wav");
                SaveWavFromSamples(micSamples.ToArray(), micSampleRate > 0 ? micSampleRate : audioSampleRate, micChannels, micPath, microphoneGain);
                Debug.Log("🎤 YOUR voice saved: " + micPath);
            }
            else
            {
                Debug.LogWarning("🎤 Mic recording had no samples. Your voice won't be in the recording.");
            }

            micClip = null;
        }

        // Cleanup textures
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        if (screenShot != null)
        {
            Destroy(screenShot);
            screenShot = null;
        }

        Debug.Log("⬛ Stopped recording: " + frameCount + " frames");

        if (autoCreateMP4 && File.Exists(ffmpegPath) && frameCount > 0)
            StartCoroutine(CreateMP4Coroutine(systemAudioPath, audioPath, micPath));
    }

    IEnumerator CreateMP4Coroutine(string systemAudioPath, string audioPath, string micPath)
    {
        isProcessing = true;
        processingStatus = "Creating MP4...";

        yield return new WaitForSeconds(0.5f);

        string outputPath = Path.Combine(currentSessionPath, "meeting.mp4");
        string batPath = Path.Combine(currentSessionPath, "convert.bat");

        // Determine which audio files are available
        bool hasSystemAudio = !string.IsNullOrEmpty(systemAudioPath) && File.Exists(systemAudioPath);
        bool hasGameAudio = !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath);
        bool hasMicAudio = !string.IsNullOrEmpty(micPath) && File.Exists(micPath);

        string batContent;

        // Best case: System audio (other participants) + Mic (your voice)
        if (hasSystemAudio && hasMicAudio)
        {
            // Mix system audio (other participants + game sounds) with microphone (your voice)
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(systemAudioPath)}"" -i ""{Path.GetFileName(micPath)}"" -filter_complex ""[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[a]"" -map 0:v -map ""[a]"" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
            Debug.Log("✅ Using system audio (participants) + microphone (your voice)");
        }
        else if (hasSystemAudio)
        {
            // System audio only (other participants, but no host voice)
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(systemAudioPath)}"" -map 0:v -map 1:a -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
            Debug.Log("Using system audio only (participants voices, but YOUR voice may be missing!)");
        }
        else if (hasGameAudio && hasMicAudio)
        {
            // Fallback: Mix game audio with microphone
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(audioPath)}"" -i ""{Path.GetFileName(micPath)}"" -filter_complex ""[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[a]"" -map 0:v -map ""[a]"" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
            Debug.Log("Using game audio + microphone (Vivox participants may not be captured)");
        }
        else if (hasGameAudio)
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(audioPath)}"" -map 0:v -map 1:a -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
            Debug.Log("Using game audio only (voices may be missing)");
        }
        else if (hasMicAudio)
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(micPath)}"" -map 0:v -map 1:a -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
            Debug.Log("Using microphone audio only (your voice only)");
        }
        else
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p ""meeting.mp4""
exit /b %errorlevel%
";
            Debug.LogWarning("No audio available - video will be silent");
        }

        File.WriteAllText(batPath, batContent);
        Debug.Log("Created batch file: " + batPath);

        bool success = false;
        Process process = null;

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + batPath + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = currentSessionPath
            };

            process = new Process { StartInfo = startInfo };
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error starting FFmpeg: " + ex.Message);
            isProcessing = false;
            yield break;
        }

        while (process != null && !process.HasExited)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (process != null)
        {
            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(stdOut)) Debug.Log("FFmpeg stdout: " + stdOut);
            if (!string.IsNullOrEmpty(stdErr)) Debug.Log("FFmpeg stderr: " + stdErr);

            success = (process.ExitCode == 0) && File.Exists(outputPath);
            if (!success)
                Debug.LogError("FFmpeg failed. ExitCode=" + process.ExitCode);

            process.Dispose();
        }

        try { File.Delete(batPath); } catch { }

        if (success)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string finalVideoPath = Path.Combine(desktopPath, Path.GetFileName(outputPath));

            if (File.Exists(finalVideoPath))
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                string extension = Path.GetExtension(outputPath);
                string timestamp = DateTime.Now.ToString("_HHmmss");
                finalVideoPath = Path.Combine(desktopPath, fileNameWithoutExt + timestamp + extension);
            }

            try
            {
                File.Copy(outputPath, finalVideoPath, true);
                processingStatus = "✅ RECORDING SUCCESS!";
                Debug.Log("✅ MP4 created: " + finalVideoPath);
            }
            catch (Exception ex)
            {
                processingStatus = "✅ RECORDING SUCCESS (saved in session folder)";
                Debug.LogWarning("Could not copy MP4 to Desktop. Saved at: " + outputPath);
                Debug.LogWarning("Copy error: " + ex.Message);
            }

            if (deleteFramesAfterMP4)
            {
                yield return new WaitForSeconds(0.2f);
                DeleteTemporaryFiles();
            }
        }
        else
        {
            processingStatus = "❌ RECORDING ERROR!";
            Debug.LogError("❌ Failed to create MP4");
        }

        yield return new WaitForSeconds(4f);
        isProcessing = false;
        processingStatus = "";
    }

    void DeleteTemporaryFiles()
    {
        try
        {
            foreach (string file in Directory.GetFiles(currentSessionPath, "frame_*.jpg"))
                File.Delete(file);

            foreach (string file in Directory.GetFiles(currentSessionPath, "*.meta"))
                File.Delete(file);

            // keep audio.wav for debug, you can delete if you want:
            // foreach (string file in Directory.GetFiles(currentSessionPath, "*.wav"))
            //     File.Delete(file);

            Debug.Log("🗑 Temp frames deleted");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Delete error: " + ex.Message);
        }
    }

    void CaptureFrame()
    {
        if (recordingCamera == null || renderTexture == null || screenShot == null) return;

        RenderTexture prevRT = recordingCamera.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        recordingCamera.targetTexture = renderTexture;
        recordingCamera.Render();

        RenderTexture.active = renderTexture;
        screenShot.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
        screenShot.Apply();

        recordingCamera.targetTexture = prevRT;
        RenderTexture.active = prevActive;

        byte[] bytes = screenShot.EncodeToJPG(90);
        File.WriteAllBytes(Path.Combine(currentSessionPath, $"frame_{frameCount:D6}.jpg"), bytes);
        frameCount++;
    }

    // -------- WAV helpers --------

    void CaptureMicSamples()
    {
        if (micClip == null) return;

        // Try to get position, handle both specified device and null (default)
        int position = -1;
        try
        {
            position = Microphone.GetPosition(microphoneDevice);
            if (position < 0)
                position = Microphone.GetPosition(null);
        }
        catch
        {
            try { position = Microphone.GetPosition(null); } catch { }
        }

        if (position < 0) return;

        int clipSamples = micClip.samples;
        if (clipSamples <= 0) return;

        if (position == 0 && lastMicPosition == 0)
            return;

        if (position == lastMicPosition)
            return;

        if (position > lastMicPosition)
        {
            ReadMicRange(lastMicPosition, position - lastMicPosition);
        }
        else
        {
            // Wrapped around the buffer
            ReadMicRange(lastMicPosition, clipSamples - lastMicPosition);
            if (position > 0)
                ReadMicRange(0, position);
        }

        lastMicPosition = position;
    }

    void ReadMicRange(int offsetSamples, int sampleCount)
    {
        if (sampleCount <= 0) return;

        int total = sampleCount * micChannels;
        if (micReadBuffer == null || micReadBuffer.Length != total)
            micReadBuffer = new float[total];

        micClip.GetData(micReadBuffer, offsetSamples);
        micSamples.AddRange(micReadBuffer);
    }

    void SaveWavFromSamples(float[] samples, int sampleRate, int channels, string path, float gain)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(fs))
        {
            int sampleCount = samples.Length;
            int dataSize = sampleCount * 2; // 16-bit
            int byteRate = sampleRate * channels * 2;

            // RIFF header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16); // chunk size
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2)); // block align
            writer.Write((short)16); // bits per sample

            // data chunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            // write samples
            foreach (float sample in samples)
            {
                float boosted = Mathf.Clamp(sample * gain, -1f, 1f);
                short s = (short)(boosted * 32767f);
                writer.Write(s);
            }
        }
    }

    void SaveWavFromClip(AudioClip clip, int position, int sampleRate, string path)
    {
        int clipSampleRate = clip.frequency;
        int channels = clip.channels;
        int sampleFrames = Mathf.Min(position, clip.samples);
        float[] samples = new float[sampleFrames * channels];
        clip.GetData(samples, 0);

        using (var fs = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(fs))
        {
            int sampleCount = samples.Length;
            int dataSize = sampleCount * 2; // 16-bit
            int byteRate = clipSampleRate * channels * 2;

            // RIFF header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(clipSampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);

            // data chunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            // write samples
            foreach (float sample in samples)
            {
                float boosted = Mathf.Clamp(sample * microphoneGain, -1f, 1f);
                short s = (short)(boosted * 32767f);
                writer.Write(s);
            }
        }
    }
}
