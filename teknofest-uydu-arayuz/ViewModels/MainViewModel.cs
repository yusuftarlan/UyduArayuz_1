using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TeknofestUyduArayuz.Models;
using TeknofestUyduArayuz.Services;

namespace TeknofestUyduArayuz.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private const int GpsErrorMask = 1 << 1;
    private const byte SeparationCommandByte = 0x00;
    private const byte OpenParachuteCommandByte = 0x01;
    private const byte MissionCodePrefix = 0b1010_1010;
    private const int MaximumHistoryCount = 100;

    private readonly SerialTelemetryService _telemetryService;
    private readonly TelemetryCsvRecorder? _csvRecorder;
    private readonly AlarmSoundService _alarmSoundService;
    private TelemetryPacket? _currentPacket;
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoggerService LoggerService { get; }
    public ObservableCollection<TelemetryPacket> TelemetryHistory { get; } = [];
    public HeaderControlViewModel HeaderControlViewModel { get; }
    public AlarmPanelViewModel AlarmPanelViewModel { get; }
    public GraphDashboardViewModel GraphViewModel { get; }
    public AttitudeViewModel AttitudeViewModel { get; }
    public MapViewModel MapViewControl { get; }

    public TelemetryPacket? CurrentPacket
    {
        get => _currentPacket;
        private set
        {
            _currentPacket = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel()
    {
        string recordsDirectory = ResolveTelemetryRecordsDirectory();
        ApplicationLogRecorder? applicationLogRecorder =
            TryCreateApplicationLogRecorder(recordsDirectory, out string? logError);

        LoggerService = new LoggerService(applicationLogRecorder);
        if (applicationLogRecorder is not null)
        {
            SerialDiagnostics.Write(
                $"WARN/ERROR TXT çıktı yolu: {applicationLogRecorder.FilePath}");
        }
        else
        {
            LoggerService.AddLog(
                $"WARN/ERROR TXT başlatma hatası: {logError}",
                "ERROR");
        }

        _csvRecorder = TryCreateCsvRecorder(recordsDirectory, LoggerService);
        _telemetryService = new SerialTelemetryService(LoggerService, _csvRecorder);
        _alarmSoundService = new AlarmSoundService();

        HeaderControlViewModel = new HeaderControlViewModel(
            StartTelemetry,
            StopTelemetry,
            SendOpenParachuteCommand,
            SendSeparationCommand,
            SendMissionCode);
        AlarmPanelViewModel = new AlarmPanelViewModel();
        GraphViewModel = new GraphDashboardViewModel();
        AttitudeViewModel = new AttitudeViewModel();
        MapViewControl = new MapViewModel();

        _telemetryService.OnTelemetryReceived += TelemetryService_OnTelemetryReceived;
        _telemetryService.OnConnectionEnded += TelemetryService_OnConnectionEnded;
    }

    private void StartTelemetry(string port, int baudRate)
    {
        try
        {
            _telemetryService.Start(port, baudRate);
            HeaderControlViewModel.ShowConnected(port, baudRate);
        }
        catch (Exception exception)
        {
            HeaderControlViewModel.ShowConnectionError(exception.Message);
        }
    }

    private void StopTelemetry()
    {
        _alarmSoundService.Stop();
        _telemetryService.Stop();
        HeaderControlViewModel.ShowDisconnected();
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
        }
        catch (Exception exception)
        {
            LoggerService.AddLog(
                $"{commandName} komutu gönderilemedi: {exception.Message}",
                "ERROR");
        }
    }

    private void SendMissionCode(string missionCode)
    {
        if (missionCode.Length != 3 ||
            missionCode.Any(character => character is not ('0' or '1' or '2')))
        {
            LoggerService.AddLog(
                "Görev kodu tam 3 haneli olmalı ve yalnızca 0, 1 veya 2 içermelidir.",
                "WARN");
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
        }
        catch (Exception exception)
        {
            LoggerService.AddLog(
                $"Görev kodu gönderilemedi: {exception.Message}",
                "ERROR");
        }
    }

    private void TelemetryService_OnTelemetryReceived(
        object? sender,
        TelemetryPacket packet)
    {
        // Event seri okuma görevinde yükselir; UI-bound property ve koleksiyonların
        // tamamı Dispatcher sınırının içinde değiştirilmelidir.
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_disposed || !HeaderControlViewModel.IsConnected)
            {
                return;
            }

            bool isAlarmActive = AlarmPanelViewModel.UpdateAlarms(packet.ErrorCode);
            _alarmSoundService.SetAlarmState(isAlarmActive);
            GraphViewModel.UpdateGraphs(packet);
            AttitudeViewModel.UpdateAttitude(packet.Yaw, packet.Pitch, packet.Roll);

            if ((packet.ErrorCode & GpsErrorMask) == 0)
            {
                MapViewControl.UpdatePosition(packet.GpsLatitude, packet.GpsLongitude);
            }

            CurrentPacket = packet;
            TelemetryHistory.Add(packet);
            if (TelemetryHistory.Count > MaximumHistoryCount)
            {
                TelemetryHistory.RemoveAt(0);
            }
        });
    }

    private void TelemetryService_OnConnectionEnded(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_disposed)
            {
                return;
            }

            _alarmSoundService.Stop();
            HeaderControlViewModel.ShowDisconnected();
        });
    }

    private static ApplicationLogRecorder? TryCreateApplicationLogRecorder(
        string recordsDirectory,
        out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            return new ApplicationLogRecorder(recordsDirectory);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            Debug.WriteLine($"Uygulama logu başlatma hatası: {exception.Message}");
            return null;
        }
    }

    private static TelemetryCsvRecorder? TryCreateCsvRecorder(
        string recordsDirectory,
        LoggerService logger)
    {
        try
        {
            var recorder = new TelemetryCsvRecorder(recordsDirectory);
            SerialDiagnostics.Write($"Telemetri CSV çıktı yolu: {recorder.FilePath}");
            return recorder;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Telemetri CSV başlatma hatası: {exception.Message}");
            logger.AddLog(
                $"Telemetri CSV başlatma hatası: {exception.Message}",
                "ERROR");
            return null;
        }
    }

    private static string ResolveTelemetryRecordsDirectory()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = AppContext.BaseDirectory;
        }

        return Path.Combine(
            localApplicationData,
            "teknofest-uydu-arayuz",
            "telemetry-records");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _telemetryService.OnTelemetryReceived -= TelemetryService_OnTelemetryReceived;
        _telemetryService.OnConnectionEnded -= TelemetryService_OnConnectionEnded;
        _alarmSoundService.Dispose();

        TryDispose(
            _telemetryService,
            "Telemetri servisi kapatma hatası",
            writeToApplicationLog: true);
        TryDispose(
            _csvRecorder,
            "Telemetri CSV kapatma hatası",
            writeToApplicationLog: true);
        TryDispose(
            LoggerService,
            "Uygulama TXT logu kapatma hatası",
            writeToApplicationLog: false);

        GC.SuppressFinalize(this);
    }

    private void TryDispose(
        IDisposable? disposable,
        string errorContext,
        bool writeToApplicationLog)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"{errorContext}: {exception.Message}");
            if (writeToApplicationLog)
            {
                LoggerService.AddLog(
                    $"{errorContext}: {exception.Message}",
                    "ERROR");
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
