using System;
using System.Collections.Generic;
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
    /// Map.xaml etkileşim mantığı
    /// </summary>
    public partial class Map : UserControl
    {
        public Map()
        {
            InitializeComponent();

            // 1. Durum: Veri sonradan değişirse çalışır (Ama UI'ın hazır olduğundan emin oluruz)
            this.DataContextChanged += (s, e) =>
            {
                // Eğer MyMapControl henüz XAML tarafından oluşturulmadıysa (null ise) çökmesini engelle
                if (this.DataContext is MapViewModel vm && MyMapControl != null)
                {
                    MyMapControl.Map = vm.MyMap;
                }
            };

            // 2. Durum: Ekran (XAML) tamamen çizilip hazır olduğunda çalışır
            this.Loaded += (s, e) =>
            {
                // Ekran hazır. Şimdi o veriyi güvenle bağlayabiliriz.
                if (this.DataContext is MapViewModel vm && MyMapControl != null)
                {
                    MyMapControl.Map = vm.MyMap;
                }
            };
        }
    }
}
