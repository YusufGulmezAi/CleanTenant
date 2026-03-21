# 🏛️ CleanTenant — Teknik Mimari Dokümanı

## 1. Genel Bakış

CleanTenant, .NET 10 üzerinde Clean Architecture ile tasarlanmış, hiyerarşik multi-tenant enterprise framework'üdür. Tek kod tabanıyla sınırsız sayıda tenant, şirket ve kullanıcıyı güvenli şekilde yönetir.

### Teknoloji Yığını

| Katman | Teknoloji | Versiyon | Amaç |
|--------|-----------|----------|------|
| Runtime | .NET | 10.0 (Stable) | LTS, Minimal API, performans |
| Veritabanı | PostgreSQL | 17 | JSONB, CIDR, text[] desteği |
| Cache | Redis | 7 | Oturum, izin cache, IP blacklist |
| ORM | EF Core | 10.x | Code-first, interceptor, query filter |
| CQRS | MediatR | - | Pipeline behaviors, command/query ayrımı |
| Validation | FluentValidation | - | İş kuralı doğrulama |
| UI | MudBlazor | 9.1 | Blazor Server, Material Design |
| E-posta | MailKit | 4.x | SMTP, CC/BCC, çoklu ek, HTML template |
| Background | Hangfire | 1.8.x | E-posta kuyruğu, retry, dashboard |
| Loglama | Serilog + Seq | - | Yapısal loglama, KVKK audit |
| Container | Docker Compose | - | Development + Production |
| Auth Storage | Blazored.LocalStorage | 4.x | JWT token client-side saklama |

### Sayısal Özet

| Metrik | Değer |
|--------|-------|
| Toplam dosya | 155+ |
| C# + Razor dosyası | 123 |
| API Endpoint | 57 (8 grup) |
| CQRS Handler | 50 |
| Domain Entity | 18 |
| Domain Enum | 8 |
| Pipeline Behavior | 4 (Validation, Caching, Logging, Authorization) |
| Middleware | 5 |
| Blazor Sayfa | 11 |
| Blazor Component | 5 |
| Infrastructure Servis | 10 |
| Unit Test | 119 (tümü başarılı) |
| Docker Servis | 5 |
| Varsayılan Ayar | 21 |

## 2. Proje Yapısı (Clean Architecture)

```
CleanTenant/
├── src/
│   ├── CleanTenant.Domain/              → Entity, Enum, Event (0 bağımlılık)
│   │   ├── Common/                      → BaseEntity, BaseAuditableEntity, ISoftDeletable
│   │   ├── Identity/                    → ApplicationUser, SystemRole, TenantRole, CompanyRole
│   │   │                                  UserSystemRole, UserTenantRole, UserCompanyRole, Membership
│   │   ├── Tenancy/                     → Tenant, Company + Domain Events
│   │   ├── Security/                    → AccessPolicy, UserPolicyAssignment, UserSession
│   │   │                                  UserAccessPolicy, UserBlock, IpBlacklist
│   │   ├── Settings/                    → SystemSetting, SettingValueType, SettingLevel
│   │   ├── Email/                       → EmailLog, EmailStatus
│   │   └── Enums/                       → UserLevel, TwoFactorMethod, BlockType, SecurityEventType
│   │
│   ├── CleanTenant.Application/         → CQRS Handlers, Behaviors, Rules, Mappings
│   │   ├── Common/
│   │   │   ├── Behaviors/               → Validation, Caching, Logging, Authorization pipeline
│   │   │   ├── Interfaces/              → IApplicationDbContext, ICacheService, IEmailService
│   │   │   │                              ISessionManager, ISettingsService, AuditLog, SecurityLog
│   │   │   ├── Rules/                   → Authorization, Tenant, Company, User business rules
│   │   │   ├── Mappings/               → Entity → DTO extension methods
│   │   │   └── Models/                  → Result<T> (Railway pattern)
│   │   └── Features/
│   │       ├── Auth/Commands/           → Login, Verify2FA, Refresh, Logout, ChangePassword
│   │       │                              TwoFactor (Enable/Disable/Setup/Verify Authenticator)
│   │       ├── Auth/Queries/            → GetCurrentUser, GetUserContext
│   │       ├── Tenants/                 → CRUD Commands + Queries
│   │       ├── Companies/               → CRUD (tenant-scoped)
│   │       ├── Users/                   → CRUD + Block + Force Logout
│   │       ├── Roles/                   → Tenant/Company Role CRUD + Assign
│   │       ├── Sessions/                → List, Revoke
│   │       ├── AccessPolicies/          → CRUD + Assign/Unassign + User Policy
│   │       └── Settings/                → List, Get, Upsert, Delete + DefaultSeeder (21 ayar)
│   │
│   ├── CleanTenant.Shared/             → DTOs, Constants, Helpers (API ↔ UI ortak)
│   │   ├── DTOs/Auth/                   → 16 DTO (Login, 2FA, Token, Password, Context)
│   │   ├── DTOs/Tenants/               → 4 DTO
│   │   ├── DTOs/Companies/             → 4 DTO
│   │   ├── DTOs/Users/                 → 7 DTO
│   │   ├── DTOs/Common/                → ApiResponse<T>, PaginatedResult<T>
│   │   ├── Constants/                   → SystemRoles, Permissions (nested), CacheKeys
│   │   └── Helpers/                     → SecurityHelper (PBKDF2, TOTP, Base32), DateTimeHelper
│   │
│   ├── CleanTenant.Infrastructure/      → EF Core, Redis, JWT, SMTP, Hangfire, Settings
│   │   ├── Persistence/                 → ApplicationDbContext, AuditDbContext, Interceptors, Seeds
│   │   ├── Caching/                     → RedisCacheService
│   │   ├── Security/                    → TokenService, SessionManager, CurrentUserService
│   │   │                                  AccessPolicyService, DeviceFingerprintService
│   │   ├── Email/                       → SmtpEmailService (MailKit), EmailBackgroundJob (Hangfire)
│   │   └── Settings/                    → SettingsService (DB → appsettings.json fallback + Redis cache)
│   │
│   ├── CleanTenant.API/                → Minimal API, Middleware
│   │   ├── Endpoints/                   → 8 endpoint grubu (57 endpoint)
│   │   ├── Middleware/                  → Exception, Logging, IpBlacklist, RateLimit, Session
│   │   └── Extensions/                  → Result → IResult, Middleware pipeline, Endpoint kayıt
│   │
│   └── CleanTenant.BlazorUI/           → MudBlazor 9.1 Blazor Server
│       ├── Components/                  → App, Routes, StatCard, PageHeader, ConfirmDialog, RedirectToLogin
│       ├── Layout/                      → MainLayout (sidebar+topbar), LoginLayout
│       ├── Pages/Auth/                  → Login + 2FA (gerçek e-posta kodu)
│       ├── Pages/Dashboard/             → AdminLTE tarzı stat kartlar + son işlemler
│       ├── Pages/Tenants/               → CRUD + Dialog
│       ├── Pages/{Companies,Users,...}/ → Placeholder sayfalar (geliştirme aşamasında)
│       ├── Services/                    → ApiClient, AuthStateProvider, ThemeService
│       └── wwwroot/css/                 → Kurumsal yeşil tema CSS, FluentUI tarzı
│
├── tests/                               → 119 unit test
│   ├── CleanTenant.Domain.Tests/        → Entity, Security, Tenancy testleri
│   ├── CleanTenant.Application.Tests/   → Behavior, Constants, Result testleri
│   ├── CleanTenant.Infrastructure.Tests/
│   └── CleanTenant.API.IntegrationTests/
│
├── docker/                              → Docker Compose (dev + prod), cleanup.ps1
└── docs/                                → Teknik, idari, akış dokümanları
```

## 3. Hiyerarşik Multi-Tenancy Modeli

```
System (Platform)
  └── Tenant (Mali Müşavirlik Firması)
       └── Company (Şirket/Müşteri)
            └── Member (Çalışan/Kişi)
```

### Kullanıcı Seviyeleri

| Seviye | Puan | Yetkiler |
|--------|------|----------|
| SuperAdmin | 100 | Platform sahibi, sınırsız yetki, tüm seviyelere müdahale |
| SystemUser | 80 | Platform operatör, alt seviyelere müdahale |
| TenantAdmin | 60 | Firma yöneticisi, kendi tenant + alt şirketler |
| TenantUser | 40 | Firma çalışanı, kendi tenant + alt şirketler |
| CompanyAdmin | 20 | Şirket yöneticisi, kendi şirketi |
| CompanyUser | 10 | Şirket çalışanı |
| CompanyMember | 5 | Şirket üyesi (sınırlı erişim) |

Kural: `currentLevel > targetLevel` gerekli (alt seviye üst seviyeye müdahale edemez).

### Çapraz Kimlik (Cross-Identity)

Tek e-posta adresi ile birden fazla tenant/şirket/rol. Context Switching header'ları ile aktif bağlam seçilir:
- `X-Tenant-Id`: Aktif tenant
- `X-Company-Id`: Aktif şirket

## 4. Güvenlik Mimarisi

### 4.1 Kimlik Doğrulama (Custom — Microsoft.Identity kullanılmıyor)

| Bileşen | Teknoloji | Detay |
|---------|-----------|-------|
| Şifre Hash | PBKDF2 | SHA-256, 100K iterasyon, 128-bit salt |
| Token | JWT | HS256, Access (15dk) + Refresh (7 gün) |
| 2FA | TOTP (RFC 6238) | Google/Microsoft Authenticator + gerçek e-posta kodu |
| Oturum | Redis + DB | Dual storage, device fingerprint kontrolü |
| TempToken | Redis | 5dk ömürlü, 2FA doğrulama öncesi geçici token |

### 4.2 Token Akışı

- **Access Token**: 15dk (DB Settings'ten parametrik — UI'dan değiştirilebilir)
- **Refresh Token**: 7 gün (rotation — her kullanımda yenisi üretilir, eski revoke edilir)
- **TempToken**: 5dk (2FA doğrulama öncesi, tek kullanımlık)
- Token süresi uzatılmaz — dolunca UI otomatik refresh yapar

### 4.3 Erişim Politikası (3 Katmanlı — Açık Kapı YOK)

```
Kurallar:
• Her seviyede 1 DEFAULT politika (silinemez, tümünü reddeder)
• Kullanıcı oluşturulunca → default politika otomatik atanır
• Özel politika silinince → kullanıcılar default'a düşer
• Politika YOKSA → GİRİŞ YASAK (açık kapı yok!)
• Üst seviye alt seviyeye müdahale edebilir
• Cross-level işlemler KVKK SecurityLog'a detaylı kaydedilir
• Gün numaralama: Pazartesi=1, Pazar=7
```

### 4.4 Middleware Pipeline (Sıralı)

```
[1] ExceptionHandling → Tüm hata yakalama, ApiResponse dönüşümü
[2] RequestLogging    → HTTP method, path, süre, IP, tenant loglama
[3] IpBlacklist       → Redis SET kontrolü (SISMEMBER)
[4] RateLimit         → Sliding window (Redis INCR + EXPIRE)
[5] Authentication    → JWT doğrulama, Claims çıkarma
[6] SessionValidation → Redis oturum, bloke, device kontrolü
[7] Authorization     → İzin kontrolü
```

### 4.5 CQRS Pipeline

```
[1] ValidationBehavior     → FluentValidation → 422
[2] LoggingBehavior        → Stopwatch, >500ms uyarı
[3] AuthorizationBehavior  → İzin/Tenant/Company erişim kontrolü → 403
[4] CachingBehavior        → Redis cache (Query), invalidation (Command)
[5] Handler                → İş mantığı
```

## 5. E-posta Altyapısı

| Özellik | Açıklama |
|---------|----------|
| SMTP | MailKit (Gmail, Outlook, Yandex, özel) |
| Dosya Eki | Çoklu dosya, CC, BCC desteği |
| Template | HTML e-posta sarmalama (responsive, kurumsal) |
| Tracking | PostgreSQL EmailLog tablosu (Audit DB) |
| Background | Hangfire job (3 retry: 30s→120s→300s) |
| Dashboard | /hangfire → tüm job'ları izle |
| 2FA | Login sırasında gerçek 6 haneli kod gönderimi |
| Config | `EmailSettings` root level (appsettings.json) |

## 6. Ayar Yönetimi (Settings Module)

```
Öncelik sırası (en yüksekten düşüğe):
[1] Company ayarı   → CompanyAdmin belirledi (DB)
[2] Tenant ayarı    → TenantAdmin belirledi (DB)
[3] System ayarı    → SuperAdmin belirledi (DB)
[4] appsettings.json → Kod içi fallback

21 varsayılan ayar: JWT, Oturum, Şifre, 2FA, Erişim, E-posta, Genel
Tümü UI'dan yönetilebilir, Redis cache (5dk TTL)
```

## 7. Blazor UI

| Özellik | Detay |
|---------|-------|
| Framework | MudBlazor 9.1 (Blazor Server) |
| Tema | Kurumsal yeşil (#2e7d32), FluentUI tarzı |
| Dark/Light | Toggle buton (sağ üst) |
| Dashboard | AdminLTE tarzı stat kartlar, son işlemler |
| Login | 2FA destekli (gerçek e-posta kodu) |
| Tenant CRUD | Tablo + Dialog (oluştur/düzenle/sil) |
| Auth | JWT LocalStorage, AuthStateProvider |
| Responsive | Mobil uyumlu sidebar |

## 8. Docker Ortamı

| Servis | Port | Açıklama |
|--------|------|----------|
| ct-db-main | 5432 | Ana PostgreSQL veritabanı |
| ct-db-audit | 5433 | Audit PostgreSQL (log, email tracking) |
| ct-redis | 6379 | Redis cache + session + blacklist |
| ct-pgadmin | 5050 | Veritabanı yönetimi (web) |
| ct-seq | 5341 | Serilog log izleme (web) |

## 9. API Endpoint Özeti (57 endpoint)

| Grup | Sayı | Endpoint'ler |
|------|------|-------------|
| Auth | 18 | login, verify-2fa, 2fa-fallback, refresh, logout, change-password, forgot-password, me, context, 2fa/status, 2fa/enable-email, 2fa/setup-authenticator, 2fa/verify-authenticator, 2fa/disable, send-email-verification, confirm-email, emails |
| Tenants | 5 | CRUD + list |
| Companies | 5 | CRUD + list (tenant-scoped) |
| Users | 7 | CRUD + block + force-logout + list |
| Roles | 6 | Tenant/Company role CRUD + assign |
| Sessions | 3 | list + user sessions + revoke |
| Access Policies | 7 | CRUD + assign + unassign + user policy |
| Settings | 4 | list + get value + upsert + delete |
| IP Blacklist | 4 | list + add + remove + check |
