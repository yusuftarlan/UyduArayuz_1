using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UyduArayuz_1.ViewModels;
namespace UyduArayuz_1.Components
{
    /// <summary>
    /// TelemetryTable.xaml etkileşim mantığı
    /// </summary>
    public partial class TelemetryTable : UserControl
    {
        public TelemetryTable()
        {
            InitializeComponent();
            this.DataContextChanged += TelemetryTable_DataContextChanged;
        }
        private void TelemetryTable_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 2. ADIM: Şef (MainViewModel) mutfağa geldi mi diye bakıyoruz
            if (this.DataContext is MainViewModel sef)
            {
                // 3. ADIM: Şefin "Akıllı Panosu"na (TelemetryHistory) bir kulaklık takıyoruz.
                // Listede herhangi bir değişiklik olduğunda bu blok tetiklenecek.
                sef.TelemetryHistory.CollectionChanged += (s, args) =>
                {
                    // Eğer yapılan değişiklik bir "Ekleme" (Add) işlemiyse...
                    if (args.Action == NotifyCollectionChangedAction.Add)
                    {
                        // Eklenen o en son taze paketi yakala
                        var sonEklenenPaket = args.NewItems[0];

                        // 4. ADIM: DataGrid'e "Hemen o paketin olduğu satıra kay!" emrini ver
                        // Dispatcher kullanıyoruz çünkü arayüz güncellemeleri ana iş parçacığında yapılmalıdır
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            HistoryGrid.ScrollIntoView(sonEklenenPaket);
                        });
                    }
                };
            }
        }
    }
}
