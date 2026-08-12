namespace TeknofestUyduArayuz.Models
{
    public sealed class TelemetryPacket
    {
        public uint PacketNo { get; init; }
        public int SatelliteStatus { get; init; }
        public string SatelliteStatusString { get; init; } = string.Empty;
        public int ErrorCode { get; init; }
        public string ErrorCodeString { get; init; } = string.Empty;
        public DateTime SentDate { get; init; }
        public float Pressure { get; init; }
        public float Height { get; init; }
        public float LandingSpeed { get; init; }
        public float Tempreture { get; init; }
        public float BatteryVoltage { get; init; }
        public float GpsLatitude { get; init; }
        public float GpsLongitude { get; init; }
        public float GpsAltitude { get; init; }
        public float Pitch { get; init; }
        public float Roll { get; init; }
        public float Yaw { get; init; }
        public string TaskCode { get; init; } = string.Empty;
        public uint TeamNo { get; init; }
    }
}
