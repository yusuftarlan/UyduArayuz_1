using System.Diagnostics;

namespace UyduArayuz_1.Services.Video;

/// <summary>
/// Canlı yayın hatalarının kullanıcı arayüzünden bağımsız raporlanmasını sağlar.
/// </summary>
public interface ILiveStreamLogger
{
    void LogError(string operation, Exception exception);
}

/// <summary>
/// Hataları Debug çıktısına ve mevcutsa standart hata konsoluna yazar.
/// </summary>
public sealed class ConsoleLiveStreamLogger : ILiveStreamLogger
{
    public void LogError(string operation, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(exception);

        string message =
            $"[{DateTime.Now:HH:mm:ss}] [ERROR] Canlı kamera {operation}: {exception}";

        Debug.WriteLine(message);
        Console.Error.WriteLine(message);
    }
}
