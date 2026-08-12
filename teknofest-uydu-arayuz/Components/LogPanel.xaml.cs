using System.Collections.Specialized;
using System.Windows.Controls;

namespace TeknofestUyduArayuz.Components
{
    public partial class LogPanel : UserControl
    {
        public LogPanel()
        {
            InitializeComponent();

            ((INotifyCollectionChanged)TerminalListBox.Items).CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 })
                {
                    TerminalListBox.ScrollIntoView(e.NewItems[0]);
                }
            };
        }
    }
}
