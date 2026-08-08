using System.ComponentModel;
using System.Runtime.CompilerServices;
using UyduArayuz_1.Models.Video;
using UyduArayuz_1.Services.Video;

namespace UyduArayuz_1.ViewModels;

public sealed class LiveCameraViewModel :
    INotifyPropertyChanged,
    IAsyncDisposable
{
    private readonly IVideoPlaybackSessionResolver _sessionResolver;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly object _startCancellationSync = new();

    private IVideoPlaybackSession? _currentSession;
    private CancellationTokenSource? _activeStartCancellation;
    private bool _disposed;

    public VideoPlaybackState State =>
        _currentSession?.State ?? VideoPlaybackState.Idle;

    public string? ErrorMessage =>
        _currentSession?.ErrorMessage;

    public VideoSourceDescriptor? CurrentSource =>
        _currentSession?.Source;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LiveCameraViewModel(
        IVideoPlaybackSessionResolver sessionResolver)
    {
        ArgumentNullException.ThrowIfNull(sessionResolver);

        _sessionResolver = sessionResolver;
    }

    public async Task StartAsync(
        VideoSourceDescriptor source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();

            await ReleaseCurrentSessionAsync();

            cancellationToken.ThrowIfCancellationRequested();

            IVideoPlaybackSession newSession =
                _sessionResolver.Resolve(source);

            newSession.StateChanged += CurrentSession_StateChanged;
            _currentSession = newSession;

            NotifyPlaybackProperties();

            using CancellationTokenSource startCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            lock (_startCancellationSync)
            {
                _activeStartCancellation = startCancellation;
            }

            try
            {
                await newSession.StartAsync(startCancellation.Token);
            }
            catch
            {
                NotifyPlaybackProperties();
                throw;
            }
            finally
            {
                lock (_startCancellationSync)
                {
                    if (ReferenceEquals(
                        _activeStartCancellation,
                        startCancellation))
                    {
                        _activeStartCancellation = null;
                    }
                }
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancelActiveStart();

        await _lifecycleLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();

            IVideoPlaybackSession? session = _currentSession;

            if (session is null)
            {
                return;
            }

            try
            {
                await session.StopAsync(cancellationToken);
            }
            finally
            {
                NotifyPlaybackProperties();
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        CancelActiveStart();

        await _lifecycleLock.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await ReleaseCurrentSessionAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async ValueTask ReleaseCurrentSessionAsync()
    {
        IVideoPlaybackSession? session = _currentSession;

        if (session is null)
        {
            return;
        }

        session.StateChanged -= CurrentSession_StateChanged;
        _currentSession = null;

        try
        {
            await session.DisposeAsync();
        }
        finally
        {
            NotifyPlaybackProperties();
        }
    }

    private void CancelActiveStart()
    {
        lock (_startCancellationSync)
        {
            _activeStartCancellation?.Cancel();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(LiveCameraViewModel));
        }
    }

    private void CurrentSession_StateChanged(
        object? sender,
        EventArgs e)
    {
        NotifyPlaybackProperties();
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyPlaybackProperties()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(CurrentSource));
    }
}