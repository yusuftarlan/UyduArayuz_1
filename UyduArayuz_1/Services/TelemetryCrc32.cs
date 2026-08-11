using System.Buffers.Binary;

namespace UyduArayuz_1.Services
{
    public static class TelemetryCrc32
    {
        private const uint Polynomial = 0x04C11DB7;
        private const uint InitialValue = 0xFFFFFFFF;

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            if (data.Length % sizeof(uint) != 0)
            {
                throw new ArgumentException(
                    "STM32 CRC input length must be a multiple of four bytes.",
                    nameof(data));
            }

            uint crc = InitialValue;

            for (int offset = 0; offset < data.Length; offset += sizeof(uint))
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(
                    data.Slice(offset, sizeof(uint)));
                crc ^= word;

                for (int bit = 0; bit < 32; bit++)
                {
                    crc = (crc & 0x80000000) != 0
                        ? (crc << 1) ^ Polynomial
                        : crc << 1;
                }
            }

            return crc;
        }
    }
}
