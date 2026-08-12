using TeknofestUyduArayuz.Models;

namespace TeknofestUyduArayuz.Services;

internal sealed class TelemetryFrameProcessor
{
    private readonly TelemetryFrameExtractor _frameExtractor = new();
    private readonly IApplicationLogger _logger;
    private readonly ITelemetryRecorder? _recorder;
    private int _recordingFailureReported;

    public TelemetryFrameProcessor(
        IApplicationLogger logger,
        ITelemetryRecorder? recorder)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recorder = recorder;
    }

    public void Reset()
    {
        _frameExtractor.Clear();
        Volatile.Write(ref _recordingFailureReported, 0);
    }

    public IReadOnlyList<TelemetryPacket> Process(
        byte[] readBuffer,
        int bytesRead,
        long totalBytesRead)
    {
        ArgumentNullException.ThrowIfNull(readBuffer);

        SerialDiagnostics.Write(
            $"{bytesRead} bayt okundu, toplam={totalBytesRead}. " +
            $"HEX={BitConverter.ToString(readBuffer, 0, bytesRead)}");

        IReadOnlyList<byte[]> frames = _frameExtractor.AddBytes(
            readBuffer,
            bytesRead,
            out int invalidEndMarkerCount);
        SerialDiagnostics.Write(
            $"Çerçeve çıkarma sonucu: TamÇerçeve={frames.Count}, " +
            $"TampondakiBayt={_frameExtractor.BufferedByteCount}, " +
            $"BeklenenBaşlangıç=3C-3C-3C-3C, " +
            $"BeklenenUzunluk={TelemetryProtocol.PacketLength}.");

        if (invalidEndMarkerCount > 0)
        {
            _logger.AddLog(
                $"{invalidEndMarkerCount} telemetri çerçevesi geçersiz bitiş işareti nedeniyle reddedildi.",
                "WARN");
        }

        var packets = new List<TelemetryPacket>(frames.Count);
        foreach (byte[] frame in frames)
        {
            ProcessFrame(frame, packets);
        }

        return packets;
    }

    private void ProcessFrame(
        byte[] frame,
        ICollection<TelemetryPacket> packets)
    {
        SerialDiagnostics.Write($"Çerçeve ayrıştırılıyor. HEX={BitConverter.ToString(frame)}");

        if (!TelemetryPacketParser.TryParse(
                frame,
                out TelemetryPacket? packet,
                out TelemetryParseFailureReason failureReason,
                out string errorMessage))
        {
            ReportParseFailure(frame, failureReason, errorMessage);
            return;
        }

        SerialDiagnostics.Write(
            $"Çerçeve başarıyla ayrıştırıldı. PaketNo={packet.PacketNo}, " +
            $"Durum={packet.SatelliteStatus}, HataKodu={packet.ErrorCode}, " +
            $"RTC={packet.SentDate:dd.MM.yyyy HH:mm:ss}, TakımNo={packet.TeamNo}.");

        packets.Add(packet);
        TryRecord(packet);
    }

    private void TryRecord(TelemetryPacket packet)
    {
        if (_recorder is null || _recorder.TryRecord(packet))
        {
            return;
        }

        if (Interlocked.Exchange(ref _recordingFailureReported, 1) == 0)
        {
            _logger.AddLog(
                "Telemetri CSV kuyruğu kullanılamıyor veya dolu; bir kayıt kaydedilemedi.",
                "ERROR");
        }
    }

    private void ReportParseFailure(
        byte[] frame,
        TelemetryParseFailureReason failureReason,
        string errorMessage)
    {
        SerialDiagnostics.Write($"Çerçeve ayrıştırıcı tarafından reddedildi: {errorMessage}");

        if (failureReason == TelemetryParseFailureReason.CrcMismatch)
        {
            string rawFrameHex = BitConverter.ToString(frame);
            _logger.AddPersistentLog(
                $"CRC bozuk telemetri paketi. {errorMessage} " +
                $"ÇerçeveUzunluğu={frame.Length}. HamÇerçeveHex={rawFrameHex}",
                "WARN");
            _logger.AddLog("CRC bozuk paket.", "WARN", writeToFile: false);
            return;
        }

        _logger.AddLog($"Geçersiz ikili çerçeve: {errorMessage}", "WARN");
    }
}
