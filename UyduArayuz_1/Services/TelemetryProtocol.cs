using System;

namespace UyduArayuz_1.Services
{
    public static class TelemetryProtocol
    {
        public const byte StartByte = 0x3C; // '<<<<'
        public const byte EndByte = 0x3E;   // '>>>>'

        public const int PacketLength = 64;

        public const int StartOffset = 0; // 1 byte 
        public const int PacketNoOffset = 1; // 2 bytes
        public const int SatelliteStatusOffset = 3; // 1 byte
        public const int ErrorCodeOffset = 4; // 1 byte
        public const int SentTimeOffset = 5; // 4 bytes
        public const int PressureOffset = 9;  // 4 bytes  
        public const int HeightOffset = 13; // 4 bytes
        public const int LandingSpeedOffset = 17; // 4 bytes
        public const int TemperatureOffset = 21; // 4 bytes
        public const int BatteryVoltageOffset = 25; // 4 bytes
        public const int GpsLatitudeOffset = 29; // 4 bytes
        public const int GpsLongitudeOffset = 33; // 4 bytes
        public const int GpsAltitudeOffset = 37; // 4 bytes
        public const int PitchOffset = 41; // 4 bytes
        public const int RollOffset = 45; // 4 bytes
        public const int YawOffset = 49; // 4 bytes
        public const int TaskCodeOffset = 53; // 6 bytes
        public const int TeamNoOffset = 59; // 1 byte
        public const int CrcOffset = 60; // 4 bytes
        public const int EndOffset = 64; // 1 byte

        public const int TaskCodeLength = 6;
        public const int CrcLength = 4;
        public const int CrcStartOffset = PacketNoOffset;
        public const int CrcPayloadLength = CrcOffset - CrcStartOffset;

        public static string GetSatelliteStatusText(int status) => status switch
        {
            0 => "U\u00e7u\u015fa Haz\u0131r",
            1 => "Y\u00fckselme",
            2 => "Model Uydu \u0130ni\u015f",
            3 => "Ayr\u0131lma",
            4 => "G\u00f6rev Y\u00fck\u00fc \u0130ni\u015f",
            5 => "Kurtarma",
            _ => "Bilinmeyen veya Ge\u00e7ersiz Stat\u00fc!"
        };

        public static string GetErrorCodeText(int errorCode) => errorCode switch
        {
            0 => "Problemsiz",
            1 => "\u0130ni\u015f H\u0131z\u0131",
            2 => "GPS",
            3 => "GPS + \u0130ni\u015f H\u0131z\u0131",
            4 => "Ayr\u0131lma",
            5 => "Ayr\u0131lma+ \u0130ni\u015f H\u0131z\u0131 ",
            6 => "Ayr\u0131lma + GPS",
            7 => "\u0130ni\u015f H\u0131z\u0131 + GPS + Ayr\u0131lma",
            8 => "Acil Para\u015f\u00fct",
            9 => "Acil Para\u015f\u00fct + \u0130ni\u015f H\u0131z\u0131",
            10 => "Acil Para\u015f\u00fct + GPS",
            11 => "Acil Para\u015f\u00fct + GPS + \u0130ni\u015f H\u0131z\u0131",
            12 => "Acil Para\u015f\u00fct + Ayr\u0131lma",
            13 => "Acil Para\u015f\u00fct + Ayr\u0131lma + \u0130ni\u015f H\u0131z\u0131",
            14 => "Acil Para\u015f\u00fct + Ayr\u0131lma + GPS",
            15 => "Acil Para\u015f\u00fct + Ayr\u0131lma + GPS + \u0130ni\u015f H\u0131z\u0131",
            _ => "Bilinmeyen veya Ge\u00e7ersiz Stat\u00fc!"
        };

        public static string FormatUnixTimestamp(uint unixTimestamp)
        {
            return DateTimeOffset
                .FromUnixTimeSeconds(unixTimestamp)
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm:ss");
        }
    }
}
