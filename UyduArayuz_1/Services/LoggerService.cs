using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace UyduArayuz_1.Services
{
    public struct LogModel
    {
        public string Time { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }

        public override string ToString() => $"[{Time}] [{Level}] {Message}";
    }
    public class LoggerService
    {
        // Tüm uygulamanın ulaşacağı tekil (Singleton) kopya
        public static LoggerService Instance { get; private set; }

        public ObservableCollection<LogModel> Logs { get; } = new ObservableCollection<LogModel>();
        private readonly object _logLock = new object();

        // Senin bahsettiğin o kurucu metot!
        // Sadece 'new' dendiği an çalışır.
        public LoggerService()
        {
            // Kurucu metot çalıştığı an UI Thread'de isek, güvenliği sağlar.
            BindingOperations.EnableCollectionSynchronization(Logs, _logLock);

            // Kendisini küresel erişime açar
            Instance = this;
        }

        public void AddLog(string message, string level = "INFO")
        {
            // Sadece tek string yerine, modeli doldurup listeye atıyoruz
            var newLog = new LogModel
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Level = level,
                Message = message
            };

            Logs.Add(newLog);
        }
    }
}
