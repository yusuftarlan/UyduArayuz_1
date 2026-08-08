using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UyduArayuz_1.ViewModels
{
    public class AttitudeViewModel : INotifyPropertyChanged
    {
        private double _pitch;
        private double _roll;
        private double _yaw;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Pitch
        {
            get => _pitch;
            set
            {
                if (_pitch == value) return;
                _pitch = value;
                OnPropertyChanged();
            }
        }

        public double Roll
        {
            get => _roll;
            set
            {
                if (_roll == value) return;
                _roll = value;
                OnPropertyChanged();
            }
        }

        public double Yaw
        {
            get => _yaw;
            set
            {
                if (_yaw == value) return;
                _yaw = value;
                OnPropertyChanged();
            }
        }

        public void UpdateAttitude(double yaw, double pitch, double roll)
        {
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
        }

        public void ResetOrientation()
        {
            Pitch = 0;
            Roll = 0;
            Yaw = 0;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
