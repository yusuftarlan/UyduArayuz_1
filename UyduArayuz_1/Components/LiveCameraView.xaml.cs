using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using UyduArayuz_1.Adapters.Video;
using UyduArayuz_1.Services.Video;
using UyduArayuz_1.ViewModels;
using System.IO;
using Microsoft.Win32;
using UyduArayuz_1.Models.Video;
namespace UyduArayuz_1.Components;

public partial class LiveCameraView : UserControl
{
    private MediaElementPlaybackAdapter? _adapter;
    private LiveCameraViewModel? _viewModel;

    public LiveCameraView()
    {
        InitializeComponent();

        Loaded += LiveCameraView_Loaded;
        Unloaded += LiveCameraView_Unloaded;
    }

    private void LiveCameraView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            return;
        }

        var adapter =
            new MediaElementPlaybackAdapter(CameraStreamPlayer);

        var uriFactory =
            new UriPlaybackSessionFactory(adapter);

        var resolver =
            new VideoPlaybackSessionResolver(
                new IVideoPlaybackSessionFactory[]
                {
                    uriFactory
                });

        var viewModel =
            new LiveCameraViewModel(resolver);

        _adapter = adapter;
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void LiveCameraView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        LiveCameraViewModel? viewModel = _viewModel;
        MediaElementPlaybackAdapter? adapter = _adapter;

        _viewModel = null;
        _adapter = null;
        DataContext = null;

        try
        {
            if (viewModel is not null)
            {
                await viewModel.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Video ViewModel kapatılırken hata oluştu: {ex}");
        }
        finally
        {
            if (adapter is not null)
            {
                await adapter.DisposeAsync();
            }
        }
    }
    private void SelectLocalFile_Click(
    object sender,
    RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Video dosyası seç",
            Filter =
                "Video dosyaları|*.mp4;*.avi;*.wmv;*.mov;*.mkv|" +
                "Tüm dosyalar|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LocalFilePathTextBox.Text = dialog.FileName;
        }
    }

    private async void PlayLocalFile_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        string filePath =
            LocalFilePathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            MessageBox.Show(
                "Önce bir video dosyası seçmelisiniz.",
                "Video kaynağı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var source = new LocalFileSourceDescriptor(
            Guid.NewGuid().ToString("N"),
            Path.GetFileName(filePath),
            filePath);

        await StartSourceAsync(source);
    }

    private async void PlayNetworkStream_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        string address =
            NetworkAddressTextBox.Text.Trim();

        if (!Uri.TryCreate(
                address,
                UriKind.Absolute,
                out Uri? streamUri))
        {
            MessageBox.Show(
                "Geçerli ve mutlak bir medya adresi girin.",
                "Geçersiz adres",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var source = new NetworkStreamSourceDescriptor(
            Guid.NewGuid().ToString("N"),
            streamUri.ToString(),
            streamUri);

        await StartSourceAsync(source);
    }

    private async Task StartSourceAsync(
        VideoSourceDescriptor source)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.StartAsync(source);
        }
        catch (OperationCanceledException)
        {
            // StopAsync başlangıç işlemini iptal etti.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Video başlatılamadı",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void StopPlayback_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.StopAsync();
        }
        catch (OperationCanceledException)
        {
            // Kullanıcı kaynaklı iptal normal bir sonuçtur.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Video durdurulamadı",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}