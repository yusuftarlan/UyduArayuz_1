using System.Windows;
using System.Windows.Controls;
using TeknofestUyduArayuz.ViewModels;

namespace TeknofestUyduArayuz.Components;

public partial class Map : UserControl
{
    public Map()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyMap();
        Loaded += (_, _) => ApplyMap();
    }

    private void ApplyMap()
    {
        if (DataContext is MapViewModel viewModel)
        {
            MyMapControl.Map = viewModel.MyMap;
        }
    }
}
