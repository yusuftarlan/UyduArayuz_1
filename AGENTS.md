# AGENTS.md

Bu dosya, `teknofest-uydu-arayuz` projesinin güncel teknik haritasıdır.

## Proje özeti

Proje .NET 8 WPF ile geliştirilmiş bir yer istasyonu arayüzüdür. Seri porttan gelen ikili telemetri çerçevelerini doğrular ve anlık panel, grafikler, alarm LED'leri, sesli alarm, 3B durum göstergesi, harita, geçmiş tablosu ve log alanında gösterir. Canlı görüntü kaynağı olarak yalnızca USB kamera desteklenir.

Kullanılan temel paketler:

- `System.IO.Ports`: seri port haberleşmesi
- `OpenCvSharp4.Windows`: USB kamera yakalama
- `OxyPlot.Wpf`: telemetri grafikleri
- `HelixToolkit.Wpf`: 3B durum göstergesi
- `Mapsui.Wpf`: OpenStreetMap tabanlı harita

## Uygulama akışı

`App.xaml`, `MainWindow` penceresini açar. `MainWindow.xaml.cs`, `MainViewModel` örneğini doğrudan oluşturup pencerenin `DataContext` değerine atar. Pencere kapanırken `DataContext`, `IDisposable` ise dispose edilir. Projede harici uygulama ayarı veya `appsettings.json` bulunmaz.

Ana ekran bileşenleri:

- `HeaderControl`: port seçimi, bağlantı durumu, görev komutları ve alarm LED'leri
- `InstantTelemetryPanel`: son doğrulanmış paket
- `GraphDashboard`: son 60 ölçümü gösteren altı grafik
- `TelemetryTable`: son 100 doğrulanmış paketin geçmişi
- `LogPanel`: ekrandaki son 20 WARN ve ERROR kaydı
- `AttitudeIndicator`: pitch, roll ve yaw değerleriyle dönen STL modeli
- `Map`: geçerli GPS konumu
- `LiveCameraView`: USB kamera görüntüsü

Batarya grafiği doğrudan `TelemetryPacket.BatteryVoltage` değerini volt cinsinden gösterir. Sabit batarya yüzdesi seçeneği yoktur.

## Telemetri protokolü

Protokol sabitleri `Services/TelemetryProtocol.cs` dosyasındadır.

- Paket uzunluğu: 80 bayt
- Başlangıç işareti: `3C 3C 3C 3C`
- Bitiş işareti: `3E 3E 3E 3E`
- Görev kodu: offset 62'den başlayan 6 ASCII karakter
- Takım numarası: offset 68–71
- CRC alanı: offset 72–75
- Bitiş alanı: offset 76–79
- Sayısal alanlar little-endian düzenindedir
- CRC, başlangıç alanından takım numarasının sonuna kadar olan 72 bayt üzerinde STM32 uyumlu `0x04C11DB7` polinomu ile hesaplanır

Doğrulanan `TelemetryPacket` nesneleri `sealed` sınıftır ve property'leri `init` erişimlidir; parse işleminden sonra değiştirilmemelidir.
RTC değeri modelde `DateTime`, protokolde 32 bit olan GPS alanları ise `float` olarak tutulur. Metin biçimlendirmesi UI ve CSV sınırında yapılır.

Veri akışı:

```text
SerialPort
    -> SerialTelemetryService üretici görevi
    -> TelemetryFrameProcessor
        -> TelemetryFrameExtractor
        -> TelemetryPacketParser / TelemetryCrc32
        -> ITelemetryRecorder (CSV)
    -> OnTelemetryReceived
    -> MainViewModel (WPF Dispatcher)
    -> panel / grafik / alarm / 3B / harita / tablo
```

`TelemetryFrameExtractor`, parçalı okumaları tamponlar ve tam 80 baytlık çerçeveleri ayırır. Başlangıç işareti bulunan fakat beklenen konumda bitiş işareti olmayan adayları sayarak işlemciye bildirir. `TelemetryPacketParser` uzunluk, başlangıç, bitiş, CRC ve RTC alanlarını doğrular. `TelemetryFrameProcessor`, çıkarma, parse etme, doğrulanmış paketi kaydetme ve parse hatalarını raporlama sorumluluklarını seri port yaşam döngüsünden ayırır. Yalnızca doğrulanan paketler CSV kuyruğuna ve UI event'ine gönderilir.

CRC uyuşmazlığında çerçevenin ham hex içeriği kalıcı WARN loguna yazılır; ekranda daha kısa bir uyarı gösterilir. Geçersiz bitiş işaretleri toplu WARN, parser'ın reddettiği diğer çerçeveler WARN olarak raporlanır.

Protokol değişikliğinde `TelemetryProtocol`, `TelemetryFrameExtractor`, `TelemetryPacketParser`, `TelemetryCrc32`, `TelemetryPacket`, `TelemetryFrameProcessor`, CSV kolonları ve karşı cihazın paket üreticisi birlikte gözden geçirilmelidir.

## Seri servis ve bağlantı yaşam döngüsü

`SerialTelemetryService`, seri port yaşam döngüsünü ve arka plan üretici görevini yönetir. UI nesnelerine doğrudan erişmez. Doğrulanmış paket event'ini üretici görevinden yükseltir; `MainViewModel` handler'ı yalnızca Dispatcher kuyruğuna aktarım yaptığı için ayrıca bir UI kanalı ve tüketici görevi yoktur. Loglama ve telemetri kaydı için sırasıyla `IApplicationLogger` ve `ITelemetryRecorder` bağımlılıklarını kullanır.

`Start` çağrısı portu önce çağıran thread üzerinde açar. Port gerçekten açılmadan metot başarılı dönmez ve `HeaderControlViewModel` bağlı duruma geçirilmez. Başarılı açılıştan sonra seri okuma için uzun süreli üretici görevi başlatılır. Yeni bağlantı başlatılırsa önceki oturum önce durdurulur.

`Stop`, iptal sinyalini gönderir, açık portu kapatarak bloklayan okumayı sonlandırır, üretici görevini bekler ve oturum kaynaklarını dispose eder. Normal durdurmada bağlantı durumunu çağıran ViewModel günceller. Üretici iptal isteği olmadan sona erdiğinde UI'a `OnConnectionEnded` bildirilir; `MainViewModel` alarm sesini durdurur ve bağlantı durumunu günceller.

Görev komutları yalnızca bağlı ve açık port `COM6` ise gönderilir. Başka portlardan telemetri dinlenebilir fakat komut gönderilemez:

- Ayrılma: `00 00 00 00`
- Acil paraşüt: `01 00 00 00`
- Görev kodu: `AA` ve ardından ASCII olmayan üç ayrı sayısal `0`, `1` veya `2` baytı

UI'daki görev kodu, ayrılma ve paraşüt kontrolleri yalnızca etkin bağlantı portu `COM6` olduğunda açılır. Servis aynı kuralı gönderim anında tekrar doğrular.

## Kayıtlar ve tanılama

Kayıtlar kullanıcıya yazılabilir sabit bir konum olan `%LOCALAPPDATA%/teknofest-uydu-arayuz/telemetry-records` altında tutulur. `LocalApplicationData` işletim sistemi tarafından sağlanamazsa uygulama dizini yedek konum olarak kullanılır.

- `TelemetryCsvRecorder`: doğrulanmış paketleri sınırlı bir kanal üzerinden toplu ve asenkron biçimde CSV dosyasına yazar
- `ApplicationLogRecorder`: WARN ve ERROR kayıtlarını sınırlı bir kanal üzerinden TXT dosyasına yazar
- `LoggerService`: thread-safe koleksiyonda ekrandaki son 20 WARN/ERROR kaydını tutar ve kalıcı kaydı `ApplicationLogRecorder` nesnesine iletir
- `SerialDiagnostics`: ayrıntılı seri port ve çerçeve tanılamasını debug/console çıktısına yazar

Kaydediciler dispose edilirken kanalları tamamlar, kuyruktaki verileri tüketir ve yazıcı görevlerini bekler. Kayıt başlatılamazsa uygulama telemetri göstermeye devam eder ve hata loglanır.

## UI güncelleme kuralları

`MainViewModel.TelemetryService_OnTelemetryReceived`, telemetri event'ini WPF Dispatcher kuyruğuna aktarır. ViewModel dispose edilmişse veya bağlantı artık aktif değilse gecikmiş paket yok sayılır. Aktif bir paket geldiğinde:

1. Alarm LED'lerini ve alarm sesini günceller.
2. OxyPlot serilerine veri ekler.
3. 3B göstergeyi günceller.
4. GPS hata biti kapalıysa harita konumunu günceller.
5. `CurrentPacket` değerini değiştirerek anlık paneli yeniler.
6. Paketi `TelemetryHistory` koleksiyonuna ekler ve geçmişi 100 paketle sınırlar.

Arka plan thread'inden UI-bound property veya koleksiyon doğrudan değiştirilmemelidir. Seri servis WPF tiplerine bağımlı olmamalıdır. Telemetri için Dispatcher sınırı `MainViewModel` içindedir; kamera tarafındaki WPF `Image` ve Dispatcher bağımlılıkları View/Adapter katmanında kalır.

## USB kamera

USB kamera akışı:

```text
LiveCameraView (kamera sıra numarası)
    -> OpenCvUsbCameraPlaybackAdapter
    -> DirectShow, başarısızsa Media Foundation
    -> dondurulmuş BitmapSource
    -> WPF Image
```

`LiveCameraView`, start, stop ve unload işlemlerini tek bir `SemaphoreSlim` ile sıralar ve UI düğmelerinin `Idle`, `Starting`, `Playing`, `Stopping`, `Faulted` durumlarını yönetir. Başlangıç iptal edilebilir; View unload olduğunda OpenCV adapter dispose edilir. Tek kamera implementasyonu bulunduğu için arada player ve playback interface katmanı yoktur.

`OpenCvUsbCameraPlaybackAdapter`, kareleri arka plan görevinde okur. Oluşturulan `BitmapSource` dondurulur ve render kuyruğunda yalnızca en yeni kare tutulur; böylece yavaş UI durumunda eski kareler birikmez.

UI kamera aygıtlarını adlarıyla keşfetmez; kullanıcı OpenCV kamera sıra numarasını (`0`, `1`, ...) verir. Varsayılan sıra numarası `0`'dır ve negatif değerler reddedilir. Kamera testi için Windows kamera izni, uygun sürücü ve fiziksel aygıt gerekir.

## Derleme ve çalıştırma

Kök dizinde:

```powershell
dotnet build .\teknofest-uydu-arayuz.slnx
```

Uygulamayı çalıştırmak için:

```powershell
dotnet run --project .\teknofest-uydu-arayuz\teknofest-uydu-arayuz.csproj
```

Nihai Windows x64 klasör paketini üretmek için:

```powershell
dotnet publish .\teknofest-uydu-arayuz\teknofest-uydu-arayuz.csproj -p:PublishProfile=WinX64
```

Publish çıktısı `artifacts/publish/teknofest-uydu-arayuz-win-x64` altında oluşur. Profil .NET çalışma zamanını pakete dahil eder; hedef makinede ayrıca .NET kurulumu gerekmez. WPF ve OpenCV'nin reflection/yerel bağımlılık gereksinimleri nedeniyle trimming ve single-file publish kapalıdır. Paket `body-model/counsat.stl`, `sound/warning.mp3`, OpenCV yerel DLL'leri ve `teknofest-uydu-arayuz.exe` dosyasını birlikte içermelidir. Ürün ve assembly sürümü `teknofest-uydu-arayuz.csproj` içinde tutulur; her dağıtım öncesinde sürüm yükseltilmelidir.

Debug yapılandırması konsol çıktısını göstermek için `Exe`, diğer yapılandırmalar WPF uygulaması olarak `WinExe` üretir. Donanım olmadan derleme ve UI başlangıcı doğrulanabilir; gerçek seri telemetri ve USB kamera yakalama hattı uçtan uca doğrulanamaz.

Çözümde otomatik test projesi bulunmaz. Protokol, frame extractor, CRC, seri yaşam döngüsü ve kamera state geçişleri değiştirildiğinde mümkün olan davranışlar ayrıca hedefli test edilmelidir.

## Değişiklik kontrol listesi

1. Değişiklik doğru katmanda mı; seri taşıma ile çerçeve işleme sorumlulukları ayrık mı?
2. XAML binding yolu mevcut property ile eşleşiyor mu?
3. UI güncellemesi Dispatcher sınırında mı ve bağlantı/dispose durumu kontrol ediliyor mu?
4. Event abonelikleri, kanallar, görevler ve async kaynaklar dispose sırasında temizleniyor mu?
5. Protokol değiştiyse tüm offset, uzunluk, endian, CRC, model ve CSV kolonları birlikte güncellendi mi?
6. Komut formatı tam 4 bayt mı ve yalnızca açık `COM6` üzerinden mi gönderiliyor?
7. İlgisiz kullanıcı değişiklikleri korunuyor mu?
8. `dotnet build .\teknofest-uydu-arayuz.slnx` başarıyla tamamlanıyor mu?
9. `WinX64` profiliyle publish başarılı mı ve zorunlu model, ses, OpenCV native DLL ve EXE dosyaları çıktıda mevcut mu?
