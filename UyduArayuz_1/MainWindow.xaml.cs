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
using UyduArayuz_1.Configuration;
using UyduArayuz_1.ViewModels;

namespace UyduArayuz_1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ApplicationSettings applicationSettings = ApplicationSettingsLoader.Load();
            MainViewModel mainViewModel = new MainViewModel(applicationSettings);

            this.DataContext = mainViewModel; //Tüm arayüzün veri kaynağı olaran MainViewModel'ı atıyoruz
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}
