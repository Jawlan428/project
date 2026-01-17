using System.Collections.Generic;
using UnityEngine;

// Attach this to a GameObject with an AudioListener (or any object in the scene).
public class GameAudioCapture : MonoBehaviour
{
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }

    [Header("Fallback Capture")]
    public bool useListenerOutputData = true;
    [Range(256, 8192)]
    public int outputDataSampleSize = 1024;

    private readonly List<float> buffer = new List<float>(1024 * 10);
    private bool isCapturing;
    private bool hasAudioFilterRead;
    private float[] outputLeft;
    private float[] outputRight;

    void Awake()
    {
        SampleRate = AudioSettings.outputSampleRate;
        Channels = 2;
    }

    public void StartCapture()
    {
        buffer.Clear();
        isCapturing = true;
        hasAudioFilterRead = false;
    }

    public float[] StopCapture()
    {
        isCapturing = false;
        return buffer.Count > 0 ? buffer.ToArray() : null;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isCapturing) return;

        hasAudioFilterRead = true;
        Channels = channels;
        buffer.AddRange(data);
    }

    void Update()
    {
        if (!isCapturing || hasAudioFilterRead || !useListenerOutputData) return;

        int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        Channels = channels;

        EnsureOutputBuffers();

        if (channels == 1)
        {
            AudioListener.GetOutputData(outputLeft, 0);
            buffer.AddRange(outputLeft);
            return;
        }

        AudioListener.GetOutputData(outputLeft, 0);
        AudioListener.GetOutputData(outputRight, 1);

        for (int i = 0; i < outputLeft.Length; i++)
        {
            buffer.Add(outputLeft[i]);
            buffer.Add(outputRight[i]);
        }
    }

    void EnsureOutputBuffers()
    {
        if (outputLeft == null || outputLeft.Length != outputDataSampleSize)
            outputLeft = new float[outputDataSampleSize];
        if (outputRight == null || outputRight.Length != outputDataSampleSize)
            outputRight = new float[outputDataSampleSize];
    }
}

