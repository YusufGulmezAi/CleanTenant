# 🏗️ CleanTenant

**Hiyerarşik Multi-Tenant Enterprise Framework**

.NET 10 · PostgreSQL 17 · Redis 7 · MudBlazor · Docker

---

## Nedir?

CleanTenant, Clean Architecture ile tasarlanmış, 3 katmanlı hiyerarşik multi-tenant framework'üdür. Mali müşavirlik firmaları ve çok kiracılı SaaS platformları için idealdir.

## Özellikler

- **53 API Endpoint** — Auth, Tenant, Company, User, Role, Session, Policy, Settings
- **3 Katmanlı Hiyerarşi** — System → Tenant → Company → Member
- **İki Faktörlü Doğrulama** — Google/Microsoft Authenticator (TOTP) + E-posta
- **Erişim Politikası** — IP whitelist (CIDR) + Gün/Saat kısıtlama, 3 seviyeli default
- **E-posta Servisi** — MailKit SMTP, CC/BCC, ekler, Hangfire arka plan, PostgreSQL tracking
- **Parametrik Ayarlar** — 21 ayar, DB'den yönetilebilir, tenant/company override
- **Token Rotation** — JWT Access + Refresh, device fingerprint, Redis + DB dual storage
- **KVKK Uyumlu Denetim** — AuditLog, SecurityLog, EmailLog, cross-level loglama
- **99 Unit Test** — Domain, Application, Behavior testleri (tümü başarılı)
- **Docker Ready** — 5 servis (PostgreSQL×2, Redis, pgAdmin, Seq)

## Hızlı Başlangıç

```bash
git clone https://github.com/YusufGulmezAi/CleanTenant.git
cd CleanTenant
cp .env.example .env

cd docker && docker-compose --env-file ../.env up -d && cd ..
dotnet build && dotnet run --project src/CleanTenant.API
```

Detaylı kurulum: [docs/getting-started.md](docs/getting-started.md)

## Dokümanlar

| Doküman | Açıklama |
|---------|----------|
| [Teknik Mimari](docs/01-technical-architecture.md) | Teknoloji, yapı, güvenlik detayları |
| [Proje Tanıtımı](docs/02-project-introduction.md) | Vizyon, özellikler, yol haritası |
| [Akış Diyagramları](docs/03-flow-diagrams.md) | Login, Token, Policy, Email akışları |
| [Başlangıç Kılavuzu](docs/getting-started.md) | Kurulum ve ilk çalıştırma |

## Lisans

MIT — [LICENSE](LICENSE)
