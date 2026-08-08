using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using UyduArayuz_1.Models;
using UyduArayuz_1.Services;

namespace UyduArayuz_1.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        // ====================================================================
        // 1. ZİL ALTYAPISI (INotifyPropertyChanged)
        // ====================================================================
        public event PropertyChangedEventHandler PropertyChanged;

        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ====================================================================
        // 2. SERVİS (Telsiz)
        // ====================================================================
        private readonly SerialTelemetryService _telemetryService;

        private LoggerService _loggerService;

        // ====================================================================
        // 3. ARAYÜZE GİDECEK TEK KUTU (Eski 17 değişkenin yerini alan yapı)
        // ====================================================================
        private TelemetryPacket _current_Packet;
        public TelemetryPacket CurrentPacket
        {
            get => _current_Packet;
            set
            {
                _current_Packet = value;

                // Mutfaktaki zile tüm paket için SADECE 1 KEZ basıyoruz!
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TelemetryPacket> TelemetryHistory { get; set; }

        public HeaderControlViewModel HeaderControlViewModel { get; set; }
        public AlarmPanelViewModel AlarmPanelViewModel { get; set; }

        public GraphDashboardViewModel GraphViewModel { get; set; }

        public AttitudeViewModel AttitudeViewModel { get; set; }

        public MapViewModel MapViewControl { get; set; }

        public MainViewModel()
        {
            // Kutunun başlangıçta boş görünmemesi için sahte(dummy) bir boş kutu koyabiliriz
            CurrentPacket = new TelemetryPacket();

            // Servisi ayağa kaldır
            _telemetryService = new SerialTelemetryService();
            _loggerService = new LoggerService();

            TelemetryHistory = new ObservableCollection<TelemetryPacket>();

            HeaderControlViewModel = new HeaderControlViewModel();

            AlarmPanelViewModel = new AlarmPanelViewModel();

            GraphViewModel = new GraphDashboardViewModel();

            AttitudeViewModel = new AttitudeViewModel();

            MapViewControl = new MapViewModel();

            // Telsizi dinle
            _telemetryService.OnTelemetryReceived += TelemetryService_OnTelemetryReceived;

            // Haberleşmeyi başlat (COM3 yerine kendi portunu yazmayı unutma)

            HeaderControlViewModel.ConnectRequested = StartTelemetry;

            // Header "Kes" dediğinde benim "StopTelemetry" metodumu çalıştır.
            HeaderControlViewModel.DisconnectRequested = StopTelemetry;



        }


        private void StartTelemetry(string port, int baudRate)
        {
            try
            {
                // Servisi MainViewModel başlatıyor
                _telemetryService.Start(port, baudRate);

                // Başarılı olursa Header'ın arayüzünü kilitle (Yeşil ışık)
                HeaderControlViewModel.IsConnected = true;
                HeaderControlViewModel.SystemStatus = $"PORT DİNLENİYOR - {port} ({baudRate})";
                HeaderControlViewModel.SystemStatusColor = HeaderControlViewModel._greenMessageColor;
            }
            catch (Exception ex)
            {
                // Eğer COM port kullanımda falansa program çökmez, buraya düşer.
                HeaderControlViewModel.IsConnected = false;
                HeaderControlViewModel.SystemStatus = $"BAĞLANTI HATASI: {ex.Message}";
            }

        }
        private void StopTelemetry()
        {
            _telemetryService.Stop();
            HeaderControlViewModel.IsConnected = false;
            HeaderControlViewModel.SystemStatus = "BAĞLANTI KESİLDİ - SİSTEM HAZIR";
            HeaderControlViewModel.SystemStatusColor = HeaderControlViewModel._orangeMessageColor;

        }


        private void TelemetryService_OnTelemetryReceived(object sender, TelemetryPacket e)
        {
           
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AlarmPanelViewModel.UpdateAlarms(e.ErrorCode); // Alarm paneli güncellensin
                GraphViewModel.UpdateGraphs(e); // Grafikler güncellensin
                AttitudeViewModel.UpdateAttitude(e.Yaw, e.Pitch, e.Roll); // 3d gösterim güncellensin
                CurrentPacket = e; // Instant panel güncellensin
                TelemetryHistory.Add(e); // Geçmişe ekle

                // Opsiyonel: Tablo çok şişmesin diye sadece son 100 veriyi tutabiliriz
                if (TelemetryHistory.Count > 100)
                {
                    TelemetryHistory.RemoveAt(0); // En eskiyi sil
                }
            });
        }
    }
}