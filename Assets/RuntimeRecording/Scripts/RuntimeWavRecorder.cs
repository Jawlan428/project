using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace RuntimeRecording
{
    /// <summary>
    /// Records AudioListener output to a 16-bit PCM WAV file.
    /// OnAudioFilterRead runs on Unity's audio thread; avoid heavy work there.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeWavRecorder : MonoBehaviour
    {
        [Tooltip("If false, this component is idle even if Begin() was called.")]
        public bool enabledForRecording = true;

        private readonly ConcurrentQueue<float[]> _queue = new ConcurrentQueue<float[]>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);

        private Thread _writerThread;
        private volatile bool _running;

        private FileStream _fs;
        private string _path;
        private int _sampleRate;
        private int _channels;
        private long _pcmBytesWritten;

        public void Begin(string wavPath)
        {
            if (_running)
                return;

            _path = wavPath;
            _sampleRate = AudioSettings.outputSampleRate;
            _channels = 0; // will be set on first OnAudioFilterRead callback
            _pcmBytesWritten = 0;

            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");

            _fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read);
            WriteWavHeaderPlaceholder(_fs);

            _running = true;
            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "RuntimeWavRecorderWriter"
            };
            _writerThread.Start();
        }

        public void End()
        {
            if (!_running)
                return;

            _running = false;
            _signal.Set();

            try
            {
                _writerThread?.Join(2000);
            }
            catch
            {
                // ignore
            }

            _writerThread = null;

            try
            {
                if (_fs != null)
                {
                    FinalizeWavHeader(_fs, _sampleRate, Math.Max(1, _channels), _pcmBytesWritten);
                    _fs.Dispose();
                }
            }
            finally
            {
                _fs = null;
            }

            while (_queue.TryDequeue(out _)) { }
        }

        private void OnDisable()
        {
            End();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!enabledForRecording || !_running || _fs == null)
                return;

            if (_channels == 0)
                _channels = channels;

            // Copy buffer (Unity reuses 'data').
            var copy = new float[data.Length];
            Array.Copy(data, copy, data.Length);

            // Prevent unbounded memory growth (drop if writer can't keep up).
            if (_queue.Count > 120) // ~2 seconds at 60 callbacks/sec worst-case
                return;

            _queue.Enqueue(copy);
            _signal.Set();
        }

        private void WriterLoop()
        {
            try
            {
                while (_running || !_queue.IsEmpty)
                {
                    if (!_queue.TryDequeue(out var block))
                    {
                        _signal.WaitOne(50);
                        continue;
                    }

                    // Convert float [-1..1] to 16-bit PCM.
                    var pcm = new byte[block.Length * 2];
                    var o = 0;

                    for (var i = 0; i < block.Length; i++)
                    {
                        var f = Mathf.Clamp(block[i], -1f, 1f);
                        short s = (short)Mathf.RoundToInt(f * short.MaxValue);
                        pcm[o++] = (byte)(s & 0xFF);
                        pcm[o++] = (byte)((s >> 8) & 0xFF);
                    }

                    _fs.Write(pcm, 0, pcm.Length);
                    _pcmBytesWritten += pcm.Length;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[RuntimeWavRecorder] Writer thread error: {e.Message}");
            }
        }

        private static void WriteWavHeaderPlaceholder(Stream s)
        {
            // 44-byte WAV header placeholder (PCM 16-bit).
            var header = new byte[44];
            s.Write(header, 0, header.Length);
        }

        private static void FinalizeWavHeader(Stream s, int sampleRate, int channels, long pcmBytes)
        {
            // RIFF header
            s.Seek(0, SeekOrigin.Begin);

            using var bw = new BinaryWriter(s, System.Text.Encoding.ASCII, true);

            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write((int)(36 + pcmBytes));
            bw.Write(new[] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16); // PCM chunk size
            bw.Write((short)1); // format = PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            var byteRate = sampleRate * channels * 2;
            bw.Write(byteRate);
            var blockAlign = (short)(channels * 2);
            bw.Write(blockAlign);
            bw.Write((short)16); // bits per sample

            // data chunk
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write((int)pcmBytes);
        }
    }
}


