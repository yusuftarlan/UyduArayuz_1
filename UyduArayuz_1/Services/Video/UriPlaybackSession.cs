using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using UyduArayuz_1.Models.Video;

namespace UyduArayuz_1.Services.Video
{
    public sealed class UriPlaybackSession : IVideoPlaybackSession
    {
        private readonly IUriPlaybackAdapter _adapter;
        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private bool _disposed;

        public VideoSourceDescriptor Source { get; }

        public VideoPlaybackState State { get; private set; }

        public string? ErrorMessage { get; private set; }

        public event EventHandler? StateChanged;

        public UriPlaybackSession(
            VideoSourceDescriptor source,
            IUriPlaybackAdapter adapter)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(adapter);

            if (source is not LocalFileSourceDescriptor &&
                source is not NetworkStreamSourceDescriptor)
            {
                throw new ArgumentException(
                    "URI playback session yalnızca yerel dosya veya ağ kaynağı kabul eder.",
                    nameof(source));
            }

            Source = source;
            _adapter = adapter;

            State = VideoPlaybackState.Idle;
            _adapter.PlaybackEnded += Adapter_PlaybackEnded;
            _adapter.PlaybackFailed += Adapter_PlaybackFailed;
        }

        private Uri ResolvePlaybackUri()
        {
            if (Source is LocalFileSourceDescriptor localFile)
            {
                if (string.IsNullOrWhiteSpace(localFile.FilePath))
                {
                    throw new InvalidOperationException(
                        "Yerel video dosyasının yolu boş olamaz.");
                }

                string fullPath = Path.GetFullPath(localFile.FilePath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException(
                        "Yerel video dosyası bulunamadı.",
                        fullPath);
                }

                return new Uri(fullPath, UriKind.Absolute);
            }

            if (Source is NetworkStreamSourceDescriptor networkStream)
            {
                if (!networkStream.StreamUri.IsAbsoluteUri)
                {
                    throw new InvalidOperationException(
                        "Ağ medya adresi mutlak bir URI olmalıdır.");
                }

                return networkStream.StreamUri;
            }

            throw new NotSupportedException(
                $"'{Source.Kind}' türü URI oynatımı tarafından desteklenmiyor.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(UriPlaybackSession));
            }
        }

        public async Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            await _operationLock.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                if (State is VideoPlaybackState.Starting or
                    VideoPlaybackState.Playing)
                {
                    return;
                }

                try
                {
                    ChangeState(VideoPlaybackState.Starting);

                    Uri playbackUri = ResolvePlaybackUri();

                    await _adapter.PlayAsync(
                        playbackUri,
                        cancellationToken);

                    if (State != VideoPlaybackState.Faulted)
                    {
                        ChangeState(VideoPlaybackState.Playing);
                    }
                }
                catch (OperationCanceledException)
                {
                    ChangeState(VideoPlaybackState.Idle);
                    throw;
                }
                catch (Exception ex)
                {
                    ChangeState(
                        VideoPlaybackState.Faulted,
                        ex.Message);

                    throw;
                }
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            await _operationLock.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                if (State == VideoPlaybackState.Stopping ||
                    (State == VideoPlaybackState.Idle &&
                    _adapter.CurrentSource is null))
                {
                    return;
                }

                VideoPlaybackState previousState = State;
                string? previousError = ErrorMessage;

                try
                {
                    ChangeState(VideoPlaybackState.Stopping);

                    await _adapter.StopAsync(cancellationToken);

                    ChangeState(VideoPlaybackState.Idle);
                }
                catch (OperationCanceledException)
                {
                    ChangeState(previousState, previousError);
                    throw;
                }
                catch (Exception ex)
                {
                    ChangeState(
                        VideoPlaybackState.Faulted,
                        ex.Message);

                    throw;
                }
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _operationLock.WaitAsync();

            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                try
                {
                    if (_adapter.CurrentSource is not null)
                    {
                        ChangeState(VideoPlaybackState.Stopping);

                        await _adapter.StopAsync(
                            CancellationToken.None);
                    }

                    ChangeState(VideoPlaybackState.Idle);
                }
                catch (Exception ex)
                {
                    ChangeState(
                        VideoPlaybackState.Faulted,
                        ex.Message);

                    throw;
                }
                finally
                {
                    _adapter.PlaybackEnded -= Adapter_PlaybackEnded;
                    _adapter.PlaybackFailed -= Adapter_PlaybackFailed;
                }
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private void ChangeState(VideoPlaybackState newState, string? errorMessage = null)
        {
            if (State == newState &&
                ErrorMessage == errorMessage)
            {
                return;
            }

            State = newState;
            ErrorMessage = errorMessage;

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        private void Adapter_PlaybackEnded(
            object? sender,
            EventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            if (State == VideoPlaybackState.Playing)
            {
                ChangeState(VideoPlaybackState.Idle);
            }
        }

        private void Adapter_PlaybackFailed(
            object? sender,
            EventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            if (State is VideoPlaybackState.Idle or
                VideoPlaybackState.Stopping)
            {
                return;
            }

            string errorMessage =
                _adapter.LastError?.Message ??
                "Video oynatılırken bilinmeyen bir hata oluştu.";

            ChangeState(
                VideoPlaybackState.Faulted,
                errorMessage);
        }
    }
}