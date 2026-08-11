using System.Windows;
using System.Windows.Controls;
using UyduArayuz_1.Adapters.Video;
using UyduArayuz_1.Services.Video;

namespace UyduArayuz_1.Components;

/// <summary>
/// WebRTC canlı yayın görünümünün WPF composition root'udur.
/// </summary>
public partial class LiveCameraView : UserControl
{
    private const LiveStreamProtocol ConfiguredProtocol =
        LiveStreamProtocol.WebRtc;

    private const string DefaultStreamUrl =
        "https://camera.mahuk.online/camera/";

    public string? StreamUrl
    {
        get => (string?)GetValue(StreamUrlProperty);
        set => SetValue(StreamUrlProperty, value);
    }

    public static readonly DependencyProperty StreamUrlProperty =
        DependencyProperty.Register(
            nameof(StreamUrl),
            typeof(string),
            typeof(LiveCameraView),
            new PropertyMetadata(DefaultStreamUrl));

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly ILiveStreamLogger _logger =
        new ConsoleLiveStreamLogger();
    private ILiveStreamPlayer? _player;
    private CancellationTokenSource? _startCancellation;
    private bool _isUnloaded;

    public LiveCameraView()
    {
        InitializeComponent();
        Loaded += LiveCameraView_Loaded;
        Unloaded += LiveCameraView_Unloaded;
        ApplyState(LiveStreamState.Idle);
    }

    private void LiveCameraView_Loaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = false;
    }

    private async void StartStream_Click(object sender, RoutedEventArgs e)
    {
        string address = StreamUrl?.Trim() ?? string.Empty;
        CancellationTokenSource? startCancellation = null;

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? streamUri))
        {
            _logger.LogError(
                "adres doğrulama",
                new ArgumentException(
                    "Canlı yayın adresi geçerli bir mutlak URL değil."));
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

            ILiveStreamPlayerResolver resolver = CreatePlayerResolver();
            ILiveStreamPlayer player = resolver.Resolve(ConfiguredProtocol);

            player.StateChanged += Player_StateChanged;
            player.ErrorOccurred += Player_ErrorOccurred;
            _player = player;

            startCancellation = new CancellationTokenSource();
            _startCancellation = startCancellation;
            await player.StartAsync(streamUri, startCancellation.Token);
        }
        catch (OperationCanceledException)
            when (startCancellation?.IsCancellationRequested == true)
        {
            await ReleasePlayerAsync();
        }
        catch (Exception exception)
        {
            if (_player is null)
            {
                _logger.LogError("başlatma", exception);
            }

            await ReleasePlayerAsync();
        }
        finally
        {
            if (ReferenceEquals(_startCancellation, startCancellation))
            {
                _startCancellation = null;
            }

            startCancellation?.Dispose();
            _lifecycleLock.Release();
        }
    }

    private async void StopStream_Click(object sender, RoutedEventArgs e)
    {
        _startCancellation?.Cancel();
        await _lifecycleLock.WaitAsync();

        try
        {
            await ReleasePlayerAsync();
            ApplyState(LiveStreamState.Idle);
        }
        catch (Exception exception)
        {
            _logger.LogError("durdurma", exception);
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
        _startCancellation?.Cancel();
        await _lifecycleLock.WaitAsync();

        try
        {
            await ReleasePlayerAsync();
            ApplyState(LiveStreamState.Idle);
        }
        catch (Exception exception)
        {
            _logger.LogError("kapatma", exception);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private ILiveStreamPlayerResolver CreatePlayerResolver()
    {
        ILiveStreamPlayerFactory[] factories =
        [
            new WebRtcLiveStreamPlayerFactory(
                () => new WebView2WebRtcPlaybackAdapter(StreamBrowser))
        ];

        return new LiveStreamPlayerResolver(factories);
    }

    private void Player_StateChanged(
        object? sender,
        LiveStreamStateChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _player) || _isUnloaded)
        {
            return;
        }

        ApplyState(e.State);
    }

    private void Player_ErrorOccurred(
        object? sender,
        LiveStreamErrorEventArgs e)
    {
        if (!ReferenceEquals(sender, _player) || _isUnloaded)
        {
            return;
        }

        _logger.LogError("canlı yayın", e.Exception);
    }

    private async ValueTask ReleasePlayerAsync()
    {
        ILiveStreamPlayer? player = _player;
        _player = null;

        if (player is null)
        {
            return;
        }

        player.StateChanged -= Player_StateChanged;
        player.ErrorOccurred -= Player_ErrorOccurred;
        await player.DisposeAsync();
    }

    private void ApplyState(LiveStreamState state)
    {
        StartButton.IsEnabled =
            state is LiveStreamState.Idle or LiveStreamState.Faulted;
        StopButton.IsEnabled =
            state is LiveStreamState.Starting or LiveStreamState.Playing;
    }
}
