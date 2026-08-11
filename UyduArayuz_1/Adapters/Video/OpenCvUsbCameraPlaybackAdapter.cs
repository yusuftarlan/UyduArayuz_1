using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using UyduArayuz_1.Services.Video;

namespace UyduArayuz_1.Adapters.Video;

/// <summary>
/// Windows'un DirectShow/Media Foundation kamera arka uçlarını OpenCV üzerinden
/// kullanır ve yalnızca en yeni kareyi WPF Image kontrolüne taşır.
/// </summary>
public sealed class OpenCvUsbCameraPlaybackAdapter : IUsbCameraPlaybackAdapter
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
    private readonly Image _image;
    private readonly object _lifecycleSync = new();
    private readonly object _frameSync = new();

    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private BitmapSource? _pendingFrame;
    private bool _frameDispatchScheduled;
    private bool _disposed;

    public event EventHandler<UsbCameraPlaybackFailedEventArgs>? PlaybackFailed;

    public OpenCvUsbCameraPlaybackAdapter(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _image = image;
    }

    public async Task StartAsync(
        int deviceIndex,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (deviceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceIndex));
        }

        var firstFrame = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lifecycleSync)
        {
            if (_runTask is not null)
            {
                throw new InvalidOperationException("USB kamera zaten çalışıyor.");
            }

            _runCancellation = new CancellationTokenSource();
            CancellationToken runToken = _runCancellation.Token;
            _runTask = Task.Run(
                () => CaptureLoop(deviceIndex, firstFrame, runToken),
                CancellationToken.None);
        }

        using var startCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startCancellation.CancelAfter(StartTimeout);

        try
        {
            await firstFrame.Task.WaitAsync(startCancellation.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            await StopAsync();
            throw new TimeoutException(
                $"USB kamera {StartTimeout.TotalSeconds:0} saniye içinde görüntü üretmedi.");
        }
        catch
        {
            await StopAsync();
            throw;
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task? runTask;

        lock (_lifecycleSync)
        {
            cancellation = _runCancellation;
            runTask = _runTask;
            _runCancellation = null;
            _runTask = null;
        }

        if (cancellation is not null)
        {
            cancellation.Cancel();

            try
            {
                if (runTask is not null)
                {
                    await runTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Kullanıcının durdurma isteği normal yaşam döngüsüdür.
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        ClearPendingFrame();
        await InvokeOnUiAsync(() => _image.Source = null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void CaptureLoop(
        int deviceIndex,
        TaskCompletionSource firstFrame,
        CancellationToken cancellationToken)
    {
        try
        {
            using VideoCapture capture = OpenCamera(deviceIndex);
            using var frame = new Mat();
            int consecutiveReadFailures = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!capture.Read(frame) || frame.Empty())
                {
                    consecutiveReadFailures++;
                    if (consecutiveReadFailures >= 30)
                    {
                        throw new InvalidOperationException(
                            $"{deviceIndex} numaralı USB kameradan kare okunamadı.");
                    }

                    cancellationToken.WaitHandle.WaitOne(20);
                    continue;
                }

                consecutiveReadFailures = 0;
                BitmapSource bitmap = CreateBitmapSource(frame);
                PublishFrame(bitmap);
                firstFrame.TrySetResult();
            }
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            if (!firstFrame.TrySetException(exception) &&
                !cancellationToken.IsCancellationRequested)
            {
                PublishFailure(exception);
            }
        }
    }

    private static VideoCapture OpenCamera(int deviceIndex)
    {
        var capture = new VideoCapture();

        if (!capture.Open(deviceIndex, VideoCaptureAPIs.DSHOW) &&
            !capture.Open(deviceIndex, VideoCaptureAPIs.MSMF))
        {
            capture.Dispose();
            throw new InvalidOperationException(
                $"{deviceIndex} numaralı USB kamera açılamadı. Kamera başka bir uygulama tarafından kullanılıyor veya bu numarada bir aygıt yok.");
        }

        capture.Set(VideoCaptureProperties.BufferSize, 1);
        return capture;
    }

    private static BitmapSource CreateBitmapSource(Mat frame)
    {
        PixelFormat pixelFormat = frame.Channels() switch
        {
            1 => PixelFormats.Gray8,
            3 => PixelFormats.Bgr24,
            4 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException(
                $"Kameranın {frame.Channels()} kanallı piksel biçimi desteklenmiyor.")
        };

        int stride = checked((int)frame.Step());
        int byteCount = checked(stride * frame.Rows);
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(frame.Data, pixels, 0, byteCount);

        BitmapSource bitmap = BitmapSource.Create(
            frame.Cols,
            frame.Rows,
            96,
            96,
            pixelFormat,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }

    private void PublishFrame(BitmapSource frame)
    {
        bool scheduleDispatch;

        lock (_frameSync)
        {
            if (_disposed)
            {
                return;
            }

            _pendingFrame = frame;
            scheduleDispatch = !_frameDispatchScheduled;
            _frameDispatchScheduled = true;
        }

        if (scheduleDispatch)
        {
            _image.Dispatcher.BeginInvoke(
                DispatcherPriority.Render,
                new Action(DispatchLatestFrame));
        }
    }

    private void DispatchLatestFrame()
    {
        BitmapSource? frame;

        lock (_frameSync)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
            _frameDispatchScheduled = false;
        }

        if (!_disposed && frame is not null)
        {
            _image.Source = frame;
        }
    }

    private void PublishFailure(Exception exception)
    {
        _image.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                if (!_disposed)
                {
                    PlaybackFailed?.Invoke(
                        this,
                        new UsbCameraPlaybackFailedEventArgs(exception));
                }
            }));
    }

    private void ClearPendingFrame()
    {
        lock (_frameSync)
        {
            _pendingFrame = null;
            _frameDispatchScheduled = false;
        }
    }

    private Task InvokeOnUiAsync(Action action)
    {
        if (_image.Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _image.Dispatcher.InvokeAsync(action).Task;
    }
}
