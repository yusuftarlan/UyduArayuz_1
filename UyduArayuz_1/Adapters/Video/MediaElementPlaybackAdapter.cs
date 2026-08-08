using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using UyduArayuz_1.Services.Video;
using System.Windows;
namespace UyduArayuz_1.Adapters.Video
{
    public sealed class MediaElementPlaybackAdapter
        : IUriPlaybackAdapter, IAsyncDisposable
    {
        private readonly MediaElement _mediaElement;
        private TaskCompletionSource<bool>? _openCompletionSource;
        private bool _disposed;
        public Uri? CurrentSource { get; private set; }

        public Exception? LastError { get; private set; }

        public event EventHandler? PlaybackEnded;

        public event EventHandler? PlaybackFailed;

        public MediaElementPlaybackAdapter(
            MediaElement mediaElement)
        {
            ArgumentNullException.ThrowIfNull(mediaElement);

            _mediaElement = mediaElement;
            _mediaElement.MediaOpened += MediaElement_MediaOpened;
            _mediaElement.MediaEnded += MediaElement_MediaEnded;
            _mediaElement.MediaFailed += MediaElement_MediaFailed;
        }

        public async Task PlayAsync(
    Uri source,
    CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(source);

            cancellationToken.ThrowIfCancellationRequested();

            var completionSource =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            _openCompletionSource = completionSource;
            LastError = null;

            using CancellationTokenRegistration registration =
                cancellationToken.Register(
                    () => completionSource.TrySetCanceled(
                        cancellationToken));

            try
            {
                await _mediaElement.Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _mediaElement.Stop();

                    CurrentSource = source;
                    _mediaElement.Source = source;

                    _mediaElement.Play();
                });

                await completionSource.Task;
            }
            catch
            {
                await ClearMediaElementAsync();
                throw;
            }
            finally
            {
                if (ReferenceEquals(
                    _openCompletionSource,
                    completionSource))
                {
                    _openCompletionSource = null;
                }
            }
        }
        private async Task ClearMediaElementAsync()
        {
            await _mediaElement.Dispatcher.InvokeAsync(() =>
            {
                _mediaElement.Stop();
                _mediaElement.Source = null;

                CurrentSource = null;
            });
        }

        public async Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<bool>? pendingOpen =
                _openCompletionSource;

            _openCompletionSource = null;

            // PlayAsync hâlâ MediaOpened olayını bekliyorsa askıda kalmasını önler.
            pendingOpen?.TrySetCanceled();

            await ClearMediaElementAsync();

            cancellationToken.ThrowIfCancellationRequested();
        }
        private void MediaElement_MediaOpened(
            object sender,
            RoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            LastError = null;

            _openCompletionSource?.TrySetResult(true);
        }

        private void MediaElement_MediaEnded(
            object sender,
            RoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            PlaybackEnded?.Invoke(
                this,
                EventArgs.Empty);
        }

        private void MediaElement_MediaFailed(
            object sender,
            ExceptionRoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            Exception error =
                e.ErrorException ??
                new InvalidOperationException(
                    "MediaElement bilinmeyen bir oynatma hatası bildirdi.");

            LastError = error;

            _openCompletionSource?.TrySetException(error);

            PlaybackFailed?.Invoke(
                this,
                EventArgs.Empty);
        }
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(MediaElementPlaybackAdapter));
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            TaskCompletionSource<bool>? pendingOpen =
                _openCompletionSource;

            _openCompletionSource = null;
            pendingOpen?.TrySetCanceled();

            await _mediaElement.Dispatcher.InvokeAsync(() =>
            {
                _mediaElement.MediaOpened -= MediaElement_MediaOpened;
                _mediaElement.MediaEnded -= MediaElement_MediaEnded;
                _mediaElement.MediaFailed -= MediaElement_MediaFailed;

                _mediaElement.Stop();
                _mediaElement.Source = null;

                CurrentSource = null;
            });
        }
    }
}