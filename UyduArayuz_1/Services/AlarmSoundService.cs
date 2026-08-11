using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace UyduArayuz_1.Services
{
    /// <summary>
    /// Plays one shared warning sound while at least one telemetry alarm is active.
    /// </summary>
    public sealed class AlarmSoundService : IDisposable
    {
        private static readonly TimeSpan WarningInterval = TimeSpan.FromSeconds(5);

        private readonly DispatcherTimer _warningTimer;
        private readonly MediaPlayer _warningPlayer;
        private bool _isAlarmActive;
        private bool _isDisposed;

        public AlarmSoundService()
        {
            string warningSoundPath = Path.Combine(
                AppContext.BaseDirectory,
                "sound",
                "warning.mp3");
            if (!File.Exists(warningSoundPath))
            {
                throw new FileNotFoundException(
                    "Alarm sesi bulunamadı. warning.mp3 dosyasının çıktı dizinine kopyalandığını kontrol edin.",
                    warningSoundPath);
            }

            _warningPlayer = new MediaPlayer();
            _warningPlayer.Open(new Uri(warningSoundPath, UriKind.Absolute));

            _warningTimer = new DispatcherTimer
            {
                Interval = WarningInterval
            };
            _warningTimer.Tick += WarningTimer_Tick;
        }

        public void SetAlarmState(bool isAlarmActive)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isAlarmActive == isAlarmActive)
            {
                if (!isAlarmActive)
                {
                    StopPlayback();
                }
                return;
            }

            _isAlarmActive = isAlarmActive;

            if (_isAlarmActive)
            {
                PlayWarningSound();
                _warningTimer.Start();
                return;
            }

            StopPlayback();
        }

        public void Stop()
        {
            if (_isDisposed)
            {
                return;
            }

            _isAlarmActive = false;
            StopPlayback();
        }

        private void WarningTimer_Tick(object? sender, EventArgs e)
        {
            if (_isAlarmActive)
            {
                PlayWarningSound();
            }
        }

        private void PlayWarningSound()
        {
            _warningPlayer.Stop();
            _warningPlayer.Position = TimeSpan.Zero;
            _warningPlayer.Play();
        }

        private void StopPlayback()
        {
            _warningTimer.Stop();
            _warningPlayer.Stop();
            _warningPlayer.Position = TimeSpan.Zero;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isAlarmActive = false;
            StopPlayback();
            _warningTimer.Tick -= WarningTimer_Tick;
            _warningPlayer.Close();
            GC.SuppressFinalize(this);
        }
    }
}
