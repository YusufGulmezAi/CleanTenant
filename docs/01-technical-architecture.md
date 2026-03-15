# 🏛️ CleanTenant — Teknik Mimari Dokümanı

## 1. Genel Bakış

CleanTenant, .NET 10 üzerinde Clean Architecture ile tasarlanmış, hiyerarşik multi-tenant enterprise framework'üdür. Tek kod tabanıyla sınırsız sayıda tenant (mali müşavirlik firması), şirket ve kullanıcıyı güvenli şekilde yönetir.

### Teknoloji Yığını

| Katman | Teknoloji | Amaç |
|--------|-----------|------|
| Runtime | .NET 10 | LTS, Minimal API, performans |
| Veritabanı | PostgreSQL 17 | JSONB, text[], CIDR desteği |
| Cache | Redis 7 | Oturum, izin cache, IP blacklist |
| ORM | EF Core 10 | Code-first, interceptor, query filter |
| CQRS | MediatR | Pipeline behaviors, command/query ayrımı |
| Validation | FluentValidation | İş kuralı doğrulama |
| E-posta | MailKit | SMTP, CC/BCC, çoklu ek, HTML template |
| Background Jobs | Hangfire + PostgreSQL | E-posta kuyruğu, retry, dashboard |
| Loglama | Serilog + Seq | Yapısal loglama, KVKK audit |
| UI | MudBlazor | .NET native component kütüphanesi |
| Container | Docker Compose | Development + Production |

### Sayısal Özet

| Metrik | Değer |
|--------|-------|
| Toplam dosya | 130+ |
| API Endpoint | 53 |
| CQRS Handler | 50 |
| Domain Entity | 18 |
| Pipeline Behavior | 5 |
| Middleware | 5 |
| Unit Test | 99 (tümü başarılı) |
| Docker Servis | 5 |
| Varsayılan Ayar | 21 |

## 2. Proje Yapısı (Clean Architecture)

```
CleanTenant/
├── src/
│   ├── CleanTenant.Domain/          → Entity, Enum, ValueObject (0 bağımlılık)
│   │   ├── Common/                  → BaseEntity, ISoftDeletable, IDomainEvent
│   │   ├── Identity/                → ApplicationUser, Roles (System/Tenant/Company)
│   │   ├── Tenancy/                 → Tenant, Company
│   │   ├── Security/                → SecurityEntities, AccessPolicy, UserPolicyAssignment
│   │   ├── Settings/                → SystemSetting (hiyerarşik key-value)
│   │   ├── Email/                   → EmailLog (PostgreSQL tracking)
│   │   └── Enums/                   → UserLevel, TwoFactorMethod, SecurityEnums
│   │
│   ├── CleanTenant.Application/     → CQRS, Behaviors, Rules, Mappings
│   │   ├── Common/
│   │   │   ├── Behaviors/           → Validation, Caching, Logging, Authorization
│   │   │   ├── Interfaces/          → IApplicationDbContext, ICacheService, IEmailService, ISettingsService
│   │   │   ├── Rules/               → Authorization, Tenant, Company, User rules
│   │   │   ├── Mappings/            → Entity → DTO (extension methods)
│   │   │   └── Models/              → Result<T> (Railway pattern)
│   │   └── Features/
│   │       ├── Auth/                → Login, 2FA, Refresh, Password, Email Verification
│   │       ├── Tenants/             → CRUD
│   │       ├── Companies/           → CRUD (tenant-scoped)
│   │       ├── Users/               → CRUD + Block + Force Logout
│   │       ├── Roles/               → Tenant/Company Role CRUD + Assign
│   │       ├── Sessions/            → List, Revoke
│   │       ├── AccessPolicies/      → CRUD + Assign/Unassign + User Policy
│   │       └── Settings/            → List, Get, Upsert, Delete + DefaultSeeder
│   │
│   ├── CleanTenant.Shared/          → DTOs, Constants, Helpers (API ↔ UI ortak)
│   │   ├── DTOs/                    → Auth, Tenant, Company, User DTOs
│   │   ├── Constants/               → SystemRoles, Permissions, UserLevels
│   │   └── Helpers/                 → SecurityHelper (PBKDF2, TOTP, Base32), DateTimeHelper
│   │
│   ├── CleanTenant.Infrastructure/  → EF Core, Redis, JWT, SMTP, Hangfire
│   │   ├── Persistence/             → DbContext, Interceptors, Seeds, Configurations
│   │   ├── Caching/                 → RedisCacheService
│   │   ├── Security/                → TokenService, SessionManager, CurrentUser, AccessPolicy
│   │   ├── Email/                   → SmtpEmailService, EmailBackgroundJob
│   │   └── Settings/                → SettingsService (DB → appsettings.json fallback)
│   │
│   ├── CleanTenant.API/             → Minimal API Endpoints, Middleware
│   │   ├── Endpoints/               → 8 endpoint grubu (53 endpoint)
│   │   ├── Middleware/              → Exception, Logging, IpBlacklist, RateLimit, Session
│   │   └── Extensions/              → Result → IResult, Middleware pipeline
│   │
│   └── CleanTenant.BlazorUI/        → MudBlazor (geliştirme aşamasında)
│
├── tests/                           → 99 unit test
│   ├── CleanTenant.Domain.Tests/
│   ├── CleanTenant.Application.Tests/
│   ├── CleanTenant.Infrastructure.Tests/
│   └── CleanTenant.API.IntegrationTests/
│
├── docker/                          → Docker Compose (dev + prod)
└── docs/                            → Teknik, idari, akış dokümanları
```

## 3. Hiyerarşik Multi-Tenancy Modeli

```
System (Platform)
  └── Tenant (Mali Müşavirlik Firması)
       └── Company (Şirket/Müşteri)
            └── Member (Çalışan/Kişi)
```

### Kullanıcı Seviyeleri (Numeric — karşılaştırma için)

| Seviye | Puan | Açıklama |
|--------|------|----------|
| SuperAdmin | 100 | Platform sahibi, sınırsız yetki |
| SystemUser | 80 | Platform operatör |
| TenantAdmin | 60 | Firma yöneticisi |
| TenantUser | 40 | Firma çalışanı |
| CompanyAdmin | 20 | Şirket yöneticisi |
| CompanyUser | 10 | Şirket çalışanı |
| CompanyMember | 5 | Şirket üyesi (sınırlı erişim) |

Kural: `currentLevel > targetLevel` gerekli (alt seviye üst seviyeye müdahale edemez).

## 4. Güvenlik Mimarisi

### 4.1 Kimlik Doğrulama (Custom — Microsoft.Identity kullanılmıyor)

| Bileşen | Teknoloji |
|---------|-----------|
| Şifre Hash | PBKDF2 (SHA-256, 100K iterasyon, 128-bit salt) |
| Token | JWT (HS256, kısa ömürlü Access + uzun ömürlü Refresh) |
| 2FA | TOTP (RFC 6238, Google/Microsoft Authenticator) + E-posta |
| Oturum | Redis + DB dual storage, device fingerprint |

### 4.2 Token Akışı

- Access Token: 15dk (parametrik — DB Settings'ten yönetilebilir)
- Refresh Token: 7 gün (rotation — her kullanımda yenisi üretilir)
- TempToken: 5dk (2FA doğrulama öncesi geçici token)

### 4.3 Erişim Politikası (3 Katmanlı)

```
System Default  → Tüm IP/Zaman reddet (silinemez)
Tenant Default  → Tenant oluşturulunca otomatik
Company Default → Company oluşturulunca otomatik

Kurallar:
• Default politika silinemez
• Özel politika silinince kullanıcılar default'a düşer
• Politika YOKSA → giriş YASAK (açık kapı yok)
• Cross-level işlemler KVKK loglarına kaydedilir
• Gün numaralama: Pazartesi=1, Pazar=7
```

### 4.4 Middleware Pipeline (Sıralı)

```
[1] ExceptionHandling → Hata yakalama
[2] RequestLogging    → HTTP loglama
[3] IpBlacklist       → Redis SET kontrolü
[4] RateLimit         → Sliding window (Redis INCR)
[5] Authentication    → JWT doğrulama
[6] SessionValidation → Redis oturum kontrolü
[7] Authorization     → İzin kontrolü
```

## 5. E-posta Altyapısı

| Özellik | Açıklama |
|---------|----------|
| SMTP | MailKit (Gmail, Outlook, Yandex, özel) |
| Ek dosya | Çoklu dosya, CC, BCC desteği |
| Template | HTML e-posta sarmalama (responsive) |
| Tracking | PostgreSQL EmailLog tablosu |
| Background | Hangfire job (3 retry, 30s-120s-300s) |
| Dashboard | /hangfire → tüm job'ları izle |

## 6. Ayar Yönetimi (Settings Module)

```
Öncelik sırası (en yüksekten düşüğe):
[1] Company ayarı   → CompanyAdmin belirledi
[2] Tenant ayarı    → TenantAdmin belirledi
[3] System ayarı    → SuperAdmin belirledi (DB)
[4] appsettings.json → Kod içi fallback

21 varsayılan ayar: JWT, Oturum, Şifre, 2FA, Erişim, E-posta, Genel
Tümü UI'dan yönetilebilir, Redis cache (5dk TTL)
```

## 7. Docker Ortamı

| Servis | Port | Açıklama |
|--------|------|----------|
| ct-db-main | 5432 | Ana PostgreSQL |
| ct-db-audit | 5433 | Audit PostgreSQL |
| ct-redis | 6379 | Redis cache + session |
| ct-pgadmin | 5050 | Veritabanı yönetimi |
| ct-seq | 5341 | Log izleme |

## 8. API Endpoint Özeti (53 endpoint)

| Grup | Endpoint Sayısı | Açıklama |
|------|----------------|----------|
| Auth | 16 | Login, 2FA, Refresh, Password, Email Verify |
| Tenants | 5 | CRUD + List |
| Companies | 5 | CRUD + List (tenant-scoped) |
| Users | 7 | CRUD + Block + Force Logout |
| Roles | 6 | Tenant/Company Role CRUD + Assign |
| Sessions | 3 | List + Revoke |
| Access Policies | 7 | CRUD + Assign/Unassign + User Policy |
| Settings | 4 | List + Get + Upsert + Delete |
