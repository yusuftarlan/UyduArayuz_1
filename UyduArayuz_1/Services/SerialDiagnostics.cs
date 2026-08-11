using System.Diagnostics;

namespace UyduArayuz_1.Services;

internal static class SerialDiagnostics
{
    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [SERIAL] {message}";
        Console.WriteLine(line);
        Debug.WriteLine(line);
    }

    [Conditional("DEBUG")]
    public static void WriteException(string context, Exception exception)
    {
        Write($"{context}: {exception.GetType().Name}: {exception.Message}");
        Write(exception.StackTrace ?? "Stack trace is unavailable.");
    }
}
