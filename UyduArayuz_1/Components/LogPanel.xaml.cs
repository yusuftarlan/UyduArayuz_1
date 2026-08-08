using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls;
namespace UyduArayuz_1.Components
{
    /// <summary>
    /// LogPanel.xaml etkileşim mantığı
    /// </summary>
    public partial class LogPanel : UserControl
    {
        public LogPanel()
        {
            InitializeComponent();

            ((INotifyCollectionChanged)TerminalListBox.Items).CollectionChanged += (s, e) =>
            {
                // Eğer listeye yeni bir şey EKLENDİYSE
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    // Eklenen son elemanı bul ve kamerayı (Scroll) ona kaydır
                    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 })
                    {
                        TerminalListBox.ScrollIntoView(e.NewItems[0]);
                    }
                }
            };
        }
    }
}
