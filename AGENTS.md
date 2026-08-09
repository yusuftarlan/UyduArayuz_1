# AGENTS.md

Bu dosya, bu repoda çalışan yapay zeka ajanları ve geliştiriciler için güncel proje haritasıdır. Amaç yalnızca dosyaların yerini göstermek değil; telemetri ve video verisinin katmanlar arasında nasıl aktığını, bir değişiklikte hangi parçaların birlikte ele alınması gerektiğini ve bilinen riskleri görünür kılmaktır.

## Proje Özeti

`UyduArayuz_1`, .NET 8 WPF ile geliştirilmiş bir yer istasyonu arayüzüdür. Uygulama seri porttan ikili telemetri çerçeveleri alır, çerçeveleri doğrulayıp `TelemetryPacket` modeline dönüştürür ve verileri anlık panel, grafik, alarm LED'leri, 3D durum göstergesi, harita, tablo ve log alanlarında gösterir.

Uygulamada ayrıca WPF `MediaElement` tabanlı bir video oynatma altyapısı bulunur. Yerel video dosyaları ve desteklenen doğrudan ağ medya URI'leri aynı URI tabanlı session altyapısı üzerinden oynatılır. USB kamera için model türü hazırlanmıştır; ancak yakalama adapter'ı, session'ı ve factory'si henüz uygulanmamıştır.

Ana teknoloji ve paketler:

- `net8.0-windows` ve WPF
- `System.IO.Ports` 10.0.6: seri port haberleşmesi
- `OxyPlot.Wpf` 2.2.0: canlı grafikler
- `HelixToolkit.Wpf` 3.1.2: 3D attitude/durum göstergesi
- `Mapsui.Wpf` 4.1.8: OpenStreetMap tabanlı harita
- `System.Data.SqlClient` 4.9.1: projede referanslıdır; mevcut ana akışta belirgin bir kullanım görünmemektedir

## Üst Düzey Mimari

Proje MVVM yaklaşımına yakın ilerler:

- `Models`: telemetri ve video kaynak tanımları.
- `ViewModels`: UI'nin bağlandığı durum ve davranışlar.
- `Components`: WPF `UserControl` ekran parçaları ve sınırlı composition/UI glue kodu.
- `Services`: seri haberleşme, protokol çözümleme, loglama ve video session soyutlamaları.
- `Adapters`: WPF gibi dış teknoloji ayrıntılarını servis arayüzlerinin arkasına alır.
- `Shared/Mvvm`: ortak command yardımcıları.

Telemetri için zihinsel model:

```text
SerialPort byte akışı
    -> TelemetryFrameExtractor tam çerçeveleri ayırır
    -> TelemetryPacketParser başlangıç/bitiş, uzunluk ve CRC32 doğrular
    -> geçerli çerçeveyi TelemetryPacket'a dönüştürür
    -> SerialTelemetryService paketi UI ve CSV log kanallarına yazar
    -> OnTelemetryReceived event'i MainViewModel'e ulaşır
    -> MainViewModel Dispatcher üzerinde panel, grafik, alarm, 3D ve geçmişi günceller
```

URI tabanlı video için zihinsel model:

```text
LiveCameraView kullanıcı girdisi
    -> LocalFileSourceDescriptor veya NetworkStreamSourceDescriptor
    -> LiveCameraViewModel
    -> VideoPlaybackSessionResolver
    -> UriPlaybackSessionFactory
    -> UriPlaybackSession
    -> IUriPlaybackAdapter
    -> MediaElementPlaybackAdapter
    -> WPF MediaElement
```

## Uygulama Giriş Noktası ve Yerleşim

- `UyduArayuz_1/App.xaml`: uygulamayı `MainWindow.xaml` ile başlatır.
- `UyduArayuz_1/MainWindow.xaml.cs`: `MainViewModel` oluşturur ve pencerenin `DataContext` değerine atar.
- `UyduArayuz_1/MainWindow.xaml`: ana ekran yerleşimini kurar.

Ana pencere:

- Üst satırda `HeaderControl`.
- Ana içerikte `CenterDisplayArea`.
- Sağ sütunda `LiveCameraView`, `AttitudeIndicator` ve `Map`.
- `Map` bileşeni `DataContext="{Binding MapViewControl}"` ile `MapViewModel` alır.
- `LiveCameraView` kendi `MediaElement` bağımlılığı nedeniyle video composition root'unu kendi code-behind dosyasında kurar ve kendi `DataContext` değerini oluşturur.

## Telemetri Modeli ve İkili Protokol

### `TelemetryPacket`

Dosya: `UyduArayuz_1/Models/TelemetryPacket.cs`

Tek bir doğrulanmış telemetri paketini temsil eder. Başlıca alanlar:

- paket numarası, uydu statüsü ve hata kodu
- gönderme zamanı
- basınç, yükseklik, iniş hızı, sıcaklık ve pil gerilimi
- GPS enlem, boylam ve irtifa
- pitch, roll ve yaw
- görev kodu ve takım numarası

### Protokol sınıfları

- `TelemetryProtocol`: başlangıç/bitiş baytları, çerçeve uzunluğu, alan offset'leri, CRC aralığı ve durum metinleri.
- `TelemetryFrameExtractor`: parçalı seri port okumalarını tamponlar, başlangıç baytına senkronize olur ve tam çerçeveleri ayırır.
- `TelemetryPacketParser`: çerçeve yapısını ve CRC32 değerini doğrular; little-endian sayıları ve float alanlarını okur.
- `TelemetryCrc32`: `0xEDB88320` polinomu ile CRC32 hesaplar.

Yeni bir telemetri alanı veya protokol değişikliği yalnızca modele eklenmemelidir. En az şu noktalar birlikte kontrol edilmelidir:

1. `TelemetryProtocol` uzunluk ve offset sabitleri
2. `TelemetryFrameExtractor`
3. `TelemetryPacketParser`
4. `TelemetryPacket`
5. `ArduinoTelemetrySimulator/ArduinoTelemetrySimulator.ino`
6. İlgili ViewModel, tablo, grafik veya XAML binding'i

Protokol değişikliğinde masaüstü okuyucusu ve Arduino üreticisinin aynı paket uzunluğunu, offset'leri, endian düzenini, CRC aralığını ve baud rate'i kullandığını doğrulamadan değişikliği tamamlanmış sayma.

## Telemetri Servis Katmanı

### `SerialTelemetryService`

Dosya: `UyduArayuz_1/Services/SerialTelemetryService.cs`

Sorumlulukları:

- seçilen port ve baud rate ile yeni bir `SerialPort` oluşturmak
- seri porttan ham bayt blokları okumak
- `TelemetryFrameExtractor` ile tam ikili çerçeveleri çıkarmak
- `TelemetryPacketParser` ile çerçeveleri doğrulamak ve parse etmek
- geçerli paketleri iki `Channel<TelemetryPacket>` kanalına ayırmak:
  - UI event kanalı
  - `telemetri_log.csv` dosyasına yazan log kanalı
- geçerli paketler için `OnTelemetryReceived` event'ini yayınlamak
- geçersiz çerçeve, CRC ve seri port hatalarını uygulama loguna yazmak

Thread kuralları:

- Servis WPF `Dispatcher` bilmez ve UI koleksiyonlarına dokunmaz.
- `OnTelemetryReceived` arka plan tüketicisinden gelebilir.
- UI thread'e geçiş `MainViewModel.TelemetryService_OnTelemetryReceived` içinde yapılır.
- `LoggerService` koleksiyonu `BindingOperations.EnableCollectionSynchronization` ile eşzamanlı erişime açılmıştır.

### `LoggerService`

Dosya: `UyduArayuz_1/Services/LoggerService.cs`

- Logları `ObservableCollection<LogModel>` içinde tutar.
- `LoggerService.Instance` kurucuda atanır.
- `LogPanel` bu singleton instance üzerinden logları gösterir.
- Seri servis başlamadan önce `LoggerService` oluşturulmuş olmalıdır; mevcut `MainViewModel` sırası bunu sağlar.

## ViewModel Katmanı

### `MainViewModel`

Dosya: `UyduArayuz_1/ViewModels/MainViewModel.cs`

Merkezi telemetri koordinasyon noktasıdır:

- `SerialTelemetryService` ve `LoggerService` oluşturur.
- `HeaderControlViewModel`, `AlarmPanelViewModel`, `GraphDashboardViewModel`, `AttitudeViewModel` ve `MapViewModel` oluşturur.
- `CurrentPacket` ile anlık paketi tutar.
- `TelemetryHistory` içinde son 100 paketi saklar.
- Telemetri event'ini `Application.Current.Dispatcher.InvokeAsync` ile UI thread'e taşır.
- Alarm, grafik, 3D attitude, anlık panel ve geçmiş tablosunu günceller.

Önemli: `MapViewModel.UpdatePosition` mevcut olmasına rağmen `MainViewModel` telemetri event'inde henüz çağrılmıyor. Bu nedenle harita katmanı hazırlanmış olsa da canlı GPS akışı uçtan uca tamamlanmış değildir.

### `HeaderControlViewModel`

- Port listesini ve baud rate seçeneklerini yönetir.
- Bağlan/kes command'larını yayınlar.
- Seri servisi doğrudan başlatmaz; `ConnectRequested` ve `DisconnectRequested` callback'leri üzerinden `MainViewModel` ile konuşur.

### `GraphDashboardViewModel`

- OxyPlot modellerini kurar ve telemetri geldikçe serileri günceller.
- Grafiklerde yaklaşık son 60 noktayı tutan kayan pencere kullanır.
- Yeni grafik eklerken `PlotModel` property, `InitializeGraphs`, `UpdateGraphs` ve `GraphDashboard.xaml` birlikte güncellenmelidir.
- GPS rota serisine veri ekleyen bölüm mevcut kodda yorum satırındadır.

### `AlarmPanelViewModel`

`ErrorCode` değerini bit maskesi olarak yorumlar:

- bit 0: iniş hızı
- bit 1: GPS
- bit 2: ayrılma
- bit 3: acil paraşüt

### `AttitudeViewModel`

- `INotifyPropertyChanged` uygular.
- Pitch, roll ve yaw değişimlerini binding sistemine bildirir.
- `UpdateAttitude` telemetri değerlerini, `ResetOrientation` sıfır durumunu uygular.

### `MapViewModel`

- Mapsui `Map` nesnesini EPSG:3857 olarak oluşturur.
- OpenStreetMap tile katmanı ve `MemoryLayer` tabanlı "Uydu Konumu" katmanı ekler.
- `UpdatePosition(latitude, longitude)` koordinat aralıklarını doğrular.
- GPS WGS84 koordinatını `SphericalMercator.FromLonLat(longitude, latitude)` ile Web Mercator'a çevirir.
- İşaretçiyi günceller, `DataHasChanged()` çağırır, ilk konuma yakınlaşır ve sonraki konumları takip eder.
- `Map.xaml.cs`, `DataContextChanged` ve `Loaded` sırasında `MyMapControl.Map` değerini ViewModel'den atar.

### `LiveCameraViewModel`

Dosya: `UyduArayuz_1/ViewModels/LiveCameraViewModel.cs`

- `IVideoPlaybackSessionResolver` constructor injection ile alınır.
- `State`, `ErrorMessage` ve `CurrentSource` değerlerini UI'ya bildirir.
- `StartAsync`, önceki session'ı bırakır ve uygun factory üzerinden yeni session başlatır.
- `StopAsync`, devam eden başlangıcı iptal edip mevcut session'ı durdurur.
- `SemaphoreSlim` ile start/stop/dispose yaşam döngüsünü sıralar.
- `DisposeAsync`, session event aboneliğini kaldırır ve kaynakları kapatır.
- Dispose sonrasında yeniden kullanımı `ObjectDisposedException` ile engeller.

## Video Modeli, Session ve Adapter Katmanları

### Video kaynak modelleri

Konum: `UyduArayuz_1/Models/Video`

- `VideoSourceKind`: `UsbCamera`, `LocalFile`, `NetworkStream`.
- `VideoSourceDescriptor`: `Id`, `DisplayName` ve soyut `Kind` alanlarını taşıyan temel record.
- `LocalFileSourceDescriptor`: yerel `FilePath` taşır.
- `NetworkStreamSourceDescriptor`: mutlak `StreamUri` taşır.
- `UsbCameraSourceDescriptor`: `DeviceId` taşır; henüz oynatma uygulaması yoktur.
- `VideoPlaybackState`: `Idle`, `Starting`, `Playing`, `Stopping`, `Faulted`.

### Video servisleri

Konum: `UyduArayuz_1/Services/Video`

- `IVideoPlaybackSession`: kaynak, state, hata, state event'i ve async start/stop/dispose sözleşmesi.
- `IVideoPlaybackSessionFactory`: bir kaynağı destekleyip desteklemediğini belirler ve session oluşturur.
- `IVideoPlaybackSessionResolver`: uygun factory'yi seçer.
- `VideoPlaybackSessionResolver`: sıfır eşleşmede `NotSupportedException`, birden fazla eşleşmede `InvalidOperationException` atar.
- `IUriPlaybackAdapter`: URI kaynağını oynatma teknolojisinden ayırır.
- `UriPlaybackSessionFactory`: yalnızca yerel dosya ve ağ kaynağı için `UriPlaybackSession` oluşturur.
- `UriPlaybackSession`: kaynak doğrulaması, state geçişleri, adapter event'leri ve async yaşam döngüsünü yönetir.

### `MediaElementPlaybackAdapter`

Dosya: `UyduArayuz_1/Adapters/Video/MediaElementPlaybackAdapter.cs`

- WPF `MediaElement` nesnesini `IUriPlaybackAdapter` arkasına alır.
- UI işlemlerini `MediaElement.Dispatcher` üzerinde yürütür.
- `MediaOpened` gelene kadar `TaskCompletionSource` bekler.
- `MediaEnded` ve `MediaFailed` olaylarını servis katmanına aktarır.
- Stop sırasında bekleyen açılışı iptal eder ve medya kaynağını temizler.
- `IAsyncDisposable` ile WPF event aboneliklerini kaldırır.

`MediaElement` desteği kurulu Windows medya codec'leri ve desteklenen protokollerle sınırlıdır. Network girişine web sayfası URL'si değil, doğrudan ve mutlak bir medya URI'si verilmelidir. YouTube/Twitch sayfası, HLS/DASH manifesti veya RTSP adresi mevcut adapter ile otomatik olarak desteklenmiş sayılmaz.

### `LiveCameraView`

- Yerel dosya seçimi, yerel oynatma, ağ adresi oynatma ve durdurma kontrollerini içerir.
- State, kaynak adı ve hata mesajını `LiveCameraViewModel` üzerinden gösterir.
- `Loaded` sırasında adapter → factory → resolver → ViewModel zincirini kurar.
- `Unloaded` sırasında önce ViewModel/session, sonra adapter/MediaElement kaynaklarını kapatır.
- Dosya seçici ve UI event yönlendirmeleri code-behind'dadır; gerçek oynatma ve state mantığı ViewModel/session/adapter katmanlarındadır.

### USB kamera sınırı

`UsbCameraSourceDescriptor` yalnızca model seviyesinde hazırdır. Şunlar henüz yoktur:

- USB cihaz listeleme ve seçim servisi
- `IUsbCameraAdapter` benzeri yakalama soyutlaması
- `UsbCameraPlaybackSession`
- `UsbCameraPlaybackSessionFactory`
- WPF üzerinde kare gösterimi veya özel kamera kontrolü

USB kamerayı `MediaElement` URI akışına zorla ekleme. USB kamera cihaz kimliği üzerinden açılan bir capture kaynağıdır; ayrı adapter/session/factory ile resolver'a katılmalıdır.

## Component Katmanı

WPF `UserControl` dosyaları `UyduArayuz_1/Components` altındadır:

- `HeaderControl`: port, bağlantı, görev komutu ve alarm alanı.
- `CenterDisplayArea`: ana merkez yerleşimi.
- `InstantTelemetryPanel`: `CurrentPacket` değerleri.
- `GraphDashboard`: OxyPlot grafikleri.
- `TelemetryTable`: `TelemetryHistory` tablosu.
- `LogPanel`: `LoggerService.Instance.Logs` görünümü.
- `LiveCameraView`: yerel ve ağ videosu kontrolleri ile `MediaElement`.
- `AttitudeIndicator`: HelixToolkit 3D attitude görünümü.
- `Map`: Mapsui WPF harita kontrolü.

Yeni UI eklerken:

- Salt görünüm ise XAML/Component katmanında tut.
- Kullanıcı aksiyonu veya UI state'i varsa ViewModel/command seçeneğini değerlendir.
- Dosya seçici veya WPF kontrol oluşturma gibi framework işi gerekiyorsa ince bir View composition katmanı kullan.
- Seri port, protokol, dosya, ağ veya cihaz yakalama işi ise Service/Adapter katmanına koy.

## Binding, Thread ve Yaşam Döngüsü Kuralları

- Arka plan thread'inden doğrudan UI-bound koleksiyon veya property güncellemesi yapma.
- Telemetri UI güncellemelerini `MainViewModel` içindeki Dispatcher sınırında tut.
- Servis katmanına WPF kontrolü veya `Dispatcher` bağımlılığı ekleme.
- `MediaElement` bağımlılığını adapter ve View composition noktasında tut; ViewModel `MediaElement` bilmemelidir.
- UI-bound property değişiyorsa `INotifyPropertyChanged` gereksinimini kontrol et.
- Event aboneliklerini sahiplik sırasına göre kaldır; View kapanırken önce session/ViewModel, sonra adapter dispose edilmelidir.
- Async start/stop/dispose işlemlerinde iptal, tekrar çağrı ve eşzamanlı çağrı davranışlarını koru.

Yaygın binding yolları:

- `CurrentPacket.PacketNo`
- `TelemetryHistory`
- `GraphViewModel.PacketModel`
- `HeaderControlViewModel.ConnectCommand`
- `AlarmPanelViewModel.GpsErrorLed`
- `MapViewControl`
- Video View içinde `State`, `ErrorMessage`, `CurrentSource.DisplayName`

## Arduino Telemetri Simülatörü

Konum: `ArduinoTelemetrySimulator/ArduinoTelemetrySimulator.ino`

- USB seri port üzerinden ikili telemetri çerçevesi üretir.
- Varsayılan baud rate `9600` ve gönderim aralığı yaklaşık 1 saniyedir.
- Basınç, yükseklik, GPS ve attitude alanlarını zamanla değiştirerek UI testine yardımcı olur.
- Seri hatta `Serial.print/println` gibi ek metin yazılmamalıdır; masaüstü uygulaması yalnızca ikili çerçeve bekler.
- Masaüstü `TelemetryProtocol` ile paket uzunluğu, adres/channel alanları, offset'ler, CRC aralığı ve endian düzeni birebir aynı olmalıdır.

## Derleme ve Çalıştırma

Kök dizinde:

```powershell
dotnet build .\UyduArayuz_1.slnx
```

Uygulamayı çalıştırmak için:

```powershell
dotnet run --project .\UyduArayuz_1\UyduArayuz_1.csproj
```

Son normal build doğrulama tabanı 0 hata ve 41 kaynak uyarısıdır. Uyarılar çoğunlukla nullability ve tekrarlı `using` kaynaklıdır. `--no-incremental` WPF derlemesinde aynı uyarılar geçici `*_wpftmp.csproj` ve ana proje için tekrarlanarak toplam 82 kez raporlanabilir. Uyarı sayısı kod değiştikçe değişebileceği için sabit bir başarı ölçütü olarak kullanılmamalıdır.

Gerçek seri testte doğru COM port ve baud rate seçilmelidir. Donanım olmadan UI ve yerel video test edilebilir; canlı telemetri alınamaz. Network video testi için doğrudan medya URI'si ve ağ erişimi gerekir.

## Bilinen Kritik Riskler ve Bakım Noktaları

Bu bölüm mevcut davranışı belgeleyen bir yön tabelasıdır; maddelerin belgede bulunması sorunun çözüldüğü anlamına gelmez.

### Kritik: masaüstü ve Arduino protokolü uyuşmuyor

- Arduino simülatörü `PACKET_LENGTH = 71`, `OFFSET_END = 70` kullanır ve adres/channel alanlarını içerir.
- Masaüstü `TelemetryProtocol` şu anda `PacketLength = 64` ve `EndOffset = 64` tanımlar. 64 elemanlı dizide 64. indeks geçersizdir.
- Masaüstünde `TeamNoOffset = 59` yorumu 1 bayt derken `TelemetryPacketParser` takım numarasını 4 bayt okur.
- Masaüstü takım numarası okuması 59–62 baytlarını kullanır; `CrcOffset = 60` ile alanlar çakışır.
- Bu uyumsuzluk build sırasında yakalanmaz; seri çalışma anında çerçeve çıkarma/parsing hatasına yol açabilir.
- Çözümde tek bir protokol şeması seçilip hem masaüstü hem Arduino sabitleri ve parser testleri birlikte güncellenmelidir.

### Telemetri yaşam döngüsü

- `SerialTelemetryService.Start` producer ve consumer görevlerini başlatıp hemen döner; donanım portu daha sonra producer içinde açılır. Bu nedenle üst katman `Start` dönüşünü donanım bağlantısının kesin başarı işareti saymamalıdır.
- `Stop`, cancellation uygular ve kaynakları dispose eder; ancak producer/consumer görevlerini saklayıp tamamlanmalarını beklemez. Kapanış ve hızlı yeniden bağlantıda yarış durumları test edilmelidir.
- `LoggerService.Instance` başlatma sırasına duyarlıdır.

### Harita

- `MapViewModel.UpdatePosition` hazırdır fakat `MainViewModel` tarafından çağrılmaz.
- Canlı GPS entegrasyonunda GPS hata biti ve koordinat geçerliliği kontrol edildikten sonra UI Dispatcher içinde `MapViewControl.UpdatePosition` çağrılmalıdır.
- Sürekli `PointFeature` üretiminin uzun süreli telemetride allocation etkisi değerlendirilmelidir.

### Video

- Yerel MP4 oynatma doğrulanmıştır; ağ kaynağında codec/protokol desteği `MediaElement` ve Windows ortamına bağlıdır.
- USB kamera altyapısı uygulanmamıştır.
- `async void` WPF event handler'larında tüm hata yollarının kullanıcıya gösterildiğini veya loglandığını kontrol et.
- Adapter/session event aboneliklerini değiştirirken dispose sırasını ve bekleyen `MediaOpened` iptalini koru.

### Genel bakım

- Otomatik test projesi bulunmamaktadır. Parser, frame extractor, CRC, resolver ve video state geçişleri test için öncelikli adaylardır.
- `LoggerService.cs` ve `LogPanel.xaml.cs` içinde tekrarlı `using` uyarıları vardır.
- Bazı ViewModel ve command üyelerinde nullability uyarıları bulunur.
- `RelayCommand` dosyası `Shared/Mvvm` altında olsa da namespace'i hâlâ `UyduArayuz_1.Helpers` değeridir; taşıma/refactor sırasında namespace kullanımını kontrol et.
- Çalışma ağacında kullanıcıya ait başka değişiklikler olabilir; ilgisiz dosyaları geri alma veya ezme.

## Yeni Katkı İçin Kontrol Listesi

1. Değişiklik hangi katmana ait: Model, ViewModel, Component, Service, Adapter veya Shared?
2. Veri akışı ve sahiplik hangi nesneden hangi nesneye geçiyor?
3. Binding path ve `DataContext` kaynağı doğru mu?
4. UI-bound değişiklik doğru thread'de mi ve `INotifyPropertyChanged` gerekiyor mu?
5. Event/async kaynaklar start, stop, iptal ve dispose yollarında temizleniyor mu?
6. Telemetri protokolü değiştiyse masaüstü ve Arduino şeması birlikte güncellendi mi?
7. Video kaynağı URI tabanlı mı, USB capture cihazı mı; doğru factory/adapter seçildi mi?
8. Harita GPS verisi WGS84'ten Web Mercator'a doğru sırayla dönüştürülüyor mu?
9. Değişiklik küçük ve doğrulanabilir mi; ilgisiz kullanıcı değişiklikleri korundu mu?
10. `dotnet build .\UyduArayuz_1.slnx` başarıyla tamamlanıyor mu?
