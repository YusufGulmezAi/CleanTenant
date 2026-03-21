# 🏛️ CleanTenant — Teknik Mimari Dokümanı

**Hedef Kitle:** Sistem Mühendisleri, Backend Geliştiriciler, DevOps Mühendisleri
**Son Güncelleme:** Mart 2026
**Proje Durumu:** Backend tamamlandı, Blazor UI temel yapı hazır, CRUD sayfaları geliştirme aşamasında

---

## 1. Mimari Genel Bakış

CleanTenant, Clean Architecture (Onion Architecture) prensibiyle katmanlı olarak tasarlanmıştır. Her katman yalnızca bir içteki katmana bağımlıdır. Dış katmanlar iç katmanları bilir ancak iç katmanlar dış katmanlardan habersizdir.

```
┌─────────────────────────────────────────────────────┐
│                    Blazor UI                         │  → MudBlazor 9.1 (Blazor Server)
│               (Kullanıcı Arayüzü)                   │    HTTP ile API'ye bağlanır
├─────────────────────────────────────────────────────┤
│                   API Katmanı                        │  → Minimal API, Middleware, Endpoint
│              (Sunum + Endpoint)                      │    Request → MediatR → Response
├─────────────────────────────────────────────────────┤
│              Infrastructure Katmanı                  │  → EF Core, Redis, SMTP, Hangfire
│           (Veritabanı, Cache, Harici)                │    Interface implementasyonları
├─────────────────────────────────────────────────────┤
│              Application Katmanı                     │  → CQRS (MediatR), Behaviors, Rules
│              (İş Mantığı Orkestrasyon)               │    Command/Query → Handler → Result<T>
├─────────────────────────────────────────────────────┤
│                Domain Katmanı                        │  → Entity, Enum, Event (0 bağımlılık)
│              (İş Kuralları + Veri)                   │    Factory method, Domain validation
└─────────────────────────────────────────────────────┘
```

---

## 2. Teknoloji Yığını

### 2.1 Çalışma Zamanı ve Framework

| Teknoloji | Versiyon | Kullanım Amacı | Neden Bu Seçim |
|-----------|----------|----------------|----------------|
| .NET | 10.0 (Stable) | Uygulama çatısı | LTS, Minimal API, Native AOT desteği |
| C# | 13 | Geliştirme dili | Record, Pattern matching, File-scoped namespace |
| ASP.NET Core | 10.0 | Web API | Minimal API, pipeline middleware |
| Blazor Server | 10.0 | Yönetim paneli | Tek dil (C#), gerçek zamanlı, SignalR |

### 2.2 Veri Depolama

| Teknoloji | Versiyon | Port | Kullanım Amacı |
|-----------|----------|------|----------------|
| PostgreSQL | 17 | 5432 (main), 5433 (audit) | Ana veri + Audit loglama (ayrı DB) |
| Redis | 7 | 6379 | Oturum, cache, IP blacklist, 2FA kodları |
| EF Core | 10.x | — | ORM: Code-first, interceptor, global query filter |

**İki ayrı PostgreSQL veritabanı kullanılmasının nedeni:** Ana veriler ile denetim loglarının fiziksel olarak ayrılması. Audit DB bağımsız olarak yedeklenebilir, sorgulanabilir ve büyümesi ana DB performansını etkilemez. KVKK gereği denetim verileri farklı saklama politikasına tabi olabilir.

### 2.3 Güvenlik ve Kimlik Doğrulama

| Teknoloji | Kullanım |
|-----------|----------|
| PBKDF2 (SHA-256, 100K iterasyon) | Şifre hash'leme |
| JWT (HS256) | Access Token (15dk) |
| Refresh Token Rotation | 7 gün, her kullanımda yenisi üretilir |
| Otp.NET | TOTP doğrulama (RFC 6238 — Google/Microsoft Authenticator) |
| QRCoder | QR kod PNG üretimi (API'de byte[] olarak) |
| MailKit | SMTP e-posta gönderimi (2FA kodu, doğrulama) |

**NOT:** Microsoft.AspNetCore.Identity kullanılmamıştır. Tüm kimlik doğrulama özel (custom) olarak geliştirilmiştir. Bunun nedeni hiyerarşik multi-tenant yapının Identity framework ile uyumsuzluğudur.

### 2.4 UI ve Görselleştirme

| Teknoloji | Versiyon | Kullanım |
|-----------|----------|----------|
| MudBlazor | 9.1 | Material Design bileşen kütüphanesi |
| Blazored.LocalStorage | 4.x | JWT token istemci tarafı saklama |
| Inter Font | — | Kurumsal tipografi |

### 2.5 Altyapı ve Araçlar

| Teknoloji | Kullanım |
|-----------|----------|
| Hangfire + PostgreSQL | Arka plan iş yönetimi (e-posta kuyruğu, retry) |
| Serilog + Seq | Yapısal loglama, merkezi log izleme |
| Docker Compose | Geliştirme + Production ortamları |
| MediatR | CQRS pattern (Command/Query ayrımı) |
| FluentValidation | DTO ve iş kuralı doğrulama |

---

## 3. Proje Dosya Yapısı

```
CleanTenant/
│
├── src/
│   ├── CleanTenant.Domain/                 [0 bağımlılık — en iç katman]
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs               → Id (Guid v7), domain event listesi
│   │   │   ├── BaseAuditableEntity.cs      → CreatedAt/By, UpdatedAt/By
│   │   │   ├── BaseTenantEntity.cs         → TenantId zorunlu
│   │   │   ├── BaseCompanyEntity.cs        → TenantId + CompanyId zorunlu
│   │   │   ├── ISoftDeletable.cs           → IsDeleted, DeletedAt
│   │   │   └── IDomainEvent.cs             → MediatR INotification marker
│   │   ├── Identity/
│   │   │   ├── ApplicationUser.cs          → Ana kullanıcı entity (2FA, şifre, blokaj)
│   │   │   └── Roles.cs                    → SystemRole, TenantRole, CompanyRole
│   │   │                                     + UserSystemRole, UserTenantRole, UserCompanyRole
│   │   │                                     + UserCompanyMembership (7 class)
│   │   ├── Tenancy/
│   │   │   ├── Tenant.cs                   → Firma + domain event'ler
│   │   │   └── Company.cs                  → Şirket + domain event'ler
│   │   ├── Security/
│   │   │   ├── AccessPolicy.cs             → 3 seviyeli erişim politikası + UserPolicyAssignment
│   │   │   └── SecurityEntities.cs         → UserSession, UserAccessPolicy, UserBlock, IpBlacklist
│   │   ├── Settings/
│   │   │   └── SystemSetting.cs            → Parametrik ayar + SettingValueType, SettingLevel
│   │   ├── Email/
│   │   │   └── EmailLog.cs                 → E-posta takip + EmailStatus enum
│   │   └── Enums/
│   │       ├── SecurityEnums.cs            → TwoFactorMethod, BlockType, SecurityEventType
│   │       └── UserLevel.cs                → 7 seviyeli yetki puanlaması
│   │
│   ├── CleanTenant.Application/            [Domain'e bağımlı]
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs → EF Core DbSet sözleşmesi
│   │   │   │   ├── IAuditDbContext.cs       → Audit DB + AuditLog, SecurityLog class'ları
│   │   │   │   ├── IServices.cs            → ICurrentUser, ICache, ISession, IEmail,
│   │   │   │   │                             IBackup, ISms + EmailMessage, EmailAttachment
│   │   │   │   ├── ISettingsService.cs     → Hiyerarşik ayar okuma
│   │   │   │   └── ITotpService.cs         → QR kod üretimi, TOTP doğrulama, recovery kodları
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs   → FluentValidation → 422
│   │   │   │   ├── LoggingBehavior.cs      → Stopwatch, >500ms uyarı
│   │   │   │   ├── AuthorizationBehavior.cs→ İzin/Tenant/Company erişim kontrolü → 403
│   │   │   │   └── CachingBehavior.cs      → Redis cache (Query), invalidation (Command)
│   │   │   ├── Rules/                      → AuthorizationRules, TenantRules, CompanyRules, UserRules
│   │   │   ├── Mappings/                   → Entity → DTO extension method'lar
│   │   │   └── Models/
│   │   │       └── Result.cs               → Result<T> Railway pattern (Success/Failure/NotFound)
│   │   ├── Features/
│   │   │   ├── Auth/Commands/
│   │   │   │   ├── AuthCommands.cs         → Login, Verify2FA, Refresh, Logout, ChangePassword,
│   │   │   │   │                             ForgotPassword + TempTokenData
│   │   │   │   └── TwoFactorCommands.cs    → Get2FAStatus, EnableEmail, EnableSMS, VerifySms,
│   │   │   │                                 SetupAuthenticator, VerifyAuthenticator, Disable2FA,
│   │   │   │                                 DisableSpecificMethod, SetPrimary
│   │   │   ├── Auth/Queries/
│   │   │   │   └── AuthQueries.cs          → GetCurrentUser, GetUserContext
│   │   │   ├── Tenants/                    → CRUD Commands + Queries
│   │   │   ├── Companies/                  → CRUD (tenant-scoped)
│   │   │   ├── Users/                      → CRUD + Block + Force Logout
│   │   │   ├── Roles/                      → Tenant/Company Role CRUD + Assign
│   │   │   ├── Sessions/                   → List + Revoke
│   │   │   ├── AccessPolicies/             → CRUD + Assign/Unassign + User Policy
│   │   │   └── Settings/                   → List + Get + Upsert + Delete + DefaultSeeder
│   │   └── DependencyInjection.cs          → MediatR, FluentValidation, Rules kayıt
│   │
│   ├── CleanTenant.Shared/                 [0 bağımlılık — API ↔ UI ortak katman]
│   │   ├── DTOs/Auth/AuthDtos.cs           → 22 DTO: Login, 2FA, Token, Password, SMS, Context
│   │   ├── DTOs/Tenants/TenantDtos.cs      → 4 DTO
│   │   ├── DTOs/Companies/CompanyDtos.cs   → 4 DTO
│   │   ├── DTOs/Users/UserDtos.cs          → 7 DTO
│   │   ├── DTOs/Common/ApiResponse.cs      → ApiResponse<T>, PaginatedResult<T>
│   │   ├── Constants/SystemConstants.cs    → SystemRoles, Permissions (nested), CacheKeys
│   │   └── Helpers/
│   │       ├── SecurityHelper.cs           → PBKDF2, Hash, VerificationCode, TOTP (eski)
│   │       └── DateTimeHelper.cs           → UTC dönüşüm, Pazartesi=1 convention
│   │
│   ├── CleanTenant.Infrastructure/         [Application'a bağımlı]
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs     → 12 DbSet, Global Query Filters, Seed çağrısı
│   │   │   ├── AuditDbContext.cs           → AuditLog, SecurityLog, EmailLog
│   │   │   ├── Configurations/            → EF Core Fluent API entity konfigürasyonları
│   │   │   ├── Interceptors/
│   │   │   │   ├── AuditableInterceptor.cs → CreatedAt/By, UpdatedAt/By otomatik doldurma
│   │   │   │   ├── SoftDeleteInterceptor.cs→ Delete → IsDeleted=true dönüşümü
│   │   │   │   └── AuditTrailInterceptor.cs→ Entity değişikliklerini AuditLog'a kaydetme
│   │   │   ├── Seeds/
│   │   │   │   └── DefaultDataSeeder.cs    → Roller, SuperAdmin, Default politikalar, 21 ayar
│   │   │   └── UtcDateTimeConverters.cs    → EF Core DateTime → UTC otomatik dönüşüm
│   │   ├── Security/
│   │   │   ├── TokenService.cs             → JWT Access Token + Refresh Token üretimi
│   │   │   ├── SessionManager.cs           → Redis + DB dual oturum yönetimi
│   │   │   ├── CurrentUserService.cs       → HTTP context'ten kullanıcı bilgileri
│   │   │   ├── AccessPolicyService.cs      → IP/Zaman erişim kontrolü
│   │   │   ├── DeviceFingerprintService.cs → Cihaz parmak izi üretimi
│   │   │   └── TotpService.cs              → QRCoder + Otp.NET (QR PNG, TOTP, Recovery)
│   │   ├── Email/
│   │   │   ├── SmtpEmailService.cs         → MailKit SMTP (Gmail/Outlook/Yandex)
│   │   │   └── EmailBackgroundJob.cs       → Hangfire job (3 retry: 30s→120s→300s)
│   │   ├── Caching/
│   │   │   └── RedisCacheService.cs        → Redis GET/SET/DEL/SADD/SISMEMBER
│   │   ├── Settings/
│   │   │   └── SettingsService.cs          → Hiyerarşik okuma + Redis cache (5dk TTL)
│   │   └── DependencyInjection.cs          → Tüm Infrastructure DI kayıtları
│   │
│   ├── CleanTenant.API/                    [Infrastructure + Application'a bağımlı]
│   │   ├── Endpoints/                      → 8 endpoint grubu (57 endpoint)
│   │   │   ├── AuthEndpoints.cs            → /api/auth/* (22 endpoint)
│   │   │   ├── TenantEndpoints.cs          → /api/tenants/* (5 endpoint)
│   │   │   ├── CompanyEndpoints.cs         → /api/companies/* (5 endpoint)
│   │   │   ├── UserEndpoints.cs            → /api/users/* (7 endpoint)
│   │   │   ├── RoleEndpoints.cs            → /api/roles/* (6 endpoint)
│   │   │   ├── SessionEndpoints.cs         → /api/sessions/* (3 endpoint)
│   │   │   ├── AccessPolicyEndpoints.cs    → /api/access-policies/* (7 endpoint)
│   │   │   └── SettingsEndpoints.cs        → /api/settings/* (4 endpoint)
│   │   ├── Middleware/                      → 5 middleware (sıralı pipeline)
│   │   │   ├── ExceptionHandlingMiddleware → Global hata yakalama → ApiResponse
│   │   │   ├── RequestLoggingMiddleware    → HTTP log (method, path, süre, IP)
│   │   │   ├── IpBlacklistMiddleware       → Redis SET kontrolü → 403
│   │   │   ├── RateLimitMiddleware         → Sliding window (Redis) → 429
│   │   │   └── SessionValidationMiddleware → Oturum, bloke, device kontrolü → 401/403
│   │   ├── Extensions/                     → EndpointExtensions, MiddlewareExtensions, ResultExtensions
│   │   └── Program.cs                      → Uygulama başlangıç noktası, DI, pipeline
│   │
│   └── CleanTenant.BlazorUI/              [Shared'a bağımlı — API ile HTTP haberleşir]
│       ├── Components/                     → App, Routes, StatCard, PageHeader, RedirectToLogin
│       ├── Layout/                         → MainLayout (sidebar+topbar), LoginLayout
│       ├── Pages/Auth/Login.razor          → Login + çoklu 2FA (gerçek e-posta kodu)
│       ├── Pages/Dashboard/Index.razor     → AdminLTE tarzı stat kartlar + son işlemler
│       ├── Pages/Tenants/                  → CRUD tablo + Dialog (oluştur/düzenle/sil)
│       ├── Pages/{diğer}/Index.razor       → Placeholder sayfalar (geliştirme aşamasında)
│       ├── Services/
│       │   ├── ApiClient.cs                → HTTP istemci, token injection, hata yakalama
│       │   ├── CleanTenantAuthStateProvider→ JWT'den ClaimsPrincipal, süre kontrolü
│       │   └── ThemeService.cs             → Dark/Light toggle, kurumsal yeşil palette
│       └── wwwroot/css/app.css             → FluentUI tarzı CSS overrides
│
├── tests/
│   ├── CleanTenant.Domain.Tests/           → Entity, Security, Tenancy testleri
│   ├── CleanTenant.Application.Tests/      → Behavior, Constants, Result testleri
│   ├── CleanTenant.Infrastructure.Tests/   → Placeholder
│   └── CleanTenant.API.IntegrationTests/   → Placeholder
│
├── docker/
│   ├── docker-compose.yml                  → Development (5 servis)
│   └── docker-compose.production.yml       → Production yapılandırması
│
└── docs/                                   → Bu dokümanlar
```

---

## 4. Hiyerarşik Multi-Tenancy Modeli

### 4.1 Kavramsal Model

```
Platform (CleanTenant)
    │
    ├── Tenant A (Mali Müşavirlik Firması — "ABC Mali Müşavirlik")
    │   ├── Company A1 (Müşteri — "Yıldız AŞ")
    │   │   ├── CompanyAdmin (Muhasebeci: Ahmet)
    │   │   ├── CompanyUser (Çalışan: Mehmet)
    │   │   └── Member (Şirket ortağı: Ali)
    │   ├── Company A2 (Müşteri — "Güneş Ltd")
    │   │   └── ...
    │   ├── TenantAdmin (Firma sahibi: Hasan)
    │   └── TenantUser (Firma çalışanı: Veli)
    │
    ├── Tenant B (Başka Mali Müşavir)
    │   └── ...
    │
    ├── SuperAdmin (Platform sahibi)
    └── SystemUser (Platform operatörü)
```

### 4.2 Kullanıcı Seviyeleri ve Yetki Puanları

Her kullanıcı bir veya birden fazla seviyeye sahip olabilir. Yetki kontrolünde `currentLevel > targetLevel` kuralı uygulanır.

| Seviye | Puan | Erişim Kapsamı | Tipik Kullanıcı |
|--------|------|----------------|-----------------|
| SuperAdmin | 100 | Tüm platform, sınırsız | Platform sahibi |
| SystemUser | 80 | Tüm alt seviyeler | Platform operatörü |
| TenantAdmin | 60 | Kendi tenant + tüm şirketleri | Mali müşavir (firma sahibi) |
| TenantUser | 40 | Kendi tenant + atanmış şirketler | Firma çalışanı |
| CompanyAdmin | 20 | Tek şirket (tam yetki) | Şirket müdürü |
| CompanyUser | 10 | Tek şirket (sınırlı) | Şirket çalışanı |
| CompanyMember | 5 | Tek şirket (salt okunur) | Şirket ortağı, dış danışman |

### 4.3 Çapraz Kimlik (Cross-Identity)

Tek e-posta adresi ile birden fazla tenant/şirket/rol atanabilir. Kullanıcı, HTTP header'ları ile aktif bağlamını seçer:

```
X-Tenant-Id: {guid}     → Aktif tenant
X-Company-Id: {guid}    → Aktif şirket
```

API, her istekte bu header'ları kontrol eder ve kullanıcının seçilen bağlamda yetkisi olup olmadığını doğrular.

---

## 5. Güvenlik Mimarisi

### 5.1 Kimlik Doğrulama Zinciri

```
┌────────────────────────────────────────────────────────────────────────┐
│ 1. Şifre Doğrulama                                                     │
│    PBKDF2 (SHA-256, 100.000 iterasyon, 128-bit salt)                   │
│    Veritabanında sadece hash saklanır                                   │
├────────────────────────────────────────────────────────────────────────┤
│ 2. İki Faktörlü Doğrulama (Çoklu Yöntem — aynı anda aktif olabilir)   │
│    ┌─────────────┬──────────────────┬─────────────────────────────┐    │
│    │ E-posta      │ SMS              │ Authenticator               │    │
│    │ MailKit SMTP │ ISmsProvider     │ QRCoder + Otp.NET           │    │
│    │ 6 haneli kod │ 6 haneli kod     │ TOTP (RFC 6238, ±30sn)     │    │
│    │ Redis 5dk    │ Redis 5dk        │ Google/Microsoft Compat     │    │
│    └─────────────┴──────────────────┴─────────────────────────────┘    │
│    Primary yöntem otomatik: Authenticator > SMS > Email                 │
│    Kullanıcı primary'yi değiştirebilir (POST /2fa/set-primary)         │
│    Fallback: "Kodumu alamıyorum" → e-posta ile yeni kod               │
├────────────────────────────────────────────────────────────────────────┤
│ 3. Token Üretimi                                                        │
│    Access Token: JWT HS256, 15dk (DB Settings'ten parametrik)           │
│    Refresh Token: 7 gün, rotation (her kullanımda yenisi üretilir)     │
│    TempToken: 5dk, 2FA doğrulama öncesi geçici, Redis'te saklanır      │
├────────────────────────────────────────────────────────────────────────┤
│ 4. Oturum Yönetimi (Dual Storage)                                       │
│    Redis: Her API isteğinde hızlı doğrulama                            │
│    PostgreSQL: Kalıcı kayıt, audit trail, raporlama                    │
│    Device Fingerprint: IP + UserAgent + ek bilgi hash                  │
└────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Erişim Politikası Sistemi (3 Katmanlı — Açık Kapı YOK)

```
KURALLAR:
• Her seviyede (System, Tenant, Company) 1 adet SİLİNEMEZ default politika vardır
• Default politika: DenyAllIps=true, DenyAllTimes=true → her şeyi reddeder
• Kullanıcı oluşturulunca → default politika OTOMATİK atanır
• Özel politika silinince → kullanıcılar default'a geri düşer
• Politika YOKSA → GİRİŞ YASAKTIR (açık kapı asla olmaz)
• Üst seviye alt seviyeye müdahale edebilir (loglama zorunlu)
• Cross-level işlemler KVKK SecurityLog'a detaylı kaydedilir
```

Erişim kontrolü sırasıyla şunları kontrol eder:
1. Atanmış politika var mı? (yoksa → 403)
2. IP adresi izinli mi? (CIDR desteği: 192.168.1.0/24)
3. Bugün izinli gün mü? (Pazartesi=1, Pazar=7)
4. Şu an izinli saat aralığında mı? (08:00-18:00 gibi)

### 5.3 Middleware Pipeline (İstek İşleme Sırası)

```
HTTP İstek →
  [1] ExceptionHandling  → Tüm hataları yakalar, ApiResponse<T> formatında döner
  [2] RequestLogging      → HTTP method, path, süre, IP, tenant loglar (Serilog)
  [3] IpBlacklist         → Redis SISMEMBER: ct:blacklist:ips → 403
  [4] RateLimit           → Redis sliding window (INCR + EXPIRE) → 429
  [5] Authentication      → JWT doğrulama, Claims çıkarma
  [6] SessionValidation   → Redis oturum geçerliliği, bloke, device fingerprint
  [7] Authorization       → .NET built-in → 401/403
  [8] Endpoint            → MediatR → Handler → Result<T> → ApiResponse<T>
```

### 5.4 CQRS Pipeline (MediatR Behaviors)

```
Command/Query →
  [1] ValidationBehavior      → FluentValidation ile DTO doğrulama → 422
  [2] LoggingBehavior          → Stopwatch; >500ms ise uyarı logu
  [3] AuthorizationBehavior    → İzin, Tenant/Company erişim kontrolü → 403
  [4] CachingBehavior          → Query: Redis'ten oku; Command: Cache invalidation
  [5] Handler                  → İş mantığı
```

---

## 6. Domain Entity Envanteri (18 Entity + 8 Enum)

### 6.1 Entity'ler

| Entity | Tablo | Katman | Açıklama |
|--------|-------|--------|----------|
| ApplicationUser | Users | Identity | Ana kullanıcı: şifre, 2FA, blokaj, seviye |
| SystemRole | SystemRoles | Identity | Platform seviyesi roller (SuperAdmin, SystemUser) |
| TenantRole | TenantRoles | Identity | Tenant seviyesi roller (TenantAdmin, TenantUser) |
| CompanyRole | CompanyRoles | Identity | Şirket seviyesi roller |
| UserSystemRole | UserSystemRoles | Identity | Kullanıcı ↔ SystemRole bağlantısı |
| UserTenantRole | UserTenantRoles | Identity | Kullanıcı ↔ TenantRole bağlantısı |
| UserCompanyRole | UserCompanyRoles | Identity | Kullanıcı ↔ CompanyRole bağlantısı |
| UserCompanyMembership | UserCompanyMemberships | Identity | Kullanıcı ↔ Company üyelik |
| Tenant | Tenants | Tenancy | Mali müşavirlik firması |
| Company | Companies | Tenancy | Müşteri şirketi (tenant altında) |
| AccessPolicy | AccessPolicies | Security | IP + Zaman erişim kuralları (3 seviye) |
| UserPolicyAssignment | UserPolicyAssignments | Security | Kullanıcı ↔ Politika ataması |
| UserSession | UserSessions | Security | JWT oturum kaydı (Redis + DB dual) |
| UserAccessPolicy | UserAccessPolicies | Security | Eski format (uyumluluk) |
| UserBlock | UserBlocks | Security | Geçici/kalıcı kullanıcı engelleme |
| IpBlacklist | IpBlacklists | Security | Sistem geneli IP kara listesi |
| SystemSetting | SystemSettings | Settings | Parametrik ayar (Key-Value, hiyerarşik) |
| EmailLog | EmailLogs | Email/Audit | E-posta gönderim takibi (Audit DB) |

### 6.2 Enum'lar

| Enum | Değerler | Kullanım |
|------|----------|----------|
| UserLevel | SuperAdmin(100), SystemUser(80), TenantAdmin(60), TenantUser(40), CompanyAdmin(20), CompanyUser(10), CompanyMember(5) | Yetki hiyerarşisi |
| TwoFactorMethod | None, Email, Sms, Authenticator | 2FA yöntemi |
| BlockType | Temporary, Permanent, ForceLogout | Blokaj türü |
| SecurityEventType | Login, Logout, FailedLogin, TwoFactor, PasswordChange, Block, PolicyChange | Güvenlik log türü |
| EmailStatus | Queued, Sending, Sent, Failed | E-posta durumu |
| PolicyLevel | System, Tenant, Company | Politika kapsamı |
| SettingLevel | System, Tenant, Company | Ayar kapsamı |
| SettingValueType | String, Int, Bool, Json, TimeSpan | Ayar değer tipi |

---

## 7. API Endpoint Referansı (57 Endpoint)

### 7.1 Kimlik Doğrulama ve 2FA (22 endpoint)

| Method | Endpoint | Açıklama | Auth |
|--------|----------|----------|------|
| POST | /api/auth/login | E-posta + şifre ile giriş | ❌ |
| POST | /api/auth/verify-2fa | 2FA kodu doğrulama | ❌ |
| POST | /api/auth/2fa-fallback | "Kodumu alamıyorum" — e-posta fallback | ❌ |
| POST | /api/auth/refresh | Token yenileme (rotation) | ❌ |
| POST | /api/auth/logout | Oturum sonlandırma | ✅ |
| POST | /api/auth/change-password | Şifre değiştirme | ✅ |
| POST | /api/auth/forgot-password | Şifre sıfırlama talebi | ❌ |
| GET | /api/auth/me | Oturumdaki kullanıcı bilgisi | ✅ |
| GET | /api/auth/context | Kullanıcı tenant/şirket bağlamları | ✅ |
| POST | /api/auth/send-email-verification | E-posta doğrulama kodu gönder | ✅ |
| POST | /api/auth/confirm-email | E-posta doğrulama kodunu onayla | ✅ |
| GET | /api/auth/2fa/status | 2FA durumu (çoklu yöntem detayı) | ✅ |
| POST | /api/auth/2fa/enable-email | E-posta 2FA aktifleştir | ✅ |
| POST | /api/auth/2fa/enable-sms | SMS 2FA aktifleştir + telefon doğrulama | ✅ |
| POST | /api/auth/2fa/verify-sms | SMS kodunu doğrula + SMS 2FA aktifleştir | ✅ |
| POST | /api/auth/2fa/setup-authenticator | Authenticator kurulumu (QR PNG + secret) | ✅ |
| POST | /api/auth/2fa/verify-authenticator | Authenticator kodunu doğrula + aktifleştir | ✅ |
| POST | /api/auth/2fa/set-primary | Primary 2FA yöntemini değiştir | ✅ |
| POST | /api/auth/2fa/disable-method | Belirli bir 2FA yöntemini kapat | ✅ |
| POST | /api/auth/2fa/disable | Tüm 2FA yöntemlerini kapat | ✅ |

### 7.2 Tenant Yönetimi (5 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/tenants | Tenant listesi (sayfalı) |
| GET | /api/tenants/{id} | Tenant detayı |
| POST | /api/tenants | Yeni tenant oluştur |
| PUT | /api/tenants/{id} | Tenant güncelle |
| DELETE | /api/tenants/{id} | Tenant sil (soft delete) |

### 7.3 Şirket Yönetimi (5 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/companies | Aktif tenant'ın şirketleri |
| GET | /api/companies/{id} | Şirket detayı |
| POST | /api/companies | Yeni şirket oluştur |
| PUT | /api/companies/{id} | Şirket güncelle |
| DELETE | /api/companies/{id} | Şirket sil (soft delete) |

### 7.4 Kullanıcı Yönetimi (7 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/users | Kullanıcı listesi (sayfalı) |
| GET | /api/users/{id} | Kullanıcı detayı (roller dahil) |
| POST | /api/users | Kullanıcı oluştur |
| PUT | /api/users/{id} | Profil güncelle |
| DELETE | /api/users/{id} | Kullanıcı sil (soft delete) |
| POST | /api/users/{id}/block | Kullanıcı bloke et |
| POST | /api/users/{id}/force-logout | Zorla çıkış yaptır |

### 7.5 Rol Yönetimi (6 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/roles/tenant/{tenantId} | Tenant rolleri |
| GET | /api/roles/company/{companyId} | Şirket rolleri |
| POST | /api/roles/tenant | Tenant rolü oluştur |
| POST | /api/roles/company | Şirket rolü oluştur |
| POST | /api/roles/tenant/assign | Tenant rolü ata |
| POST | /api/roles/company/assign | Şirket rolü ata |

### 7.6 Oturum Yönetimi (3 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/sessions | Aktif oturumlar |
| GET | /api/sessions/user/{userId} | Kullanıcı oturumları |
| DELETE | /api/sessions/user/{userId} | Kullanıcı oturumlarını revoke et |

### 7.7 Erişim Politikası (7 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/access-policies | Politika listesi |
| POST | /api/access-policies | Politika oluştur |
| PUT | /api/access-policies/{id} | Politika güncelle |
| DELETE | /api/access-policies/{id} | Politika sil |
| POST | /api/access-policies/{policyId}/assign/{userId} | Kullanıcıya politika ata |
| DELETE | /api/access-policies/unassign/{userId} | Politika atamasını kaldır |
| GET | /api/access-policies/user/{userId} | Kullanıcı politikası |

### 7.8 Sistem Ayarları (4 endpoint)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | /api/settings | Ayar listesi |
| GET | /api/settings/{key} | Ayar değeri oku (hiyerarşik) |
| PUT | /api/settings | Ayar ekle/güncelle |
| DELETE | /api/settings/{id} | Ayar sil |

---

## 8. E-posta Altyapısı

| Özellik | Detay |
|---------|-------|
| Protokol | SMTP (TLS/StartTLS) |
| Kütüphane | MailKit 4.x |
| Sağlayıcılar | Gmail, Outlook, Yandex, özel SMTP |
| CC/BCC | Desteklenir |
| Ekler | Çoklu dosya eki (attachmentNames, attachmentTotalSize tracking) |
| Şablon | HTML responsive e-posta sarmalama (kurumsal header/footer) |
| Tracking | PostgreSQL EmailLog tablosu (Audit DB): Queued→Sending→Sent/Failed |
| Arka Plan | Hangfire job, 3 retry (30s→120s→300s artan bekleme) |
| Dashboard | /hangfire (web UI) — tüm job'ları izleme |
| Konfigürasyon | `EmailSettings` (appsettings.json root seviye — CleanTenant altında DEĞİL) |

---

## 9. Ayar Yönetimi (Settings Module)

21 varsayılan ayar, 5 kategoride. Her ayar hiyerarşik olarak override edilebilir.

```
Okuma Önceliği (en yüksekten düşüğe):
  [1] Company ayarı  → CompanyAdmin belirlemiş (DB)
  [2] Tenant ayarı   → TenantAdmin belirlemiş (DB)
  [3] System ayarı   → SuperAdmin belirlemiş (DB)
  [4] appsettings.json → Kod içi varsayılan (fallback)

Tüm okumalar Redis cache üzerinden yapılır (5dk TTL).
Cache miss → DB sorgusu → Redis'e yaz → döndür.
```

---

## 10. Docker Altyapısı

| Servis | Container | Port | Açıklama |
|--------|-----------|------|----------|
| Ana Veritabanı | ct-db-main | 5432 | PostgreSQL 17: Kullanıcı, tenant, şirket, politika, ayar |
| Audit Veritabanı | ct-db-audit | 5433 | PostgreSQL 17: AuditLog, SecurityLog, EmailLog |
| Cache | ct-redis | 6379 | Redis 7: Oturum, cache, blacklist, 2FA kodları |
| DB Yönetimi | ct-pgadmin | 5050 | pgAdmin 4 (web) |
| Log İzleme | ct-seq | 5341 | Seq (Serilog sink, web UI) |

---

## 11. Sayısal Özet

| Metrik | Değer |
|--------|-------|
| Toplam C# + Razor dosyası | ~130 |
| API Endpoint | 57 (8 grup) |
| CQRS Handler | 54 |
| Domain Entity | 18 |
| Domain Enum | 8 |
| Application Interface | 10 |
| Pipeline Behavior | 4 |
| Middleware | 5 |
| Infrastructure Servis | 11 |
| Blazor Sayfa | 11 |
| Blazor Component | 5 |
| Unit Test | 119 (tümü başarılı) |
| Docker Servis | 5 |
| Varsayılan Ayar | 21 |
| NuGet Paketi | 15 |
