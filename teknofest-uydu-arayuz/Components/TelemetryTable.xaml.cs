using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using TeknofestUyduArayuz.Models;
using TeknofestUyduArayuz.ViewModels;

namespace TeknofestUyduArayuz.Components;

public partial class TelemetryTable : UserControl
{
    private ObservableCollection<TelemetryPacket>? _history;

    public TelemetryTable()
    {
        InitializeComponent();
        DataContextChanged += TelemetryTable_DataContextChanged;
        Loaded += TelemetryTable_Loaded;
        Unloaded += TelemetryTable_Unloaded;
    }

    private void TelemetryTable_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeFromHistory();
        SubscribeToHistory(e.NewValue as MainViewModel);
    }

    private void TelemetryTable_Loaded(object sender, RoutedEventArgs e)
    {
        SubscribeToHistory(DataContext as MainViewModel);
    }

    private void SubscribeToHistory(MainViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_history, viewModel.TelemetryHistory))
        {
            return;
        }

        _history = viewModel.TelemetryHistory;
        _history.CollectionChanged += TelemetryHistory_CollectionChanged;
    }

    private void TelemetryHistory_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add ||
            e.NewItems is not { Count: > 0 })
        {
            return;
        }

        object newPacket = e.NewItems[0]!;
        Dispatcher.InvokeAsync(() => HistoryGrid.ScrollIntoView(newPacket));
    }

    private void TelemetryTable_Unloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeFromHistory();
    }

    private void UnsubscribeFromHistory()
    {
        if (_history is null)
        {
            return;
        }

        _history.CollectionChanged -= TelemetryHistory_CollectionChanged;
        _history = null;
    }
}
