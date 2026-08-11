using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Data;

namespace UyduArayuz_1.Services
{
    public readonly struct LogModel
    {
        public DateTime Timestamp { get; init; }
        public string Level { get; init; }
        public string Message { get; init; }

        public override string ToString() => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
    }

    public sealed class LoggerService : IDisposable
    {
        private const int MaximumVisibleLogCount = 20;

        private readonly object _logLock = new object();
        private readonly ApplicationLogRecorder? _persistentRecorder;
        private int _disposeState;

        public ObservableCollection<LogModel> Logs { get; } = new ObservableCollection<LogModel>();

        public LoggerService(ApplicationLogRecorder? persistentRecorder = null)
        {
            _persistentRecorder = persistentRecorder;
            BindingOperations.EnableCollectionSynchronization(Logs, _logLock);
        }

        public void AddLog(
            string message,
            string level,
            bool writeToFile = true)
        {
            LogModel newLog = CreateLog(message, level);
            AddToVisibleLogs(newLog);

            if (writeToFile && IsWarnOrError(newLog.Level))
            {
                TryWriteToFile(newLog);
            }
        }

        public void AddPersistentLog(string message, string level)
        {
            LogModel newLog = CreateLog(message, level);
            if (!IsWarnOrError(newLog.Level))
            {
                throw new ArgumentException(
                    "Persistent application logs must use WARN or ERROR level.",
                    nameof(level));
            }

            TryWriteToFile(newLog);
        }

        private static LogModel CreateLog(string message, string level)
        {
            ArgumentNullException.ThrowIfNull(message);

            string normalizedLevel = string.IsNullOrWhiteSpace(level)
                ? "INFO"
                : level.Trim().ToUpperInvariant();

            return new LogModel
            {
                Timestamp = DateTime.Now,
                Level = normalizedLevel,
                Message = message
            };
        }

        private void AddToVisibleLogs(LogModel log)
        {
            if (!IsWarnOrError(log.Level))
            {
                return;
            }

            lock (_logLock)
            {
                Logs.Add(log);
                while (Logs.Count > MaximumVisibleLogCount)
                {
                    Logs.RemoveAt(0);
                }
            }
        }

        private void TryWriteToFile(LogModel log)
        {
            if (_persistentRecorder != null && !_persistentRecorder.TryRecord(log))
            {
                Debug.WriteLine("Application log could not be queued for the TXT recorder.");
            }
        }

        private static bool IsWarnOrError(string level) =>
            level is "WARN" or "ERROR";

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            _persistentRecorder?.Dispose();
        }
    }
}
