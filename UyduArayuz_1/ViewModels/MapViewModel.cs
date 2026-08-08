using System.ComponentModel;
using System.Runtime.CompilerServices;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;

namespace UyduArayuz_1.ViewModels;

public class MapViewModel : INotifyPropertyChanged
{
    private readonly MemoryLayer _positionLayer;
    private bool _hasCentered;
    private Map _myMap;

    public Map MyMap
    {
        get => _myMap;
        set
        {
            _myMap = value;
            OnPropertyChanged();
        }
    }

    public MapViewModel()
    {
        MyMap = new Map
        {
            CRS = "EPSG:3857"
        };

        MyMap.Layers.Add(
            OpenStreetMap.CreateTileLayer("UyduArayuz_1"));

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

    public void UpdatePosition(
        double latitude,
        double longitude)
    {
        if (!IsValidCoordinate(latitude, longitude))
        {
            return;
        }

        var projectedCoordinate =
            SphericalMercator.FromLonLat(
                longitude,
                latitude);

        var mapPoint = new MPoint(
            projectedCoordinate.x,
            projectedCoordinate.y);

        var positionFeature =
            new PointFeature(mapPoint);

        positionFeature["Latitude"] = latitude;
        positionFeature["Longitude"] = longitude;

        _positionLayer.Features =
            new IFeature[]
            {
                positionFeature
            };

        _positionLayer.DataHasChanged();

        if (!_hasCentered)
        {
            MyMap.Navigator.CenterOnAndZoomTo(
                mapPoint,
                resolution: 5,
                duration: 500);

            _hasCentered = true;
        }
        else
        {
            MyMap.Navigator.CenterOn(
                mapPoint,
                duration: 250);
        }
    }

    private static bool IsValidCoordinate(
        double latitude,
        double longitude)
    {
        return
            double.IsFinite(latitude) &&
            double.IsFinite(longitude) &&
            latitude is >= -90 and <= 90 &&
            longitude is >= -180 and <= 180;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}