using System;
using System.IO;
using System.Text;

namespace RuntimeRecording
{
    /// <summary>
    /// Very small WAV writer for 16-bit PCM. Writes a placeholder header, streams samples,
    /// and patches the header on Dispose().
    /// </summary>
    internal sealed class RuntimePcmWavWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _sampleRate;
        private readonly int _channels;
        private long _dataChunkSizePos;
        private long _riffChunkSizePos;
        private long _dataBytesWritten;

        public RuntimePcmWavWriter(string path, int sampleRate, int channels)
        {
            _sampleRate = sampleRate;
            _channels = channels;

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);

            WriteHeaderPlaceholder();
        }

        public void WriteInterleavedFloatSamples(float[] data, int length)
        {
            // Convert float [-1, 1] to 16-bit PCM.
            for (int i = 0; i < length; i++)
            {
                var v = data[i];
                if (v > 1f) v = 1f;
                if (v < -1f) v = -1f;
                short s = (short)(v < 0 ? v * 32768f : v * 32767f);
                _writer.Write(s);
                _dataBytesWritten += sizeof(short);
            }
        }

        private void WriteHeaderPlaceholder()
        {
            // RIFF header
            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _riffChunkSizePos = _stream.Position;
            _writer.Write(0); // to be patched
            _writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            _writer.Write(Encoding.ASCII.GetBytes("fmt "));
            _writer.Write(16); // PCM fmt chunk size
            _writer.Write((short)1); // PCM format
            _writer.Write((short)_channels);
            _writer.Write(_sampleRate);
            int byteRate = _sampleRate * _channels * 2;
            short blockAlign = (short)(_channels * 2);
            _writer.Write(byteRate);
            _writer.Write(blockAlign);
            _writer.Write((short)16); // bits per sample

            // data chunk
            _writer.Write(Encoding.ASCII.GetBytes("data"));
            _dataChunkSizePos = _stream.Position;
            _writer.Write(0); // to be patched
        }

        private void PatchHeader()
        {
            // data chunk size
            _stream.Seek(_dataChunkSizePos, SeekOrigin.Begin);
            _writer.Write((int)_dataBytesWritten);

            // RIFF chunk size = fileSize - 8
            long fileSize = 44 + _dataBytesWritten;
            _stream.Seek(_riffChunkSizePos, SeekOrigin.Begin);
            _writer.Write((int)(fileSize - 8));

            _stream.Seek(0, SeekOrigin.End);
        }

        public void Dispose()
        {
            try
            {
                _writer.Flush();
                PatchHeader();
                _writer.Flush();
            }
            catch
            {
                // ignore
            }
            finally
            {
                _writer.Dispose();
                _stream.Dispose();
            }
        }
    }
}


