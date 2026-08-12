using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TeknofestUyduArayuz.Adapters.Video;

namespace TeknofestUyduArayuz.Components;

public partial class LiveCameraView : UserControl
{
    private enum CameraViewState
    {
        Idle,
        Starting,
        Playing,
        Stopping,
        Faulted
    }

    public int CameraIndex
    {
        get => (int)GetValue(CameraIndexProperty);
        set => SetValue(CameraIndexProperty, value);
    }

    public static readonly DependencyProperty CameraIndexProperty =
        DependencyProperty.Register(
            nameof(CameraIndex),
            typeof(int),
            typeof(LiveCameraView),
            new PropertyMetadata(0));

    // Start, stop ve unload aynı adaptörü değiştirdiği için yaşam döngüsü tek geçide alınır.
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private OpenCvUsbCameraPlaybackAdapter? _adapter;
    private CancellationTokenSource? _startCancellation;
    private bool _isUnloaded;

    public LiveCameraView()
    {
        InitializeComponent();
        Loaded += LiveCameraView_Loaded;
        Unloaded += LiveCameraView_Unloaded;
        ApplyState(CameraViewState.Idle);
    }

    private void LiveCameraView_Loaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = false;
    }

    private async void StartStream_Click(object sender, RoutedEventArgs e)
    {
        CancellationTokenSource? startCancellation = null;

        if (CameraIndex < 0)
        {
            LogError(
                "kaynak doğrulama",
                new ArgumentOutOfRangeException(
                    nameof(CameraIndex),
                    "Kamera numarası negatif olamaz."));
            return;
        }

        await _lifecycleLock.WaitAsync();
        try
        {
            if (_isUnloaded)
            {
                return;
            }

            await ReleaseAdapterAsync();

            var adapter = new OpenCvUsbCameraPlaybackAdapter(UsbCameraImage);
            adapter.PlaybackFailed += Adapter_PlaybackFailed;
            _adapter = adapter;

            startCancellation = new CancellationTokenSource();
            _startCancellation = startCancellation;
            ApplyState(CameraViewState.Starting);

            await adapter.StartAsync(CameraIndex, startCancellation.Token);
            ApplyState(CameraViewState.Playing);
        }
        catch (OperationCanceledException)
            when (startCancellation?.IsCancellationRequested == true)
        {
            await ReleaseAdapterAsync();
            ApplyState(CameraViewState.Idle);
        }
        catch (Exception exception)
        {
            LogError("başlatma", exception);
            await ReleaseAdapterAsync();
            ApplyState(CameraViewState.Faulted);
        }
        finally
        {
            if (ReferenceEquals(_startCancellation, startCancellation))
            {
                _startCancellation = null;
            }

            startCancellation?.Dispose();
            _lifecycleLock.Release();
        }
    }

    private async void StopStream_Click(object sender, RoutedEventArgs e)
    {
        _startCancellation?.Cancel();
        await _lifecycleLock.WaitAsync();

        try
        {
            ApplyState(CameraViewState.Stopping);
            await ReleaseAdapterAsync();
            ApplyState(CameraViewState.Idle);
        }
        catch (Exception exception)
        {
            LogError("durdurma", exception);
            ApplyState(CameraViewState.Faulted);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async void LiveCameraView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _isUnloaded = true;
        _startCancellation?.Cancel();
        await _lifecycleLock.WaitAsync();

        try
        {
            ApplyState(CameraViewState.Stopping);
            await ReleaseAdapterAsync();
            ApplyState(CameraViewState.Idle);
        }
        catch (Exception exception)
        {
            LogError("kapatma", exception);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private void Adapter_PlaybackFailed(Exception exception)
    {
        if (_adapter is null || _isUnloaded)
        {
            return;
        }

        LogError("canlı yayın", exception);
        ApplyState(CameraViewState.Faulted);
    }

    private async ValueTask ReleaseAdapterAsync()
    {
        OpenCvUsbCameraPlaybackAdapter? adapter = _adapter;
        _adapter = null;

        if (adapter is null)
        {
            return;
        }

        adapter.PlaybackFailed -= Adapter_PlaybackFailed;
        await adapter.DisposeAsync();
    }

    private void ApplyState(CameraViewState state)
    {
        StartButton.IsEnabled =
            state is CameraViewState.Idle or CameraViewState.Faulted;
        StopButton.IsEnabled =
            state is CameraViewState.Starting or CameraViewState.Playing;
    }

    private static void LogError(string operation, Exception exception)
    {
        string message =
            $"[{DateTime.Now:HH:mm:ss}] [ERROR] USB kamera {operation}: {exception}";
        Debug.WriteLine(message);
        Console.Error.WriteLine(message);
    }
}
