using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading.Channels;
using UyduArayuz_1.Models;

namespace UyduArayuz_1.Services
{
    public class SerialTelemetryService : IDisposable
    {
        private SerialPort _serialPort;
        private CancellationTokenSource? _cancellationTokenSource;

        private Channel<TelemetryPacket>? _uiChannel;
        private Task? _producerTask;
        private Task? _uiConsumerTask;
        private readonly TelemetryCsvRecorder? _csvRecorder;
        private int _recordingFailureReported;

        private readonly TelemetryFrameExtractor _frameExtractor;
        private readonly TelemetryPacketParser _packetParser;

        public event EventHandler<TelemetryPacket>? OnTelemetryReceived;

        public SerialTelemetryService(TelemetryCsvRecorder? csvRecorder)
        {
            _csvRecorder = csvRecorder;
            _serialPort = new SerialPort();
            _frameExtractor = new TelemetryFrameExtractor();
            _packetParser = new TelemetryPacketParser();
        }

        public void Start(string portName, int baudRate)
        {
            if (_cancellationTokenSource != null)
            {
                Stop();
            }

            _serialPort = new SerialPort
            {
                BaudRate = baudRate,
                PortName = portName,
                DtrEnable = false,
                RtsEnable = false,
                ReadTimeout = 2000
            };

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Serial port start error: {ex.Message}");
            }
        }

        private void ProducerLoop()
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                    _serialPort.DiscardInBuffer();
                    Debug.WriteLine($"Serial port opened: {_serialPort.PortName} {_serialPort.BaudRate}.");
                    LoggerService.Instance.AddLog($"Serial port opened: {_serialPort.PortName} {_serialPort.BaudRate}.", "INFO");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Serial port open hardware error: {ex.Message}");
                LoggerService.Instance.AddLog($"Serial port could not be opened: {ex.Message}", "ERROR");
                return;
            }

            byte[] readBuffer = new byte[256];

            while (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (!_serialPort.IsOpen)
                    {
                        continue;
                    }

                    int bytesRead = _serialPort.Read(readBuffer, 0, readBuffer.Length);
                    var frames = _frameExtractor.AddBytes(readBuffer, bytesRead);

                    foreach (byte[] frame in frames) 
                    {
                        if (_packetParser.TryParse(frame, out TelemetryPacket? packet, out string errorMessage))
                        {
                            Debug.WriteLine("New binary telemetry packet received; writing to channels.");
                            LoggerService.Instance.AddLog($"Zaman: {packet.SentDate}");
                            _uiChannel?.Writer.TryWrite(packet);
                            if (_csvRecorder != null
                                && !_csvRecorder.TryRecord(packet)
                                && Interlocked.Exchange(ref _recordingFailureReported, 1) == 0)
                            {
                                LoggerService.Instance.AddLog(
                                    "Telemetry CSV queue is unavailable or full; a record could not be saved.",
                                    "ERROR");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Invalid binary frame: {errorMessage}");
                            LoggerService.Instance.AddLog($"Invalid binary frame: {errorMessage}", "WARN");
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // No bytes arrived before the serial read timeout; keep waiting.
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"Serial port operation error: {ex.Message}");
                    LoggerService.Instance.AddLog($"Serial port operation error: {ex.Message}", "ERROR");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Producer error: {ex.Message}");
                    LoggerService.Instance.AddLog($"Producer error: {ex.Message}", "ERROR");
                }
            }
        }

        private async Task UiConsumerLoop()
        {
            var uiChannel = _uiChannel;
            if (uiChannel == null) return;

            await foreach (var packet in uiChannel.Reader.ReadAllAsync())
            {
                Debug.WriteLine("UI consumer received a telemetry packet; raising update event.");
                OnTelemetryReceived?.Invoke(this, packet);
            }
        }

        public void Stop()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
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
                Debug.WriteLine($"Producer shutdown error: {ex.Message}");
                LoggerService.Instance.AddLog($"Producer shutdown error: {ex.Message}", "ERROR");
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
                Debug.WriteLine($"UI consumer shutdown error: {ex.Message}");
                LoggerService.Instance.AddLog($"UI consumer shutdown error: {ex.Message}", "ERROR");
            }

            _serialPort?.Dispose();

            LoggerService.Instance.AddLog("Serial port closed.", "WARN");

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _producerTask = null;
            _uiConsumerTask = null;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
