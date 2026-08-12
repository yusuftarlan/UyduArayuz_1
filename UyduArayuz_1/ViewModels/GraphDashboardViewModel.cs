using Mapsui.Logging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UyduArayuz_1.Configuration;
using UyduArayuz_1.Models;
using UyduArayuz_1.Services;
namespace UyduArayuz_1.ViewModels
{
    public  class GraphDashboardViewModel
    {
        private readonly BatteryGraphSettings _batteryGraphSettings;

        public PlotModel PacketModel { get; private set; }
        public PlotModel HeightModel { get; private set; }
        public PlotModel PressureModel { get; private set; }
        public PlotModel VelocityModel { get; private set; }
        public PlotModel TemperatureModel { get; private set; }
        public PlotModel BatteryModel { get; private set; }
        public string BatteryGraphTitle => _batteryGraphSettings.UseFixedPercentage
            ? "Batarya (%)"
            : "Pil Gerilimi (V)";
        public PlotModel OrientationModel { get; private set; }
        public PlotModel RouteModel { get; private set; }
        public PlotModel SpareModel { get; private set; }
       
        private const int MaxDataPoints = 60;
        public readonly int MarkerSize = 3;

        public GraphDashboardViewModel(BatteryGraphSettings batteryGraphSettings)
        {
            _batteryGraphSettings = batteryGraphSettings
                ?? throw new ArgumentNullException(nameof(batteryGraphSettings));

            InitializeGraphs();
        }

        private void InitializeGraphs()
        {
            //1. Paket numarası
            PacketModel = CreateDarkAvionicsModel("");
            PacketModel.Series.Add(new LineSeries { Color = OxyColors.Yellow, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });
            var xAxis = PacketModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            if (xAxis != null)
            {
                xAxis.LabelFormatter = val => ""; // Rakamları sil
                xAxis.TickStyle = TickStyle.None; // Çentikleri sil
                xAxis.Title = "";
            }
            PacketModel.PlotMargins = new OxyThickness(35, 0, 5, 3);

            // 2. Basınç
            PressureModel = CreateDarkAvionicsModel("Pa");
            PressureModel.Series.Add(new LineSeries { Color = OxyColors.Yellow, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });
            xAxis = PressureModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);

            if (xAxis != null)
            {
                xAxis.LabelFormatter = val => ""; // Rakamları sil
                xAxis.TickStyle = TickStyle.None; // Çentikleri sil
                xAxis.Title = "";
            }
            PressureModel.PlotMargins = new OxyThickness(35, 0, 5, 3);

            // 3. Yükseklik Karşılaştırması (Barometrik vs GPS)
            HeightModel = CreateDarkAvionicsModel("m");
            HeightModel.Series.Add(new LineSeries { Title = "Barometric", Color = OxyColors.Cyan, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });
            HeightModel.Series.Add(new LineSeries { Title = "GPS", Color = OxyColors.Magenta, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });

            xAxis = HeightModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);

            if (xAxis != null)
            {
                xAxis.LabelFormatter = val => ""; // Rakamları sil
                xAxis.TickStyle = TickStyle.None; // Çentikleri sil
                xAxis.Title = "";
            }
            HeightModel.PlotMargins = new OxyThickness(35, 0, 5, 3);


            // 4. İniş Hızı
            VelocityModel = CreateDarkAvionicsModel("m/s");
            VelocityModel.Series.Add(new LineSeries { Color = OxyColors.LimeGreen, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });

           

            // 5. Sıcaklık
            TemperatureModel = CreateDarkAvionicsModel("C");
            TemperatureModel.Series.Add(new LineSeries { Color = OxyColors.Orange, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });

            // 6. Batarya
            BatteryModel = CreateDarkAvionicsModel(_batteryGraphSettings.UseFixedPercentage ? "%" : "V");
            BatteryModel.Series.Add(new LineSeries { Color = OxyColors.Red, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = MarkerSize });

            if (_batteryGraphSettings.UseFixedPercentage)
            {
                LinearAxis batteryAxis = (LinearAxis)BatteryModel.Axes.Single(axis => axis.Position == AxisPosition.Left);
                batteryAxis.Minimum = 0;
                batteryAxis.Maximum = 100;
            }

            // 7. Oryantasyon (Pitch, Roll, Yaw)
           /* OrientationModel = CreateDarkAvionicsModel("o");
            OrientationModel.Series.Add(new LineSeries { Title = "Pitch", Color = OxyColors.Cyan, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = 4 });
            OrientationModel.Series.Add(new LineSeries { Title = "Roll", Color = OxyColors.Yellow, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = 4 });
            OrientationModel.Series.Add(new LineSeries { Title = "Yaw", Color = OxyColors.Magenta, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = 4 });
           
            // 8. 2D GPS Rotası (Zaman ekseni yok, Longitude vs Latitude)
            RouteModel = new PlotModel
            {
                
                Background = OxyColors.Transparent,
                TextColor = OxyColors.LightGray,
                PlotAreaBorderColor = OxyColors.LightGray
            };
            RouteModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Longitude", TextColor = OxyColors.LightGray, TicklineColor = OxyColors.LightGray });
            RouteModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Latitude", TextColor = OxyColors.LightGray, TicklineColor = OxyColors.LightGray });
            // Rota için çizgi serisi (Sliding window uygulanmayacak)
            RouteModel.Series.Add(new LineSeries { Color = OxyColors.Lime, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = 3 });
           */
            
        }

        // Karanlık Tema Şablonu Üreticisi
        private PlotModel CreateDarkAvionicsModel(string yAxisUnit)
        {
            var model = new PlotModel
            {
                
                Background = OxyColors.Transparent,
                TextColor = OxyColors.LightGray,
                PlotAreaBorderColor = OxyColors.LightGray
            };

            // Şık ve sade gösterge (Legend)
            model.Legends.Add(new OxyPlot.Legends.Legend { LegendTextColor = OxyColors.White });

          

            // X Ekseni: Paket Numarası
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman",
                StringFormat = "HH:mm:ss",
                Angle = 45,
                IntervalType = DateTimeIntervalType.Seconds,
                MinorIntervalType = DateTimeIntervalType.Seconds,
                TextColor = OxyColors.LightGray,
                TitleColor = OxyColors.White, // Eksen başlığı daha belirgin olsun
                TicklineColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromArgb(30, 255, 255, 255)
            });

            // Y Ekseni: Birim (Pascal, Metre, vb.)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColors.LightGray,
                TitleColor = OxyColors.White,
                TicklineColor = OxyColors.LightGray,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromArgb(30, 255, 255, 255)
            });
            return model;
        }
        public void UpdateGraphs(TelemetryPacket packet)
        {
            if (packet == null) return;

            DateTime sentDate = DateTime.ParseExact(
                packet.SentDate,
                "dd.MM.yyyy HH:mm:ss",
                CultureInfo.InvariantCulture
            );

            Debug.WriteLine($"Grafikler güncelleniyor Zaman: {sentDate:dd.MM.yyyy HH:mm:ss}");

            


            // X Ekseni (Zaman çizgisi) için Paket Numarasını kullanıyoruz.
            double xValue = DateTimeAxis.ToDouble(sentDate);


            lock (PacketModel.SyncRoot)
            {
                var series = (LineSeries)PacketModel.Series[0];
                series.Points.Add(new DataPoint(xValue, packet.PacketNo));
                ApplySlidingWindow(series);
                PacketModel.InvalidatePlot(true);
            } 

            // 1. Yükseklik Grafiği
            lock (HeightModel.SyncRoot)
            {
                var baroSeries = (LineSeries)HeightModel.Series[0];
                var gpsSeries = (LineSeries)HeightModel.Series[1];

                // Yeni noktaları ekle
                baroSeries.Points.Add(new DataPoint(xValue, packet.Height));
                gpsSeries.Points.Add(new DataPoint(xValue, packet.GpsAltitude));

                // 60 noktadan eskisini sil (RAM Şişmesini Önle)
                ApplySlidingWindow(baroSeries);
                ApplySlidingWindow(gpsSeries);

                // Grafiğe "Kendini Yenile" emri ver
                HeightModel.InvalidatePlot(true);
            }

            // 2. Basınç Grafiği
            lock (PressureModel.SyncRoot)
            {
                var series = (LineSeries)PressureModel.Series[0];
                series.Points.Add(new DataPoint(xValue, packet.Pressure));
                ApplySlidingWindow(series);
                PressureModel.InvalidatePlot(true);
            }

            // 3. İniş Hızı
            lock (VelocityModel.SyncRoot)
            {
                var series = (LineSeries)VelocityModel.Series[0];
                series.Points.Add(new DataPoint(xValue, packet.LandingSpeed));
                ApplySlidingWindow(series);
                VelocityModel.InvalidatePlot(true);
            }

            // 4. Sıcaklık
            lock (TemperatureModel.SyncRoot)
            {
                var series = (LineSeries)TemperatureModel.Series[0];
                series.Points.Add(new DataPoint(xValue, packet.Tempreture));
                ApplySlidingWindow(series);
                TemperatureModel.InvalidatePlot(true);
            }

            // 5. Batarya: config açıksa geçici sabit yüzde, kapalıysa telemetri voltajı.
            lock (BatteryModel.SyncRoot)
            {
                double batteryValue = _batteryGraphSettings.UseFixedPercentage
                    ? _batteryGraphSettings.FixedPercentage
                    : packet.BatteryVoltage;

                var series = (LineSeries)BatteryModel.Series[0];
                series.Points.Add(new DataPoint(xValue, batteryValue));
                ApplySlidingWindow(series);
                BatteryModel.InvalidatePlot(true);
            }

            // 6. Oryantasyon Grafiği (Pitch, Roll, Yaw)
            /*lock (OrientationModel.SyncRoot)
            {
                var pitchSeries = (LineSeries)OrientationModel.Series[0];
                var rollSeries = (LineSeries)OrientationModel.Series[1];
                var yawSeries = (LineSeries)OrientationModel.Series[2];

                pitchSeries.Points.Add(new DataPoint(xValue, packet.Pitch));
                rollSeries.Points.Add(new DataPoint(xValue, packet.Roll));
                yawSeries.Points.Add(new DataPoint(xValue, packet.Yaw));

                ApplySlidingWindow(pitchSeries);
                ApplySlidingWindow(rollSeries);
                ApplySlidingWindow(yawSeries);

                OrientationModel.InvalidatePlot(true);
            }

            // 7. 2D GPS Rotası (Radar)
            // GPS verisi 0'dan farklıysa (yani uydu sinyal bulduysa) çiz!
           /* if (packet.GpsLatitude != 0 && packet.GpsLongitude != 0)
            {
                lock (RouteModel.SyncRoot)
                {
                    var routeSeries = (LineSeries)RouteModel.Series[0];
                    // X Ekseni: Boylam (Longitude), Y Ekseni: Enlem (Latitude)
                    routeSeries.Points.Add(new DataPoint(packet.GpsLongitude, packet.GpsLatitude));
                    // Radarda Sliding Window kullanmıyoruz, tüm rotayı görmek istiyoruz.
                    RouteModel.InvalidatePlot(true);
                }
        }*/
        }

        // Kayan Pencere (Sliding Window) Yardımcı Metodu
        private void ApplySlidingWindow(LineSeries series)
        {
            if (series.Points.Count > 60) // 60 saniyeden eski verileri sil
            {
                series.Points.RemoveAt(0); // En baştaki (en eski) noktayı uçur
            }
        }




    }
}
