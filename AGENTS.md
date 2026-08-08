# AGENTS.md

Bu dosya, bu repoda çalışan yapay zeka ajanları ve geliştiriciler için hızlı proje haritasıdır. Amaç sadece "hangi dosya nerede?" demek değil; verinin projede nasıl aktığını ve değişiklik yaparken hangi katmana dokunulması gerektiğini göstermektir.

## Proje Özeti

`UyduArayuz_1`, .NET 8 WPF ile geliştirilmiş bir yer istasyonu arayüzüdür. Uygulama seri porttan telemetri verisi alır, bu veriyi `TelemetryPacket` modeline çevirir ve ekranda anlık değerler, grafikler, alarm LED'leri, 3D durum göstergesi, harita ve tablo olarak gösterir.

Ana teknoloji ve paketler:

- `net8.0-windows` ve WPF
- `System.IO.Ports`: seri port haberleşmesi
- `OxyPlot.Wpf`: canlı grafikler
- `HelixToolkit.Wpf`: 3D attitude/durum göstergesi
- `Mapsui.Wpf`: harita görünümü

## Üst Düzey Mimari

Proje MVVM yaklaşımına yakın ilerliyor:

- `Models`: Telemetri veri yapıları.
- `ViewModels`: UI'nin bağlandığı durum ve davranışlar.
- `Components`: WPF `UserControl` ekran parçaları.
- `Services`: dış dünya ile haberleşme, loglama ve veri üretimi.
- `Helpers`: ortak yardımcı sınıflar.

Basit zihinsel model:

```text
SerialTelemetryService
    -> ham seri port satırını okur
    -> TelemetryPacket'a parse eder
    -> OnTelemetryReceived event'i yayınlar

MainViewModel
    -> event'i dinler
    -> UI thread üzerinde CurrentPacket, history, grafik, alarm ve 3D state'i günceller

Components / XAML
    -> MainViewModel ve alt ViewModel property'lerine binding yapar
    -> ekranda gösterir
```

## Uygulama Giriş Noktası

- `UyduArayuz_1/App.xaml`: WPF uygulamasını `MainWindow.xaml` ile başlatır.
- `UyduArayuz_1/MainWindow.xaml.cs`: `MainViewModel` oluşturur ve pencerenin `DataContext` alanına atar.
- `UyduArayuz_1/MainWindow.xaml`: ana ekran yerleşimini kurar.

`MainWindow.xaml` iki ana bölgeye ayrılmıştır:

- Üst satır: `HeaderControl`
- Ana içerik: `CenterDisplayArea`, sağ panelde `LiveCameraView`, `AttitudeIndicator`, `Map`

## Veri Modeli

Ana model:

- `UyduArayuz_1/Models/TelemetryPacket.cs`

Bu sınıf tek bir telemetri paketini temsil eder. Seri porttan gelen CSV satırı şu alanlara ayrılır:

- paket numarası
- uydu statüsü
- hata kodu
- gönderme zamanı
- basınç, yükseklik, iniş hızı, sıcaklık, pil gerilimi
- GPS enlem, boylam, irtifa
- pitch, roll, yaw
- görev kodu
- takım numarası

Yeni telemetri alanı eklenecekse sadece XAML'e alan eklemek yeterli değildir. Genellikle şu üç nokta birlikte güncellenmelidir:

1. `TelemetryPacket`
2. `SerialTelemetryService.ParseTelemetry`
3. İlgili XAML binding'i veya ViewModel güncellemesi

## Servis Katmanı

### `SerialTelemetryService`

Dosya: `UyduArayuz_1/Services/SerialTelemetryService.cs`

Sorumlulukları:

- seri portu seçilen port ve baud rate ile başlatmak
- satır bazlı telemetri okumak
- CSV veriyi `TelemetryPacket` nesnesine parse etmek
- geçerli paketleri iki kanala ayırmak:
  - UI güncelleme kanalı
  - CSV log kanalı
- `OnTelemetryReceived` event'i ile `MainViewModel` tarafını bilgilendirmek

Dikkat edilmesi gerekenler:

- Servis WPF `Dispatcher` bilmemelidir. UI thread'e geçme işi `MainViewModel` tarafında yapılır.
- Parse edilen veri sayısı şu an `17` alan bekler.
- Ondalıklı sayılar `CultureInfo.InvariantCulture` ile parse edilir; yani nokta `.` ondalık ayracıdır.
- Log çıktısı çalışma dizinine `telemetri_log.csv` olarak yazılır.

### `LoggerService`

Dosya: `UyduArayuz_1/Services/LoggerService.cs`

Uygulama içi logları `ObservableCollection<LogModel>` içinde tutar. `LogPanel.xaml`, bu koleksiyona `LoggerService.Instance` üzerinden bağlanır.

Bu sınıf singleton gibi kullanılıyor. Yeni kullanım eklerken `Instance` değerinin `MainViewModel` kurucusunda oluşturulan `LoggerService` sonrasında dolduğunu unutma.

## ViewModel Katmanı

### `MainViewModel`

Dosya: `UyduArayuz_1/ViewModels/MainViewModel.cs`

Merkezi koordinasyon noktasıdır. Şu işleri yapar:

- `SerialTelemetryService` ve `LoggerService` oluşturur.
- `CurrentPacket` ile anlık paketi tutar.
- `TelemetryHistory` ile son telemetri paketlerini tutar.
- alt ViewModel'leri oluşturur:
  - `HeaderControlViewModel`
  - `AlarmPanelViewModel`
  - `GraphDashboardViewModel`
  - `AttitudeViewModel`
  - `MapViewModel`
- seri porttan veri gelince alarm, grafik, 3D durum ve tabloyu günceller.

Burayı evdeki elektrik panosu gibi düşün: veri geldiğinde hangi odaya elektrik gideceğine burada karar veriliyor.

### `HeaderControlViewModel`

Dosya: `UyduArayuz_1/ViewModels/HeaderControlViewModel.cs`

Bağlantı üst barının state ve command'larını yönetir:

- port listesini yeniler
- baud rate seçimini tutar
- bağlan/kes komutlarını yayınlar
- bağlantı durum metni ve rengini yönetir

Bu ViewModel doğrudan seri portu başlatmaz. `ConnectRequested` ve `DisconnectRequested` callback'leri ile isteği `MainViewModel` tarafına iletir.

### `GraphDashboardViewModel`

Dosya: `UyduArayuz_1/ViewModels/GraphDashboardViewModel.cs`

OxyPlot modellerini oluşturur ve telemetri geldikçe grafiklere yeni noktalar ekler. Şu an grafiklerde kayan pencere mantığı vardır; eski noktalar silinerek yaklaşık son `60` veri tutulur.

Yeni grafik eklenecekse:

1. Yeni `PlotModel` property ekle.
2. `InitializeGraphs` içinde modeli ve seriyi oluştur.
3. `UpdateGraphs` içinde yeni telemetri alanını seriye ekle.
4. `GraphDashboard.xaml` içinde yeni `PlotView` binding'i ekle.

### `AlarmPanelViewModel`

Dosya: `UyduArayuz_1/ViewModels/AlarmPanelViewModel.cs`

`ErrorCode` değerini bit maskesi gibi yorumlayarak alarm LED renklerini günceller:

- bit 0: iniş hızı
- bit 1: GPS
- bit 2: ayrılma
- bit 3: acil paraşüt

### `AttitudeViewModel`

Dosya: `UyduArayuz_1/ViewModels/AttitudeViewModel.cs`

Pitch, roll ve yaw değerlerini tutar. `AttitudeIndicator.xaml`, HelixToolkit içindeki 3D kutunun dönüş açılarını bu değerlere bağlar.

Not: Bu sınıf şu an `INotifyPropertyChanged` uygulamıyor. Eğer 3D göstergede canlı güncelleme bekleniyorsa property değişim bildirimleri eklemek gerekebilir.

### `MapViewModel`

Dosya: `UyduArayuz_1/ViewModels/MapViewModel.cs`

Mapsui harita nesnesini oluşturur ve OpenStreetMap katmanını ekler. GPS koordinatını haritada işaretleme mantığı henüz belirgin şekilde uygulanmamış görünüyor.

## Component Katmanı

WPF `UserControl` dosyaları `UyduArayuz_1/Components` altında bulunur.

- `HeaderControl`: port seçimi, bağlan/kes, görev komutu alanı ve alarm LED'leri.
- `CenterDisplayArea`: orta ekranın layout bileşeni.
- `InstantTelemetryPanel`: `CurrentPacket` alanlarını anlık gösterir.
- `GraphDashboard`: OxyPlot grafikleri gösterir.
- `TelemetryTable`: `TelemetryHistory` listesini tabloda gösterir.
- `LogPanel`: `LoggerService.Instance.Logs` listesini gösterir.
- `LiveCameraView`: kamera/medya alanı için yer tutar.
- `AttitudeIndicator`: HelixToolkit ile 3D durum göstergesi.
- `Map`: Mapsui harita kontrolü.

Yeni UI eklerken önce kendine şu soruyu sor:

- Bu sadece görünüm mü? O zaman `Components` içinde XAML yeterli olabilir.
- Kullanıcı aksiyonu veya state var mı? O zaman ilgili `ViewModel` içinde property/command eklenmeli.
- Seri port, dosya, ağ, veritabanı gibi dış dünya işi mi? O zaman `Services` katmanına koy.

## Binding ve Thread Kuralları

WPF binding'leri ana olarak `MainViewModel` üzerinden akar. Örnek:

- `CurrentPacket.PacketNo`
- `TelemetryHistory`
- `GraphViewModel.PacketModel`
- `HeaderControlViewModel.ConnectCommand`
- `AlarmPanelViewModel.GpsErrorLed`

Seri port okuma arka planda çalışır. UI güncellemesi için `MainViewModel.TelemetryService_OnTelemetryReceived` içinde `Application.Current.Dispatcher.InvokeAsync` kullanılır.

Kural:

- Arka plan thread'inden doğrudan UI koleksiyonlarını veya UI-bound property'leri güncelleme.
- Servis katmanına WPF UI bağımlılığı ekleme.
- UI'ya bağlı property değişiyorsa `INotifyPropertyChanged` gerekip gerekmediğini kontrol et.

## Çalıştırma ve Derleme

Kök dizinde:

```powershell
dotnet build .\UyduArayuz_1.slnx
```

Uygulamayı çalıştırmak için:

```powershell
dotnet run --project .\UyduArayuz_1\UyduArayuz_1.csproj
```

Seri port testi yaparken doğru COM port ve baud rate seçilmelidir. Gerçek donanım yoksa UI açılabilir, fakat canlı telemetri akışı gelmez.

## Değişiklik Yaparken Dikkat Edilecekler

- Mevcut mimariyi bozmayacak şekilde MVVM ayrımını koru.
- `SerialTelemetryService` içinde parse formatı değişirse `TelemetryPacket` ve UI binding'lerini birlikte kontrol et.
- Grafiklerde `DateTime.ParseExact` formatı şu an `dd.MM.yyyy HH:mm:ss` bekler.
- `TelemetryHistory` şu an son `100` paketi tutar.
- `GraphDashboardViewModel` grafiklerde son `60` noktayı tutar.
- `LoggerService.Instance` kullanımı uygulama başlatma sırasına duyarlıdır.
- Projede Türkçe alan adları ve UI metinleri var; yeni görünen metinlerde aynı dil tutarlılığını koru.
- Geniş refactor yapmadan önce küçük ve doğrulanabilir değişiklikler yap.

## Bilinen Bakım Noktaları

Bu bölüm hata listesi değil, ileride çalışacak kişiye yön tabelasıdır:

- `AttitudeViewModel` property değişim bildirimi yapmıyor; binding güncellenmiyorsa ilk bakılacak yer burasıdır.
- `Map.xaml` içinde `MapControl` henüz `MapViewModel.MyMap` ile açıkça bağlanmamış görünüyor.
- `LoggerService.cs` içinde bazı gereksiz/tekrarlı `using` ifadeleri var.
- `SerialTelemetryService.Stop()` sonrasında `_serialPort.Dispose()` çağrıldığı için aynı servis instance'ı ile tekrar `Start()` senaryosu dikkatle test edilmelidir.
- Bazı code-behind dosyaları sadece `InitializeComponent` içeriyor; iş mantığı eklenirse önce ViewModel'e koymanın mümkün olup olmadığını değerlendir.

## Yeni Katkı İçin Mini Kontrol Listesi

1. Değişiklik hangi katmana ait: Model, ViewModel, Component, Service?
2. Binding path doğru mu ve `DataContext` nereden geliyor?
3. UI-bound değişiklik için `OnPropertyChanged` gerekiyor mu?
4. Arka plan thread'inden UI koleksiyonuna dokunuluyor mu?
5. Telemetri formatı değiştiyse alan sayısı, parse sırası ve tablo/grafik binding'leri birlikte güncellendi mi?
6. `dotnet build .\UyduArayuz_1.slnx` çalışıyor mu?
