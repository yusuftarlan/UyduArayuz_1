using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using TeknofestUyduArayuz.Models;

namespace TeknofestUyduArayuz.Services
{
    public enum TelemetryParseFailureReason
    {
        None,
        NullFrame,
        InvalidLength,
        InvalidStartMarker,
        InvalidEndMarker,
        CrcMismatch,
        InvalidRtc
    }

    public static class TelemetryPacketParser
    {
        public static bool TryParse(
            byte[]? frame,
            [NotNullWhen(true)] out TelemetryPacket? packet,
            out TelemetryParseFailureReason failureReason,
            out string errorMessage)
        {
            packet = null;
            failureReason = TelemetryParseFailureReason.None;
            errorMessage = string.Empty;

            if (frame == null)
            {
                failureReason = TelemetryParseFailureReason.NullFrame;
                errorMessage = "Çerçeve null olamaz.";
                return false;
            }

            if (frame.Length != TelemetryProtocol.PacketLength)
            {
                failureReason = TelemetryParseFailureReason.InvalidLength;
                errorMessage = $"Çerçeve uzunluğu {frame.Length}; beklenen uzunluk {TelemetryProtocol.PacketLength}.";
                return false;
            }

            ReadOnlySpan<byte> bytes = frame;

            if (ReadUInt32(bytes, TelemetryProtocol.StartOffset) != TelemetryProtocol.StartMarker)
            {
                failureReason = TelemetryParseFailureReason.InvalidStartMarker;
                errorMessage = "Çerçeve başlangıç işareti geçersiz.";
                return false;
            }

            if (ReadUInt32(bytes, TelemetryProtocol.EndOffset) != TelemetryProtocol.EndMarker)
            {
                failureReason = TelemetryParseFailureReason.InvalidEndMarker;
                errorMessage = "Çerçeve bitiş işareti geçersiz.";
                return false;
            }

            uint receivedCrc = ReadUInt32(bytes, TelemetryProtocol.CrcOffset);
            uint calculatedCrc = TelemetryCrc32.Compute(bytes.Slice(
                TelemetryProtocol.CrcStartOffset,
                TelemetryProtocol.CrcPayloadLength));

            if (receivedCrc != calculatedCrc)
            {
                failureReason = TelemetryParseFailureReason.CrcMismatch;
                errorMessage = $"CRC uyuşmazlığı. Alınan 0x{receivedCrc:X8}, hesaplanan 0x{calculatedCrc:X8}.";
                return false;
            }

            if (!TryReadRtcDate(bytes, out DateTime sentDate, out errorMessage))
            {
                failureReason = TelemetryParseFailureReason.InvalidRtc;
                return false;
            }

            int status = ReadUInt16(bytes, TelemetryProtocol.SatelliteStatusOffset);
            int errorCode = ReadUInt16(bytes, TelemetryProtocol.ErrorCodeOffset);

            packet = new TelemetryPacket
            {
                PacketNo = ReadUInt32(bytes, TelemetryProtocol.PacketNoOffset),
                SatelliteStatus = status,
                SatelliteStatusString = TelemetryProtocol.GetSatelliteStatusText(status),
                ErrorCode = errorCode,
                ErrorCodeString = TelemetryProtocol.GetErrorCodeText(errorCode),
                SentDate = sentDate,
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
                TeamNo = ReadUInt32(bytes, TelemetryProtocol.TeamNoOffset)
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

        private static bool TryReadRtcDate(
            ReadOnlySpan<byte> bytes,
            out DateTime date,
            out string errorMessage)
        {
            int year = 2000 + bytes[TelemetryProtocol.RtcYearOffset];
            int month = bytes[TelemetryProtocol.RtcMonthOffset];
            int day = bytes[TelemetryProtocol.RtcDayOffset];
            int hour = bytes[TelemetryProtocol.RtcHourOffset];
            int minute = bytes[TelemetryProtocol.RtcMinuteOffset];
            int second = bytes[TelemetryProtocol.RtcSecondOffset];

            try
            {
                date = new DateTime(
                    year,
                    month,
                    day,
                    hour,
                    minute,
                    second,
                    DateTimeKind.Unspecified);
                errorMessage = string.Empty;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                date = default;
                errorMessage = $"RTC değeri geçersiz: {year:D4}-{month:D2}-{day:D2} {hour:D2}:{minute:D2}:{second:D2}.";
                return false;
            }
        }
    }
}
