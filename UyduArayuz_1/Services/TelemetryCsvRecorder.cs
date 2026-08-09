using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Channels;
using UyduArayuz_1.Models;

namespace UyduArayuz_1.Services;

public sealed class TelemetryCsvRecorder : IDisposable
{
    private const int DefaultBatchSize = 10;
    private const int DefaultQueueCapacity = 1024;
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);

    private readonly Channel<TelemetryPacket> _queue;
    private readonly StreamWriter _writer;
    private readonly Task _writerTask;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private int _disposeState;
    private Exception? _writerFailure;

    public string FilePath { get; }

    public TelemetryCsvRecorder(
        string outputDirectory,
        int batchSize = DefaultBatchSize,
        TimeSpan? flushInterval = null,
        int queueCapacity = DefaultQueueCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);

        _batchSize = batchSize;
        _flushInterval = flushInterval ?? DefaultFlushInterval;
        if (_flushInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(flushInterval));
        }

        Directory.CreateDirectory(outputDirectory);
        FilePath = CreateUniqueFilePath(outputDirectory);

        var fileStream = new FileStream(
            FilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        _writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _writer.WriteLine(CreateHeader());
        _writer.Flush();

        _queue = Channel.CreateBounded<TelemetryPacket>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _writerTask = Task.Run(ProcessQueueAsync);
    }

    public bool TryRecord(TelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return Volatile.Read(ref _disposeState) == 0
            && Volatile.Read(ref _writerFailure) == null
            && _queue.Writer.TryWrite(packet);
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<TelemetryPacket>(_batchSize);
        long batchStartedAt = 0;

        try
        {
            while (true)
            {
                while (batch.Count < _batchSize && _queue.Reader.TryRead(out TelemetryPacket? packet))
                {
                    if (batch.Count == 0)
                    {
                        batchStartedAt = Stopwatch.GetTimestamp();
                    }

                    batch.Add(packet);
                }

                if (batch.Count >= _batchSize)
                {
                    await WriteBatchAsync(batch).ConfigureAwait(false);
                    continue;
                }

                Task<bool> dataAvailableTask = _queue.Reader.WaitToReadAsync().AsTask();
                if (batch.Count == 0)
                {
                    if (!await dataAvailableTask.ConfigureAwait(false))
                    {
                        break;
                    }

                    continue;
                }

                TimeSpan remainingFlushTime = _flushInterval - Stopwatch.GetElapsedTime(batchStartedAt);
                if (remainingFlushTime <= TimeSpan.Zero)
                {
                    await WriteBatchAsync(batch).ConfigureAwait(false);
                    continue;
                }

                Task flushDelayTask = Task.Delay(remainingFlushTime);
                Task completedTask = await Task.WhenAny(dataAvailableTask, flushDelayTask).ConfigureAwait(false);

                if (completedTask == flushDelayTask && batch.Count > 0)
                {
                    await WriteBatchAsync(batch).ConfigureAwait(false);
                    continue;
                }

                if (!await dataAvailableTask.ConfigureAwait(false))
                {
                    break;
                }
            }

            if (batch.Count > 0)
            {
                await WriteBatchAsync(batch).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _writerFailure, ex);
            _queue.Writer.TryComplete(ex);
            throw;
        }
    }

    private async Task WriteBatchAsync(List<TelemetryPacket> batch)
    {
        foreach (TelemetryPacket packet in batch)
        {
            await _writer.WriteLineAsync(ToCsvRow(packet)).ConfigureAwait(false);
        }

        await _writer.FlushAsync().ConfigureAwait(false);
        batch.Clear();
    }

    private static string CreateHeader() =>
        "PacketNo,SatelliteStatus,SatelliteStatusText,ErrorCode,ErrorCodeText,SentDate,Pressure,Height,LandingSpeed,Temperature,BatteryVoltage,GpsLatitude,GpsLongitude,GpsAltitude,Pitch,Roll,Yaw,TaskCode,TeamNo";

    private static string ToCsvRow(TelemetryPacket packet)
    {
        return string.Join(',',
            packet.PacketNo.ToString(CultureInfo.InvariantCulture),
            packet.SatelliteStatus.ToString(CultureInfo.InvariantCulture),
            Escape(packet.SatelliteStatusString),
            packet.ErrorCode.ToString(CultureInfo.InvariantCulture),
            Escape(packet.ErrorCodeString),
            Escape(packet.SentDate),
            packet.Pressure.ToString("R", CultureInfo.InvariantCulture),
            packet.Height.ToString("R", CultureInfo.InvariantCulture),
            packet.LandingSpeed.ToString("R", CultureInfo.InvariantCulture),
            packet.Tempreture.ToString("R", CultureInfo.InvariantCulture),
            packet.BatteryVoltage.ToString("R", CultureInfo.InvariantCulture),
            packet.GpsLatitude.ToString("R", CultureInfo.InvariantCulture),
            packet.GpsLongitude.ToString("R", CultureInfo.InvariantCulture),
            packet.GpsAltitude.ToString("R", CultureInfo.InvariantCulture),
            packet.Pitch.ToString("R", CultureInfo.InvariantCulture),
            packet.Roll.ToString("R", CultureInfo.InvariantCulture),
            packet.Yaw.ToString("R", CultureInfo.InvariantCulture),
            Escape(packet.TaskCode),
            packet.TeamNo.ToString(CultureInfo.InvariantCulture));
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string CreateUniqueFilePath(string outputDirectory)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);
        string path = Path.Combine(outputDirectory, $"telemetry_{timestamp}.csv");

        return File.Exists(path)
            ? Path.Combine(outputDirectory, $"telemetry_{timestamp}_{Guid.NewGuid():N}.csv")
            : path;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _queue.Writer.TryComplete();

        try
        {
            _writerTask.GetAwaiter().GetResult();
        }
        finally
        {
            _writer.Dispose();
        }
    }
}
