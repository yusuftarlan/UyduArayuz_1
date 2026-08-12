using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace TeknofestUyduArayuz.ViewModels;

public sealed class AlarmPanelViewModel : INotifyPropertyChanged
{
    private static readonly SolidColorBrush HealthyBrush =
        new(Color.FromRgb(46, 204, 113));
    private static readonly SolidColorBrush ErrorBrush =
        new(Color.FromRgb(231, 76, 60));

    private SolidColorBrush _landingSpeedErrorLed = HealthyBrush;
    private SolidColorBrush _gpsErrorLed = HealthyBrush;
    private SolidColorBrush _separationErrorLed = HealthyBrush;
    private SolidColorBrush _emergencyParachuteErrorLed = HealthyBrush;
    private bool _isAnyAlarmActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SolidColorBrush LandingSpeedErrorLed => _landingSpeedErrorLed;
    public SolidColorBrush GpsErrorLed => _gpsErrorLed;
    public SolidColorBrush SeperationErrorLed => _separationErrorLed;
    public SolidColorBrush EmergencyParachuteErrorLed => _emergencyParachuteErrorLed;

    public bool UpdateAlarms(int errorCode)
    {
        if (errorCode is < 0 or > 15)
        {
            return _isAnyAlarmActive;
        }

        SetLed(ref _landingSpeedErrorLed, errorCode, 0, nameof(LandingSpeedErrorLed));
        SetLed(ref _gpsErrorLed, errorCode, 1, nameof(GpsErrorLed));
        SetLed(ref _separationErrorLed, errorCode, 2, nameof(SeperationErrorLed));
        SetLed(
            ref _emergencyParachuteErrorLed,
            errorCode,
            3,
            nameof(EmergencyParachuteErrorLed));

        _isAnyAlarmActive = errorCode != 0;
        return _isAnyAlarmActive;
    }

    private void SetLed(
        ref SolidColorBrush field,
        int errorCode,
        int bit,
        string propertyName)
    {
        SolidColorBrush value = (errorCode & (1 << bit)) != 0
            ? ErrorBrush
            : HealthyBrush;
        if (ReferenceEquals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
