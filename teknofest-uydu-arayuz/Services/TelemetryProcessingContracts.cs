using TeknofestUyduArayuz.Models;

namespace TeknofestUyduArayuz.Services;

public interface IApplicationLogger
{
    void AddLog(string message, string level, bool writeToFile = true);

    void AddPersistentLog(string message, string level);
}

public interface ITelemetryRecorder
{
    bool TryRecord(TelemetryPacket packet);
}
