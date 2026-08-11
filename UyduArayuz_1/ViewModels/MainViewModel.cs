using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using UyduArayuz_1.Models;
using UyduArayuz_1.Services;

namespace UyduArayuz_1.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private const int GpsErrorMask = 1 << 1;
        private const byte SeparationCommandByte = 0x00;
        private const byte OpenParachuteCommandByte = 0x01;
        private const byte MissionCodePrefix = 0b1010_1010;

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
        private readonly TelemetryCsvRecorder? _csvRecorder;
        private readonly AlarmSoundService _alarmSoundService;

        public LoggerService LoggerService { get; }

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

            string recordsDirectory = ResolveTelemetryRecordsDirectory();
            ApplicationLogRecorder? applicationLogRecorder = null;
            string? applicationLogInitializationError = null;

            try
            {
                applicationLogRecorder = new ApplicationLogRecorder(recordsDirectory);
            }
            catch (Exception ex)
            {
                applicationLogInitializationError = ex.Message;
                Debug.WriteLine($"Application log initialization error: {ex.Message}");
            }

            LoggerService = new LoggerService(applicationLogRecorder);
            if (applicationLogRecorder != null)
            {
                SerialDiagnostics.Write($"WARN/ERROR TXT output path: {applicationLogRecorder.FilePath}");
            }
            else
            {
                LoggerService.AddLog(
                    $"WARN/ERROR TXT initialization error: {applicationLogInitializationError}",
                    "ERROR");
            }

            try
            {
                _csvRecorder = new TelemetryCsvRecorder(recordsDirectory);
                SerialDiagnostics.Write($"Telemetry CSV output path: {_csvRecorder.FilePath}");
            }
            catch (Exception ex)
            {
                _csvRecorder = null;
                Debug.WriteLine($"Telemetry CSV initialization error: {ex.Message}");
                LoggerService.AddLog($"Telemetry CSV initialization error: {ex.Message}", "ERROR");
            }

            _telemetryService = new SerialTelemetryService(LoggerService, _csvRecorder);
            _alarmSoundService = new AlarmSoundService();

            TelemetryHistory = new ObservableCollection<TelemetryPacket>();

            HeaderControlViewModel = new HeaderControlViewModel();

            AlarmPanelViewModel = new AlarmPanelViewModel();

            GraphViewModel = new GraphDashboardViewModel();

            AttitudeViewModel = new AttitudeViewModel();

            MapViewControl = new MapViewModel();

            // Telsizi dinle
            _telemetryService.OnTelemetryReceived += TelemetryService_OnTelemetryReceived;
            _telemetryService.OnConnectionEnded += TelemetryService_OnConnectionEnded;

            // Haberleşmeyi başlat (COM3 yerine kendi portunu yazmayı unutma)

            HeaderControlViewModel.ConnectRequested = StartTelemetry;

            // Header "Kes" dediğinde benim "StopTelemetry" metodumu çalıştır.
            HeaderControlViewModel.DisconnectRequested = StopTelemetry;

            HeaderControlViewModel.OpenParachuteRequested = SendOpenParachuteCommand;
            HeaderControlViewModel.SeparationRequested = SendSeparationCommand;
            HeaderControlViewModel.SendMissionCodeRequested = SendMissionCode;

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
            // Alarm LED'leri son telemetri durumunu gösterebilir; bağlantı yokken
            // sesin devam etmemesi için ses yaşam döngüsünü ayrıca durduruyoruz.
            _alarmSoundService.Stop();
            _telemetryService.Stop();
            HeaderControlViewModel.IsConnected = false;
            HeaderControlViewModel.SystemStatus = "BAĞLANTI KESİLDİ - SİSTEM HAZIR";
            HeaderControlViewModel.SystemStatusColor = HeaderControlViewModel._orangeMessageColor;

        }

        private void SendOpenParachuteCommand()
        {
            SendCommand(OpenParachuteCommandByte, "Paraşüt açma");
        }

        private void SendSeparationCommand()
        {
            SendCommand(SeparationCommandByte, "Ayrılma");
        }

        private void SendCommand(byte commandByte, string commandName)
        {
            try
            {
                _telemetryService.SendCommand(commandByte);
                LoggerService.AddLog(
                    $"{commandName} komutu COM6 portuna 4 bayt olarak gönderildi " +
                    $"(0x{commandByte:X2} 0x00 0x00 0x00).",
                    "WARN");
            }
            catch (Exception ex)
            {
                LoggerService.AddLog(
                    $"{commandName} komutu gönderilemedi: {ex.Message}",
                    "ERROR");
            }
        }

        private void SendMissionCode(string missionCode)
        {
            if (missionCode.Length != 3 || missionCode.Any(character => character is not ('0' or '1' or '2')))
            {
                LoggerService.AddLog(
                    "Görev kodu tam 3 haneli olmalı ve yalnızca 0, 1 veya 2 içermelidir.",
                    "ERROR");
                return;
            }

            byte[] commandBuffer =
            [
                MissionCodePrefix,
                (byte)(missionCode[0] - '0'),
                (byte)(missionCode[1] - '0'),
                (byte)(missionCode[2] - '0')
            ];

            try
            {
                _telemetryService.SendCommand(commandBuffer);
                LoggerService.AddLog(
                    $"Görev kodu {missionCode} COM6 portuna gönderildi " +
                    $"({BitConverter.ToString(commandBuffer)}).",
                    "WARN",
                    writeToFile: true);
            }
            catch (Exception ex)
            {
                LoggerService.AddLog(
                    $"Görev kodu gönderilemedi: {ex.Message}",
                    "ERROR");
            }
        }


        private void TelemetryService_OnTelemetryReceived(object sender, TelemetryPacket e)
        {
           
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                bool isAlarmActive = AlarmPanelViewModel.UpdateAlarms(e.ErrorCode);
                _alarmSoundService.SetAlarmState(isAlarmActive);
                GraphViewModel.UpdateGraphs(e); // Grafikler güncellensin
                AttitudeViewModel.UpdateAttitude(e.Yaw, e.Pitch, e.Roll); // 3d gösterim güncellensin

                if ((e.ErrorCode & GpsErrorMask) == 0)
                {
                    MapViewControl.UpdatePosition(e.GpsLatitude, e.GpsLongitude);
                }

                CurrentPacket = e; // Instant panel güncellensin
                TelemetryHistory.Add(e); // Geçmişe ekle

                // Opsiyonel: Tablo çok şişmesin diye sadece son 100 veriyi tutabiliriz
                if (TelemetryHistory.Count > 100)
                {
                    TelemetryHistory.RemoveAt(0); // En eskiyi sil
                }
            });
        }

        private void TelemetryService_OnConnectionEnded(object? sender, EventArgs e)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _alarmSoundService.Stop();
            });
        }

        private static string ResolveTelemetryRecordsDirectory()
        {
            string[] searchRoots = [Environment.CurrentDirectory, AppContext.BaseDirectory];
            foreach (string searchRoot in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DirectoryInfo? directory = new DirectoryInfo(searchRoot);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "UyduArayuz_1.csproj")))
                    {
                        return Path.Combine(directory.FullName, "telemetry-records");
                    }

                    string nestedProjectDirectory = Path.Combine(directory.FullName, "UyduArayuz_1");
                    if (File.Exists(Path.Combine(nestedProjectDirectory, "UyduArayuz_1.csproj")))
                    {
                        return Path.Combine(nestedProjectDirectory, "telemetry-records");
                    }

                    directory = directory.Parent;
                }
            }

            // Published deployments do not contain the project file. In that case,
            // keep recordings beside the application rather than depending on CWD.
            return Path.Combine(AppContext.BaseDirectory, "telemetry-records");
        }

        public void Dispose()
        {
            _telemetryService.OnTelemetryReceived -= TelemetryService_OnTelemetryReceived;
            _telemetryService.OnConnectionEnded -= TelemetryService_OnConnectionEnded;

            _alarmSoundService.Dispose();

            try
            {
                _telemetryService.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Telemetry service shutdown error: {ex.Message}");
                LoggerService.AddLog($"Telemetry service shutdown error: {ex.Message}", "ERROR");
            }

            try
            {
                _csvRecorder?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Telemetry CSV shutdown error: {ex.Message}");
                LoggerService.AddLog($"Telemetry CSV shutdown error: {ex.Message}", "ERROR");
            }

            try
            {
                LoggerService.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Application TXT log shutdown error: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}
