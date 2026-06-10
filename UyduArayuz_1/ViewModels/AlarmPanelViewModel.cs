using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;

namespace UyduArayuz_1.ViewModels
{
    public class AlarmPanelViewModel : INotifyPropertyChanged
    {
        // Renk Tanımlamaları (Yeşil = Sağlam, Kırmızı = Hata)
        private readonly SolidColorBrush _ledOn = new SolidColorBrush(Color.FromRgb(46, 204, 113));  // #2ECC71
        private readonly SolidColorBrush _ledError = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // #E74C3C

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private SolidColorBrush _landingSpeedErrorLed;
        public SolidColorBrush LandingSpeedErrorLed
        {
            get => _landingSpeedErrorLed;
            set { _landingSpeedErrorLed = value; OnPropertyChanged(); }
        }

        private SolidColorBrush _gpsErrorLed;
        public SolidColorBrush GpsErrorLed
        {
            get => _gpsErrorLed;
            set { _gpsErrorLed = value; OnPropertyChanged(); }
        }

        private SolidColorBrush _separationErrorLed;
        public SolidColorBrush SeperationErrorLed
        {
            get => _separationErrorLed;
            set { _separationErrorLed = value; OnPropertyChanged(); }
        }
        private SolidColorBrush _emergencyParachuteErrorLed;
        public SolidColorBrush EmergencyParachuteErrorLed
        {
            get => _emergencyParachuteErrorLed;
            set { _emergencyParachuteErrorLed = value; OnPropertyChanged(); }
        }
        
        public AlarmPanelViewModel()
        {
            // Başlangıçta tüm LED'ler sağlıklı (yeşil) olarak ayarlanır
            LandingSpeedErrorLed = _ledOn;
            GpsErrorLed = _ledOn;
            SeperationErrorLed = _ledOn;
            EmergencyParachuteErrorLed = _ledOn;
        }

        // Telemetriden gelen "0000" şeklindeki kodu çözen metot
        public void UpdateAlarms(int ErrorCode)
        {
            if (ErrorCode < 0 || ErrorCode > 15) return;

            else if (ErrorCode == 0)
            {
                LandingSpeedErrorLed = _ledOn;
                GpsErrorLed = _ledOn;
                SeperationErrorLed = _ledOn;
                EmergencyParachuteErrorLed= _ledOn;

            }

            // Karakter '1' ise Kırmızı (Hata), '0' ise Yeşil (Sağlam)
            LandingSpeedErrorLed       = ((ErrorCode & (1 << 0)) != 0) ? _ledError : _ledOn;
            GpsErrorLed                = ((ErrorCode & (1 << 1)) != 0) ? _ledError : _ledOn;
            SeperationErrorLed         = ((ErrorCode & (1 << 2)) != 0) ? _ledError : _ledOn;
            EmergencyParachuteErrorLed = ((ErrorCode & (1 << 3)) != 0) ? _ledError : _ledOn;


        }  

    }
}
