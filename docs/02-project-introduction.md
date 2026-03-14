# 📋 CleanTenant — İdari Proje Tanıtım Dokümanı

## 1. Proje Özeti

**Proje Adı:** CleanTenant
**Tür:** Açık kaynak enterprise framework
**Hedef:** Hiyerarşik multi-tenant uygulamalar için hazır altyapı
**Lisans:** MIT (ücretsiz, ticari kullanıma açık)
**Platform:** NuGet paket + GitHub açık kaynak

---

## 2. Problem Tanımı

### Mevcut Durum

Enterprise uygulamalarda (özellikle mali müşavirlik, muhasebe, ERP) her firma kendi müşteri şirketlerini yönetir. Bu yapıda:

- Firma (Tenant) → Birden fazla müşteri şirketi yönetir
- Her şirketin kendi kullanıcıları, rolleri ve verileri vardır
- Bir kullanıcı birden fazla firmada/şirkette çalışabilir
- Güvenlik gereksinimleri çok yüksektir (KVKK, yasal uyumluluk)

### Piyasadaki Boşluk

- Mevcut çözümler genellikle tek katmanlı tenant yapısı sunar
- 3 katmanlı hiyerarşi (System → Tenant → Company) nadir bulunur
- Çapraz kullanıcı kimliği (bir kullanıcı birden fazla yerde) desteklenmez
- Enterprise seviye güvenlik (device fingerprint, anlık bloke) yoktur

---

## 3. CleanTenant'ın Sunduğu Çözüm

### Hiyerarşik Yapı

```
Platform
 └── Firma (Tenant)      → Mali müşavirlik ofisi
      └── Şirket (Company) → Müşteri şirketi
           └── Üye (Member) → Sınırlı erişimli kullanıcı
```

### Temel Özellikler

| Özellik | Açıklama |
|---------|----------|
| **3 Katmanlı Hiyerarşi** | System → Tenant → Company → Member |
| **Çapraz Kimlik** | Tek kullanıcı, birden fazla firma/şirkette |
| **7 Seviye Yetki** | SuperAdmin'den Member'a kadar |
| **2FA + TempToken** | SMS, E-posta, Authenticator + Fallback |
| **Anlık Kullanıcı İzleme** | Bloke, force logout, oturum takibi |
| **Cihaz Doğrulama** | Token çalınma koruması |
| **IP/Zaman Kısıtlaması** | Kullanıcı bazlı erişim politikası |
| **Kapsamlı Audit Trail** | Kim, ne zaman, nereden, ne değiştirdi |
| **Şirket Bazlı Yedekleme** | Filtered backup, background job |
| **Parametrik Güvenlik** | Tüm ayarlar appsettings.json'dan |

---

## 4. Hedef Kitle

### Birincil Hedef

- **Mali müşavirlik firmaları** — Müşteri şirketlerinin muhasebesini yürütür
- **Muhasebe yazılım firmaları** — SaaS muhasebe platformu geliştirir
- **ERP geliştiricileri** — Multi-tenant ERP altyapısı arar

### İkincil Hedef

- **.NET geliştiricileri** — Clean Architecture öğrenmek ister
- **Startup'lar** — Hızlı enterprise altyapı kurmak ister
- **Eğitimciler** — Profesyonel mimari öğretmek için referans arar

---

## 5. Rekabet Analizi

| Özellik | CleanTenant | Finbuckle | ABP Framework | Boilerplate'ler |
|---------|:-----------:|:---------:|:-------------:|:---------------:|
| 3 Katmanlı Hiyerarşi | ✅ | ❌ | ❌ | ❌ |
| Çapraz Kullanıcı | ✅ | ❌ | ❌ | ❌ |
| 2FA + TempToken | ✅ | ❌ | ✅ | ❌ |
| Device Fingerprint | ✅ | ❌ | ❌ | ❌ |
| Anlık Bloke/Logout | ✅ | ❌ | Kısmen | ❌ |
| IP/Zaman Kısıtlama | ✅ | ❌ | ❌ | ❌ |
| Custom Identity | ✅ | ❌ | ❌ | ❌ |
| Audit Trail (JSONB) | ✅ | ❌ | ✅ | Kısmen |
| Ücretsiz (MIT) | ✅ | ✅ | Kısmen | ✅ |
| .NET 10 | ✅ | ❌ | ❌ | Değişir |

---

## 6. Proje Zaman Çizelgesi

| Faz | Süre | Durum | İçerik |
|-----|------|-------|--------|
| Faz 1 | 2 hafta | ✅ Tamamlandı | Solution yapısı, Domain entity'ler, Docker |
| Faz 2 | 2 hafta | ✅ Tamamlandı | EF Core, Interceptor'lar, Mappings, Rules |
| Faz 3 | 2 hafta | ✅ Tamamlandı | Güvenlik modülü, Redis, Middleware pipeline |
| Faz 4 | 2 hafta | ✅ Tamamlandı | CQRS Handlers, Minimal API, Auth (35 endpoint) |
| Faz 5 | 2 hafta | ✅ Tamamlandı | Unit test'ler (94 test case) |
| Faz 6 | Planlı | 📋 Planlanıyor | MudBlazor UI |
| Faz 7 | Planlı | 📋 Planlanıyor | NuGet paket yayını, CI/CD |

---

## 7. İş Modeli

### Açık Kaynak (MIT Lisansı)

- Framework tamamen ücretsiz
- Ticari kullanıma açık
- Fork edilebilir, değiştirilebilir

### Gelir Potansiyeli (Opsiyonel)

- Premium destek paketi
- Danışmanlık hizmeti
- Eğitim videoları / kurslar
- Özel modül geliştirme

---

## 8. Risk Analizi

| Risk | Olasılık | Etki | Çözüm |
|------|----------|------|-------|
| .NET 10 breaking change | Düşük | Orta | Preview takip, erken adaptasyon |
| NuGet paket uyumsuzluk | Orta | Düşük | Merkezi versiyon yönetimi (Directory.Build.props) |
| Güvenlik açığı | Düşük | Yüksek | Penetrasyon testi, OWASP kontrol listesi |
| Topluluk ilgisizliği | Orta | Orta | Detaylı dokümantasyon, eğitici kod yorumları |
