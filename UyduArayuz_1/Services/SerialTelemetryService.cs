using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading.Channels;
using UyduArayuz_1.Models;

namespace UyduArayuz_1.Services
{
    public class SerialTelemetryService : IDisposable
    {
        private const string CommandPortName = "COM6";

        private SerialPort _serialPort;
        private readonly object _serialPortWriteLock = new();
        private CancellationTokenSource? _cancellationTokenSource;

        private Channel<TelemetryPacket>? _uiChannel;
        private Task? _producerTask;
        private Task? _uiConsumerTask;
        private readonly TelemetryCsvRecorder? _csvRecorder;
        private readonly LoggerService _loggerService;
        private int _recordingFailureReported;

        private readonly TelemetryFrameExtractor _frameExtractor;
        private readonly TelemetryPacketParser _packetParser;

        public event EventHandler<TelemetryPacket>? OnTelemetryReceived;
        public event EventHandler? OnConnectionEnded;

        public SerialTelemetryService(
            LoggerService loggerService,
            TelemetryCsvRecorder? csvRecorder)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _csvRecorder = csvRecorder;
            _serialPort = new SerialPort();
            _frameExtractor = new TelemetryFrameExtractor();
            _packetParser = new TelemetryPacketParser();
        }

        public void Start(string portName, int baudRate)
        {
            SerialDiagnostics.Write(
                $"Start requested. Port={portName}, BaudRate={baudRate}, " +
                $"DetectedPorts=[{string.Join(", ", SerialPort.GetPortNames())}]");

            if (_cancellationTokenSource != null)
            {
                SerialDiagnostics.Write("An existing serial session is active; stopping it first.");
                Stop();
            }

            _serialPort = new SerialPort
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
                $"Port configured. DataBits={_serialPort.DataBits}, Parity={_serialPort.Parity}, " +
                $"StopBits={_serialPort.StopBits}, Handshake={_serialPort.Handshake}, " +
                $"DTR={_serialPort.DtrEnable}, RTS={_serialPort.RtsEnable}, " +
                $"ReadTimeout={_serialPort.ReadTimeout}ms, ExpectedFrameLength={TelemetryProtocol.PacketLength} bytes.");

            _uiChannel = Channel.CreateUnbounded<TelemetryPacket>();
            _cancellationTokenSource = new CancellationTokenSource();
            _frameExtractor.Clear();

            try
            {
                _producerTask = Task.Factory.StartNew(
                    ProducerLoop,
                    _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                _uiConsumerTask = Task.Run(UiConsumerLoop);
                SerialDiagnostics.Write(
                    "Serial worker tasks started. Port opening happens on the producer thread; " +
                    "this message alone does not mean that the hardware port is open yet.");
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Serial worker startup failed", ex);
                throw;
            }
        }

        private void ProducerLoop()
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    SerialDiagnostics.Write($"Opening {_serialPort.PortName}...");
                    _serialPort.Open();
                    SerialDiagnostics.Write(
                        $"Port opened successfully. IsOpen={_serialPort.IsOpen}, " +
                        $"BytesToRead={_serialPort.BytesToRead}, CanRead={_serialPort.BaseStream.CanRead}.");
                    _serialPort.DiscardInBuffer();
                    SerialDiagnostics.Write("Input buffer discarded; beginning blocking reads.");
                }
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Serial port could not be opened", ex);
                _loggerService.AddLog(
                    $"Bağlantı kurulamadı: {_serialPort.PortName} - {ex.Message}",
                    "ERROR");
                OnConnectionEnded?.Invoke(this, EventArgs.Empty);
                return;
            }

            byte[] readBuffer = new byte[256];
            long totalBytesRead = 0;
            int timeoutCount = 0;

            while (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (!_serialPort.IsOpen)
                    {
                        SerialDiagnostics.Write("Read loop found the port closed; leaving the producer loop.");
                        break;
                    }

                    int queuedBytes = _serialPort.BytesToRead;
                    if (queuedBytes > 0)
                    {
                        SerialDiagnostics.Write($"Driver reports {queuedBytes} queued byte(s) before Read().");
                    }

                    int bytesRead = _serialPort.Read(readBuffer, 0, readBuffer.Length);
                    timeoutCount = 0;
                    totalBytesRead += bytesRead;

                    ProcessReceivedBytes(readBuffer, bytesRead, totalBytesRead);
                }
                catch (TimeoutException)
                {
                    timeoutCount++;
                    SerialDiagnostics.Write(
                        $"Read timeout #{timeoutCount}: no byte arrived in {_serialPort.ReadTimeout}ms. " +
                        $"IsOpen={_serialPort.IsOpen}, BytesToRead={_serialPort.BytesToRead}, totalBytes={totalBytesRead}.");
                }
                catch (OperationCanceledException) when (
                    _cancellationTokenSource?.IsCancellationRequested == true)
                {
                    SerialDiagnostics.Write("Blocking serial read was cancelled during normal shutdown.");
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    SerialDiagnostics.WriteException("Serial port operation failed", ex);
                    _loggerService.AddLog($"Serial port operation error: {ex.Message}", "ERROR");
                    break;
                }
                catch (Exception ex)
                {
                    SerialDiagnostics.WriteException("Unexpected producer error", ex);
                    _loggerService.AddLog($"Producer error: {ex.Message}", "ERROR");
                    break;
                }
            }

            SerialDiagnostics.Write($"Producer loop finished. Total bytes read={totalBytesRead}.");
            OnConnectionEnded?.Invoke(this, EventArgs.Empty);
        }

        private void ProcessReceivedBytes(byte[] readBuffer, int bytesRead, long totalBytesRead)
        {
            SerialDiagnostics.Write(
                $"Read {bytesRead} byte(s), total={totalBytesRead}. " +
                $"HEX={BitConverter.ToString(readBuffer, 0, bytesRead)}");

            var frames = _frameExtractor.AddBytes(readBuffer, bytesRead);
            SerialDiagnostics.Write(
                $"Frame extraction result: completeFrames={frames.Count}, " +
                $"bufferedBytes={_frameExtractor.BufferedByteCount}, " +
                $"expectedStart=3C-3C-3C-3C, expectedLength={TelemetryProtocol.PacketLength}.");

            foreach (byte[] frame in frames)
            {
                SerialDiagnostics.Write($"Parsing frame. HEX={BitConverter.ToString(frame)}");

                if (_packetParser.TryParse(
                    frame,
                    out TelemetryPacket? packet,
                    out TelemetryParseFailureReason failureReason,
                    out string errorMessage))
                {
                    SerialDiagnostics.Write(
                        $"Frame parsed successfully. PacketNo={packet.PacketNo}, " +
                        $"Status={packet.SatelliteStatus}, ErrorCode={packet.ErrorCode}, " +
                        $"RTC={packet.SentDate}, TeamNo={packet.TeamNo}.");
                    bool queuedForUi = _uiChannel?.Writer.TryWrite(packet) == true;
                    SerialDiagnostics.Write($"Packet queued for UI: {queuedForUi}.");
                    if (_csvRecorder != null
                        && !_csvRecorder.TryRecord(packet)
                        && Interlocked.Exchange(ref _recordingFailureReported, 1) == 0)
                    {
                        _loggerService.AddLog(
                            "Telemetry CSV queue is unavailable or full; a record could not be saved.",
                            "ERROR");
                    }
                }
                else
                {
                    SerialDiagnostics.Write($"Frame rejected by parser: {errorMessage}");

                    if (failureReason == TelemetryParseFailureReason.CrcMismatch)
                    {
                        string rawFrameHex = BitConverter.ToString(frame);
                        _loggerService.AddPersistentLog(
                            $"CRC bozuk telemetri paketi. {errorMessage} " +
                            $"FrameLength={frame.Length}. RawFrameHex={rawFrameHex}",
                            "WARN");
                        _loggerService.AddLog(
                            "CRC bozuk paket.",
                            "WARN",
                            writeToFile: false);
                    }
                    else
                    {
                        _loggerService.AddLog(
                            $"Invalid binary frame: {errorMessage}",
                            "WARN");
                    }
                }
            }
        }

        private async Task UiConsumerLoop()
        {
            var uiChannel = _uiChannel;
            if (uiChannel == null) return;

            await foreach (var packet in uiChannel.Reader.ReadAllAsync())
            {
                SerialDiagnostics.Write($"UI consumer received packet {packet.PacketNo}; raising telemetry event.");
                OnTelemetryReceived?.Invoke(this, packet);
            }

            SerialDiagnostics.Write("UI consumer loop finished.");
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
                if (!_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("COM6 portu açık değil.");
                }

                if (!string.Equals(
                    _serialPort.PortName,
                    CommandPortName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Komutlar yalnızca COM6 üzerinden gönderilebilir; açık port {_serialPort.PortName}.");
                }

                _serialPort.Write(commandBuffer, 0, commandBuffer.Length);
            }

            SerialDiagnostics.Write(
                $"Command sent to {CommandPortName}. " +
                $"Bytes={BitConverter.ToString(commandBuffer)}, Length={commandBuffer.Length}.");
        }

        public void Stop()
        {
            if (_cancellationTokenSource == null)
            {
                SerialDiagnostics.Write("Stop requested, but there is no active serial session.");
                return;
            }

            SerialDiagnostics.Write("Stop requested; cancelling serial worker.");
            string closedPortName = _serialPort.PortName;
            _cancellationTokenSource.Cancel();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                SerialDiagnostics.Write($"Closing {_serialPort.PortName}.");
                lock (_serialPortWriteLock)
                {
                    _serialPort.Close();
                }
            }

            try
            {
                (_producerTask ?? Task.CompletedTask).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("Producer shutdown failed", ex);
                _loggerService.AddLog($"Producer shutdown error: {ex.Message}", "ERROR");
            }

            _uiChannel?.Writer.TryComplete();

            try
            {
                (_uiConsumerTask ?? Task.CompletedTask).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
            catch (Exception ex)
            {
                SerialDiagnostics.WriteException("UI consumer shutdown failed", ex);
                _loggerService.AddLog($"UI consumer shutdown error: {ex.Message}", "ERROR");
            }

            lock (_serialPortWriteLock)
            {
                _serialPort?.Dispose();
            }

            _loggerService.AddLog($"Bağlantı kesildi: {closedPortName}.", "WARN");

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _producerTask = null;
            _uiConsumerTask = null;
            SerialDiagnostics.Write("Serial session stopped and disposed.");
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
