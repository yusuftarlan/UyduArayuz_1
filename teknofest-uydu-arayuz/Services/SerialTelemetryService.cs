using System;
using System.IO.Ports;
using TeknofestUyduArayuz.Models;

namespace TeknofestUyduArayuz.Services
{
    public sealed class SerialTelemetryService : IDisposable
    {
        private const string CommandPortName = "COM6";

        private SerialPort? _serialPort;
        private readonly object _serialPortWriteLock = new();
        private CancellationTokenSource? _cancellationTokenSource;

        private Task? _producerTask;
        private readonly IApplicationLogger _loggerService;
        private readonly TelemetryFrameProcessor _frameProcessor;
        public event EventHandler<TelemetryPacket>? OnTelemetryReceived;
        public event EventHandler? OnConnectionEnded;

        public SerialTelemetryService(
            IApplicationLogger loggerService,
            ITelemetryRecorder? telemetryRecorder)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _frameProcessor = new TelemetryFrameProcessor(
                loggerService,
                telemetryRecorder);
        }

        public void Start(string portName, int baudRate)
        {
            SerialDiagnostics.Write(
                $"Başlatma istendi. Port={portName}, BaudHızı={baudRate}, " +
                $"AlgılananPortlar=[{string.Join(", ", SerialPort.GetPortNames())}]");

            if (_cancellationTokenSource != null)
            {
                SerialDiagnostics.Write("Etkin bir seri port oturumu var; önce bu oturum durduruluyor.");
                Stop();
            }

            SerialPort serialPort = CreateSerialPort(portName, baudRate);
            OpenSerialPort(serialPort);

            var cancellationTokenSource = new CancellationTokenSource();
            _frameProcessor.Reset();

            _serialPort = serialPort;
            _cancellationTokenSource = cancellationTokenSource;

            try
            {
                _producerTask = Task.Factory.StartNew(
                    () => ProducerLoop(
                        serialPort,
                        cancellationTokenSource.Token),
                    cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                SerialDiagnostics.Write(
                    "Seri port açıldı ve üretici görevi başlatıldı.");
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Seri port çalışma görevi başlatılamadı", ex);
                _loggerService.AddLog(
                    $"Seri port çalışma görevi başlatılamadı: {ex.Message}",
                    "ERROR");
                cancellationTokenSource.Cancel();
                serialPort.Dispose();
                cancellationTokenSource.Dispose();
                ClearSessionReferences();
                throw;
            }
        }

        private static SerialPort CreateSerialPort(string portName, int baudRate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(portName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baudRate);

            var serialPort = new SerialPort
            {
                BaudRate = baudRate,
                PortName = portName,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };

            SerialDiagnostics.Write(
                $"Port yapılandırıldı. VeriBitleri={serialPort.DataBits}, " +
                $"Parite={serialPort.Parity}, DurdurmaBitleri={serialPort.StopBits}, " +
                $"AkışKontrolü={serialPort.Handshake}, DTR={serialPort.DtrEnable}, " +
                $"RTS={serialPort.RtsEnable}, OkumaZamanAşımı={serialPort.ReadTimeout}ms, " +
                $"BeklenenÇerçeveUzunluğu={TelemetryProtocol.PacketLength} bayt.");
            return serialPort;
        }

        private void OpenSerialPort(SerialPort serialPort)
        {
            try
            {
                SerialDiagnostics.Write($"{serialPort.PortName} açılıyor...");
                serialPort.Open();
                SerialDiagnostics.Write(
                    $"Port başarıyla açıldı. Açık={serialPort.IsOpen}, " +
                    $"OkunacakBayt={serialPort.BytesToRead}, " +
                    $"Okunabilir={serialPort.BaseStream.CanRead}.");
                serialPort.DiscardInBuffer();
                SerialDiagnostics.Write("Giriş tamponu temizlendi; bloklayan okumalar başlatılıyor.");
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Seri port açılamadı", ex);
                _loggerService.AddLog(
                    $"Bağlantı kurulamadı: {serialPort.PortName} - {ex.Message}",
                    "ERROR");
                serialPort.Dispose();
                throw;
            }
        }

        private void ProducerLoop(
            SerialPort serialPort,
            CancellationToken cancellationToken)
        {
            byte[] readBuffer = new byte[256];
            long totalBytesRead = 0;
            int timeoutCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!serialPort.IsOpen)
                    {
                        SerialDiagnostics.Write("Okuma döngüsü portun kapalı olduğunu algıladı; üretici döngüsünden çıkılıyor.");
                        break;
                    }

                    int queuedBytes = serialPort.BytesToRead;
                    if (queuedBytes > 0)
                    {
                        SerialDiagnostics.Write($"Sürücü, Read() öncesinde kuyrukta {queuedBytes} bayt olduğunu bildiriyor.");
                    }

                    int bytesRead = serialPort.Read(readBuffer, 0, readBuffer.Length);
                    timeoutCount = 0;
                    totalBytesRead += bytesRead;

                    IReadOnlyList<TelemetryPacket> packets = _frameProcessor.Process(
                        readBuffer,
                        bytesRead,
                        totalBytesRead);
                    foreach (TelemetryPacket packet in packets)
                    {
                        SerialDiagnostics.Write(
                            $"{packet.PacketNo} numaralı paket alındı; telemetri olayı tetikleniyor.");
                        OnTelemetryReceived?.Invoke(this, packet);
                    }
                }
                catch (TimeoutException)
                {
                    timeoutCount++;
                    SerialDiagnostics.Write(
                        $"Okuma zaman aşımı #{timeoutCount}: {serialPort.ReadTimeout}ms içinde bayt gelmedi. " +
                        $"Açık={serialPort.IsOpen}, OkunacakBayt={serialPort.BytesToRead}, ToplamBayt={totalBytesRead}.");
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    SerialDiagnostics.Write("Bloklayan seri port okuması normal kapatma sırasında iptal edildi.");
                    break;
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    SerialDiagnostics.WriteException(
                        "Seri port okuması normal kapatma sırasında sona erdi",
                        ex);
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    SerialDiagnostics.WriteException("Seri port işlemi başarısız oldu", ex);
                    _loggerService.AddLog($"Seri port işlem hatası: {ex.Message}", "ERROR");
                    break;
                }
                catch (Exception ex)
                {
                    SerialDiagnostics.WriteException("Beklenmeyen üretici hatası", ex);
                    _loggerService.AddLog($"Üretici hatası: {ex.Message}", "ERROR");
                    break;
                }
            }

            SerialDiagnostics.Write($"Üretici döngüsü tamamlandı. OkunanToplamBayt={totalBytesRead}.");
            if (!cancellationToken.IsCancellationRequested)
            {
                OnConnectionEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SendCommand(byte commandValue)
        {
            SendCommand([commandValue, 0x00, 0x00, 0x00]);
        }

        public void SendCommand(byte[] commandBuffer)
        {
            ArgumentNullException.ThrowIfNull(commandBuffer);
            if (commandBuffer.Length != 4)
            {
                throw new ArgumentException("Komut paketi tam olarak 4 bayt olmalıdır.", nameof(commandBuffer));
            }

            lock (_serialPortWriteLock)
            {
                SerialPort serialPort = _serialPort
                    ?? throw new InvalidOperationException("COM6 portu açık değil.");

                if (!serialPort.IsOpen)
                {
                    throw new InvalidOperationException("COM6 portu açık değil.");
                }

                if (!string.Equals(
                    serialPort.PortName,
                    CommandPortName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Komutlar yalnızca COM6 üzerinden gönderilebilir; açık port {serialPort.PortName}.");
                }

                serialPort.Write(commandBuffer, 0, commandBuffer.Length);
            }

            SerialDiagnostics.Write(
                $"Komut {CommandPortName} portuna gönderildi. " +
                $"Baytlar={BitConverter.ToString(commandBuffer)}, Uzunluk={commandBuffer.Length}.");
        }

        public void Stop()
        {
            if (_cancellationTokenSource == null)
            {
                SerialDiagnostics.Write("Durdurma istendi ancak etkin bir seri port oturumu yok.");
                return;
            }

            SerialDiagnostics.Write("Durdurma istendi; seri port çalışma görevi iptal ediliyor.");
            SerialPort? serialPort = _serialPort;
            CancellationTokenSource cancellationTokenSource = _cancellationTokenSource;
            cancellationTokenSource.Cancel();

            // SerialPort.Read iptali doğrudan desteklemez; portu kapatmak bloklayan
            // okumayı sonlandırır. Üretici görevi ancak bundan sonra beklenmelidir.
            try
            {
                if (serialPort?.IsOpen == true)
                {
                    SerialDiagnostics.Write($"{serialPort.PortName} kapatılıyor.");
                    lock (_serialPortWriteLock)
                    {
                        serialPort.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Seri port kapatılamadı", ex);
                _loggerService.AddLog($"Seri port kapatma hatası: {ex.Message}", "ERROR");
            }

            try
            {
                (_producerTask ?? Task.CompletedTask).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Cancel ile başlayan beklenen kapanış yolu.
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Üretici görevi kapatılamadı", ex);
                _loggerService.AddLog($"Üretici görevi kapatma hatası: {ex.Message}", "ERROR");
            }

            lock (_serialPortWriteLock)
            {
                serialPort?.Dispose();
            }

            cancellationTokenSource.Dispose();
            ClearSessionReferences();
            SerialDiagnostics.Write("Seri port oturumu durduruldu ve kaynakları serbest bırakıldı.");
        }

        private void ClearSessionReferences()
        {
            _serialPort = null;
            _cancellationTokenSource = null;
            _producerTask = null;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
