using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace UyduArayuz_1.Components
{
    /// <summary>
    /// AttitudeIndicator.xaml etkileşim mantığı
    /// </summary>
    public partial class AttitudeIndicator : UserControl
    {
        private const double TargetModelSize = 3.0;
        private const double InitialCameraDistanceFactor = 0.72;

        private bool _isModelLoaded;
        private bool _initialCameraViewApplied;

        public AttitudeIndicator()
        {
            InitializeComponent();
            SatelliteModel.ModelLoaded += SatelliteModel_OnModelLoaded;
            Loaded += AttitudeIndicator_OnLoaded;
            LoadSatelliteModel();
        }

        private void LoadSatelliteModel()
        {
            string modelPath = Path.Combine(AppContext.BaseDirectory, "body-model", "counsat.stl");

            if (!File.Exists(modelPath))
            {
                ShowModelLoadError($"3B model bulunamadı: {modelPath}");
                return;
            }

            try
            {
                SatelliteModel.Source = modelPath;
            }
            catch (Exception ex)
            {
                ShowModelLoadError($"3B model yüklenemedi: {ex.Message}");
            }
        }

        private void SatelliteModel_OnModelLoaded(object sender, RoutedEventArgs e)
        {
            Rect3D bounds = VisualTreeHelper.GetDescendantBounds(SatelliteModel);
            double largestDimension = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));

            if (bounds.IsEmpty || largestDimension <= 0 || double.IsNaN(largestDimension))
            {
                ShowModelLoadError("3B modelin geçerli geometrisi bulunamadı.");
                return;
            }

            // Dönüşlerin modelin geometrik merkezi etrafında gerçekleşmesini sağlar.
            ModelCenterOffset.OffsetX = -(bounds.X + (bounds.SizeX / 2));
            ModelCenterOffset.OffsetY = -(bounds.Y + (bounds.SizeY / 2));
            ModelCenterOffset.OffsetZ = -(bounds.Z + (bounds.SizeZ / 2));

            double uniformScale = TargetModelSize / largestDimension;
            ModelScale.ScaleX = uniformScale;
            ModelScale.ScaleY = uniformScale;
            ModelScale.ScaleZ = uniformScale;

            ModelLoadError.Visibility = Visibility.Collapsed;
            _isModelLoaded = true;
            ApplyInitialCameraView();
        }

        private void AttitudeIndicator_OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyInitialCameraView();
        }

        private void ApplyInitialCameraView()
        {
            if (!IsLoaded || !_isModelLoaded || _initialCameraViewApplied)
            {
                return;
            }

            viewPort.ZoomExtents(0);

            if (viewPort.Camera is ProjectionCamera camera)
            {
                Point3D target = camera.Position + camera.LookDirection;
                Vector3D closerLookDirection = camera.LookDirection * InitialCameraDistanceFactor;

                camera.Position = target - closerLookDirection;
                camera.LookDirection = closerLookDirection;
            }

            _initialCameraViewApplied = true;
        }

        private void ShowModelLoadError(string message)
        {
            ModelLoadError.Text = message;
            ModelLoadError.Visibility = Visibility.Visible;
        }
    }
}
