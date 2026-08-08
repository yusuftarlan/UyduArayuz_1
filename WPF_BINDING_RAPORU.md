# WPF Property–UI Etki Raporu

Bu rapor, projedeki ViewModel property'lerinin, koleksiyonların ve özel yenileme çağrılarının hangi WPF elemanlarını etkilediğini açıklar. İnceleme statiktir; kaynak kod değiştirilmemiş ve uygulama çalıştırılarak görsel test yapılmamıştır.

## 1. Temel zihinsel model

Projede ekran her zaman `OnPropertyChanged()` ile güncellenmiyor. Dört farklı güncelleme yolu var:

| Mekanizma | Ne zaman kullanılıyor? | Bu projedeki örnek |
|---|---|---|
| `INotifyPropertyChanged` | Tek bir property'nin değeri değiştiğinde | `CurrentPacket`, `IsConnected`, alarm LED renkleri, `Pitch` |
| `ObservableCollection.CollectionChanged` | Listeye eleman eklendiğinde veya listeden eleman silindiğinde | `TelemetryHistory`, `AvailablePorts`, `Logs` |
| Kütüphaneye özel yenileme | Kontrol kendi veri modelini içeride değiştirdiğinde | OxyPlot için `InvalidatePlot(true)` |
| Code-behind olayı | Binding dışında ek UI davranışı gerektiğinde | Tabloyu son satıra kaydırma, haritayı `MapControl`'e atama |

Önemli sonuç:

> Bir metodun çağrılması tek başına WPF ekranını güncellemez. Metodun içinde bir property bildirimi, koleksiyon olayı, kütüphaneye özel yenileme veya doğrudan kontrol güncellemesi bulunmalıdır.

## 2. DataContext akışı

`MainWindow.xaml.cs`, `MainViewModel` nesnesini oluşturur ve pencerenin `DataContext` değeri yapar:

```text
MainWindow.DataContext
        |
        v
MainViewModel
        |
        +-- CurrentPacket
        +-- TelemetryHistory
        +-- HeaderControlViewModel
        +-- AlarmPanelViewModel
        +-- GraphViewModel
        +-- AttitudeViewModel
        +-- MapViewControl
```

`HeaderControl`, `CenterDisplayArea`, `InstantTelemetryPanel`, `GraphDashboard`, `TelemetryTable` ve `AttitudeIndicator` kendi `DataContext` değerlerini değiştirmediği için `MainWindow` üzerindeki `MainViewModel` nesnesini miras alır.

Harita farklıdır:

```xml
<comp:Map DataContext="{Binding MapViewControl}" />
```

Bu nedenle `Map` bileşeninin `DataContext` değeri doğrudan `MapViewModel` olur.

`LogPanel` ise miras alınan `DataContext` yerine statik olarak `LoggerService.Instance` kaynağını kullanır.

## 3. Telemetri paketi geldiğinde oluşan güncellemeler

`TelemetryService_OnTelemetryReceived` içindeki işlemler UI thread'inde aşağıdaki sırayla çalışır:

| Çalışan kod | Güncelleme yolu | Etkilenen ekran alanı |
|---|---|---|
| `AlarmPanelViewModel.UpdateAlarms(e.ErrorCode)` | Dört LED property setter'ı ve `PropertyChanged` | Header içindeki dört alarm elipsi |
| `GraphViewModel.UpdateGraphs(e)` | Serilere nokta ekleme ve `InvalidatePlot(true)` | Altı OxyPlot grafiği |
| `AttitudeViewModel.UpdateAttitude(...)` | `Yaw`, `Pitch`, `Roll` setter'ları ve `PropertyChanged` | 3D kutunun üç dönüş açısı |
| `CurrentPacket = e` | `MainViewModel.PropertyChanged("CurrentPacket")` | Anlık telemetri panelindeki paket alanları |
| `TelemetryHistory.Add(e)` | `CollectionChanged(Add)` | Telemetri tablosuna yeni satır ve son satıra kaydırma |
| `TelemetryHistory.RemoveAt(0)` | `CollectionChanged(Remove)` | 100 paket aşıldığında en eski tablo satırının kaldırılması |

### `CurrentPacket = e` neden hâlâ gerekli?

Grafikler daha önce güncellenmiş olsa da grafikler `CurrentPacket` binding'ini kullanmaz. `UpdateGraphs(e)`, `e` paketindeki değerleri doğrudan OxyPlot serilerine ekler.

`CurrentPacket = e` ise `InstantTelemetryPanel` içindeki şu tür binding'lerin yeniden okunmasını sağlar:

```xml
Text="{Binding CurrentPacket.Pressure}"
```

Akış şöyledir:

```text
CurrentPacket referansı değişir
        |
        v
PropertyChanged("CurrentPacket")
        |
        v
WPF bütün CurrentPacket.* yollarını yeniden okur
        |
        v
Anlık telemetri TextBlock'ları yeni paketi gösterir
```

`TelemetryPacket` sınıfı `INotifyPropertyChanged` uygulamıyor. Buna rağmen burada ekran güncellenir, çünkü mevcut paketin içindeki alanlar tek tek değiştirilmek yerine bütün `CurrentPacket` nesnesi yenisiyle değiştirilmektedir. Aynı `TelemetryPacket` nesnesinin örneğin yalnızca `Pressure` değeri sonradan değiştirilseydi WPF bu değişikliği otomatik öğrenemezdi.

## 4. `MainViewModel` property ve koleksiyonları

### `CurrentPacket`

| Kaynak değişim | Bildirim | XAML binding'i | Güncellenen WPF elemanı |
|---|---|---|---|
| `CurrentPacket = e` | `PropertyChanged("CurrentPacket")` | `CurrentPacket.PacketNo` | `txtPaketNo` `TextBlock.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.SatelliteStatus` | `txtStatus` `TextBlock.Text` içindeki aktif `MultiBinding` |
| Aynı atama | Aynı bildirim | `CurrentPacket.ErrorCode` | `txtError` `TextBlock.Text` içindeki aktif `MultiBinding` |
| Aynı atama | Aynı bildirim | `CurrentPacket.SentDate` | `txtSaat.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.Pressure` | `txtBasinc.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.Height` | `txtYukseklik.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.LandingSpeed` | `txtInisHizi.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.Tempreture` | `txtSicaklik.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.BatteryVoltage` | `txtPil.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.GpsLatitude` | `txtLat.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.GpsLongitude` | `txtLong.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.GpsAltitude` | `txtGpsAlt.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.Pitch` | `txtPitch.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.Roll` | `txtRoll.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.Yaw` | `txtYaw.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.TaskCode` | `txtBonus.Text` |
| Aynı atama | Aynı bildirim | `CurrentPacket.TeamNo` | `txtTeamNo.Text` |

Yorum satırındaki `CurrentPacket.SatelliteStatusString` ve `CurrentPacket.ErrorCodeString` binding'leri aktif değildir ve ekranı etkilemez.

### `TelemetryHistory`

| Kaynak değişim | Bildirim | XAML/code-behind hedefi | Sonuç |
|---|---|---|---|
| `TelemetryHistory.Add(e)` | `CollectionChanged(Add)` | `DataGrid.ItemsSource="{Binding TelemetryHistory}"` | Yeni telemetri satırı oluşur |
| Aynı ekleme | Aynı koleksiyon olayı | `TelemetryTable.xaml.cs` dinleyicisi | `HistoryGrid.ScrollIntoView(...)` ile yeni satıra kayılır |
| `TelemetryHistory.RemoveAt(0)` | `CollectionChanged(Remove)` | Aynı `DataGrid` | En eski satır kaldırılır |

Her satırdaki sütunlar, eklenen `TelemetryPacket` nesnesinin `PacketNo`, `SatelliteStatus`, `ErrorCode`, `SentDate`, basınç, yükseklik, hız, sıcaklık, pil, GPS, yönelim, görev kodu ve takım numarası property'lerini okur.

Burada `MainViewModel.OnPropertyChanged()` çağrılmaz; buna gerek yoktur çünkü koleksiyon nesnesi aynı kalır, yalnızca içeriği değişir. `ObservableCollection` bu değişimi kendisi bildirir.

`TelemetryHistory` property referansı çalışma sırasında tamamen başka bir koleksiyonla değiştirilirse setter ve `PropertyChanged` bulunmadığı için `DataGrid` yeni koleksiyona otomatik geçmez. Mevcut kodda koleksiyon yalnızca kurucuda oluşturulduğu için bu durum şu an gerçekleşmiyor.

## 5. `HeaderControlViewModel`

| Property/koleksiyon | Değişimi tetikleyen durum | Bildirim | Etkilenen WPF elemanları |
|---|---|---|---|
| `AvailablePorts` içeriği | Açılış veya yenile butonu ile `Clear()` / `Add()` | `CollectionChanged` | Port `ComboBox.ItemsSource` |
| `SelectedPort` | İlk portun otomatik seçilmesi veya kullanıcının ComboBox seçimi | `PropertyChanged("SelectedPort")`; kullanıcı seçiminde binding varsayılan olarak kaynağa da yazar | Port `ComboBox.SelectedItem` |
| `BaudRates` içeriği | Kurucudaki sabit başlangıç listesi | İlk binding okuması; sonradan değişirse `CollectionChanged` | Baud rate `ComboBox.ItemsSource` |
| `SelectedBaudRate` | Kullanıcının ComboBox seçimi | `PropertyChanged("SelectedBaudRate")` | Baud rate `ComboBox.SelectedItem` |
| `IsConnected` | Başarılı bağlantı, bağlantı hatası veya bağlantıyı kesme | `PropertyChanged("IsConnected")` | KES butonu, görev kodu kutusu, GÖNDER, kalibrasyon ve ayrılma butonlarının `IsEnabled` değeri |
| `AreSettingsEnabled` | `IsConnected` setter'ı tarafından ayrıca bildirilir | `PropertyChanged("AreSettingsEnabled")` | Yenile ve BAĞLAN butonları ile iki ComboBox'ın `IsEnabled` değeri |
| `SystemStatus` | Bağlantı sonucu veya bağlantıyı kesme | `PropertyChanged("SystemStatus")` | `txtDetailedStatus.Text` |
| `SystemStatusColor` | Bağlantı veya bağlantıyı kesme sırasında renk atanması | `PropertyChanged("SystemStatusColor")` | Durum elipsinin `Fill` değeri ve durum metninin `Foreground` değeri |

Command binding'leri:

| ViewModel property | WPF hedefi | Davranış |
|---|---|---|
| `RefreshPortsCommand` | Yenile butonunun `Command` değeri | Port listesini yeniden doldurur |
| `ConnectCommand` | BAĞLAN butonunun `Command` değeri | Seçili port ve baud rate ile bağlantı isteği gönderir |
| `DisconnectCommand` | KES butonunun `Command` değeri | Bağlantıyı kesme isteği gönderir |

Bu command property'leri çalışma sırasında değiştirilmiyor. Bu nedenle `PropertyChanged` bildirimlerinin olmaması mevcut kullanımda sorun yaratmıyor; kurulum sırasında ilk kez okunuyorlar.

### Dikkat çeken bildirim

`SystemStatus` setter'ı hem `SystemStatus` hem de `SystemStatusColor` için bildirim gönderiyor:

```csharp
OnPropertyChanged();
OnPropertyChanged(nameof(SystemStatusColor));
```

Ancak `SystemStatus` değeri kendi başına rengi hesaplamıyor. Renk ayrıca `SystemStatusColor` setter'ı ile atanıyor ve o setter zaten tekrar bildirim gönderiyor. Bu nedenle bağlantı akışlarında ilk renk bildirimi çoğunlukla mevcut/eski rengi yeniden okutuyor, sonraki gerçek renk ataması ikinci kez güncelleme yapıyor. Bu bir çalışma hatası olmak zorunda değildir fakat gereksiz bildirimdir.

## 6. `AlarmPanelViewModel`

`UpdateAlarms(ErrorCode)`, hata kodunun bitlerini dört LED rengine dönüştürür.

| Property | Kontrol edilen bit | Bildirim | XAML hedefi |
|---|---:|---|---|
| `LandingSpeedErrorLed` | Bit 0 | `PropertyChanged("LandingSpeedErrorLed")` | İniş alarmı `Ellipse.Fill` |
| `GpsErrorLed` | Bit 1 | `PropertyChanged("GpsErrorLed")` | GPS alarmı `Ellipse.Fill` |
| `SeperationErrorLed` | Bit 2 | `PropertyChanged("SeperationErrorLed")` | Ayrılma alarmı `Ellipse.Fill` |
| `EmergencyParachuteErrorLed` | Bit 3 | `PropertyChanged("EmergencyParachuteErrorLed")` | Paraşüt alarmı `Ellipse.Fill` |

LED stilindeki `DropShadowEffect.Color`, ilgili elipsin `Fill.Color` değerine `RelativeSource` ile bağlıdır. Bu yüzden `Fill` değişince yalnızca LED gövdesi değil, gölge rengi de değişir.

Setter'larda eski ve yeni değer karşılaştırması yoktur. Aynı fırça tekrar atansa bile `PropertyChanged` gönderilir.

`ErrorCode == 0` olduğunda dört property önce ayrı blokta yeşile, ardından bit kontrollerinde tekrar yeşile atanır. Böylece aynı telemetri paketi için her LED iki kez, toplam sekiz bildirim gönderebilir. Görsel sonuç doğrudur; bildirim sayısı gereksizdir.

## 7. `AttitudeViewModel`

| Property | Değişimi tetikleyen kod | Bildirim | XAML hedefi |
|---|---|---|---|
| `Pitch` | `UpdateAttitude(..., pitch, ...)` | Değer farklıysa `PropertyChanged("Pitch")` | X eksenindeki `AxisAngleRotation3D.Angle` |
| `Yaw` | `UpdateAttitude(yaw, ...)` | Değer farklıysa `PropertyChanged("Yaw")` | Y eksenindeki `AxisAngleRotation3D.Angle` |
| `Roll` | `UpdateAttitude(..., roll)` | Değer farklıysa `PropertyChanged("Roll")` | Z eksenindeki `AxisAngleRotation3D.Angle` |

Bu üç binding, `AttitudeIndicator` içindeki `BoxVisual3D` nesnesinin `Transform3DGroup` dönüşlerini değiştirir. Böylece telemetri panelindeki sayısal `CurrentPacket.Pitch/Roll/Yaw` alanlarından bağımsız olarak 3D kutu döner.

Setter'lar aynı değer tekrar geldiğinde erken çıkar:

```csharp
if (_pitch == value) return;
```

Dolayısıyla değer değişmediyse gereksiz WPF bildirimi gönderilmez.

## 8. Grafik modelleri

`GraphDashboardViewModel`, `INotifyPropertyChanged` uygulamaz. `PlotModel` property'leri kurucuda bir kez oluşturulur ve `PlotView.Model` binding'leri ilk yüklemede bu nesneleri alır.

| Telemetri alanı | Değiştirilen model/seri | WPF hedefi | Yenileme |
|---|---|---|---|
| `PacketNo` | `PacketModel.Series[0]` | Paket No `PlotView` | `PacketModel.InvalidatePlot(true)` |
| `Pressure` | `PressureModel.Series[0]` | Basınç `PlotView` | `PressureModel.InvalidatePlot(true)` |
| `Height` | `HeightModel.Series[0]` | Yükseklik grafiğinin barometrik serisi | `HeightModel.InvalidatePlot(true)` |
| `GpsAltitude` | `HeightModel.Series[1]` | Yükseklik grafiğinin GPS serisi | Aynı `HeightModel.InvalidatePlot(true)` |
| `LandingSpeed` | `VelocityModel.Series[0]` | İniş Hızı `PlotView` | `VelocityModel.InvalidatePlot(true)` |
| `Tempreture` | `TemperatureModel.Series[0]` | Sıcaklık `PlotView` | `TemperatureModel.InvalidatePlot(true)` |
| `BatteryVoltage` | `VoltageModel.Series[0]` | Pil Gerilimi `PlotView` | `VoltageModel.InvalidatePlot(true)` |

Grafiklerin model binding'leri şunlardır:

- `GraphViewModel.PacketModel`
- `GraphViewModel.PressureModel`
- `GraphViewModel.HeightModel`
- `GraphViewModel.VelocityModel`
- `GraphViewModel.TemperatureModel`
- `GraphViewModel.VoltageModel`

Her seride son 60 nokta tutulur. Yeni nokta eklenmesi normal bir WPF property değişimi değildir; OxyPlot'a yeniden çizim emrini `InvalidatePlot(true)` verir.

`OrientationModel`, `RouteModel` ve bunların güncelleme blokları yorum satırındadır. XAML içinde bunlara bağlı bir `PlotView` da yoktur. `SpareModel` tanımlıdır fakat oluşturulmuyor, güncellenmiyor ve binding'i bulunmuyor.

Grafik model property'leri çalışma sırasında yeni `PlotModel` nesneleriyle değiştirilirse `PropertyChanged` olmadığı için `PlotView` binding'leri bunu otomatik öğrenmez. Mevcut akışta model nesneleri değiştirilmiyor; yalnızca içlerindeki seri noktaları değiştiriliyor.

## 9. Harita

Harita akışı standart bir XAML `Map` property binding'i yerine code-behind ile kurulmuştur:

```text
MainViewModel.MapViewControl
        |
        | MainWindow üzerindeki DataContext binding'i
        v
Map UserControl.DataContext = MapViewModel
        |
        | DataContextChanged veya Loaded
        v
MyMapControl.Map = vm.MyMap
```

| Kaynak | Mekanizma | Etkilenen WPF elemanı |
|---|---|---|
| `MainViewModel.MapViewControl` | `<comp:Map DataContext="{Binding MapViewControl}">` | `Map` UserControl'ünün `DataContext` değeri |
| `MapViewModel.MyMap` | `Map.xaml.cs` içindeki doğrudan atama | Mapsui `MyMapControl.Map` |
| `MyMap.Layers.Add(...)` | Mapsui modelinin iç koleksiyon değişimi | OpenStreetMap katmanı |

`MapViewModel.MyMap` setter'ı `PropertyChanged("MyMap")` gönderir. Fakat `Map.xaml` içinde `MyMap` için doğrudan bir binding yoktur ve code-behind `MapViewModel.PropertyChanged` olayını dinlemez. Bu nedenle `Map` bileşeni yüklendikten sonra `MyMap` tamamen yeni bir nesneyle değiştirilirse bu bildirim tek başına `MyMapControl.Map` değerini yenilemez.

Mevcut kullanımda `MyMap`, `MapViewModel` kurucusunda oluşturulur; `DataContextChanged` veya `Loaded` sırasında bir kez kontrole atanır. Bu yüzden başlangıç haritası görünür, ancak `MyMap.PropertyChanged` altyapısı mevcut code-behind bağlantısında etkili kullanılmamaktadır.

Harita şu anda telemetri paketindeki `GpsLatitude` ve `GpsLongitude` değişimlerinden etkilenmez. Bu koordinatları haritada işaretleyen veya harita merkezini güncelleyen aktif kod bulunmamaktadır.

## 10. Log paneli

`LogPanel`, `MainViewModel` üzerinden değil şu statik kaynak üzerinden bağlanır:

```xml
ItemsSource="{Binding Path=Logs,
              Source={x:Static services:LoggerService.Instance}}"
```

| Kaynak değişim | Bildirim | WPF hedefi | Ek davranış |
|---|---|---|---|
| `LoggerService.Logs.Add(newLog)` | `CollectionChanged(Add)` | `TerminalListBox.ItemsSource` | Code-behind yeni loga `ScrollIntoView` uygular |
| Yeni öğenin `Level` değeri `ERROR` | Öğe oluşturulurken ilk binding okuması | `ListBoxItem.Foreground` ve `FontWeight` | Kırmızı ve kalın görünür |
| Yeni öğenin `Level` değeri `WARN` | Öğe oluşturulurken ilk binding okuması | `ListBoxItem.Foreground` | Sarı görünür |
| Yeni `LogModel` öğesi | Koleksiyona ekleme | `TextBlock.Text="{Binding}"` | `LogModel.ToString()` sonucu gösterilir |

`LogModel` bir `struct` ve `INotifyPropertyChanged` uygulamıyor. Bu projede log öğeleri oluşturulduktan sonra değiştirilmediği için ilk gösterim yeterlidir. Mevcut bir öğenin `Level`, `Message` veya `Time` değeri sonradan değiştirilecek olsaydı ListBox bunu otomatik güncelleyemezdi.

`MainViewModel`, `InitializeComponent()` çalışmadan önce oluşturulur ve kurucusunda `LoggerService.Instance` atanır. Bu başlangıç sırası sayesinde `LogPanel` XAML'i statik kaynağı okuduğunda instance hazırdır.

## 11. Binding'i olmayan veya çalışma sırasında bildirim almayan alanlar

| Alan | Mevcut durum | Olası sonuç |
|---|---|---|
| `TelemetryPacket.SatelliteStatusString` | XAML binding'i yorum satırında | Ekranda gösterilmez |
| `TelemetryPacket.ErrorCodeString` | XAML binding'i yorum satırında | Ekranda gösterilmez |
| `GraphDashboardViewModel.OrientationModel` | Oluşturma/güncelleme kodu yorum satırında, XAML binding'i yok | Ekranı etkilemez |
| `GraphDashboardViewModel.RouteModel` | Oluşturma/güncelleme kodu yorum satırında, XAML binding'i yok | Ekranı etkilemez |
| `GraphDashboardViewModel.SpareModel` | Yalnızca tanımlı | Ekranı etkilemez |
| `MapViewModel.MyMap.PropertyChanged` | Aktif binding veya event aboneliği yok | İlk yüklemeden sonraki nesne değişimi MapControl'e ulaşmaz |
| `MainViewModel` alt ViewModel property'leri | Setter/bildirim yok; kurucuda bir kez atanıyor | Çalışma sırasında alt ViewModel nesnesi değiştirilirse ilgili binding'ler bunu öğrenmeyebilir |
| `TelemetryHistory` property referansı | Setter/bildirim yok | Koleksiyon nesnesi değiştirilirse DataGrid yeni kaynağa geçmeyebilir |
| `TelemetryPacket` property'leri | `INotifyPropertyChanged` yok | Koleksiyona eklendikten veya `CurrentPacket` olarak atandıktan sonra aynı nesnenin iç alanları değiştirilirse UI yenilenmez |
| Kamera `MediaElement` | ViewModel binding'i yok | Bu rapordaki property değişimlerinden etkilenmez |
| Görev kodu `TextBox.Text` | ViewModel binding'i yok | Girilen metin ViewModel'e aktarılmaz |
| GÖNDER, kalibrasyon ve ayrılma butonları | `Command` veya click handler binding'i yok | `IsConnected` yalnızca aktif/pasif durumlarını değiştirir; tıklama davranışı tanımlı değildir |

## 12. XAML içindeki ViewModel dışı binding'ler

Bütün binding ifadelerini ayırmak için, ViewModel property değişimlerinden bağımsız olan binding'ler de burada listelenmiştir:

| Binding | Kaynak | Etki |
|---|---|---|
| `DropShadowEffect.Color="{Binding Fill.Color, RelativeSource=...Ellipse}"` | Aynı LED elipsinin `Fill.Color` değeri | LED gölgesini dolgu rengiyle eşleştirir |
| `{TemplateBinding Background}` | Şablonlanan Button/HeaderedContentControl | Üst kontrolün arka planını template içindeki elemana aktarır |
| `{TemplateBinding BorderBrush}` | Şablonlanan Button | Kenarlık rengini template'e aktarır |
| `{TemplateBinding BorderThickness}` | Şablonlanan Button | Kenarlık kalınlığını template'e aktarır |
| `{TemplateBinding Padding}` | Şablonlanan Button | İç boşluğu template'e aktarır |
| `{TemplateBinding Header}` | `HeaderedContentControl.Header` | Grafik kartının başlık `TextBlock` metnini oluşturur |

Bu binding'ler `MainViewModel.OnPropertyChanged()` olayını dinleyen uygulama verisi binding'leri değildir; WPF kontrol şablonu ve görsel ağaç içindeki property aktarımıdır.

## 13. Sonuç

`TelemetryService_OnTelemetryReceived` içindeki satırlar birbirinin tekrarı değildir; aynı paketi farklı ekran bölgelerine, farklı bildirim mekanizmalarıyla dağıtır:

```text
ErrorCode                 -> alarm property'leri -> LED'ler
Grafik alanları           -> OxyPlot serileri    -> grafikler
Yaw / Pitch / Roll        -> attitude property'leri -> 3D kutu
CurrentPacket = e         -> PropertyChanged     -> anlık değerler
TelemetryHistory.Add(e)   -> CollectionChanged   -> tablo
```

Dolayısıyla `CurrentPacket = e` satırı kaldırılırsa grafikler, alarm LED'leri, 3D gösterge ve tablo kendi mekanizmalarıyla çalışmaya devam edebilir; fakat `InstantTelemetryPanel` içindeki `CurrentPacket.*` alanları yeni telemetri paketini göstermez.
