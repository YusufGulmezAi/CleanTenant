# 📋 CleanTenant — Proje Tanıtım Dokümanı

**Hedef Kitle:** Proje Yöneticileri, İş Analistleri, Karar Vericiler
**Son Güncelleme:** Mart 2026

---

## 1. Yönetici Özeti

CleanTenant, mali müşavirlik firmaları ve çok kiracılı (multi-tenant) SaaS platformları için geliştirilmiş bir kurumsal yazılım çatısıdır. Tek platform üzerinden sınırsız firma, şirket ve kullanıcıyı güvenli şekilde yönetir.

Projenin temel farkı **3 katmanlı hiyerarşi** yapısıdır: Platform (System) → Firma (Tenant) → Şirket (Company) → Kişi (Member). Bu yapı sayesinde bir mali müşavirlik firması, tüm müşteri şirketlerini tek platformda yönetirken, her şirketin verisi birbirinden tamamen izole kalır.

---

## 2. Problem Tanımı

### 2.1 Mevcut Durum

Mali müşavirlik firmaları tipik olarak 30-200 şirkete hizmet verir. Her şirket için:

- Farklı kullanıcılar farklı rollere sahiptir (muhasebeci, şirket müdürü, ortak)
- Veriler kesinlikle birbirinden izole olmalıdır (yasal zorunluluk)
- Erişim saatlerine ve IP adreslerine göre kısıtlama gerekebilir (güvenlik politikası)
- Tüm işlemler denetlenebilir olmalıdır (KVKK/GDPR uyumu)
- İki faktörlü doğrulama zorunlu veya önerilen olmalıdır

### 2.2 Mevcut Çözümlerin Eksikleri

| Problem | Açıklama |
|---------|----------|
| Tek seviyeli tenant | Firma → Şirket hiyerarşisi desteklenmiyor |
| Yetersiz yetki yönetimi | IP/zaman bazlı erişim kontrolü yok |
| Eksik denetim izi | KVKK uyumsuzluğu riski |
| Sınırlı 2FA | E-posta/SMS/Authenticator aynı anda desteklenmiyor |
| Teknoloji borcu | Eski framework, güncellenmesi maliyetli |

---

## 3. Çözüm: CleanTenant

### 3.1 Temel Özellikler

| Kategori | Özellik | Detay |
|----------|---------|-------|
| **Hiyerarşi** | 3 katmanlı multi-tenancy | System → Tenant → Company → Member |
| **Kullanıcı** | 7 seviyeli yetki sistemi | SuperAdmin'den CompanyMember'a kadar |
| **2FA** | Çoklu yöntem desteği | E-posta + SMS + Authenticator (aynı anda aktif) |
| **QR Kod** | API'de üretim | QRCoder ile PNG byte[], harici API bağımlılığı yok |
| **Erişim** | IP + Zaman politikası | CIDR desteği, gün/saat kısıtlama, 3 seviyeli default |
| **E-posta** | MailKit SMTP | Gmail/Outlook, CC/BCC, ek, Hangfire arka plan |
| **Ayarlar** | Parametrik yönetim | 21 ayar, UI'dan değiştirilebilir, hiyerarşik override |
| **Denetim** | KVKK uyumlu | AuditLog, SecurityLog, EmailLog, cross-level loglama |
| **UI** | Blazor Server | MudBlazor 9.1, kurumsal yeşil tema, dark/light toggle |
| **API** | 57 endpoint | Minimal API, Scalar dokümantasyon |
| **Test** | 119 unit test | Domain, Application, Behavior katmanları |
| **Altyapı** | Docker Ready | 5 servis, tek komutla ayağa kalkar |

### 3.2 Güvenlik Yaklaşımı (8 Prensip)

1. **Açık kapı asla olmaz** — Politika atanmamış kullanıcı giriş yapamaz
2. **Default politika silinemez** — Her seviyede "tümünü reddet" politikası bulunur
3. **Hiyerarşik yetki** — Alt seviye üst seviyeye müdahale edemez
4. **Cross-level loglama** — Üst seviye müdahalesi detaylı loglanır
5. **Token rotation** — Her refresh'te eski token silinir, yenisi üretilir
6. **Çoklu 2FA** — E-posta, SMS ve Authenticator aynı anda aktif olabilir
7. **Gerçek 2FA** — Hardcoded kod yok, MailKit/TOTP ile gerçek doğrulama
8. **PBKDF2** — 100.000 iterasyon, 128-bit salt ile endüstri standardı hash

### 3.3 Hedef Kitle

| Segment | Kullanım Senaryosu |
|---------|-------------------|
| Mali Müşavirlik Firmaları | Müşteri şirketlerini tek platformda yönetme |
| Çok Müşterili SaaS Platformları | Tenant izolasyonu, yetki hiyerarşisi |
| Kurum İçi Uygulamalar | Departman/şube bazlı erişim kontrolü |
| KVKK Uyumlu Projeler | Denetim izi, veri izolasyonu, erişim loglaması |

---

## 4. Teknik Gereksinimler

| Gereksinim | Minimum | Önerilen |
|-----------|---------|----------|
| .NET SDK | 10.0 | 10.0 (Stable) |
| PostgreSQL | 16 | 17 |
| Redis | 6 | 7 |
| Docker | 24 | En güncel |
| RAM | 4 GB | 8 GB |
| Disk | 5 GB | 20 GB |
| İşletim Sistemi | Windows 10+ / Ubuntu 22+ / macOS 13+ | — |

---

## 5. Geliştirme Yol Haritası

### 5.1 Tamamlanan Fazlar

| Faz | Süre | Durum | Açıklama |
|-----|------|-------|----------|
| 1. Domain Katmanı | — | ✅ Tamamlandı | 18 entity, 8 enum, base class'lar, domain event'ler |
| 2. Infrastructure Katmanı | — | ✅ Tamamlandı | EF Core, Redis, JWT, Docker (5 servis) |
| 3. Güvenlik Modülü | — | ✅ Tamamlandı | PBKDF2, JWT token, session, device fingerprint |
| 4. CQRS + API | — | ✅ Tamamlandı | 57 endpoint, 54 handler, 4 behavior, 5 middleware |
| 5. E-posta + Hangfire | — | ✅ Tamamlandı | MailKit SMTP, Hangfire arka plan, PostgreSQL tracking |
| 6. Ayar Modülü (Settings) | — | ✅ Tamamlandı | 21 ayar, hiyerarşik okuma, Redis cache, UI yönetimi |
| 7. Erişim Politikası v2 | — | ✅ Tamamlandı | 3 katman, default politika, KVKK loglama |
| 8. Unit Test | — | ✅ Tamamlandı | 119/119 başarılı (Domain, Application, Behavior) |
| 9. Blazor UI Temel Yapı | — | ✅ Tamamlandı | MudBlazor 9.1, Login+2FA, Dashboard, Tenant CRUD |
| 10. Çoklu 2FA + QRCoder | — | ✅ Tamamlandı | E-posta + SMS + Authenticator, QR PNG, Recovery kodları |
| 11. Dosya Ayrıştırma | — | ✅ Tamamlandı | 1 dosya = 1 sınıf convention (kısmen) |

### 5.2 Planlanan Fazlar

| Faz | Öncelik | Açıklama |
|-----|---------|----------|
| 12. CRUD Sayfaları | Yüksek | Company, User, Role, Session, Settings, AccessPolicy, IpBlacklist |
| 13. IP Blacklist Endpoint | Yüksek | API endpoint'leri (list, add, remove, check) |
| 14. Real SMS Entegrasyonu | Orta | ISmsProvider implementasyonu (Twilio/Netgsm) |
| 15. Şifre Sıfırlama | Orta | E-posta ile link, token-based reset akışı |
| 16. Audit Log Sayfası | Orta | Blazor UI'da SecurityLog/AuditLog görüntüleme |
| 17. NuGet Paket Yayını | Düşük | CleanTenant.Domain, Shared NuGet paketi |
| 18. CI/CD Pipeline | Düşük | GitHub Actions: build, test, Docker push |
| 19. Backup Modülü | Düşük | Şirket bazlı yedekleme/geri yükleme |

---

## 6. Risk Analizi

| Risk | Olasılık | Etki | Azaltma Stratejisi |
|------|----------|------|---------------------|
| SMS sağlayıcı entegrasyonu gecikmesi | Orta | Düşük | ISmsProvider interface mevcut, geçici olarak dev kodu ile çalışır |
| EF Core versiyon çakışması | Düşük | Orta | Directory.Build.props'ta versiyon sabitleme zaten yapıldı |
| MudBlazor breaking change | Düşük | Orta | 9.1'e migration tamamlandı, sabitlendi |
| Redis bağlantı kopması | Düşük | Yüksek | Fallback: DB'den oturum doğrulama, graceful degradation |
| SMTP sağlayıcı blokajı | Orta | Orta | Çoklu sağlayıcı desteği (Gmail + Outlook), Hangfire retry |

---

## 7. Lisans ve İletişim

- **Lisans:** MIT (açık kaynak, ticari kullanım serbest)
- **Kaynak Kodu:** https://github.com/YusufGulmezAi/CleanTenant
- **Geliştirici:** Yusuf Gülmez
