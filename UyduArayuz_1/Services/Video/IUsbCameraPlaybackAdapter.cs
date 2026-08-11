namespace UyduArayuz_1.Services.Video;

/// <summary>
/// USB kamera yakalama ve WPF üzerinde kare çizme teknolojisini servis
/// katmanındaki yaşam döngüsünden ayırır.
/// </summary>
public interface IUsbCameraPlaybackAdapter : IAsyncDisposable
{
    event EventHandler<UsbCameraPlaybackFailedEventArgs>? PlaybackFailed;

    Task StartAsync(int deviceIndex, CancellationToken cancellationToken);

    Task StopAsync();
}

public sealed class UsbCameraPlaybackFailedEventArgs : EventArgs
{
    public Exception Exception { get; }

    public UsbCameraPlaybackFailedEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }
}
