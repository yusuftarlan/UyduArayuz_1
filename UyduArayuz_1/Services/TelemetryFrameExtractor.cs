using System;
using System.Collections.Generic;

namespace UyduArayuz_1.Services
{
    public class TelemetryFrameExtractor
    {
        private readonly List<byte> _buffer = new List<byte>();

        public int BufferedByteCount => _buffer.Count;

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
                int startIndex = FindMarker(
                    _buffer,
                    TelemetryProtocol.StartByte,
                    TelemetryProtocol.MarkerLength);
                if (startIndex < 0)
                {
                    PreservePossibleMarkerPrefix();
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

                if (!MarkerMatches(
                    _buffer,
                    TelemetryProtocol.EndOffset,
                    TelemetryProtocol.EndByte,
                    TelemetryProtocol.MarkerLength))
                {
                    _buffer.RemoveAt(0);
                    continue;
                }

                byte[] frame = _buffer.GetRange(0, TelemetryProtocol.PacketLength).ToArray(); // Extract the complete frame
                frames.Add(frame);
                _buffer.RemoveRange(0, TelemetryProtocol.PacketLength);
            }
        }

        private void PreservePossibleMarkerPrefix()
        {
            int bytesToKeep = 0;
            int maximumPrefixLength = Math.Min(
                _buffer.Count,
                TelemetryProtocol.MarkerLength - 1);

            for (int length = maximumPrefixLength; length > 0; length--)
            {
                bool isPrefix = true;
                for (int index = _buffer.Count - length; index < _buffer.Count; index++)
                {
                    if (_buffer[index] != TelemetryProtocol.StartByte)
                    {
                        isPrefix = false;
                        break;
                    }
                }

                if (isPrefix)
                {
                    bytesToKeep = length;
                    break;
                }
            }

            if (_buffer.Count > bytesToKeep)
            {
                _buffer.RemoveRange(0, _buffer.Count - bytesToKeep);
            }
        }

        private static int FindMarker(List<byte> buffer, byte markerByte, int markerLength)
        {
            int lastStartIndex = buffer.Count - markerLength;
            for (int startIndex = 0; startIndex <= lastStartIndex; startIndex++)
            {
                if (MarkerMatches(buffer, startIndex, markerByte, markerLength))
                {
                    return startIndex;
                }
            }

            return -1;
        }

        private static bool MarkerMatches(
            List<byte> buffer,
            int offset,
            byte markerByte,
            int markerLength)
        {
            if (offset < 0 || offset + markerLength > buffer.Count)
            {
                return false;
            }

            for (int index = 0; index < markerLength; index++)
            {
                if (buffer[offset + index] != markerByte)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
