using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading.Channels;
using UyduArayuz_1.Models;

namespace UyduArayuz_1.Services
{
    public class SerialTelemetryService
    {
        private SerialPort _serialPort;
        private CancellationTokenSource? _cancellationTokenSource;

        private Channel<TelemetryPacket>? _uiChannel;
        private Channel<TelemetryPacket>? _logChannel;

        private readonly TelemetryFrameExtractor _frameExtractor;
        private readonly TelemetryPacketParser _packetParser;

        public event EventHandler<TelemetryPacket>? OnTelemetryReceived;

        public SerialTelemetryService()
        {
            _serialPort = new SerialPort();
            _frameExtractor = new TelemetryFrameExtractor();
            _packetParser = new TelemetryPacketParser();
        }

        public void Start(string portName, int baudRate)
        {
            if (_serialPort != null && _serialPort.IsOpen)
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
            _logChannel = Channel.CreateUnbounded<TelemetryPacket>();
            _cancellationTokenSource = new CancellationTokenSource();
            _frameExtractor.Clear();

            try
            {
                Task.Factory.StartNew(ProducerLoop, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Run(LoggerConsumerLoop, _cancellationTokenSource.Token);
                Task.Run(UiConsumerLoop, _cancellationTokenSource.Token);
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
                            _logChannel?.Writer.TryWrite(packet);
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

        private async Task LoggerConsumerLoop()
        {
            var logChannel = _logChannel;
            var cancellationTokenSource = _cancellationTokenSource;
            if (logChannel == null || cancellationTokenSource == null) return;

            using StreamWriter sw = new StreamWriter("telemetri_log.csv", true);

            await foreach (var packet in logChannel.Reader.ReadAllAsync(cancellationTokenSource.Token))
            {
                try
                {
                    await sw.WriteLineAsync($"{packet.PacketNo},{packet.Height},{packet.ErrorCode}");
                    await sw.FlushAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }

        private async Task UiConsumerLoop()
        {
            var uiChannel = _uiChannel;
            var cancellationTokenSource = _cancellationTokenSource;
            if (uiChannel == null || cancellationTokenSource == null) return;

            await foreach (var packet in uiChannel.Reader.ReadAllAsync(cancellationTokenSource.Token))
            {
                Debug.WriteLine("UI consumer received a telemetry packet; raising update event.");
                OnTelemetryReceived?.Invoke(this, packet);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();

            _uiChannel?.Writer.TryComplete();
            _logChannel?.Writer.TryComplete();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            LoggerService.Instance.AddLog("Serial port closed.", "WARN");

            _serialPort?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
