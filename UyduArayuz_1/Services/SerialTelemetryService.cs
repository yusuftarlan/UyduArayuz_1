using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports; // Seri port kütüphanesi
using System.Net.Sockets;
using System.Printing;
using System.Text;
using System.Threading.Channels;
using UyduArayuz_1.Models; // Az önce oluşturduğumuz modelleri kullanmak için
namespace UyduArayuz_1.Services
{
    public class SerialTelemetryService
    {
        private SerialPort _serialPort;
        private CancellationTokenSource _cancellationTokenSource;

        private  Channel<TelemetryPacket> _uiChannel;
        private  Channel<TelemetryPacket> _logChannel;

        public event EventHandler<TelemetryPacket> OnTelemetryReceived; // Veri geldiğinde UI'ı tetikleyecek olay (Event)


        public SerialTelemetryService()
        {
            _serialPort = new SerialPort();
            
        }

        public void Start(string portName, int baudRate)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                Stop();
            }

            // Kanalları her Start işleminde yeniden oluşturuyoruz ki Stop'tan sonra tekrar çalışabilsin
            _uiChannel = Channel.CreateUnbounded<TelemetryPacket>();
            _logChannel = Channel.CreateUnbounded<TelemetryPacket>();
            _cancellationTokenSource = new CancellationTokenSource();

            _serialPort.BaudRate = baudRate;
            _serialPort.PortName = portName;
            _serialPort.DtrEnable = false;
            _serialPort.RtsEnable = false;
            _serialPort.ReadTimeout = 2000;
            _serialPort.NewLine = "\r\n";

            try
            {
                Task.Factory.StartNew(ProducerLoop, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Run(LoggerConsumerLoop, _cancellationTokenSource.Token);
                Task.Run(UiConsumerLoop, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Seri port açılırken hata: {ex.Message}");
            }
        }

        // =========================================================
        // ÜRETİCİ (PRODUCER): SADECE OKUR VE FIRLATIR
        // =========================================================
        private void ProducerLoop()
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open(); // PORTU BURADA AÇIYORUZ!
                   _serialPort.DiscardInBuffer();
                    Debug.WriteLine($"Seri port {_serialPort.PortName} {_serialPort.BaudRate} hızında açıldı.");
                    LoggerService.Instance.AddLog($"Seri port {_serialPort.PortName} {_serialPort.BaudRate} hızında açıldı.", "INFO");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Seri port açılırken donanımsal hata: {ex.Message}");
                return; // Hata olursa işçiyi iptal et, döngüye girme
            }

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    
                    if (_serialPort.IsOpen)
                    {
                        // Porttan \r\n görene kadar bloğa girer ve bekler (Thread burada uyur, CPU harcamaz)
                        string rawData = _serialPort.ReadLine();

                        TelemetryPacket packet = ParseTelemetry(rawData); // Senin yazdığın parse metodu
                        
                        if (packet != null)
                        {
                            // Veriyi yakaladı, ayrıştırdı ve her iki tüketicinin kuyruğuna asenkron fırlattı!
                            Debug.WriteLine("UI Tüketici: Yeni telemetri paketi geldi.kanallara yazıyorum");
                            _uiChannel.Writer.TryWrite(packet);
                            _logChannel.Writer.TryWrite(packet);
                        }
                        else
                        {
                            Debug.WriteLine($"Geçersiz veri alındı: '{rawData}'");
                           
                        }
                    }
                }
                catch (TimeoutException) { /* Okuma zaman aşımı, döngü devam eder */ }
                catch (Exception ex) { Debug.WriteLine($"Üretici Hatası: {ex.Message}"); }
            }
        }

        // =========================================================
        // TÜKETİCİ 1 (LOGGER): KUYRUKTA VERİ VARSA UYANIR, YAZAR VE UYUR
        // =========================================================
        private async Task LoggerConsumerLoop()
        {
            // Döngü dışı: Dosyayı bir kez açıyoruz.
            // true parametresi append (üzerine ekleme) modunda açar.
            using StreamWriter sw = new StreamWriter("telemetri_log.csv", true);

            await foreach (var packet in _logChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                try
                {
                    // Formatlanmış CSV satırını yaz
                    await sw.WriteLineAsync($"{packet.PacketNo},{packet.Height},{packet.ErrorCode}");
                    await sw.FlushAsync(); // Anında diske yazmak istersen (Güvenlik için önerilir)
                }
                catch (Exception ex) { Debug.WriteLine($"Loglama Hatası: {ex.Message}"); }
            }
        }

        // =========================================================
        // TÜKETİCİ 2 (UI UPDATER): KUYRUKTA VERİ VARSA UYANIR, UI'I TETİKLER
        // =========================================================
        private async Task UiConsumerLoop()
        {
            await foreach (var packet in _uiChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                // Servis (Çırak) WPF'i veya Dispatcher'ı bilmez! 
                // Sadece "Veri geldi" diye olayı (Event) tetikler.
                Debug.WriteLine("UI Tüketici: Yeni telemetri paketi geldi, UI'ı güncellemek için olayı tetikliyorum.");
                OnTelemetryReceived?.Invoke(this, packet);
            }
        }

        private TelemetryPacket ParseTelemetry(string rawData)
        {
            Debug.WriteLine($"Raw: {rawData}");
            
            if (string.IsNullOrWhiteSpace(rawData)) return null; 

            string[] dataSet = rawData.Split(','); // Veriler virgülle ayrılmış olarak geliyor

            if (dataSet.Length != 17) // Beklenen veri sayısı 17'dir
            {
                return null;
            }
            IFormatProvider Format = CultureInfo.InvariantCulture; // Nokta (.) ondalık ayracı olarak kullanılır
            var NewPacket = new TelemetryPacket();
            
  
            if (!int.TryParse(dataSet[0], out int packetNo)) return null;
                NewPacket.PacketNo = packetNo;


            if (!int.TryParse(dataSet[1], out int status)) return null;
            
                    
                NewPacket.SatelliteStatus = status;
                    
                NewPacket.SatelliteStatusString = status switch 
                {

                        0 => "Uçuşa Hazır",
                        1 => "Yükselme",
                        2 => "Model Uydu İniş",
                        3 => "Ayrılma",
                        4 => "Görev Yükü İniş",
                        5 => "Kurtarma",
                        _ => "Bilinmeyen veya Geçersiz Statü!" // "_" işareti default (else) anlamına gelir

                };
            
             
            if (!int.TryParse(dataSet[2], out int errorCode )) return null;
   
                NewPacket.ErrorCode = errorCode;
                
                NewPacket.ErrorCodeString = errorCode switch
                {

                        0 => "Problemsiz",
                        1 => "İniş Hızı",
                        2 => "GPS",
                        3 => "GPS + İniş Hızı",
                        4 => "Ayrılma",
                        5 => "Ayrılma+ İniş Hızı ",
                        6 => "Ayrılma + GPS",
                        7 => "İniş Hızı + GPS + Ayrılma",
                        8 => "Acil Paraşüt",
                        9 => "Acil Paraşüt + İniş Hızı",
                        10 => "Acil Paraşüt + GPS",
                        11 => "Acil Paraşüt + GPS + İniş Hızı",
                        12 => "Acil Paraşüt + Ayrılma",
                        13 => "Acil Paraşüt + Ayrılma + İniş Hızı",
                        14 => "Acil Paraşüt + Ayrılma + GPS",
                        15 => "Acil Paraşüt + Ayrılma + GPS + İniş Hızı",
                        _ => "Bilinmeyen veya Geçersiz Statü!" // "_" işareti default (else) anlamına gelir

                };


            NewPacket.SentDate = dataSet[3];
            if (!float.TryParse(dataSet[4], NumberStyles.Float, Format, out float pressure)) return null;
            NewPacket.Pressure = pressure;
            if (!float.TryParse(dataSet[5], NumberStyles.Float, Format, out float height)) return null;
            NewPacket.Height = height;
            if (!float.TryParse(dataSet[6], NumberStyles.Float, Format, out float landingSpeed)) return null;
            NewPacket.LandingSpeed = landingSpeed;
            if (!float.TryParse(dataSet[7], NumberStyles.Float, Format, out float temperature)) return null;
            NewPacket.Tempreture = temperature;
            if (!float.TryParse(dataSet[8], NumberStyles.Float, Format, out float batteryVoltage)) return null;
            NewPacket.BatteryVoltage = batteryVoltage;
            if (!double.TryParse(dataSet[9], NumberStyles.Float, Format, out double gpsLatitude)) return null;
            NewPacket.GpsLatitude = gpsLatitude;
            if (!double.TryParse(dataSet[10], NumberStyles.Float, Format, out double gpsLongitude)) return null;
            NewPacket.GpsLongitude = gpsLongitude;
            if (!double.TryParse(dataSet[11], NumberStyles.Float, Format, out double gpsAltitude)) return null;
            NewPacket.GpsAltitude = gpsAltitude;
            if(!float.TryParse(dataSet[12], NumberStyles.Float, Format, out float pitch)) return null;
            NewPacket.Pitch = pitch;
            if(!float.TryParse(dataSet[13], NumberStyles.Float, Format, out float roll)) return null;
            NewPacket.Roll = roll;
            if(!float.TryParse(dataSet[14], NumberStyles.Float, Format, out float yaw)) return null;
            NewPacket.Yaw = yaw;

            NewPacket.TaskCode = dataSet[15];
            if (!int.TryParse(dataSet[16], out int teamNo)) return null;
            NewPacket.TeamNo = teamNo;

            LoggerService.Instance.AddLog($"Zaman: {NewPacket.SentDate}");
            return NewPacket;

            
           

        }
        public void Stop()
        {
            _cancellationTokenSource?.Cancel();

            // Kanalların yazıcılarını tamamla, böylece tüketici döngüleri (ReadAllAsync) hata vermeden düzgünce biter
            _uiChannel?.Writer.TryComplete();
            _logChannel?.Writer.TryComplete();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            LoggerService.Instance.AddLog($"Seri port kapatıldı.", "WARN");

            _serialPort?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
