using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;

namespace TeknofestUyduArayuz.ViewModels;

public sealed class MapViewModel
{
    private readonly MemoryLayer _positionLayer;
    private bool _hasCentered;

    public Map MyMap { get; }

    public MapViewModel()
    {
        MyMap = new Map { CRS = "EPSG:3857" };
        MyMap.Layers.Add(OpenStreetMap.CreateTileLayer("teknofest-uydu-arayuz"));

        _positionLayer = new MemoryLayer
        {
            Name = "Uydu Konumu",
            Features = Array.Empty<IFeature>(),
            Style = SymbolStyles.CreatePinStyle(
                pinColor: Color.Red,
                symbolScale: 0.5)
        };
        MyMap.Layers.Add(_positionLayer);
    }

    public void UpdatePosition(double latitude, double longitude)
    {
        if (!IsValidCoordinate(latitude, longitude))
        {
            return;
        }

        (double x, double y) = SphericalMercator.FromLonLat(longitude, latitude);
        var mapPoint = new MPoint(x, y);
        var positionFeature = new PointFeature(mapPoint);
        positionFeature["Latitude"] = latitude;
        positionFeature["Longitude"] = longitude;

        _positionLayer.Features = [positionFeature];
        _positionLayer.DataHasChanged();

        if (_hasCentered)
        {
            MyMap.Navigator.CenterOn(mapPoint, duration: 250);
            return;
        }

        MyMap.Navigator.CenterOnAndZoomTo(mapPoint, resolution: 5, duration: 500);
        _hasCentered = true;
    }

    private static bool IsValidCoordinate(double latitude, double longitude) =>
        double.IsFinite(latitude) &&
        double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;
}
