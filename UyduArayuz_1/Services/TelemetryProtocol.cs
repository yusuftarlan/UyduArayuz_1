namespace UyduArayuz_1.Services
{
    public static class TelemetryProtocol
    {
        public const uint StartMarker = 0x3C3C3C3C; // "<<<<"
        public const uint EndMarker = 0x3E3E3E3E;   // ">>>>"
        public const byte StartByte = 0x3C;
        public const byte EndByte = 0x3E;

        public const int PacketLength = 80;

        public const int StartOffset = 0; // uint32_t, 4 bytes
        public const int PacketNoOffset = 4; // uint32_t, 4 bytes
        public const int SatelliteStatusOffset = 8; // uint16_t, 2 bytes
        public const int ErrorCodeOffset = 10; // uint16_t, 2 bytes
        public const int RtcYearOffset = 12; // uint8_t
        public const int RtcMonthOffset = 13; // uint8_t
        public const int RtcDayOffset = 14; // uint8_t
        public const int RtcHourOffset = 15; // uint8_t
        public const int RtcMinuteOffset = 16; // uint8_t
        public const int RtcSecondOffset = 17; // uint8_t
        public const int PressureOffset = 18; // float, 4 bytes
        public const int HeightOffset = 22; // float, 4 bytes
        public const int LandingSpeedOffset = 26; // float, 4 bytes
        public const int TemperatureOffset = 30; // float, 4 bytes
        public const int BatteryVoltageOffset = 34; // float, 4 bytes
        public const int GpsLatitudeOffset = 38; // float, 4 bytes
        public const int GpsLongitudeOffset = 42; // float, 4 bytes
        public const int GpsAltitudeOffset = 46; // float, 4 bytes
        public const int PitchOffset = 50; // float, 4 bytes
        public const int RollOffset = 54; // float, 4 bytes
        public const int YawOffset = 58; // float, 4 bytes
        public const int TaskCodeOffset = 62; // uint8_t[6]
        public const int TeamNoOffset = 68; // uint32_t, 4 bytes
        public const int CrcOffset = 72; // uint32_t, 4 bytes
        public const int EndOffset = 76; // uint32_t, 4 bytes

        public const int MarkerLength = 4;
        public const int TaskCodeLength = 6;
        public const int CrcLength = 4;
        // The STM32 CRC peripheral processes packet_start through team_number
        // as 32-bit little-endian words. The CRC and end marker are excluded.
        public const int CrcStartOffset = StartOffset;
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

    }
}
