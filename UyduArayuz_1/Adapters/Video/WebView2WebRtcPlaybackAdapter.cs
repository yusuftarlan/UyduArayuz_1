using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using UyduArayuz_1.Services.Video;

namespace UyduArayuz_1.Adapters.Video;

/// <summary>
/// MediaMTX WebRTC player sayfasını WebView2 üzerinde çalıştırır.
/// WebRTC sinyalleşmesi, ICE, codec çözme ve çizim Edge motorunda kalır.
/// </summary>
public sealed class WebView2WebRtcPlaybackAdapter : IWebRtcPlaybackAdapter
{
    private const string BlankPage = "about:blank";

    private const string PlaybackObserverScript = """
        (() => {
          if (window.__uyduWebRtcObserverInstalled) return;
          window.__uyduWebRtcObserverInstalled = true;

          const send = (type, message) => {
            if (window.chrome?.webview) {
              window.chrome.webview.postMessage(JSON.stringify({ type, message }));
            }
          };

          const attach = () => {
            const video = document.querySelector('video');
            if (!video) {
              window.setTimeout(attach, 100);
              return;
            }

            video.addEventListener('playing', () => send('playing', ''));
            video.addEventListener('error', () => {
              const detail = video.error?.message || 'WebRTC video oynatma hatası.';
              send('error', detail);
            });

            const message = document.getElementById('message');
            if (message) {
              const reportPageError = () => {
                const detail = message.textContent?.trim();
                if (detail) send('error', detail);
              };

              new MutationObserver(reportPageError).observe(message, {
                childList: true,
                characterData: true,
                subtree: true
              });
              reportPageError();
            }

            if (!video.paused && video.readyState >= 3) {
              send('playing', '');
            }
          };

          if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', attach, { once: true });
          } else {
            attach();
          }
        })();
        """;

    private readonly WebView2 _webView;
    private readonly TimeSpan _startTimeout;
    private TaskCompletionSource? _startCompletion;
    private Uri? _allowedOrigin;
    private bool _initialized;
    private bool _isPlaying;
    private bool _isStopping;
    private bool _disposed;

    public event EventHandler<WebRtcPlaybackFailedEventArgs>? PlaybackFailed;

    public WebView2WebRtcPlaybackAdapter(
        WebView2 webView,
        TimeSpan? startTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(webView);
        _webView = webView;
        _startTimeout = startTimeout ?? TimeSpan.FromSeconds(15);
    }

    public Task StartAsync(
        Uri playerPageUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(playerPageUri);

        return InvokeOnUiAsync(
            () => StartCoreAsync(playerPageUri, cancellationToken));
    }

    public Task StopAsync() => InvokeOnUiAsync(StopCoreAsync);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await InvokeOnUiAsync(async () =>
        {
            if (_disposed)
            {
                return;
            }

            await StopCoreAsync();
            DetachCoreEvents();
            _disposed = true;
        });
    }

    private async Task StartCoreAsync(
        Uri playerPageUri,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidatePlayerPageUri(playerPageUri);

        await EnsureInitializedAsync();

        _allowedOrigin = new Uri(playerPageUri.GetLeftPart(UriPartial.Authority));
        _isStopping = false;
        _isPlaying = false;
        _startCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Uri playbackUri = BuildPlaybackUri(playerPageUri);
        _webView.CoreWebView2.Navigate(playbackUri.AbsoluteUri);

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_startTimeout);

        try
        {
            await _startCompletion.Task.WaitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            await StopCoreAsync();
            throw new TimeoutException(
                $"WebRTC yayını {_startTimeout.TotalSeconds:0} saniye içinde oynatılmaya başlamadı.");
        }
        catch
        {
            await StopCoreAsync();
            throw;
        }
        finally
        {
            _startCompletion = null;
        }
    }

    private Task StopCoreAsync()
    {
        _isStopping = true;
        _isPlaying = false;
        _startCompletion?.TrySetCanceled();

        if (_webView.CoreWebView2 is not null &&
            !string.Equals(_webView.Source?.AbsoluteUri, BlankPage, StringComparison.Ordinal))
        {
            // MediaMTX sayfasının beforeunload handler'ı WHEP oturumunu kapatır.
            _webView.CoreWebView2.Navigate(BlankPage);
        }

        return Task.CompletedTask;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _webView.EnsureCoreWebView2Async();

        CoreWebView2 core = _webView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;

        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += Core_NavigationCompleted;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.PermissionRequested += Core_PermissionRequested;
        core.ProcessFailed += Core_ProcessFailed;
        core.WebMessageReceived += Core_WebMessageReceived;

        await core.AddScriptToExecuteOnDocumentCreatedAsync(
            PlaybackObserverScript);

        _initialized = true;
    }

    private void DetachCoreEvents()
    {
        if (!_initialized || _webView.CoreWebView2 is null)
        {
            return;
        }

        CoreWebView2 core = _webView.CoreWebView2;
        core.NavigationStarting -= Core_NavigationStarting;
        core.NavigationCompleted -= Core_NavigationCompleted;
        core.NewWindowRequested -= Core_NewWindowRequested;
        core.PermissionRequested -= Core_PermissionRequested;
        core.ProcessFailed -= Core_ProcessFailed;
        core.WebMessageReceived -= Core_WebMessageReceived;
        _initialized = false;
    }

    private void Core_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (IsAllowedNavigation(e.Uri))
        {
            return;
        }

        e.Cancel = true;
        ReportFailure(new InvalidOperationException(
            $"Güvenlik nedeniyle WebRTC görünümünün '{e.Uri}' adresine yönlenmesi engellendi."));
    }

    private void Core_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess && !_isStopping)
        {
            ReportFailure(new InvalidOperationException(
                $"WebRTC sayfası yüklenemedi. WebView2 hata kodu: {e.WebErrorStatus}."));
        }
    }

    private static void Core_NewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
    }

    private static void Core_PermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs e)
    {
        // Receive-only WHEP oynatımı kamera, mikrofon veya konum izni istemez.
        e.State = CoreWebView2PermissionState.Deny;
    }

    private void Core_ProcessFailed(
        object? sender,
        CoreWebView2ProcessFailedEventArgs e)
    {
        if (!_isStopping)
        {
            ReportFailure(new InvalidOperationException(
                $"WebRTC görüntüleme işlemi beklenmedik biçimde sonlandı: {e.ProcessFailedKind}."));
        }
    }

    private void Core_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        WebRtcPageMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<WebRtcPageMessage>(
                e.TryGetWebMessageAsString(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException exception)
        {
            ReportFailure(new InvalidOperationException(
                "WebRTC sayfasından geçersiz bir durum mesajı alındı.",
                exception));
            return;
        }

        switch (message?.Type)
        {
            case "playing":
                _isPlaying = true;
                _startCompletion?.TrySetResult();
                break;

            case "error":
                ReportFailure(new InvalidOperationException(
                    string.IsNullOrWhiteSpace(message.Message)
                        ? "WebRTC video oynatma hatası."
                        : message.Message));
                break;
        }
    }

    private bool IsAllowedNavigation(string address)
    {
        if (string.Equals(address, BlankPage, StringComparison.Ordinal))
        {
            return true;
        }

        if (_allowedOrigin is null ||
            !Uri.TryCreate(address, UriKind.Absolute, out Uri? target))
        {
            return false;
        }

        return string.Equals(
                   target.Scheme,
                   _allowedOrigin.Scheme,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   target.Host,
                   _allowedOrigin.Host,
                   StringComparison.OrdinalIgnoreCase) &&
               target.Port == _allowedOrigin.Port;
    }

    private void ReportFailure(Exception exception)
    {
        if (_isStopping || _disposed)
        {
            return;
        }

        if (_startCompletion?.TrySetException(exception) == true)
        {
            return;
        }

        if (_isPlaying)
        {
            _isPlaying = false;
            PlaybackFailed?.Invoke(
                this,
                new WebRtcPlaybackFailedEventArgs(exception));
        }
    }

    private Task InvokeOnUiAsync(Func<Task> action)
    {
        if (_webView.Dispatcher.CheckAccess())
        {
            return action();
        }

        return _webView.Dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private static void ValidatePlayerPageUri(Uri playerPageUri)
    {
        if (!playerPageUri.IsAbsoluteUri ||
            playerPageUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "WebRTC player adresi mutlak bir HTTPS URL'si olmalıdır.",
                nameof(playerPageUri));
        }

        if (playerPageUri.AbsolutePath.EndsWith(
                "/whep",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "WebView2 adapter'ına WHEP uç noktası değil MediaMTX player sayfası verilmelidir.",
                nameof(playerPageUri));
        }
    }

    private static Uri BuildPlaybackUri(Uri playerPageUri)
    {
        var builder = new UriBuilder(playerPageUri);
        string existingQuery = builder.Query.TrimStart('?');
        const string playbackOptions =
            "controls=false&muted=true&autoplay=true&playsinline=true&disablepictureinpicture=true";

        builder.Query = string.IsNullOrEmpty(existingQuery)
            ? playbackOptions
            : $"{existingQuery}&{playbackOptions}";

        return builder.Uri;
    }

    private sealed class WebRtcPageMessage
    {
        public string? Type { get; init; }

        public string? Message { get; init; }
    }
}
