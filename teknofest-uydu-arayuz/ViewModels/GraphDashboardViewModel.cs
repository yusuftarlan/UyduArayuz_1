using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using TeknofestUyduArayuz.Models;

namespace TeknofestUyduArayuz.ViewModels;

public sealed class GraphDashboardViewModel
{
    private const int MaximumDataPointCount = 60;
    private const int MarkerSize = 3;

    public PlotModel PacketModel { get; }
    public PlotModel HeightModel { get; }
    public PlotModel PressureModel { get; }
    public PlotModel VelocityModel { get; }
    public PlotModel TemperatureModel { get; }
    public PlotModel BatteryModel { get; }

    public GraphDashboardViewModel()
    {
        PacketModel = CreatePlotModel();
        PacketModel.Series.Add(CreateLineSeries(OxyColors.Yellow));
        HideTimeAxis(PacketModel);

        PressureModel = CreatePlotModel();
        PressureModel.Series.Add(CreateLineSeries(OxyColors.Yellow));
        HideTimeAxis(PressureModel);

        HeightModel = CreatePlotModel();
        HeightModel.Legends.Add(new Legend { LegendTextColor = OxyColors.White });
        HeightModel.Series.Add(CreateLineSeries(OxyColors.Cyan, "Barometrik"));
        HeightModel.Series.Add(CreateLineSeries(OxyColors.Magenta, "GPS"));
        HideTimeAxis(HeightModel);

        VelocityModel = CreatePlotModel();
        VelocityModel.Series.Add(CreateLineSeries(OxyColors.LimeGreen));

        TemperatureModel = CreatePlotModel();
        TemperatureModel.Series.Add(CreateLineSeries(OxyColors.Orange));

        BatteryModel = CreatePlotModel();
        BatteryModel.Series.Add(CreateLineSeries(OxyColors.Red));
    }

    public void UpdateGraphs(TelemetryPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        double xValue = DateTimeAxis.ToDouble(packet.SentDate);

        UpdateSingleSeries(PacketModel, xValue, packet.PacketNo);
        UpdateHeightSeries(xValue, packet.Height, packet.GpsAltitude);
        UpdateSingleSeries(PressureModel, xValue, packet.Pressure);
        UpdateSingleSeries(VelocityModel, xValue, packet.LandingSpeed);
        UpdateSingleSeries(TemperatureModel, xValue, packet.Tempreture);

        UpdateSingleSeries(BatteryModel, xValue, packet.BatteryVoltage);
    }

    private static PlotModel CreatePlotModel()
    {
        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            TextColor = OxyColors.LightGray,
            PlotAreaBorderColor = OxyColors.LightGray
        };

        model.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Zaman",
            StringFormat = "HH:mm:ss",
            Angle = 45,
            IntervalType = DateTimeIntervalType.Seconds,
            MinorIntervalType = DateTimeIntervalType.Seconds,
            TextColor = OxyColors.LightGray,
            TitleColor = OxyColors.White,
            TicklineColor = OxyColors.LightGray,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromArgb(30, 255, 255, 255)
        });

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

    private static LineSeries CreateLineSeries(OxyColor color, string? title = null) =>
        new()
        {
            Title = title,
            Color = color,
            StrokeThickness = 2,
            MarkerType = MarkerType.Circle,
            MarkerSize = MarkerSize
        };

    private static void HideTimeAxis(PlotModel model)
    {
        Axis timeAxis = model.Axes.Single(axis => axis.Position == AxisPosition.Bottom);
        timeAxis.LabelFormatter = _ => string.Empty;
        timeAxis.TickStyle = TickStyle.None;
        timeAxis.Title = string.Empty;
        model.PlotMargins = new OxyThickness(35, 0, 5, 3);
    }

    private static void UpdateSingleSeries(PlotModel model, double x, double y)
    {
        LineSeries series = (LineSeries)model.Series[0];
        AddPoint(series, x, y);
        model.InvalidatePlot(true);
    }

    private void UpdateHeightSeries(double x, double barometricHeight, double gpsAltitude)
    {
        AddPoint((LineSeries)HeightModel.Series[0], x, barometricHeight);
        AddPoint((LineSeries)HeightModel.Series[1], x, gpsAltitude);
        HeightModel.InvalidatePlot(true);
    }

    private static void AddPoint(LineSeries series, double x, double y)
    {
        series.Points.Add(new DataPoint(x, y));
        if (series.Points.Count > MaximumDataPointCount)
        {
            series.Points.RemoveAt(0);
        }
    }
}
