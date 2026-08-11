namespace UyduArayuz_1.Services.Video;

/// <summary>
/// WebRTC bağlantı ve çizim teknolojisini servis katmanından ayırır.
/// </summary>
public interface IWebRtcPlaybackAdapter : IAsyncDisposable
{
    event EventHandler<WebRtcPlaybackFailedEventArgs>? PlaybackFailed;

    Task StartAsync(Uri playerPageUri, CancellationToken cancellationToken);

    Task StopAsync();
}

public sealed class WebRtcPlaybackFailedEventArgs : EventArgs
{
    public Exception Exception { get; }

    public WebRtcPlaybackFailedEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }
}
