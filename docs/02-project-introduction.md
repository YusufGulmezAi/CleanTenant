# 📋 CleanTenant — Proje Tanıtım Dokümanı

## Proje Vizyonu

CleanTenant, mali müşavirlik firmaları ve çok kiracılı (multi-tenant) işletmeler için geliştirilmiş, kurumsal düzeyde bir yazılım çatısıdır. Tek platform üzerinden sınırsız firma, şirket ve kullanıcıyı güvenli şekilde yönetmeyi hedefler.

## Problem Tanımı

Mali müşavirlik firmaları onlarca hatta yüzlerce şirkete hizmet verir. Her şirketin verileri birbirinden kesinlikle izole olmalı, farklı kullanıcılar farklı şirketlere farklı yetkilerle erişebilmeli, tüm işlemler denetlenebilir olmalıdır.

Mevcut çözümlerin eksikleri:
- Tek seviyeli tenant yapısı (firma → şirket hiyerarşisi yok)
- Yetki yönetimi yetersiz (IP/zaman bazlı erişim kontrolü yok)
- Denetim izi eksik (KVKK uyumsuzluğu)
- Teknoloji borcu yüksek (eski framework'ler)

## Çözüm: CleanTenant

### 3 Katmanlı Hiyerarşi

```
Platform (CleanTenant)
  └── Tenant (Mali Müşavirlik Firması)
       ├── Company A (Müşteri Şirket)
       │    ├── CompanyAdmin (Muhasebeci)
       │    ├── CompanyUser (Çalışan)
       │    └── Member (Şirket ortağı — sınırlı erişim)
       │
       └── Company B (Başka Müşteri)
            └── ...
```

### Temel Özellikler

| Özellik | Açıklama |
|---------|----------|
| Hiyerarşik Multi-Tenancy | System → Tenant → Company → Member |
| Clean Architecture | Domain → Application → Infrastructure → API |
| 53 API Endpoint | Auth, Tenant, Company, User, Role, Session, Policy, Settings |
| İki Faktörlü Doğrulama | TOTP (Google/Microsoft Authenticator) + E-posta |
| Erişim Politikası | IP whitelist + CIDR + Gün/Saat kısıtlama (3 seviye) |
| E-posta Servisi | MailKit SMTP, CC/BCC, ek dosya, Hangfire background |
| Parametrik Ayarlar | 21 ayar, DB'den yönetilebilir, tenant bazlı override |
| Denetim İzi | AuditLog, SecurityLog, EmailLog (KVKK uyumlu) |
| Token Yönetimi | JWT + Refresh Token rotation + Device fingerprint |
| Redis Cache | Oturum, izin, ayar cache, IP blacklist |
| Docker Ready | 5 servis, dev + production compose |

### Güvenlik Yaklaşımı

1. **Açık kapı yok** — Politika atanmamış kullanıcı giriş yapamaz
2. **Default politika** — Her seviyede silinemez "tümünü reddet" politikası
3. **Hiyerarşik yetki** — Alt seviye üst seviyeye müdahale edemez
4. **Cross-level loglama** — Üst seviyenin alt seviyeye müdahalesi detaylı loglanır
5. **Token rotation** — Her refresh'te eski token silinir, yenisi üretilir
6. **2FA zorunlu** — SuperAdmin varsayılan olarak 2FA ile girer

### Hedef Kitle

- Mali müşavirlik firmaları
- Çok müşterili SaaS platformları
- Kurum içi multi-tenant uygulamalar
- KVKK uyumlu denetim gerektiren projeler

### Lisans ve Dağıtım

- **Lisans:** MIT (açık kaynak)
- **GitHub:** https://github.com/YusufGulmezAi/CleanTenant
- **NuGet:** Planlanan (paket olarak yayın)

### Teknoloji Gereksinimleri

| Gereksinim | Minimum |
|-----------|---------|
| .NET | 10.0 |
| PostgreSQL | 17 |
| Redis | 7 |
| Docker | 24+ |
| Node.js | Gerekmiyor |

### Geliştirme Yol Haritası

| Faz | Durum | Açıklama |
|-----|-------|----------|
| 1. Domain + Infrastructure | ✅ Tamamlandı | Entity'ler, EF Core, Redis, Docker |
| 2. Güvenlik Modülü | ✅ Tamamlandı | JWT, 2FA, Session, AccessPolicy |
| 3. CQRS + API | ✅ Tamamlandı | 53 endpoint, 50 handler |
| 4. E-posta + Hangfire | ✅ Tamamlandı | MailKit, background job, tracking |
| 5. Settings Modülü | ✅ Tamamlandı | 21 ayar, hiyerarşik, UI'dan yönetim |
| 6. Access Policy v2 | ✅ Tamamlandı | 3 katman, default, KVKK loglama |
| 7. Unit Tests | ✅ 99/99 başarılı | Domain, Application, Behavior testleri |
| 8. Blazor UI (MudBlazor) | 🔄 Başlıyor | Login, Dashboard, CRUD sayfaları |
| 9. NuGet + CI/CD | 📋 Planlanıyor | GitHub Actions, NuGet paket yayını |
