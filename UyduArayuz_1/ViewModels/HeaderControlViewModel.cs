using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;
using UyduArayuz_1.Helpers;
namespace UyduArayuz_1.ViewModels
{
    public class HeaderControlViewModel : INotifyPropertyChanged
    {
        // INotifyPropertyChanged Uygulaması
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Action<string, int> ConnectRequested;
        public Action DisconnectRequested;

        public ObservableCollection<string> AvailablePorts { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<int> BaudRates { get; set; } = new ObservableCollection<int> { 9600, 19200, 38400, 57600, 115200 };

        

        public RelayCommand RefreshPortsCommand { get; set; }

        public RelayCommand ConnectCommand { get; set; }
        public RelayCommand DisconnectCommand { get; set; }

        private string _selectedPort;
        public string SelectedPort
        {
            get => _selectedPort;
            set { _selectedPort = value; OnPropertyChanged(); }
        }

        private int _selectedBaudRate = 9600; // Varsayılan değer
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set { _selectedBaudRate = value; OnPropertyChanged(); }
        }

        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                // İkinci değişkeni de tetikliyoruz
                OnPropertyChanged(nameof(AreSettingsEnabled));
            }
        }
        public bool AreSettingsEnabled => !IsConnected;

        public string _systemStatus;
        public string SystemStatus
        {
            get => _systemStatus;
            set { _systemStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(SystemStatusColor)); }
        }

        public readonly SolidColorBrush _orangeMessageColor = new SolidColorBrush(Color.FromRgb(248, 119, 6));
        public readonly SolidColorBrush _greenMessageColor = new SolidColorBrush(Color.FromRgb(6, 248, 58));

        private SolidColorBrush _systemStatusColor;
        public SolidColorBrush SystemStatusColor
        {
            get => _systemStatusColor;
            set { _systemStatusColor = value; OnPropertyChanged(); }
        }
        public HeaderControlViewModel()
        {
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            ConnectCommand = new RelayCommand(ConnectCommandExecuted);
            DisconnectCommand = new RelayCommand(DisconnectCommandExecuted);
            RefreshPorts(null); // Açılışta portları bir kez tara

        }

        private void RefreshPorts(object obj)
        {
            AvailablePorts.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                AvailablePorts.Add(port);
            }

            // Eğer port varsa ilkini otomatik seç
            if (AvailablePorts.Count > 0) SelectedPort = AvailablePorts[0];
        }
        private void ConnectCommandExecuted(object obj)
        {
            // 1. Kullanıcı BAĞLAN'a bastı.
            // 2. Seçili port ve hızı alıp Ana Şef'e (MainViewModel) bağır:
            Debug.WriteLine($"Bağlanılıyor: Port={SelectedPort}, BaudRate={SelectedBaudRate}");
            ConnectRequested?.Invoke(SelectedPort, SelectedBaudRate);
        }

        private void DisconnectCommandExecuted(object obj)
        {
            Debug.WriteLine($"disconnect basıldı");
            // KES butonuna basıldı.
            DisconnectRequested?.Invoke();
        }
    }
}
