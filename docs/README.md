# 🏗️ CleanTenant

**Hiyerarşik Multi-Tenant Enterprise Framework**

.NET 10 · PostgreSQL 17 · Redis 7 · MudBlazor 9.1 · MailKit · QRCoder · Hangfire · Docker

---

## Nedir?

CleanTenant, Clean Architecture prensibiyle tasarlanmış, 3 katmanlı hiyerarşik multi-tenant yönetim çatısıdır.
Mali müşavirlik firmaları, çok kiracılı SaaS platformları ve kurumsal uygulamalar için tasarlanmıştır.

## Öne Çıkan Özellikler

- **57 API Endpoint** — 8 grup (Auth, Tenant, Company, User, Role, Session, Access Policy, Settings)
- **Çoklu 2FA** — E-posta + SMS + Google/Microsoft Authenticator (aynı anda aktif, primary seçimi)
- **QR Kod** — API'de QRCoder ile PNG byte[] üretimi (harici API bağımlılığı yok)
- **Kurtarma Kodları** — Authenticator kurulumunda tek seferlik XXXX-XXXX-XXXX kodları
- **Blazor Server UI** — MudBlazor 9.1, kurumsal yeşil tema, dark/light toggle
- **3 Katmanlı Hiyerarşi** — System → Tenant → Company → Member (7 seviyeli kullanıcı)
- **Erişim Politikası** — IP whitelist (CIDR) + Gün/Saat kısıtlama, açık kapı yok
- **E-posta** — MailKit SMTP, CC/BCC, çoklu ek, Hangfire background, PostgreSQL tracking
- **Parametrik Ayarlar** — 21 ayar, DB'den yönetilebilir, Company→Tenant→System fallback
- **119 Unit Test** — Domain, Application, Behavior (tümü başarılı)
- **Docker Ready** — 5 servis (PostgreSQL×2, Redis, pgAdmin, Seq)

## Hızlı Başlangıç

```bash
git clone https://github.com/YusufGulmezAi/CleanTenant.git
cd CleanTenant && cp .env.example .env

# Docker başlat
cd docker && docker-compose --env-file ../.env up -d && cd ..

# Migration + Build + Çalıştır
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext
dotnet build

# Terminal 1: API    →  Terminal 2: Blazor UI
dotnet run --project src/CleanTenant.API
dotnet run --project src/CleanTenant.BlazorUI
```

İlk giriş: `admin@cleantenant.com` / `Admin123!` (2FA e-posta kodu)

## Dokümanlar

| Doküman | Hedef Kitle | İçerik |
|---------|-------------|--------|
| [Teknik Mimari](docs/01-technical-architecture.md) | Sistem Mühendisi | Katman yapısı, bileşen envanteri, güvenlik, API referansı |
| [Proje Tanıtımı](docs/02-project-introduction.md) | Proje Yöneticisi | Vizyon, özellikler, yol haritası, risk analizi |
| [Akış Diyagramları](docs/03-flow-diagrams.md) | Tüm Ekip | Login, 2FA, Token, Access Policy, Email, Settings akışları |
| [Başlangıç Kılavuzu](docs/getting-started.md) | Geliştirici | Kurulum, migration, SMTP, sorun giderme |

## Lisans

MIT — [LICENSE](LICENSE)
