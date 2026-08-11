namespace UyduArayuz_1.Services.Video;

/// <summary>
/// Uygulamanın desteklediği canlı yayın protokollerini tanımlar.
/// </summary>
public enum LiveStreamProtocol
{
    WebRtc
}

/// <summary>
/// Bir canlı yayın oturumunun kullanıcı arayüzüne yansıtılan durumudur.
/// </summary>
public enum LiveStreamState
{
    Idle,
    Starting,
    Playing,
    Stopping,
    Faulted
}

/// <summary>
/// Protokolden bağımsız canlı yayın yaşam döngüsü sözleşmesidir.
/// Görüntünün nasıl çizildiği adapter katmanının sorumluluğundadır.
/// </summary>
public interface ILiveStreamPlayer : IAsyncDisposable
{
    LiveStreamProtocol Protocol { get; }

    LiveStreamState State { get; }

    event EventHandler<LiveStreamStateChangedEventArgs>? StateChanged;

    event EventHandler<LiveStreamErrorEventArgs>? ErrorOccurred;

    Task StartAsync(Uri streamUri, CancellationToken cancellationToken = default);

    Task StopAsync();
}

/// <summary>
/// Tek bir protokol için player oluşturan factory sözleşmesidir.
/// </summary>
public interface ILiveStreamPlayerFactory
{
    LiveStreamProtocol Protocol { get; }

    ILiveStreamPlayer Create();
}

/// <summary>
/// Kayıtlı factory'lerden istenen protokole uygun olanı seçer.
/// </summary>
public interface ILiveStreamPlayerResolver
{
    ILiveStreamPlayer Resolve(LiveStreamProtocol protocol);
}

public sealed class LiveStreamStateChangedEventArgs : EventArgs
{
    public LiveStreamState State { get; }

    public LiveStreamStateChangedEventArgs(LiveStreamState state)
    {
        State = state;
    }
}

public sealed class LiveStreamErrorEventArgs : EventArgs
{
    public Exception Exception { get; }

    public bool WillReconnect { get; }

    public LiveStreamErrorEventArgs(Exception exception, bool willReconnect)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
        WillReconnect = willReconnect;
    }
}
