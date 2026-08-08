using System;

namespace UyduArayuz_1.Services
{
    public static class TelemetryCrc32
    {
        private const uint Polynomial = 0xEDB88320;

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte value in data)
            {
                crc ^= value;

                for (int bit = 0; bit < 8; bit++)
                {
                    bool leastSignificantBitSet = (crc & 1) != 0;
                    crc >>= 1;

                    if (leastSignificantBitSet)
                    {
                        crc ^= Polynomial;
                    }
                }
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}
