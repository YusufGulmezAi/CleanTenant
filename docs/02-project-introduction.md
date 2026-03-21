# 📋 CleanTenant — Proje Tanıtım Dokümanı

## Proje Vizyonu

CleanTenant, mali müşavirlik firmaları ve çok kiracılı (multi-tenant) işletmeler için geliştirilmiş, kurumsal düzeyde bir yazılım çatısıdır. Tek platform üzerinden sınırsız firma, şirket ve kullanıcıyı güvenli şekilde yönetmeyi hedefler.

## Problem Tanımı

Mali müşavirlik firmaları onlarca hatta yüzlerce şirkete hizmet verir. Her şirketin verileri birbirinden kesinlikle izole olmalı, farklı kullanıcılar farklı şirketlere farklı yetkilerle erişebilmeli, tüm işlemler denetlenebilir olmalıdır.

Mevcut çözümlerin eksikleri:
- Tek seviyeli tenant yapısı (firma → şirket hiyerarşisi yok)
- Yetki yönetimi yetersiz (IP/zaman bazlı erişim kontrolü yok)
- Denetim izi eksik (KVKK uyumsuzluğu)
- 2FA desteği sınırlı veya yok
- Teknoloji borcu yüksek

## Çözüm: CleanTenant

### 3 Katmanlı Hiyerarşi

```
Platform (CleanTenant)
  └── Tenant (Mali Müşavirlik Firması)
       ├── Company A (Müşteri Şirket)
       │    ├── CompanyAdmin (Muhasebeci)
       │    ├── CompanyUser (Çalışan)
       │    └── Member (Şirket ortağı)
       └── Company B (Başka Müşteri)
```

### Temel Özellikler

| Özellik | Açıklama |
|---------|----------|
| Hiyerarşik Multi-Tenancy | System → Tenant → Company → Member (7 seviye) |
| Clean Architecture | Domain → Application → Infrastructure → API → BlazorUI |
| 57 API Endpoint | Auth, Tenant, Company, User, Role, Session, Policy, Settings, IP Blacklist |
| Blazor Server UI | MudBlazor 9.1, kurumsal yeşil tema, dark/light, AdminLTE dashboard |
| İki Faktörlü Doğrulama | TOTP (Authenticator) + gerçek e-posta kodu (MailKit SMTP) |
| Erişim Politikası | IP whitelist (CIDR) + Gün/Saat kısıtlama, 3 seviye, açık kapı yok |
| E-posta Servisi | MailKit SMTP, CC/BCC, çoklu ek, Hangfire background, PostgreSQL tracking |
| Parametrik Ayarlar | 21 ayar, DB'den yönetilebilir, Company→Tenant→System→config fallback |
| Denetim İzi | AuditLog, SecurityLog, EmailLog (KVKK uyumlu, cross-level loglama) |
| Token Yönetimi | JWT + Refresh Token rotation + Device fingerprint + Redis dual storage |
| Redis Cache | Oturum, izin, ayar cache, IP blacklist, 2FA kodları |
| Docker Ready | 5 servis (PostgreSQL×2, Redis, pgAdmin, Seq) |

### Güvenlik Yaklaşımı

1. **Açık kapı yok** — Politika atanmamış kullanıcı giriş yapamaz
2. **Default politika** — Her seviyede silinemez "tümünü reddet" politikası
3. **Hiyerarşik yetki** — Alt seviye üst seviyeye müdahale edemez
4. **Cross-level loglama** — Üst seviyenin alt seviyeye müdahalesi detaylı loglanır
5. **Token rotation** — Her refresh'te eski token silinir, yenisi üretilir
6. **2FA zorunlu** — SuperAdmin varsayılan olarak 2FA ile girer
7. **Gerçek 2FA** — Hardcoded kod yok, MailKit ile gerçek e-posta gönderimi
8. **PBKDF2** — 100K iterasyon, 128-bit salt ile şifre hash'leme

### Hedef Kitle

- Mali müşavirlik firmaları
- Çok müşterili SaaS platformları
- Kurum içi multi-tenant uygulamalar
- KVKK uyumlu denetim gerektiren projeler

### Lisans ve Dağıtım

- **Lisans:** MIT (açık kaynak)
- **GitHub:** https://github.com/YusufGulmezAi/CleanTenant

### Teknoloji Gereksinimleri

| Gereksinim | Minimum |
|-----------|---------|
| .NET | 10.0 (Stable) |
| PostgreSQL | 17 |
| Redis | 7 |
| Docker | 24+ |
| Node.js | Gerekmiyor |

### Geliştirme Yol Haritası

| Faz | Durum | Açıklama |
|-----|-------|----------|
| 1. Domain + Infrastructure | ✅ | 18 entity, EF Core, Redis, Docker |
| 2. Güvenlik Modülü | ✅ | JWT, TOTP 2FA, Session, AccessPolicy |
| 3. CQRS + API | ✅ | 57 endpoint, 50 handler |
| 4. E-posta + Hangfire | ✅ | MailKit SMTP, gerçek 2FA kodu, background job |
| 5. Settings Modülü | ✅ | 21 ayar, hiyerarşik, UI'dan yönetim |
| 6. Access Policy v2 | ✅ | 3 katman, default politika, KVKK loglama |
| 7. Dosya Ayrıştırma | ✅ | 1 dosya = 1 sınıf convention |
| 8. Unit Tests | ✅ | 119/119 başarılı |
| 9. Blazor UI | ✅ | MudBlazor 9.1, Login+2FA, Dashboard, Tenant CRUD |
| 10. CRUD Sayfaları | 🔄 | Company, User, Role, Session, Settings sayfaları |
| 11. NuGet + CI/CD | 📋 | GitHub Actions, NuGet paket yayını |
