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
    public bool recordGameAudio = true;        // ✅ captures in-game audio (recommended)
    public GameAudioCapture gameAudioCapture;  // auto-found if null

    public bool recordMicrophone = false;      // optional
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

        // find or attach audio capture automatically
        if (recordGameAudio)
            EnsureGameAudioCapture();

        // microphone detection
        if (Microphone.devices.Length > 0)
        {
            hasMicrophone = true;

            if (string.IsNullOrEmpty(microphoneDevice))
            {
                // Use system default by passing empty string to Microphone.Start
                microphoneDevice = "";
                Debug.Log("Microphone detected. Using system default device.");
            }
            else
            {
                bool found = false;
                foreach (string device in Microphone.devices)
                {
                    if (device == microphoneDevice)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.LogWarning("Configured microphone not found. Using system default.");
                    microphoneDevice = "";
                }
                else
                {
                    Debug.Log("Microphone detected: " + microphoneDevice);
                }
            }
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

        if (isRecording && recordMicrophone && hasMicrophone && micClip != null &&
            Microphone.IsRecording(microphoneDevice))
        {
            CaptureMicSamples();
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

            if (recordGameAudio && gameAudioCapture != null)
                GUI.Label(new Rect(x, y + buttonHeight + 60, buttonWidth, 25), "🔊 Game audio: ON", statusStyle);

            if (recordMicrophone && hasMicrophone)
                GUI.Label(new Rect(x, y + buttonHeight + 85, buttonWidth, 25), "🎤 Mic: ON", statusStyle);
        }
        else
        {
            buttonStyle.normal.background = greenTex;
            buttonStyle.hover.background = greenTex;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "⏺ RECORD", buttonStyle))
                StartRecording();

            GUI.Label(new Rect(x, y + buttonHeight + 10, buttonWidth, 25), "Ready to record", statusStyle);

            if (recordGameAudio)
                GUI.Label(new Rect(x, y + buttonHeight + 35, buttonWidth, 25), "🔊 Game audio: ON", statusStyle);

            if (recordMicrophone)
                GUI.Label(new Rect(x, y + buttonHeight + 60, buttonWidth, 25),
                    hasMicrophone ? "🎤 Mic ready" : "🎤 Mic NOT found", statusStyle);

            if (autoCreateMP4)
                GUI.Label(new Rect(x, y + buttonHeight + 85, buttonWidth, 25), "🎬 Auto MP4: ON", statusStyle);

            string camName = recordingCamera != null ? recordingCamera.name : "None!";
            GUI.Label(new Rect(x, y + buttonHeight + 110, buttonWidth, 50), "Cam: " + camName, pathStyle);
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

        // Start microphone first and wait until it begins capturing samples.
        if (recordMicrophone && hasMicrophone)
        {
            int sampleRateToUse = audioSampleRate;
            Microphone.GetDeviceCaps(microphoneDevice, out int minRate, out int maxRate);
            if (maxRate != 0 && (sampleRateToUse < minRate || sampleRateToUse > maxRate))
                sampleRateToUse = maxRate;

            micClip = Microphone.Start(microphoneDevice, true, 3600, sampleRateToUse);
            micSampleRate = micClip != null ? micClip.frequency : sampleRateToUse;
            micChannels = micClip != null ? micClip.channels : 1;
            lastMicPosition = 0;
            micSamples.Clear();

            float startTime = Time.realtimeSinceStartup;
            while (Microphone.GetPosition(microphoneDevice) <= 0)
            {
                if (Time.realtimeSinceStartup - startTime > 2f)
                {
                    Debug.LogWarning("🎤 Microphone did not start producing samples in time.");
                    break;
                }
                yield return null;
            }
        }

        // Start capturing game audio after mic is ready.
        if (recordGameAudio)
        {
            EnsureGameAudioCapture();

            if (gameAudioCapture != null)
            {
                gameAudioCapture.StartCapture();
                Debug.Log("🔊 Game audio capture started");
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

        string audioPath = null;
        string micPath = null;

        // Stop and save game audio
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

        // Stop microphone (optional). If you also want MIC, you can write a separate file.
        if (recordMicrophone && hasMicrophone && micClip != null)
        {
            CaptureMicSamples();
            int position = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);

            if (position <= 0 && lastMicPosition > 0)
                position = lastMicPosition;

            if (micSamples.Count > 0)
            {
                micPath = Path.Combine(currentSessionPath, "mic.wav");
                SaveWavFromSamples(micSamples.ToArray(), micSampleRate > 0 ? micSampleRate : audioSampleRate, micChannels, micPath, microphoneGain);
                Debug.Log("🎤 Mic audio saved: " + micPath);
            }
            else
            {
                Debug.LogWarning("🎤 Mic recording had no samples. Check device permissions.");
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
            StartCoroutine(CreateMP4Coroutine(audioPath, micPath));
    }

    IEnumerator CreateMP4Coroutine(string audioPath, string micPath)
    {
        isProcessing = true;
        processingStatus = "Creating MP4...";

        yield return new WaitForSeconds(0.5f);

        string outputPath = Path.Combine(currentSessionPath, "meeting.mp4");
        string batPath = Path.Combine(currentSessionPath, "convert.bat");

        string batContent;

        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath) &&
            !string.IsNullOrEmpty(micPath) && File.Exists(micPath))
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(audioPath)}"" -i ""{Path.GetFileName(micPath)}"" -filter_complex ""[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[a]"" -map 0:v -map ""[a]"" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
        }
        else if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(audioPath)}"" -map 0:v -map 1:a -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
        }
        else if (!string.IsNullOrEmpty(micPath) && File.Exists(micPath))
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""{Path.GetFileName(micPath)}"" -map 0:v -map 1:a -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 192k -pix_fmt yuv420p -shortest ""meeting.mp4""
exit /b %errorlevel%
";
        }
        else
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p ""meeting.mp4""
exit /b %errorlevel%
";
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

        int position = Microphone.GetPosition(microphoneDevice);
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
