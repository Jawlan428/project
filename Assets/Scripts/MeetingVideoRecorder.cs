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
    public bool recordAudio = true;
    public int audioSampleRate = 44100;
    
    [Header("Output Settings")]
    public bool autoCreateMP4 = true;
    public bool deleteFramesAfterMP4 = true;
    
    [Header("UI Settings")]
    public bool showUI = true;
    [Header("Status")]
    public bool isRecording = false;
    public bool isProcessing = false;
    public string microphoneDevice = "";
    
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
    
    private AudioClip audioClip;
    private bool hasMicrophone = false;
    
    private GUIStyle buttonStyle;
    private GUIStyle statusStyle;
    private GUIStyle pathStyle;
    private Texture2D redTex;
    private Texture2D greenTex;
    private Texture2D darkTex;
    private Texture2D blueTex;

    void Start()
    {
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
        
        // Save videos to Desktop
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        videoFolderPath = Path.Combine(desktopPath, "MeetingRecordings");
        if (!Directory.Exists(videoFolderPath))
            Directory.CreateDirectory(videoFolderPath);
        
        Debug.Log("Videos will be saved to: " + videoFolderPath);
        
        captureInterval = 1f / frameRate;
        FindRecordingCamera();
        
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            hasMicrophone = true;
            Debug.Log("Microphone: " + microphoneDevice);
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
            Debug.Log("Camera: " + recordingCamera.name);
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
    }

    void OnGUI()
    {
        if (!showUI) return;
        
        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 24;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            
            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 16;
            statusStyle.fontStyle = FontStyle.Bold;
            statusStyle.normal.textColor = Color.white;
            statusStyle.alignment = TextAnchor.MiddleCenter;
            
            pathStyle = new GUIStyle(GUI.skin.label);
            pathStyle.fontSize = 11;
            pathStyle.normal.textColor = Color.yellow;
            pathStyle.wordWrap = true;
        }
        
        float buttonWidth = 220;
        float buttonHeight = 60;
        float x = 170;
        float y = 80;
        
        GUI.DrawTexture(new Rect(x - 10, y - 10, buttonWidth + 20, 200), darkTex);
        
        if (isProcessing)
        {
            // Show green if successful, blue if processing, red if failed
            if (processingStatus.Contains("SUCCESSFUL"))
            {
                buttonStyle.normal.background = greenTex;
                buttonStyle.hover.background = greenTex;
                GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "✅ DONE", buttonStyle);
            }
            else if (processingStatus.Contains("Failed"))
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
            
            GUI.Label(new Rect(x, y + buttonHeight + 10, buttonWidth, 50), processingStatus, statusStyle);
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
                string.Format("{0} REC {1:00}:{2:00}", dot, minutes, seconds), statusStyle);
            GUI.Label(new Rect(x, y + buttonHeight + 35, buttonWidth, 25), frameCount + " frames", statusStyle);
            
            if (recordAudio && hasMicrophone)
                GUI.Label(new Rect(x, y + buttonHeight + 60, buttonWidth, 25), "🎤 Recording audio", statusStyle);
        }
        else
        {
            buttonStyle.normal.background = greenTex;
            buttonStyle.hover.background = greenTex;
            
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "⏺ RECORD", buttonStyle))
                StartRecording();
            
            GUI.Label(new Rect(x, y + buttonHeight + 10, buttonWidth, 25), "Ready to record", statusStyle);
            if (hasMicrophone)
                GUI.Label(new Rect(x, y + buttonHeight + 35, buttonWidth, 25), "🎤 Mic ready", statusStyle);
            if (autoCreateMP4)
                GUI.Label(new Rect(x, y + buttonHeight + 60, buttonWidth, 25), "🎬 Auto MP4: ON", statusStyle);
            
            string camName = recordingCamera != null ? recordingCamera.name : "None!";
            GUI.Label(new Rect(x, y + buttonHeight + 90, buttonWidth, 50), "Cam: " + camName, pathStyle);
        }
    }

    public void StartRecording()
    {
        if (isRecording || isProcessing) return;
        
        FindRecordingCamera();
        if (recordingCamera == null)
        {
            Debug.LogError("No camera!");
            return;
        }
        
        string sessionName = "Recording_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        currentSessionPath = Path.Combine(videoFolderPath, sessionName);
        Directory.CreateDirectory(currentSessionPath);
        
        renderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        screenShot = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        
        if (recordAudio && hasMicrophone)
        {
            audioClip = Microphone.Start(microphoneDevice, false, 3600, audioSampleRate);
            Debug.Log("🎤 Audio started");
        }
        
        frameCount = 0;
        nextCaptureTime = Time.time;
        recordingStartTime = Time.time;
        isRecording = true;
        
        Debug.Log("🔴 Recording started: " + currentSessionPath);
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;
        
        string audioPath = null;
        if (recordAudio && hasMicrophone && audioClip != null)
        {
            int position = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);
            
            if (position > 0)
            {
                audioPath = Path.Combine(currentSessionPath, "audio.wav");
                SaveWav(audioClip, position, audioPath);
                Debug.Log("🎤 Audio saved");
            }
        }
        
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
        
        Debug.Log("⬛ Stopped: " + frameCount + " frames");
        
        if (autoCreateMP4 && File.Exists(ffmpegPath) && frameCount > 0)
            StartCoroutine(CreateMP4Coroutine(audioPath));
    }

    IEnumerator CreateMP4Coroutine(string audioPath)
    {
        isProcessing = true;
        processingStatus = "Creating MP4...";
        
        yield return new WaitForSeconds(1f);
        
        string outputPath = Path.Combine(currentSessionPath, "meeting.mp4");
        
        // יצירת קובץ BAT זמני לביצוע FFmpeg (פותר בעיות נתיבים עם רווחים)
        string batPath = Path.Combine(currentSessionPath, "convert.bat");
        
        string batContent;
        if (audioPath != null && File.Exists(audioPath))
        {
            batContent = $@"@echo off
cd /d ""{currentSessionPath}""
""{ffmpegPath}"" -y -framerate {frameRate} -i ""frame_%%06d.jpg"" -i ""audio.wav"" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k -pix_fmt yuv420p -shortest ""meeting.mp4""
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
            Debug.Log("FFmpeg process started...");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error starting FFmpeg: " + ex.Message);
            isProcessing = false;
            yield break;
        }
        
        // Wait asynchronously without freezing Unity (OUTSIDE try-catch)
        while (process != null && !process.HasExited)
        {
            yield return new WaitForSeconds(0.5f);
            processingStatus = "Creating MP4...";
        }
        
        // Get results after process completes
        if (process != null)
        {
            try
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                
                Debug.Log("FFmpeg output: " + error);
                
                success = (process.ExitCode == 0) && File.Exists(outputPath);
                
                if (!success)
                    Debug.LogError("FFmpeg failed: " + process.ExitCode);
                    
                process.Dispose();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error reading FFmpeg output: " + ex.Message);
            }
        }
        
        // מחיקת קובץ BAT
        try { File.Delete(batPath); } catch { }
        
        if (success)
        {
            // Move the final MP4 to Desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string finalVideoPath = Path.Combine(desktopPath, Path.GetFileName(outputPath));
            
            // If file already exists on desktop, add timestamp
            if (File.Exists(finalVideoPath))
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                string extension = Path.GetExtension(outputPath);
                string timestamp = System.DateTime.Now.ToString("_HHmmss");
                finalVideoPath = Path.Combine(desktopPath, fileNameWithoutExt + timestamp + extension);
            }
            
            try
            {
                File.Copy(outputPath, finalVideoPath, true);
                Debug.Log("✅ MP4 created and saved to Desktop: " + finalVideoPath);
                processingStatus = "✅ RECORDING SUCCESSFUL!";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not copy to Desktop, saved at: " + outputPath);
                Debug.LogWarning("Error: " + ex.Message);
                processingStatus = "✅ RECORDING SUCCESSFUL!";
            }
            
            if (deleteFramesAfterMP4)
            {
                yield return new WaitForSeconds(0.5f);
                DeleteTemporaryFiles(audioPath);
            }
        }
        else
        {
            Debug.LogError("❌ Failed to create MP4");
            processingStatus = "❌ Recording Failed!";
        }
        
        // Show success/failure message for 5 seconds
        yield return new WaitForSeconds(5f);
        isProcessing = false;
        processingStatus = "";
    }

    void DeleteTemporaryFiles(string audioPath)
    {
        try
        {
            foreach (string file in Directory.GetFiles(currentSessionPath, "frame_*.jpg"))
                File.Delete(file);
            foreach (string file in Directory.GetFiles(currentSessionPath, "*.meta"))
                File.Delete(file);
            if (audioPath != null && File.Exists(audioPath))
                File.Delete(audioPath);
            Debug.Log("🗑 Temp files deleted");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Delete error: " + ex.Message);
        }
    }

    void SaveWav(AudioClip clip, int sampleCount, string path)
    {
        float[] samples = new float[sampleCount * clip.channels];
        clip.GetData(samples, 0);
        
        using (var fs = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(fs))
        {
            int byteRate = audioSampleRate * clip.channels * 2;
            int dataSize = sampleCount * clip.channels * 2;
            
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(audioSampleRate);
            writer.Write(byteRate);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            
            foreach (float sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767));
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
        File.WriteAllBytes(Path.Combine(currentSessionPath, string.Format("frame_{0:D6}.jpg", frameCount)), bytes);
        frameCount++;
    }

    void OnDestroy()
    {
        if (isRecording) StopRecording();
        if (redTex) Destroy(redTex);
        if (greenTex) Destroy(greenTex);
        if (darkTex) Destroy(darkTex);
        if (blueTex) Destroy(blueTex);
    }

    void OnApplicationQuit()
    {
        if (isRecording) StopRecording();
    }
}

