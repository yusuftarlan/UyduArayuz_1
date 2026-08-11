namespace UyduArayuz_1.Models
{
    public class TelemetryPacket
    {
        public uint PacketNo { get; set; }           // <PAKET NUMARASI> [cite: 353]
        public int SatelliteStatus { get; set; }     // <UYDU STATÜSÜ> [cite: 358]
        public string SatelliteStatusString { get; set; } = string.Empty;
        public int ErrorCode { get; set; }
        public string ErrorCodeString { get; set; } = string.Empty;
        public string SentDate { get; set; } = string.Empty;
        public float Pressure { get; set; }
        public float Height { get; set; }
        public float LandingSpeed { get; set; }
        public float Tempreture { get; set; }
        public float BatteryVoltage { get; set; }
        public double GpsLatitude { get; set; }
        public double GpsLongitude { get; set; }
        public double GpsAltitude { get; set; }
        public float Pitch { get; set; }
        public float Roll { get; set; }
        public float Yaw { get; set; }
        public string TaskCode { get; set; } = string.Empty;
        public uint TeamNo { get; set; }
    }
}
