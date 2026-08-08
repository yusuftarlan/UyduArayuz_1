using System;
using System.Collections.Generic;
using System.Text;

namespace UyduArayuz_1.Models
{
    public class TelemetryPacket
    {
        public int PacketNo { get; set; }           // <PAKET NUMARASI> [cite: 353]
        public int SatelliteStatus { get; set; }        // <UYDU STATÜSÜ> [cite: 358]
        public string SatelliteStatusString { get; set; } = string.Empty; // <UYDU STATÜSÜ Kelime>
        public int ErrorCode { get; set; }       // <HATA KODU> 
        public string ErrorCodeString { get; set; } = string.Empty;      // <HATA KODU Kelime> 
        public string SentDate { get; set; } = string.Empty; // <GÖNDERME SAATİ> [cite: 367]
        public float Pressure { get; set; }          // <BASINÇ> (Pascal) [cite: 368]
        public float Height { get; set; }       // <YÜKSEKLİK> (Metre) [cite: 369]
        public float LandingSpeed { get; set; }        // <İNİŞ HIZI> (m/s) [cite: 370]
        public float Tempreture { get; set; }        // <SICAKLIK> (°C) [cite: 371]
        public float BatteryVoltage { get; set; }     // <PİL GERİLİMİ> (V) [cite: 372]
        public double GpsLatitude { get; set; }    // <GPS LATITUDE> [cite: 373]
        public double GpsLongitude { get; set; }   // <GPS LONGITUDE> [cite: 374]
        public double GpsAltitude { get; set; }    // <GPS ALTITUDE> [cite: 375]
        public float Pitch { get; set; }           // <PITCH> (Derece) [cite: 376]
        public float Roll { get; set; }            // <ROLL> (Derece) [cite: 377]
        public float Yaw { get; set; }             // <YAW> (Derece) [cite: 378]
        public string TaskCode { get; set; } = string.Empty;      // <RHRHRH> (Bonus Görev) [cite: 379]
        public int TeamNo { get; set; }           // <TAKIM NO> [cite: 381]
    }
}
