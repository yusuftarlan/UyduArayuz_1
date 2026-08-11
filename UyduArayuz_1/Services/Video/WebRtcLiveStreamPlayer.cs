namespace UyduArayuz_1.Services.Video;

/// <summary>
/// WebRTC adapter'ını protokolden bağımsız player sözleşmesine uyarlar.
/// </summary>
public sealed class WebRtcLiveStreamPlayer : ILiveStreamPlayer
{
    private readonly IWebRtcPlaybackAdapter _adapter;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private bool _disposed;

    public LiveStreamProtocol Protocol => LiveStreamProtocol.WebRtc;

    public LiveStreamState State { get; private set; } = LiveStreamState.Idle;

    public event EventHandler<LiveStreamStateChangedEventArgs>? StateChanged;

    public event EventHandler<LiveStreamErrorEventArgs>? ErrorOccurred;

    public WebRtcLiveStreamPlayer(IWebRtcPlaybackAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapter = adapter;
        _adapter.PlaybackFailed += Adapter_PlaybackFailed;
    }

    public async Task StartAsync(
        Uri streamUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streamUri);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (State is LiveStreamState.Starting or LiveStreamState.Playing)
            {
                throw new InvalidOperationException("WebRTC yayını zaten çalışıyor.");
            }

            SetState(LiveStreamState.Starting);

            try
            {
                await _adapter.StartAsync(streamUri, cancellationToken);
                SetState(LiveStreamState.Playing);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                SetState(LiveStreamState.Idle);
                throw;
            }
            catch (Exception exception)
            {
                SetState(LiveStreamState.Faulted);
                ErrorOccurred?.Invoke(
                    this,
                    new LiveStreamErrorEventArgs(exception, willReconnect: false));
                throw;
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await StopCoreAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _lifecycleLock.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            if (State != LiveStreamState.Idle)
            {
                SetState(LiveStreamState.Stopping);
                await _adapter.StopAsync();
            }

            _adapter.PlaybackFailed -= Adapter_PlaybackFailed;
            await _adapter.DisposeAsync();
            _disposed = true;
            SetState(LiveStreamState.Idle);
        }
        finally
        {
            _lifecycleLock.Release();
            _lifecycleLock.Dispose();
        }
    }

    private async Task StopCoreAsync()
    {
        await _lifecycleLock.WaitAsync();

        try
        {
            if (State == LiveStreamState.Idle)
            {
                return;
            }

            SetState(LiveStreamState.Stopping);
            await _adapter.StopAsync();
            SetState(LiveStreamState.Idle);
        }
        catch (Exception exception)
        {
            SetState(LiveStreamState.Faulted);
            ErrorOccurred?.Invoke(
                this,
                new LiveStreamErrorEventArgs(exception, willReconnect: false));
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private void Adapter_PlaybackFailed(
        object? sender,
        WebRtcPlaybackFailedEventArgs e)
    {
        if (_disposed || State is LiveStreamState.Stopping or LiveStreamState.Idle)
        {
            return;
        }

        SetState(LiveStreamState.Faulted);
        ErrorOccurred?.Invoke(
            this,
            new LiveStreamErrorEventArgs(e.Exception, willReconnect: false));
    }

    private void SetState(LiveStreamState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, new LiveStreamStateChangedEventArgs(state));
    }
}

/// <summary>
/// WebRTC player oluşturma ayrıntısını composition root'tan ayırır.
/// </summary>
public sealed class WebRtcLiveStreamPlayerFactory : ILiveStreamPlayerFactory
{
    private readonly Func<IWebRtcPlaybackAdapter> _adapterFactory;

    public LiveStreamProtocol Protocol => LiveStreamProtocol.WebRtc;

    public WebRtcLiveStreamPlayerFactory(
        Func<IWebRtcPlaybackAdapter> adapterFactory)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);
        _adapterFactory = adapterFactory;
    }

    public ILiveStreamPlayer Create() =>
        new WebRtcLiveStreamPlayer(_adapterFactory());
}
