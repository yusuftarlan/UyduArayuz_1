namespace UyduArayuz_1.Services.Video;

/// <summary>
/// USB kamera adapter'ını ortak canlı yayın yaşam döngüsüne uyarlar.
/// </summary>
public sealed class UsbCameraLiveStreamPlayer : ILiveStreamPlayer
{
    private readonly IUsbCameraPlaybackAdapter _adapter;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private bool _disposed;

    public LiveStreamProtocol Protocol => LiveStreamProtocol.UsbCamera;

    public LiveStreamState State { get; private set; } = LiveStreamState.Idle;

    public event EventHandler<LiveStreamStateChangedEventArgs>? StateChanged;

    public event EventHandler<LiveStreamErrorEventArgs>? ErrorOccurred;

    public UsbCameraLiveStreamPlayer(IUsbCameraPlaybackAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapter = adapter;
        _adapter.PlaybackFailed += Adapter_PlaybackFailed;
    }

    public async Task StartAsync(
        LiveStreamSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (source is not UsbCameraLiveStreamSource usbSource)
        {
            throw new ArgumentException(
                "USB kamera player yalnızca UsbCameraLiveStreamSource kabul eder.",
                nameof(source));
        }

        if (usbSource.DeviceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                "Kamera numarası negatif olamaz.");
        }

        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (State is LiveStreamState.Starting or LiveStreamState.Playing)
            {
                throw new InvalidOperationException("USB kamera zaten çalışıyor.");
            }

            SetState(LiveStreamState.Starting);

            try
            {
                await _adapter.StartAsync(
                    usbSource.DeviceIndex,
                    cancellationToken);
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
        UsbCameraPlaybackFailedEventArgs e)
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

public sealed class UsbCameraLiveStreamPlayerFactory : ILiveStreamPlayerFactory
{
    private readonly Func<IUsbCameraPlaybackAdapter> _adapterFactory;

    public LiveStreamProtocol Protocol => LiveStreamProtocol.UsbCamera;

    public UsbCameraLiveStreamPlayerFactory(
        Func<IUsbCameraPlaybackAdapter> adapterFactory)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);
        _adapterFactory = adapterFactory;
    }

    public ILiveStreamPlayer Create() =>
        new UsbCameraLiveStreamPlayer(_adapterFactory());
}
