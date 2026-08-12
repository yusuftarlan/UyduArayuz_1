using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using TeknofestUyduArayuz.Helpers;

namespace TeknofestUyduArayuz.ViewModels;

public sealed class HeaderControlViewModel : INotifyPropertyChanged
{
    private const string CommandPortName = "COM6";

    private static readonly SolidColorBrush ReadyColor =
        new(Color.FromRgb(248, 119, 6));
    private static readonly SolidColorBrush ConnectedColor =
        new(Color.FromRgb(6, 248, 58));

    private readonly Action<string, int> _connectRequested;
    private readonly Action _disconnectRequested;
    private readonly Action _openParachuteRequested;
    private readonly Action _separationRequested;
    private readonly Action<string> _sendMissionCodeRequested;

    private string _missionCode = string.Empty;
    private string _selectedPort = string.Empty;
    private int _selectedBaudRate = 9600;
    private bool _isConnected;
    private string _systemStatus = "SİSTEM HAZIR";
    private SolidColorBrush _systemStatusColor = ReadyColor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AvailablePorts { get; } = [];
    public IReadOnlyList<int> BaudRates { get; } =
        [4800, 9600, 19200, 38400, 57600, 115200];

    public RelayCommand RefreshPortsCommand { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand OpenParachuteCommand { get; }
    public RelayCommand SeparationCommand { get; }
    public RelayCommand SendMissionCodeCommand { get; }

    public string MissionCode
    {
        get => _missionCode;
        set
        {
            _missionCode = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string SelectedPort
    {
        get => _selectedPort;
        set
        {
            _selectedPort = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSendCommands));
        }
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set
        {
            _selectedBaudRate = value;
            OnPropertyChanged();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected == value)
            {
                return;
            }

            _isConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AreSettingsEnabled));
            OnPropertyChanged(nameof(CanSendCommands));
        }
    }

    public bool AreSettingsEnabled => !IsConnected;

    public bool CanSendCommands =>
        IsConnected &&
        string.Equals(SelectedPort, CommandPortName, StringComparison.OrdinalIgnoreCase);

    public string SystemStatus
    {
        get => _systemStatus;
        private set
        {
            _systemStatus = value;
            OnPropertyChanged();
        }
    }

    public SolidColorBrush SystemStatusColor
    {
        get => _systemStatusColor;
        private set
        {
            _systemStatusColor = value;
            OnPropertyChanged();
        }
    }

    public HeaderControlViewModel(
        Action<string, int> connectRequested,
        Action disconnectRequested,
        Action openParachuteRequested,
        Action separationRequested,
        Action<string> sendMissionCodeRequested)
    {
        ArgumentNullException.ThrowIfNull(connectRequested);
        ArgumentNullException.ThrowIfNull(disconnectRequested);
        ArgumentNullException.ThrowIfNull(openParachuteRequested);
        ArgumentNullException.ThrowIfNull(separationRequested);
        ArgumentNullException.ThrowIfNull(sendMissionCodeRequested);

        _connectRequested = connectRequested;
        _disconnectRequested = disconnectRequested;
        _openParachuteRequested = openParachuteRequested;
        _separationRequested = separationRequested;
        _sendMissionCodeRequested = sendMissionCodeRequested;

        RefreshPortsCommand = new RelayCommand(
            _ => RefreshPorts(),
            _ => AreSettingsEnabled);
        ConnectCommand = new RelayCommand(
            _ => _connectRequested(SelectedPort, SelectedBaudRate),
            _ => AreSettingsEnabled && !string.IsNullOrWhiteSpace(SelectedPort));
        DisconnectCommand = new RelayCommand(
            _ => _disconnectRequested(),
            _ => IsConnected);
        OpenParachuteCommand = new RelayCommand(
            _ => _openParachuteRequested(),
            _ => CanSendCommands);
        SeparationCommand = new RelayCommand(
            _ => _separationRequested(),
            _ => CanSendCommands);
        SendMissionCodeCommand = new RelayCommand(
            _ => _sendMissionCodeRequested(MissionCode),
            _ => CanSendCommands);

        RefreshPorts();
    }

    public void ShowConnected(string port, int baudRate)
    {
        SelectedPort = port;
        IsConnected = true;
        SystemStatus = $"PORT DİNLENİYOR - {port} ({baudRate})";
        SystemStatusColor = ConnectedColor;
    }

    public void ShowConnectionError(string message)
    {
        IsConnected = false;
        SystemStatus = $"BAĞLANTI HATASI: {message}";
        SystemStatusColor = ReadyColor;
    }

    public void ShowDisconnected()
    {
        IsConnected = false;
        SystemStatus = "BAĞLANTI KESİLDİ - SİSTEM HAZIR";
        SystemStatusColor = ReadyColor;
    }

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (string port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }

        SelectedPort = AvailablePorts.FirstOrDefault() ?? string.Empty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        CommandManager.InvalidateRequerySuggested();
    }
}
