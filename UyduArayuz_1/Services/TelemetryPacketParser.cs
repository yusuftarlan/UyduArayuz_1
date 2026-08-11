using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using UyduArayuz_1.Models;

namespace UyduArayuz_1.Services
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

    public class TelemetryPacketParser
    {
        public bool TryParse(byte[]? frame, [NotNullWhen(true)] out TelemetryPacket? packet, out string errorMessage)
        {
            return TryParse(frame, out packet, out _, out errorMessage);
        }

        public bool TryParse(
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
                errorMessage = "Frame cannot be null.";
                return false;
            }

            if (frame.Length != TelemetryProtocol.PacketLength)
            {
                failureReason = TelemetryParseFailureReason.InvalidLength;
                errorMessage = $"Frame length is {frame.Length}; expected {TelemetryProtocol.PacketLength}.";
                return false;
            }

            ReadOnlySpan<byte> bytes = frame;

            if (ReadUInt32(bytes, TelemetryProtocol.StartOffset) != TelemetryProtocol.StartMarker)
            {
                failureReason = TelemetryParseFailureReason.InvalidStartMarker;
                errorMessage = "Frame start marker is invalid.";
                return false;
            }

            if (ReadUInt32(bytes, TelemetryProtocol.EndOffset) != TelemetryProtocol.EndMarker)
            {
                failureReason = TelemetryParseFailureReason.InvalidEndMarker;
                errorMessage = "Frame end marker is invalid.";
                return false;
            }

            uint receivedCrc = ReadUInt32(bytes, TelemetryProtocol.CrcOffset);
            uint calculatedCrc = TelemetryCrc32.Compute(bytes.Slice(
                TelemetryProtocol.CrcStartOffset,
                TelemetryProtocol.CrcPayloadLength));

            //crc32 kontrolü kapatılsın
            /*if (receivedCrc != calculatedCrc)
            {
                failureReason = TelemetryParseFailureReason.CrcMismatch;
                errorMessage = $"CRC mismatch. Received 0x{receivedCrc:X8}, calculated 0x{calculatedCrc:X8}.";
                return false;
            }*/

            if (!TryFormatRtcDate(bytes, out string sentDate, out errorMessage))
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

        private static bool TryFormatRtcDate(
            ReadOnlySpan<byte> bytes,
            out string formattedDate,
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
                var date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
                formattedDate = date.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                errorMessage = string.Empty;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                formattedDate = string.Empty;
                errorMessage = $"RTC value is invalid: {year:D4}-{month:D2}-{day:D2} {hour:D2}:{minute:D2}:{second:D2}.";
                return false;
            }
        }
    }
}
