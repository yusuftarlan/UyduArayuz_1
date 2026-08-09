using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UyduArayuz_1.Services.Video;

/// <summary>
/// Bir HTTP MJPEG akışını okur, JPEG karelerini ayıklar ve WPF tarafından
/// görüntülenebilen dondurulmuş bitmap'ler olarak yayınlar.
/// </summary>
public sealed class MjpegDecoder : IDisposable, IAsyncDisposable
{
    private const byte JpegMarkerPrefix = 0xFF;
    private const byte StartOfImage = 0xD8;
    private const byte EndOfImage = 0xD9;

    private readonly object _lifecycleSync = new();
    private readonly object _dispatchSync = new();
    private readonly HttpClient _httpClient;
    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan _reconnectDelay;
    private readonly int _maximumFrameSize;

    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private BitmapSource? _pendingFrame;
    private bool _frameDispatchScheduled;
    private bool _disposed;

    /// <summary>
    /// Tam ve geçerli bir JPEG karesi çözümlendiğinde UI thread'inde oluşur.
    /// Yoğun yayınlarda gecikmeyi büyütmemek için bekleyen eski kare yerine
    /// yalnızca en yeni kare teslim edilir.
    /// </summary>
    public event EventHandler<MjpegFrameReadyEventArgs>? FrameReady;

    /// <summary>
    /// Bağlantı veya kare çözümleme hatası oluştuğunda UI thread'inde oluşur.
    /// Bağlantı hatalarından sonra decoder otomatik olarak yeniden bağlanır.
    /// </summary>
    public event EventHandler<MjpegErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Yeni bir MJPEG decoder oluşturur.
    /// </summary>
    /// <param name="dispatcher">Kare ve hata event'lerinin yayınlanacağı UI dispatcher.</param>
    /// <param name="options">HTTP, yeniden bağlanma ve kare boyutu seçenekleri.</param>
    public MjpegDecoder(
        Dispatcher dispatcher,
        MjpegDecoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        options ??= new MjpegDecoderOptions();
        options.Validate();

        _dispatcher = dispatcher;
        _reconnectDelay = options.ReconnectDelay;
        _maximumFrameSize = options.MaximumFrameSize;

        var handler = new HttpClientHandler();
        if (options.ServerCertificateValidationCallback is not null)
        {
            handler.ServerCertificateCustomValidationCallback =
                options.ServerCertificateValidationCallback;
        }

        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    /// <summary>
    /// Verilen HTTP veya HTTPS MJPEG adresini arka planda okumaya başlar.
    /// Bağlantı koparsa dispose edilene veya <see cref="Stop"/> çağrılana kadar
    /// otomatik yeniden bağlanır.
    /// </summary>
    /// <param name="url">Doğrudan MJPEG stream adresi.</param>
    public void Start(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? streamUri) ||
            (streamUri.Scheme != Uri.UriSchemeHttp &&
             streamUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "MJPEG adresi mutlak bir HTTP veya HTTPS URL'si olmalıdır.",
                nameof(url));
        }

        Start(streamUri);
    }

    /// <summary>
    /// Verilen HTTP veya HTTPS MJPEG adresini arka planda okumaya başlar.
    /// </summary>
    /// <param name="streamUri">Doğrudan MJPEG stream adresi.</param>
    public void Start(Uri streamUri)
    {
        ArgumentNullException.ThrowIfNull(streamUri);

        if (!streamUri.IsAbsoluteUri ||
            (streamUri.Scheme != Uri.UriSchemeHttp &&
             streamUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "MJPEG adresi mutlak bir HTTP veya HTTPS URL'si olmalıdır.",
                nameof(streamUri));
        }

        lock (_lifecycleSync)
        {
            ThrowIfDisposed();

            if (_runTask is not null)
            {
                throw new InvalidOperationException(
                    "Decoder zaten çalışıyor. Yeni bir yayın başlatmadan önce Stop çağırın.");
            }

            _runCancellation = new CancellationTokenSource();
            CancellationToken token = _runCancellation.Token;
            _runTask = Task.Run(() => RunReconnectLoopAsync(streamUri, token));
        }
    }

    /// <summary>
    /// Aktif okumayı ve yeniden bağlanma döngüsünü durdurur.
    /// </summary>
    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Aktif okumayı ve yeniden bağlanma döngüsünü eşzamansız olarak durdurur.
    /// </summary>
    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task? runTask;

        lock (_lifecycleSync)
        {
            cancellation = _runCancellation;
            runTask = _runTask;
            _runCancellation = null;
            _runTask = null;
        }

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();

        try
        {
            if (runTask is not null)
            {
                await runTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Kullanıcı tarafından yapılan normal durdurma akışı.
        }
        finally
        {
            cancellation.Dispose();
            ClearPendingFrame();
        }
    }

    private async Task RunReconnectLoopAsync(
        Uri streamUri,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    streamUri);

                using HttpResponseMessage response =
                    await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                ValidateContentType(response);

                await using Stream stream =
                    await response.Content
                        .ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);

                await ReadFramesAsync(stream, cancellationToken)
                    .ConfigureAwait(false);

                throw new EndOfStreamException(
                    "MJPEG sunucusu bağlantıyı kapattı.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                PublishError(exception, willReconnect: true);
            }

            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static void ValidateContentType(HttpResponseMessage response)
    {
        string? mediaType = response.Content.Headers.ContentType?.MediaType;

        if (!string.Equals(
                mediaType,
                "multipart/x-mixed-replace",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Adres bir MJPEG yayını döndürmedi. Content-Type: '{mediaType ?? "belirtilmemiş"}'.");
        }
    }

    private async Task ReadFramesAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(16 * 1024);

        try
        {
            using var frameBuffer = new MemoryStream();
            bool collectingFrame = false;
            byte previousByte = 0;

            while (true)
            {
                int bytesRead = await stream
                    .ReadAsync(readBuffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    return;
                }

                for (int index = 0; index < bytesRead; index++)
                {
                    byte currentByte = readBuffer[index];

                    if (!collectingFrame)
                    {
                        if (previousByte == JpegMarkerPrefix &&
                            currentByte == StartOfImage)
                        {
                            frameBuffer.SetLength(0);
                            frameBuffer.Position = 0;
                            frameBuffer.WriteByte(JpegMarkerPrefix);
                            frameBuffer.WriteByte(StartOfImage);
                            collectingFrame = true;
                            previousByte = 0;
                            continue;
                        }

                        previousByte = currentByte;
                        continue;
                    }

                    frameBuffer.WriteByte(currentByte);

                    if (frameBuffer.Length > _maximumFrameSize)
                    {
                        PublishError(
                            new InvalidDataException(
                                $"MJPEG karesi izin verilen {_maximumFrameSize:N0} byte sınırını aştı ve atlandı."),
                            willReconnect: false);

                        frameBuffer.SetLength(0);
                        frameBuffer.Position = 0;
                        collectingFrame = false;
                        previousByte = currentByte;
                        continue;
                    }

                    if (previousByte == JpegMarkerPrefix &&
                        currentByte == EndOfImage)
                    {
                        TryDecodeAndPublishFrame(frameBuffer);
                        frameBuffer.SetLength(0);
                        frameBuffer.Position = 0;
                        collectingFrame = false;
                        previousByte = 0;
                        continue;
                    }

                    previousByte = currentByte;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private void TryDecodeAndPublishFrame(MemoryStream frameBuffer)
    {
        try
        {
            frameBuffer.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.StreamSource = frameBuffer;
            bitmap.EndInit();
            bitmap.Freeze();

            PublishFrame(bitmap);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or
                  NotSupportedException or
                  FileFormatException)
        {
            PublishError(
                new InvalidDataException(
                    "MJPEG akışındaki bir JPEG karesi çözümlenemedi ve atlandı.",
                    exception),
                willReconnect: false);
        }
    }

    private void PublishFrame(BitmapSource frame)
    {
        bool shouldSchedule;

        lock (_dispatchSync)
        {
            if (_disposed)
            {
                return;
            }

            _pendingFrame = frame;
            shouldSchedule = !_frameDispatchScheduled;
            _frameDispatchScheduled = true;
        }

        if (!shouldSchedule)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                new Action(DispatchLatestFrame));
        }
        catch (InvalidOperationException)
        {
            ClearPendingFrame();
        }
    }

    private void DispatchLatestFrame()
    {
        BitmapSource? frame;

        lock (_dispatchSync)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
            _frameDispatchScheduled = false;
        }

        if (!_disposed && frame is not null)
        {
            FrameReady?.Invoke(
                this,
                new MjpegFrameReadyEventArgs(frame));
        }
    }

    private void PublishError(Exception exception, bool willReconnect)
    {
        if (_disposed || _dispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    if (!_disposed)
                    {
                        ErrorOccurred?.Invoke(
                            this,
                            new MjpegErrorEventArgs(
                                exception,
                                willReconnect));
                    }
                }));
        }
        catch (InvalidOperationException)
        {
            // Dispatcher kapanıyorsa artık UI bildirimi yapılamaz.
        }
    }

    private void ClearPendingFrame()
    {
        lock (_dispatchSync)
        {
            _pendingFrame = null;
            _frameDispatchScheduled = false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        ClearPendingFrame();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        ClearPendingFrame();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// MJPEG decoder bağlantı ve güvenlik seçeneklerini tanımlar.
/// </summary>
public sealed class MjpegDecoderOptions
{
    /// <summary>
    /// Kopan bağlantıdan sonra yeniden denemeden önce beklenecek süre.
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } =
        TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Hatalı bir akışın sınırsız bellek tüketmesini önleyen maksimum JPEG
    /// kare boyutu. Varsayılan değer 16 MiB'dir.
    /// </summary>
    public int MaximumFrameSize { get; init; } =
        16 * 1024 * 1024;

    /// <summary>
    /// Self-signed sertifika gibi kontrollü senaryolar için isteğe bağlı
    /// sertifika doğrulama callback'i. Varsayılan değerde sistem doğrulaması
    /// kullanılır; production ortamında tüm sertifikaları koşulsuz kabul etmeyin.
    /// </summary>
    public Func<
        HttpRequestMessage,
        X509Certificate2?,
        X509Chain?,
        SslPolicyErrors,
        bool>? ServerCertificateValidationCallback { get; init; }

    internal void Validate()
    {
        if (ReconnectDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReconnectDelay));
        }

        if (MaximumFrameSize < 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumFrameSize),
                "Bir JPEG karesi en az SOI ve EOI imzalarını taşıyabilmelidir.");
        }
    }
}

/// <summary>
/// Çözümlenmiş bir MJPEG karesini taşır.
/// </summary>
public sealed class MjpegFrameReadyEventArgs : EventArgs
{
    /// <summary>
    /// UI thread'leri arasında güvenle kullanılabilen dondurulmuş kare.
    /// </summary>
    public BitmapSource Frame { get; }

    /// <summary>
    /// Yeni bir kare event argümanı oluşturur.
    /// </summary>
    public MjpegFrameReadyEventArgs(BitmapSource frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Frame = frame;
    }
}

/// <summary>
/// MJPEG okuma veya kare çözümleme hatasını taşır.
/// </summary>
public sealed class MjpegErrorEventArgs : EventArgs
{
    /// <summary>
    /// Hatanın ayrıntısı.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Decoder'ın bu hatadan sonra yeniden bağlanmayı deneyip denemeyeceği.
    /// </summary>
    public bool WillReconnect { get; }

    /// <summary>
    /// Yeni bir hata event argümanı oluşturur.
    /// </summary>
    public MjpegErrorEventArgs(
        Exception exception,
        bool willReconnect)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
        WillReconnect = willReconnect;
    }
}
