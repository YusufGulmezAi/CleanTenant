# 🏗️ CleanTenant

**Hiyerarşik Multi-Tenant Enterprise Framework**

.NET 10 · PostgreSQL 17 · Redis 7 · MudBlazor 9.1 · MailKit · Hangfire · Docker

---

## Nedir?

CleanTenant, Clean Architecture ile tasarlanmış, 3 katmanlı hiyerarşik multi-tenant framework'üdür. Mali müşavirlik firmaları ve çok kiracılı SaaS platformları için idealdir.

## Özellikler

- **Blazor Server UI** — MudBlazor 9.1, kurumsal yeşil tema, dark/light toggle, AdminLTE tarzı dashboard
- **57 API Endpoint** — Auth, Tenant, Company, User, Role, Session, Access Policy, Settings, IP Blacklist
- **3 Katmanlı Hiyerarşi** — System → Tenant → Company → Member (7 seviyeli kullanıcı)
- **İki Faktörlü Doğrulama** — Google/Microsoft Authenticator (TOTP) + gerçek e-posta kodu
- **Erişim Politikası** — IP whitelist (CIDR) + Gün/Saat kısıtlama, 3 seviyeli default, açık kapı yok
- **E-posta Servisi** — MailKit SMTP (Gmail/Outlook), CC/BCC, çoklu ek, Hangfire background, PostgreSQL tracking
- **Parametrik Ayarlar** — 21 ayar, DB'den yönetilebilir, Company→Tenant→System→appsettings.json fallback
- **Token Yönetimi** — JWT Access (15dk) + Refresh Token rotation (7gün), device fingerprint
- **KVKK Uyumlu Denetim** — AuditLog, SecurityLog, EmailLog, cross-level loglama
- **119 Unit Test** — Domain, Application, Behavior testleri (tümü başarılı)
- **Docker Ready** — 5 servis (PostgreSQL×2, Redis, pgAdmin, Seq)

## Ekran Görüntüsü

```
┌──────────────────────────────────────────────────────────────────┐
│ 🏗️ CleanTenant          [🔍 Ara...]        🔔  🌙  👤          │
├────────────┬─────────────────────────────────────────────────────┤
│ Dashboard  │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ │
│            │  │Tenant: 3│ │Şirket:12│ │User: 47 │ │Oturum: 8│ │
│ ▸ Yönetim  │  └─────────┘ └─────────┘ └─────────┘ └─────────┘ │
│   Tenants  │                                                     │
│   Şirketler│  Son İşlemler              Hızlı Erişim            │
│   Kullanıcı│  ─────────────             ──────────────          │
│   Roller   │  14:32 admin  Login(2FA)   [+ Kullanıcı Ekle]     │
│            │  14:28 ahmet  Tenant oluş  [+ Tenant Oluştur]     │
│ ▸ Güvenlik │  14:15 IP     Başarısız    [🔒 Erişim Politikası] │
│   Oturumlar│  13:55 admin  Policy güncl [⚙ Sistem Ayarları]    │
│   Politika │                                                     │
│   IP Black │  Sistem Bilgisi                                    │
│            │  API ●  Redis ●  PostgreSQL ●                      │
│ ▸ Sistem   │                                                     │
│   Ayarlar  │                                                     │
│   E-posta  │                                                     │
└────────────┴─────────────────────────────────────────────────────┘
```

## Hızlı Başlangıç

```bash
git clone https://github.com/YusufGulmezAi/CleanTenant.git
cd CleanTenant
cp .env.example .env

# Docker servislerini başlat
cd docker && docker-compose --env-file ../.env up -d && cd ..

# Migration + Build
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext
dotnet build

# API çalıştır (Terminal 1)
dotnet run --project src/CleanTenant.API

# Blazor UI çalıştır (Terminal 2)
dotnet run --project src/CleanTenant.BlazorUI
```

İlk giriş: `admin@cleantenant.com` / `Admin123!` (2FA e-posta kodu ile)

Detaylı kurulum: [docs/getting-started.md](docs/getting-started.md)

## Dokümanlar

| Doküman | Açıklama |
|---------|----------|
| [Teknik Mimari](docs/01-technical-architecture.md) | Teknoloji yığını, proje yapısı, güvenlik, API detayları |
| [Proje Tanıtımı](docs/02-project-introduction.md) | Vizyon, özellikler, hedef kitle, yol haritası |
| [Akış Diyagramları](docs/03-flow-diagrams.md) | Login, 2FA, Token, Access Policy, Email, Settings akışları |
| [Başlangıç Kılavuzu](docs/getting-started.md) | Kurulum, migration, SMTP ayarları, ilk test |

## Lisans

MIT — [LICENSE](LICENSE)
