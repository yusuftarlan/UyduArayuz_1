using Mapsui;
using Mapsui.Tiling; // OpenStreetMap katmanı için gerekli kütüphane
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UyduArayuz_1.ViewModels
{
    // Arayüzü haberdar etmek için INotifyPropertyChanged kullanıyoruz
    public class MapViewModel : INotifyPropertyChanged
    {
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
            // 1. Boş bir harita nesnesi oluştur
            MyMap = new Map();

            // 2. Dünyanın en popüler ücretsiz haritası olan OpenStreetMap'i katman olarak ekle
            MyMap.Layers.Add(OpenStreetMap.CreateTileLayer());
        }

        // --- INotifyPropertyChanged Kurulumu (Standart MVVM) ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}