using System;
using System.Collections.Generic;

namespace UyduArayuz_1.Services
{
    public class TelemetryFrameExtractor
    {
        private readonly List<byte> _buffer = new List<byte>();

        public void Clear()
        {
            _buffer.Clear();
        }

        public IReadOnlyList<byte[]> AddBytes(byte[] data, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (count < 0 || count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

            var frames = new List<byte[]>();
            if (count == 0) return frames;

            for (int i = 0; i < count; i++)
            {
                _buffer.Add(data[i]);
            }

            ExtractAvailableFrames(frames);
            return frames;
        }

        private void ExtractAvailableFrames(List<byte[]> frames)
        {
            while (true)
            {
                int startIndex = _buffer.IndexOf(TelemetryProtocol.StartByte);
                if (startIndex < 0)
                {
                    _buffer.Clear();
                    return;
                }

                if (startIndex > 0) // Remove any bytes before the start byte
                {
                    _buffer.RemoveRange(0, startIndex);
                }

                if (_buffer.Count < TelemetryProtocol.PacketLength) // Not enough data for a complete frame
                {
                    return;
                }

                if (_buffer[TelemetryProtocol.EndOffset] != TelemetryProtocol.EndByte) // Check for the end byte
                {
                    _buffer.RemoveAt(0); // Remove the start byte and look for the next one
                    continue;
                }

                byte[] frame = _buffer.GetRange(0, TelemetryProtocol.PacketLength).ToArray(); // Extract the complete frame
                frames.Add(frame);
                _buffer.RemoveRange(0, TelemetryProtocol.PacketLength);
            }
        }
    }
}
