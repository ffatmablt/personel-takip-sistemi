# 🏢 Personel Takip Sistemi

Bir iş yerinin giriş-çıkış takibini yapan, patron paneli ile personel ekranını ayıran gerçek zamanlı bir personel yönetim sistemi.

Bu projeyi iş arayış sürecimde tamamen sıfırdan, tek başıma geliştirdim. Başlangıçta Docker ve .NET öğrenmek için basit bir proje olarak başladı. Geliştirdikçe gerçek bir iş yeri senaryosuna dönüştü: A kapısından giren personel, B kapısından çıkıyor; patron şifreli panelden kimin içeride olduğunu, kimin geç kaldığını, kimin hiç gelmediğini anlık olarak takip edebiliyor.

---

## 🎯 Özellikler

**Personel Ekranı (index.html)**
- Sicil numarası girip A veya B kapısından kart okutabiliyorsunuz
- Son geçişler anlık olarak güncelleniyor (5 saniyede bir)
- Yeşil = giriş, kırmızı = çıkış

**Patron Paneli (patron.html — şifreli)**
- Şu an içeride kimler var, saat kaçta girdiler
- Bugün kim kaçta geldi, kaçta çıktı, kaç saat çalıştı
- Kim geç kaldı (mesai saatinden 5 dakika sonra gelen)
- Kim hiç gelmedi
- Yeni personel ekleme


**Kapı Mantığı**
- A → B → A → B = ✅ Normal akış
- A → A = ❌ Hata: Zaten içeridesınız
- B → B = ❌ Hata: Zaten dışarıdasınız
- B → A → B = ✅ Normal akış

---

## 🛠️ Teknolojiler

- **.NET 8 Minimal API** — Backend ve REST API
- **C#** — Programlama dili
- **PostgreSQL** — Veritabanı
- **Entity Framework Core** — ORM (veritabanı ile C# arasında köprü)
- **Docker & Docker Compose** — Container yönetimi
- **HTML / CSS / JavaScript** — Frontend
- **Swagger** — API test ve dokümantasyon

---

## 🏗️ Sistem Mimarisi

İki Docker container çalışıyor:

**api container** → .NET uygulaması (8080 portu)

**db container** → PostgreSQL veritabanı (5432 portu)

Docker Compose bu ikisini birbirine bağlıyor. API, PostgreSQL tamamen hazır olmadan başlamıyor (healthcheck ile).

---

## 📡 API Endpoint'leri

**POST /kart-okut**
Kart okutma işlemi. Sicil numarası ve kapı bilgisi alır, giriş veya çıkış yapar. Aynı kapıdan iki kez geçmeye çalışılırsa hata döner.

**POST /personel**
Yeni personel ekler. Ad, sicil numarası ve mesai başlangıç saati alır.

**GET /gecisler**
Son 10 geçişi listeler. Personel ekranında gösterilir.

**GET /yonetici/icerdekiler**
Şifreli. Şu an içeride olan personeli listeler.

**GET /yonetici/rapor**
Şifreli. Günlük raporu döner: ilk giriş saati, son çıkış saati, toplam çalışma süresi, geç kalma durumu.

---

## ⚙️ Nasıl Çalıştırılır?

Tek gereksinim: Docker Desktop

```bash
git clone https://github.com/ffatmablt/personel-takip-sistemi.git
cd personel-takip-sistemi
docker-compose up --build
```

Tarayıcıda aç:

Personel Ekranı → http://localhost:8080/index.html

Patron Paneli → http://localhost:8080/patron.html (Şifre: admin123)

Swagger → http://localhost:8080/swagger

---

## 🗄️ Veritabanı

**Personeller tablosu**

Id — Otomatik artan kimlik numarası

Ad — Personelin adı soyadı

SicilNo — Kart okutmada kullanılan numara

MesaiBaslangic — Geç kalma hesabı için (örn: 09:00:00)

**Gecisler tablosu**

Id — Otomatik artan kimlik numarası

PersonelId — Hangi personelin geçişi

Zaman — Geçişin tam tarihi ve saati

Tip — "Giriş" veya "Çıkış"

---

## 🚧 Karşılaştığım Sorunlar ve Çözümler

**1. Docker container'ı PostgreSQL hazır olmadan başlıyordu**

API container'ı başladığında PostgreSQL henüz hazır değildi. Bağlantı hatası alıyordum. docker-compose.yml dosyasına healthcheck ekledim — API artık PostgreSQL "healthy" olmadan başlamıyor.

**2. JSON sonsuz döngü hatası**

Personel nesnesinin içinde Durum, Durum nesnesinin içinde tekrar Personel vardı. JSON'a çevirirken sonsuz döngüye giriyordu ve uygulama çöküyordu. ReferenceHandler.IgnoreCycles ekleyerek çözdüm.

**3. Kapı mantığı eksikti**

İlk versiyonda sistem sadece "son geçiş giriş miydi çıkış mıydı" diye bakıyordu. Bu yüzden aynı kapıdan iki kez geçince hata vermiyordu, sadece toggle yapıyordu. API'ye kapi parametresi ekleyerek A→A ve B→B durumlarında hata döndürdüm.

**4. wwwroot Docker'a kopyalanmıyordu**

HTML dosyaları çalışmıyordu çünkü Dockerfile'da wwwroot klasörünü kopyalamıyorduk. Dockerfile'a COPY wwwroot ./wwwroot satırını ekleyerek çözdüm. Ayrıca Program.cs'e app.UseStaticFiles() eklemek gerekti.

**5. docker-compose down -v ile veriler siliniyor**

Container'ı -v flag'i ile silince PostgreSQL volume'u da siliniyordu ve tüm veriler gidiyordu. Volume'u named volume olarak tanımlayarak bu sorunu çözdüm — artık container silinse bile veriler kalıyor.

**6. API ve veritabanı aynı anda başlatılınca çökme**

Docker Compose'da depends_on yeterliydi sandım ama değildi. API başlıyor, PostgreSQL henüz bağlantı kabul etmiyor, uygulama çöküyordu. healthcheck ile condition: service_healthy ekleyince düzeldi.

**7. TimeSpan serialization sorunu**

MesaiBaslangic alanını TimeSpan olarak tanımladım ama JSON'dan string geliyordu ve hata alıyordum. String olarak saklayıp TimeSpan.Parse ile çevirerek çözdüm.

**8. Patron panelindeki sekmeler çalışmıyordu**

JavaScript'teki sekmeAc fonksiyonunda panel id'leri HTML id'leriyle eşleşmiyordu. Null olan elementi style etmeye çalışınca hata veriyordu. Id'leri düzelterek ve null kontrolü ekleyerek çözdüm.

---

## 🔮 Gelecek Geliştirmeler

UTC ile Türkiye saati uyumu — Şu an UTC kullanılıyor, gece 00:00-03:00 arası yanlış güne kayıt düşebilir

Haftalık ve aylık raporlar

Patron şifresini veritabanında şifreli saklamak

Mobil uyumlu arayüz

Personel fotoğrafı ile giriş

---

## 👩‍💻 Geliştirici

Fatma Bulut

Elektrik-Elektronik Mühendisi — Eskişehir Osmangazi Üniversitesi (2025)

bulutffatma@gmail.com

LinkedIn: linkedin.com/in/fatma-bulut-2a3944254
