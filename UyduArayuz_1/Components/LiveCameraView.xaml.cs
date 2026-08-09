using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UyduArayuz_1.Services.Video;

namespace UyduArayuz_1.Components;

/// <summary>
/// Kod aşamasında seçilen protokole ait canlı kamera yayınını gösterir.
/// Görünüm, somut MJPEG veya WebRTC uygulamasını değil yalnızca
/// <see cref="ILiveStreamPlayer"/> sözleşmesini bilir.
/// </summary>
public partial class LiveCameraView : UserControl
{
    // WebRTC uygulaması eklendiğinde seçim burada LiveStreamProtocol.WebRtc
    // olarak değiştirilir ve CreatePlayerResolver içine WebRTC factory eklenir.
    private const LiveStreamProtocol ConfiguredProtocol =
        LiveStreamProtocol.Mjpeg;

    // MainViewModel binding'i kullanılmayacaksa yayın adresini burada verin.
    // Örnek: "http://kamera-adresi/stream.mjpg"
    private const string DefaultStreamUrl = "https://camera.mahuk.online/video";

    // Yalnızca güvendiğiniz kamera sunucularında true yapın.
    private const bool DefaultAllowSelfSignedCertificate = false;

    /// <summary>
    /// Koddan veya MainViewModel binding'inden sağlanan canlı yayın adresi.
    /// Kullanıcı arayüzünde URL giriş alanı bulunmaz.
    /// </summary>
    public string? StreamUrl
    {
        get => (string?)GetValue(StreamUrlProperty);
        set => SetValue(StreamUrlProperty, value);
    }

    /// <summary>
    /// <see cref="StreamUrl"/> dependency property tanımıdır.
    /// </summary>
    public static readonly DependencyProperty StreamUrlProperty =
        DependencyProperty.Register(
            nameof(StreamUrl),
            typeof(string),
            typeof(LiveCameraView),
            new PropertyMetadata(DefaultStreamUrl));

    /// <summary>
    /// Koddan veya MainViewModel binding'inden self-signed sertifika izni verir.
    /// Varsayılan değer güvenli olacak şekilde false'tur.
    /// </summary>
    public bool AllowSelfSignedCertificate
    {
        get => (bool)GetValue(AllowSelfSignedCertificateProperty);
        set => SetValue(AllowSelfSignedCertificateProperty, value);
    }

    /// <summary>
    /// <see cref="AllowSelfSignedCertificate"/> dependency property tanımıdır.
    /// </summary>
    public static readonly DependencyProperty AllowSelfSignedCertificateProperty =
        DependencyProperty.Register(
            nameof(AllowSelfSignedCertificate),
            typeof(bool),
            typeof(LiveCameraView),
            new PropertyMetadata(DefaultAllowSelfSignedCertificate));

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private ILiveStreamPlayer? _player;
    private bool _isUnloaded;

    /// <summary>
    /// Live Camera görünümünü oluşturur.
    /// </summary>
    public LiveCameraView()
    {
        InitializeComponent();
        Loaded += LiveCameraView_Loaded;
        Unloaded += LiveCameraView_Unloaded;
    }

    private void LiveCameraView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _isUnloaded = false;
    }

    private async void StartStream_Click(
        object sender,
        RoutedEventArgs e)
    {
        string address = StreamUrl?.Trim() ?? string.Empty;

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? streamUri))
        {
            MessageBox.Show(
                "Canlı yayın adresi kod tarafında yapılandırılmamış. " +
                "LiveCameraView.StreamUrl değerini veya DefaultStreamUrl sabitini ayarlayın.",
                "Yayın adresi eksik",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await _lifecycleLock.WaitAsync();

        try
        {
            if (_isUnloaded)
            {
                return;
            }

            await ReleasePlayerAsync();

            StreamImage.Source = null;

            ILiveStreamPlayerResolver resolver =
                CreatePlayerResolver(streamUri);
            ILiveStreamPlayer player =
                resolver.Resolve(ConfiguredProtocol);

            player.FrameReady += Player_FrameReady;
            player.ErrorOccurred += Player_ErrorOccurred;
            _player = player;

            player.Start(streamUri);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Canlı yayın başlatılamadı",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await ReleasePlayerAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async void StopStream_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _lifecycleLock.WaitAsync();

        try
        {
            await ReleasePlayerAsync();
            StreamImage.Source = null;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Canlı yayın durdurulamadı",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async void LiveCameraView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _isUnloaded = true;
        await _lifecycleLock.WaitAsync();

        try
        {
            await ReleasePlayerAsync();
            StreamImage.Source = null;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(
                $"Canlı yayın oynatıcısı kapatılırken hata oluştu: {exception}");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Uygulamanın canlı yayın composition root'udur. Yeni bir protokol
    /// eklenirken View'in akış mantığına dokunmak yerine ilgili factory bu
    /// koleksiyona kaydedilir.
    /// </summary>
    private ILiveStreamPlayerResolver CreatePlayerResolver(Uri streamUri)
    {
        var connectionOptions = new LiveStreamConnectionOptions
        {
            ReconnectDelay = TimeSpan.FromSeconds(1.5),
            ServerCertificateValidationCallback =
                CreateCertificateValidationCallback(streamUri)
        };

        ILiveStreamPlayerFactory[] factories =
        [
            new MjpegLiveStreamPlayerFactory(
                StreamImage.Dispatcher,
                connectionOptions)

            // WebRTC desteği geldiğinde örnek kayıt:
            // new WebRtcLiveStreamPlayerFactory(...)
        ];

        return new LiveStreamPlayerResolver(factories);
    }

    private Func<
        HttpRequestMessage,
        X509Certificate2?,
        X509Chain?,
        SslPolicyErrors,
        bool>? CreateCertificateValidationCallback(Uri streamUri)
    {
        if (!AllowSelfSignedCertificate)
        {
            return null;
        }

        return (request, certificate, chain, errors) =>
            errors == SslPolicyErrors.None ||
            (request.RequestUri is not null &&
             string.Equals(
                 request.RequestUri.Host,
                 streamUri.Host,
                 StringComparison.OrdinalIgnoreCase) &&
             errors == SslPolicyErrors.RemoteCertificateChainErrors);
    }

    private void Player_FrameReady(
        object? sender,
        LiveStreamFrameReadyEventArgs e)
    {
        if (!ReferenceEquals(sender, _player) || _isUnloaded)
        {
            return;
        }

        StreamImage.Source = e.Frame;
    }

    private void Player_ErrorOccurred(
        object? sender,
        LiveStreamErrorEventArgs e)
    {
        if (!ReferenceEquals(sender, _player) || _isUnloaded)
        {
            return;
        }

        Debug.WriteLine(
            e.WillReconnect
                ? $"Canlı yayın bağlantısı kesildi; yeniden denenecek: {e.Exception}"
                : $"Canlı yayında geçersiz kare atlandı: {e.Exception}");
    }

    private async ValueTask ReleasePlayerAsync()
    {
        ILiveStreamPlayer? player = _player;
        _player = null;

        if (player is null)
        {
            return;
        }

        player.FrameReady -= Player_FrameReady;
        player.ErrorOccurred -= Player_ErrorOccurred;
        await player.DisposeAsync();
    }
}

/// <summary>
/// Uygulamanın destekleyebileceği canlı yayın protokollerini tanımlar.
/// </summary>
internal enum LiveStreamProtocol
{
    Mjpeg,
    WebRtc
}

internal static class LiveStreamProtocolExtensions
{
    public static string ToDisplayName(this LiveStreamProtocol protocol) =>
        protocol switch
        {
            LiveStreamProtocol.Mjpeg => "MJPEG",
            LiveStreamProtocol.WebRtc => "WebRTC",
            _ => protocol.ToString()
        };
}

/// <summary>
/// View'in somut protokollerden bağımsız olarak kullandığı küçük oynatıcı
/// sözleşmesidir.
/// </summary>
internal interface ILiveStreamPlayer : IAsyncDisposable
{
    LiveStreamProtocol Protocol { get; }

    event EventHandler<LiveStreamFrameReadyEventArgs>? FrameReady;

    event EventHandler<LiveStreamErrorEventArgs>? ErrorOccurred;

    void Start(Uri streamUri);

    Task StopAsync();
}

/// <summary>
/// Tek bir protokole ait oynatıcıları oluşturan factory sözleşmesidir.
/// </summary>
internal interface ILiveStreamPlayerFactory
{
    LiveStreamProtocol Protocol { get; }

    ILiveStreamPlayer Create();
}

/// <summary>
/// Kayıtlı factory'ler arasından kodda seçilen protokole ait olanı çözer.
/// </summary>
internal interface ILiveStreamPlayerResolver
{
    ILiveStreamPlayer Resolve(LiveStreamProtocol protocol);
}

internal sealed class LiveStreamPlayerResolver : ILiveStreamPlayerResolver
{
    private readonly IReadOnlyList<ILiveStreamPlayerFactory> _factories;

    public LiveStreamPlayerResolver(
        IEnumerable<ILiveStreamPlayerFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        _factories = factories.ToArray();

        if (_factories.Count == 0)
        {
            throw new ArgumentException(
                "En az bir canlı yayın factory'si kaydedilmelidir.",
                nameof(factories));
        }
    }

    public ILiveStreamPlayer Resolve(LiveStreamProtocol protocol)
    {
        ILiveStreamPlayerFactory[] matches = _factories
            .Where(factory => factory.Protocol == protocol)
            .Take(2)
            .ToArray();

        return matches.Length switch
        {
            0 => throw new NotSupportedException(
                $"'{protocol.ToDisplayName()}' canlı yayın protokolü henüz uygulanmadı."),
            1 => matches[0].Create(),
            _ => throw new InvalidOperationException(
                $"'{protocol.ToDisplayName()}' protokolü için birden fazla factory kaydedildi.")
        };
    }
}

/// <summary>
/// Protokol uygulamalarına aktarılan ortak bağlantı seçenekleridir.
/// </summary>
internal sealed class LiveStreamConnectionOptions
{
    public TimeSpan ReconnectDelay { get; init; } =
        TimeSpan.FromSeconds(1.5);

    public Func<
        HttpRequestMessage,
        X509Certificate2?,
        X509Chain?,
        SslPolicyErrors,
        bool>? ServerCertificateValidationCallback { get; init; }
}

/// <summary>
/// MJPEG decoder oluşturma ayrıntısını View'den ayırır.
/// </summary>
internal sealed class MjpegLiveStreamPlayerFactory : ILiveStreamPlayerFactory
{
    private readonly Dispatcher _dispatcher;
    private readonly LiveStreamConnectionOptions _options;

    public LiveStreamProtocol Protocol => LiveStreamProtocol.Mjpeg;

    public MjpegLiveStreamPlayerFactory(
        Dispatcher dispatcher,
        LiveStreamConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(options);
        _dispatcher = dispatcher;
        _options = options;
    }

    public ILiveStreamPlayer Create()
    {
        var decoderOptions = new MjpegDecoderOptions
        {
            ReconnectDelay = _options.ReconnectDelay,
            ServerCertificateValidationCallback =
                _options.ServerCertificateValidationCallback
        };

        return new MjpegLiveStreamPlayer(
            new MjpegDecoder(_dispatcher, decoderOptions));
    }
}

/// <summary>
/// <see cref="MjpegDecoder"/> sınıfını protokolden bağımsız canlı yayın
/// sözleşmesine uyarlayan adapter'dır.
/// </summary>
internal sealed class MjpegLiveStreamPlayer : ILiveStreamPlayer
{
    private readonly MjpegDecoder _decoder;
    private bool _disposed;

    public LiveStreamProtocol Protocol => LiveStreamProtocol.Mjpeg;

    public event EventHandler<LiveStreamFrameReadyEventArgs>? FrameReady;

    public event EventHandler<LiveStreamErrorEventArgs>? ErrorOccurred;

    public MjpegLiveStreamPlayer(MjpegDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _decoder = decoder;
        _decoder.FrameReady += Decoder_FrameReady;
        _decoder.ErrorOccurred += Decoder_ErrorOccurred;
    }

    public void Start(Uri streamUri)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _decoder.Start(streamUri);
    }

    public Task StopAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _decoder.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _decoder.FrameReady -= Decoder_FrameReady;
        _decoder.ErrorOccurred -= Decoder_ErrorOccurred;
        await _decoder.DisposeAsync();
    }

    private void Decoder_FrameReady(
        object? sender,
        MjpegFrameReadyEventArgs e)
    {
        FrameReady?.Invoke(
            this,
            new LiveStreamFrameReadyEventArgs(e.Frame));
    }

    private void Decoder_ErrorOccurred(
        object? sender,
        MjpegErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(
            this,
            new LiveStreamErrorEventArgs(
                e.Exception,
                e.WillReconnect));
    }
}

internal sealed class LiveStreamFrameReadyEventArgs : EventArgs
{
    public ImageSource Frame { get; }

    public LiveStreamFrameReadyEventArgs(ImageSource frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Frame = frame;
    }
}

internal sealed class LiveStreamErrorEventArgs : EventArgs
{
    public Exception Exception { get; }

    public bool WillReconnect { get; }

    public LiveStreamErrorEventArgs(
        Exception exception,
        bool willReconnect)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
        WillReconnect = willReconnect;
    }
}
