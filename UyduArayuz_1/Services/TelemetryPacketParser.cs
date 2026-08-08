using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UyduArayuz_1.Models;

namespace UyduArayuz_1.Services
{
    public class TelemetryPacketParser
    {
        public bool TryParse(byte[]? frame, [NotNullWhen(true)] out TelemetryPacket? packet, out string errorMessage)
        {
            packet = null;
            errorMessage = string.Empty;

            if (frame == null)
            {
                errorMessage = "Frame cannot be null.";
                return false;
            }

            if (frame.Length != TelemetryProtocol.PacketLength)
            {
                errorMessage = $"Frame length is {frame.Length}; expected {TelemetryProtocol.PacketLength}.";
                return false;
            }

            if (frame[TelemetryProtocol.StartOffset] != TelemetryProtocol.StartByte)
            {
                errorMessage = "Frame start byte is invalid.";
                return false;
            }

            if (frame[TelemetryProtocol.EndOffset] != TelemetryProtocol.EndByte)
            {
                errorMessage = "Frame end byte is invalid.";
                return false;
            }

            ReadOnlySpan<byte> bytes = frame;
            uint receivedCrc = ReadUInt32(bytes, TelemetryProtocol.CrcOffset);
            uint calculatedCrc = TelemetryCrc32.Compute(bytes.Slice(
                TelemetryProtocol.CrcStartOffset,
                TelemetryProtocol.CrcPayloadLength));

            if (receivedCrc != calculatedCrc)
            {
                errorMessage = $"CRC mismatch. Received 0x{receivedCrc:X8}, calculated 0x{calculatedCrc:X8}.";
                return false;
            }

            uint teamNo = ReadUInt32(bytes, TelemetryProtocol.TeamNoOffset);
            if (teamNo > int.MaxValue)
            {
                errorMessage = $"Team number exceeds int range: {teamNo}.";
                return false;
            }

            int status = bytes[TelemetryProtocol.SatelliteStatusOffset];
            int errorCode = bytes[TelemetryProtocol.ErrorCodeOffset];

            packet = new TelemetryPacket
            {
                PacketNo = ReadUInt16(bytes, TelemetryProtocol.PacketNoOffset),
                SatelliteStatus = status,
                SatelliteStatusString = TelemetryProtocol.GetSatelliteStatusText(status),
                ErrorCode = errorCode,
                ErrorCodeString = TelemetryProtocol.GetErrorCodeText(errorCode),
                SentDate = TelemetryProtocol.FormatUnixTimestamp(ReadUInt32(bytes, TelemetryProtocol.SentTimeOffset)),
                Pressure = ReadSingle(bytes, TelemetryProtocol.PressureOffset),
                Height = ReadSingle(bytes, TelemetryProtocol.HeightOffset),
                LandingSpeed = ReadSingle(bytes, TelemetryProtocol.LandingSpeedOffset),
                Tempreture = ReadSingle(bytes, TelemetryProtocol.TemperatureOffset),
                BatteryVoltage = ReadSingle(bytes, TelemetryProtocol.BatteryVoltageOffset),
                GpsLatitude = ReadSingle(bytes, TelemetryProtocol.GpsLatitudeOffset),
                GpsLongitude = ReadSingle(bytes, TelemetryProtocol.GpsLongitudeOffset),
                GpsAltitude = ReadSingle(bytes, TelemetryProtocol.GpsAltitudeOffset),
                Pitch = ReadSingle(bytes, TelemetryProtocol.PitchOffset),
                Roll = ReadSingle(bytes, TelemetryProtocol.RollOffset),
                Yaw = ReadSingle(bytes, TelemetryProtocol.YawOffset),
                TaskCode = Encoding.ASCII
                    .GetString(frame, TelemetryProtocol.TaskCodeOffset, TelemetryProtocol.TaskCodeLength)
                    .TrimEnd('\0', ' '),
                TeamNo = (int)teamNo
            };

            return true;
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));
        }

        private static float ReadSingle(ReadOnlySpan<byte> bytes, int offset)
        {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(float)));
            return BitConverter.Int32BitsToSingle(bits);
        }
    }
}
