using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Captures all game audio including Vivox participant voices (when VivoxAudioInjector is active).
/// Attach this to a GameObject with an AudioListener (typically the main camera).
/// </summary>
public class GameAudioCapture : MonoBehaviour
{
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }

    [Header("Capture Settings")]
    [Tooltip("Always use AudioListener.GetOutputData for consistent capture including Vivox audio")]
    public bool forceListenerCapture = true;

    [Tooltip("Sample size for output data capture (higher = more accurate, lower = more responsive)")]
    [Range(512, 8192)]
    public int outputDataSampleSize = 2048;

    [Header("Audio Boost")]
    [Tooltip("Boost captured audio volume (useful if voices are quiet)")]
    [Range(0.5f, 3f)]
    public float captureGain = 1.0f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private readonly List<float> buffer = new List<float>(44100 * 60 * 2); // ~1 min stereo at 44.1kHz
    private bool isCapturing;
    private float[] outputLeft;
    private float[] outputRight;
    private float lastCaptureTime;
    private float captureInterval;

    void Awake()
    {
        SampleRate = AudioSettings.outputSampleRate;
        Channels = 2;

        // Calculate capture interval based on sample size and sample rate
        // This ensures we capture audio at the right pace without gaps
        captureInterval = outputDataSampleSize / (float)SampleRate;

        if (showDebugLogs)
            Debug.Log($"<color=#FFD700>[GameAudioCapture]</color> Initialized - SampleRate: {SampleRate}, CaptureInterval: {captureInterval * 1000:F1}ms");
    }

    public void StartCapture()
    {
        buffer.Clear();
        isCapturing = true;
        lastCaptureTime = Time.unscaledTime;

        if (showDebugLogs)
            Debug.Log($"<color=#FFD700>[GameAudioCapture]</color> Started capturing audio");
    }

    public float[] StopCapture()
    {
        isCapturing = false;

        if (showDebugLogs)
            Debug.Log($"<color=#FFD700>[GameAudioCapture]</color> Stopped - Captured {buffer.Count} samples ({buffer.Count / (float)(SampleRate * Channels):F1}s)");

        return buffer.Count > 0 ? buffer.ToArray() : null;
    }

    /// <summary>
    /// OnAudioFilterRead is called by Unity's audio system and captures audio
    /// from any AudioSource that passes through this AudioListener.
    /// This includes Vivox audio when VivoxAudioInjector routes it through Unity AudioSources.
    /// </summary>
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isCapturing) return;

        // When forceListenerCapture is true, we skip OnAudioFilterRead
        // because GetOutputData gives us the final mixed output including all sources
        if (forceListenerCapture) return;

        Channels = channels;

        // Apply gain if needed
        if (Mathf.Approximately(captureGain, 1f))
        {
            buffer.AddRange(data);
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
            {
                buffer.Add(Mathf.Clamp(data[i] * captureGain, -1f, 1f));
            }
        }
    }

    void Update()
    {
        if (!isCapturing) return;

        // Use timed capture to avoid gaps or overlaps
        float currentTime = Time.unscaledTime;
        if (currentTime - lastCaptureTime < captureInterval * 0.9f) return;
        lastCaptureTime = currentTime;

        CaptureFromListener();
    }

    void CaptureFromListener()
    {
        int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        Channels = channels;

        EnsureOutputBuffers();

        if (channels == 1)
        {
            AudioListener.GetOutputData(outputLeft, 0);

            if (Mathf.Approximately(captureGain, 1f))
            {
                buffer.AddRange(outputLeft);
            }
            else
            {
                for (int i = 0; i < outputLeft.Length; i++)
                {
                    buffer.Add(Mathf.Clamp(outputLeft[i] * captureGain, -1f, 1f));
                }
            }
            return;
        }

        // Stereo capture
        AudioListener.GetOutputData(outputLeft, 0);
        AudioListener.GetOutputData(outputRight, 1);

        // Interleave stereo samples
        for (int i = 0; i < outputLeft.Length; i++)
        {
            if (Mathf.Approximately(captureGain, 1f))
            {
                buffer.Add(outputLeft[i]);
                buffer.Add(outputRight[i]);
            }
            else
            {
                buffer.Add(Mathf.Clamp(outputLeft[i] * captureGain, -1f, 1f));
                buffer.Add(Mathf.Clamp(outputRight[i] * captureGain, -1f, 1f));
            }
        }
    }

    void EnsureOutputBuffers()
    {
        if (outputLeft == null || outputLeft.Length != outputDataSampleSize)
            outputLeft = new float[outputDataSampleSize];
        if (outputRight == null || outputRight.Length != outputDataSampleSize)
            outputRight = new float[outputDataSampleSize];
    }

    /// <summary>
    /// Returns the current capture duration in seconds
    /// </summary>
    public float GetCaptureDuration()
    {
        if (SampleRate == 0 || Channels == 0) return 0;
        return buffer.Count / (float)(SampleRate * Channels);
    }

    /// <summary>
    /// Returns true if currently capturing audio
    /// </summary>
    public bool IsCapturing => isCapturing;
}

