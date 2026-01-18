using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;
using System.Collections;
using Debug = UnityEngine.Debug;

/// <summary>
/// Captures system audio (loopback) using FFmpeg on Windows.
/// This captures ALL audio playing on the system, including Vivox participant voices.
/// 
/// SETUP REQUIRED:
/// 1. Enable "Stereo Mix" in Windows Sound Settings:
///    - Right-click speaker icon in taskbar → Sounds → Recording tab
///    - Right-click empty area → Show Disabled Devices
///    - Right-click "Stereo Mix" → Enable
/// 
/// 2. Or install virtual audio cable software like:
///    - VB-Cable (free): https://vb-audio.com/Cable/
///    - Virtual Audio Cable
/// </summary>
public class SystemAudioCapture : MonoBehaviour
{
    [Header("Audio Device")]
    [Tooltip("Name of the audio capture device. Set to exact name from Windows Sound settings.")]
    public string audioDeviceName = "Stereo Mix (Realtek(R) Audio)";

    [Header("Settings")]
    [Tooltip("Sample rate for audio capture")]
    public int sampleRate = 44100;
    
    [Tooltip("Number of audio channels (1=mono, 2=stereo)")]
    public int channels = 2;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private Process ffmpegProcess;
    private string outputPath;
    private string ffmpegPath;
    private bool isCapturing = false;
    private string detectedDevice = null;

    public bool IsCapturing => isCapturing;
    public string OutputPath => outputPath;
    public string DetectedDevice => detectedDevice;

    void Awake()
    {
        // Find FFmpeg path
        ffmpegPath = Path.Combine(Application.dataPath, "ffmpeg-8.0.1-essentials_build", "bin", "ffmpeg.exe");
        
        if (!File.Exists(ffmpegPath))
        {
            LogDebug("FFmpeg not found at: " + ffmpegPath, true);
        }
        else
        {
            // Auto-detect audio device on startup
            StartCoroutine(DetectAudioDeviceCoroutine());
        }
    }

    IEnumerator DetectAudioDeviceCoroutine()
    {
        yield return null; // Wait a frame
        DetectAudioDevice();
    }

    /// <summary>
    /// Detect available audio loopback devices
    /// </summary>
    public void DetectAudioDevice()
    {
        if (!File.Exists(ffmpegPath)) return;

        try
        {
            // Get list of devices from FFmpeg
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();

                LogDebug("Scanning for audio devices...");

                // Parse FFmpeg output to find exact device names
                // FFmpeg lists devices like: "Stereo Mix (Realtek(R) Audio)"
                string[] lines = output.Split('\n');
                bool inAudioSection = false;

                foreach (string line in lines)
                {
                    if (line.Contains("DirectShow audio devices"))
                    {
                        inAudioSection = true;
                        continue;
                    }
                    
                    if (inAudioSection && line.Contains("\""))
                    {
                        // Extract device name between quotes
                        int firstQuote = line.IndexOf('"');
                        int lastQuote = line.LastIndexOf('"');
                        if (firstQuote >= 0 && lastQuote > firstQuote)
                        {
                            string deviceName = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                            
                            // Check if this is a loopback/stereo mix device
                            string lowerName = deviceName.ToLower();
                            if (lowerName.Contains("stereo mix") || 
                                lowerName.Contains("wave out") ||
                                lowerName.Contains("what u hear") ||
                                lowerName.Contains("loopback") ||
                                lowerName.Contains("cable output"))
                            {
                                detectedDevice = deviceName;
                                LogDebug($"Found loopback device: \"{deviceName}\"");
                                return;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(detectedDevice))
                {
                    LogDebug("No loopback device auto-detected.", true);
                    LogDebug("Available audio devices:\n" + output);
                }
            }
        }
        catch (Exception e)
        {
            LogDebug($"Failed to detect audio devices: {e.Message}", true);
        }
    }

    /// <summary>
    /// Start capturing system audio to the specified file path
    /// </summary>
    public bool StartCapture(string wavOutputPath)
    {
        if (isCapturing)
        {
            LogDebug("Already capturing system audio");
            return false;
        }

        if (!File.Exists(ffmpegPath))
        {
            LogDebug("FFmpeg not found, cannot capture system audio", true);
            return false;
        }

        // Determine which device to use - prefer configured name, then detected, then try common names
        string device = !string.IsNullOrEmpty(audioDeviceName) ? audioDeviceName : detectedDevice;

        // If still no device, try common Realtek names
        if (string.IsNullOrEmpty(device))
        {
            string[] fallbackDevices = new string[]
            {
                "Stereo Mix (Realtek(R) Audio)",
                "Stereo Mix (Realtek High Definition Audio)",
                "Stereo Mix",
                "立体声混音 (Realtek(R) Audio)",  // Chinese
                "ステレオ ミキサー"  // Japanese
            };

            // List available devices first
            ListAudioDevicesInternal();

            foreach (string fallback in fallbackDevices)
            {
                device = fallback;
                LogDebug($"Trying device: {device}");
                if (TryStartCapture(wavOutputPath, device))
                {
                    return true;
                }
            }

            LogDebug("No audio capture device worked!", true);
            LogDebug("Please check the device name in Windows Sound Settings → Recording tab", true);
            return false;
        }

        return TryStartCapture(wavOutputPath, device);
    }

    private bool TryStartCapture(string wavOutputPath, string device)
    {
        outputPath = wavOutputPath;

        try
        {
            // FFmpeg command to capture audio from the specified device
            string ffmpegArgs = $"-y -f dshow -i audio=\"{device}\" " +
                               $"-ar {sampleRate} -ac {channels} " +
                               $"-acodec pcm_s16le \"{outputPath}\"";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            ffmpegProcess = new Process { StartInfo = startInfo };
            ffmpegProcess.Start();

            // Wait a moment and check if process is still running (didn't fail immediately)
            System.Threading.Thread.Sleep(500);
            
            if (ffmpegProcess.HasExited)
            {
                string error = ffmpegProcess.StandardError.ReadToEnd();
                if (error.Contains("Could not find audio device") || error.Contains("Error"))
                {
                    LogDebug($"Device '{device}' not found or failed");
                    ffmpegProcess.Dispose();
                    ffmpegProcess = null;
                    return false;
                }
            }

            isCapturing = true;
            LogDebug($"✅ System audio capture started using: \"{device}\"");
            LogDebug($"Output: {outputPath}");

            return true;
        }
        catch (Exception e)
        {
            LogDebug($"Failed to start capture with device '{device}': {e.Message}");
            return false;
        }
    }

    private void ListAudioDevicesInternal()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                // Extract just the audio device names
                LogDebug("=== Available Audio Devices ===");
                string[] lines = output.Split('\n');
                bool inAudioSection = false;
                foreach (string line in lines)
                {
                    if (line.Contains("DirectShow audio devices"))
                    {
                        inAudioSection = true;
                        continue;
                    }
                    if (inAudioSection && line.Contains("\""))
                    {
                        LogDebug(line.Trim());
                    }
                    if (inAudioSection && line.Contains("DirectShow video devices"))
                    {
                        break;
                    }
                }
                LogDebug("================================");
            }
        }
        catch { }
    }

    /// <summary>
    /// Stop capturing and return the output file path
    /// </summary>
    public string StopCapture()
    {
        if (!isCapturing || ffmpegProcess == null)
        {
            return null;
        }

        try
        {
            // Send 'q' to FFmpeg to gracefully stop recording
            if (!ffmpegProcess.HasExited)
            {
                ffmpegProcess.StandardInput.WriteLine("q");
                ffmpegProcess.StandardInput.Flush();
                
                // Wait for process to exit (max 5 seconds)
                if (!ffmpegProcess.WaitForExit(5000))
                {
                    LogDebug("FFmpeg didn't exit gracefully, killing process");
                    ffmpegProcess.Kill();
                }
            }

            string stderr = ffmpegProcess.StandardError.ReadToEnd();
            if (showDebugLogs && !string.IsNullOrEmpty(stderr))
            {
                // Only log errors, not the normal output
                if (stderr.Contains("Error") || stderr.Contains("error"))
                {
                    LogDebug($"FFmpeg error: {stderr}", true);
                }
            }

            ffmpegProcess.Dispose();
            ffmpegProcess = null;
        }
        catch (Exception e)
        {
            LogDebug($"Error stopping capture: {e.Message}", true);
        }

        isCapturing = false;
        
        if (File.Exists(outputPath))
        {
            FileInfo fi = new FileInfo(outputPath);
            LogDebug($"System audio saved: {outputPath} ({fi.Length / 1024}KB)");
            return outputPath;
        }
        else
        {
            LogDebug("System audio file not found after capture", true);
            return null;
        }
    }

    void OnDestroy()
    {
        if (isCapturing)
        {
            StopCapture();
        }
    }

    void OnApplicationQuit()
    {
        if (isCapturing)
        {
            StopCapture();
        }
    }

    void LogDebug(string message, bool isError = false)
    {
        if (showDebugLogs || isError)
        {
            if (isError)
                UnityEngine.Debug.LogError($"<color=#FF6B6B>[SystemAudioCapture]</color> {message}");
            else
                UnityEngine.Debug.Log($"<color=#6BCB77>[SystemAudioCapture]</color> {message}");
        }
    }

    /// <summary>
    /// List available audio devices (for debugging)
    /// </summary>
    [ContextMenu("List Audio Devices")]
    public void ListAudioDevices()
    {
        if (!File.Exists(ffmpegPath))
        {
            UnityEngine.Debug.LogError("FFmpeg not found");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardError.ReadToEnd();
                process.WaitForExit();
                UnityEngine.Debug.Log("=== Available Audio Devices ===\n" + output);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to list devices: {e.Message}");
        }
    }

    /// <summary>
    /// Test if a specific device works
    /// </summary>
    [ContextMenu("Test Stereo Mix")]
    public void TestStereoMix()
    {
        audioDeviceName = "Stereo Mix";
        detectedDevice = "Stereo Mix";
        UnityEngine.Debug.Log("Set device to 'Stereo Mix'. Try recording now.");
    }
}
