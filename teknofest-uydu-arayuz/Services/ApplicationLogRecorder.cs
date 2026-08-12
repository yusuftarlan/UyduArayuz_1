using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace TeknofestUyduArayuz.Services;

public sealed class ApplicationLogRecorder : IDisposable
{
    private const int DefaultQueueCapacity = 256;

    private readonly Channel<LogModel> _queue;
    private readonly StreamWriter _writer;
    private readonly Task _writerTask;
    private int _disposeState;
    private Exception? _writerFailure;

    public string FilePath { get; }

    public ApplicationLogRecorder(
        string outputDirectory,
        int queueCapacity = DefaultQueueCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);

        Directory.CreateDirectory(outputDirectory);
        FilePath = CreateUniqueFilePath(outputDirectory);

        var fileStream = new FileStream(
            FilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        _writer = new StreamWriter(
            fileStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _writer.WriteLine("Uygulama WARN/ERROR logu");
        _writer.Flush();

        _queue = Channel.CreateBounded<LogModel>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _writerTask = Task.Run(ProcessQueueAsync);
    }

    public bool TryRecord(LogModel log)
    {
        // Log üreticileri disk yazıcısını beklemez; dolu veya kapanmış kuyruk
        // başarısızlığı çağırana bildirilir.
        return Volatile.Read(ref _disposeState) == 0
            && Volatile.Read(ref _writerFailure) == null
            && _queue.Writer.TryWrite(log);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (LogModel log in _queue.Reader.ReadAllAsync())
            {
                await _writer.WriteLineAsync(Format(log)).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _writerFailure, ex);
            _queue.Writer.TryComplete(ex);
            throw;
        }
    }

    private static string Format(LogModel log) =>
        $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.Level}] {log.Message}";

    private static string CreateUniqueFilePath(string outputDirectory)
    {
        string timestamp = DateTime.Now.ToString(
            "yyyy-MM-dd_HH-mm-ss-fff",
            CultureInfo.InvariantCulture);
        string path = Path.Combine(outputDirectory, $"application_log_{timestamp}.txt");

        return File.Exists(path)
            ? Path.Combine(outputDirectory, $"application_log_{timestamp}_{Guid.NewGuid():N}.txt")
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
