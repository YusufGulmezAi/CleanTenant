# 🏛️ CleanTenant — Teknik Mimari Dokümanı

## 1. Genel Bakış

CleanTenant, .NET 10 üzerinde Clean Architecture ile tasarlanmış, hiyerarşik multi-tenant enterprise framework'üdür.

### Teknoloji Yığını

| Katman | Teknoloji | Amaç |
|--------|-----------|------|
| Runtime | .NET 10 | LTS, Minimal API native |
| Veritabanı | PostgreSQL 17 | JSONB, text[], CIDR desteği |
| Cache | Redis 7 | Oturum, izin cache, IP blacklist |
| ORM | EF Core 10 | Code-first, interceptor, query filter |
| CQRS | MediatR | Pipeline behaviors, command/query ayrımı |
| Validation | FluentValidation | İş kuralı doğrulama |
| Loglama | Serilog + Seq | Yapısal loglama |
| UI | MudBlazor | .NET native component kütüphanesi |
| Container | Docker Compose | Development + Production |

### Sayısal Özet

| Metrik | Değer |
|--------|-------|
| Toplam dosya | 114 |
| API Endpoint | 35 |
| CQRS Handler | 25 |
| Pipeline Behavior | 5 |
| Business Rule sınıfı | 4 |
| Middleware | 5 |
| Entity | 14 |
| Unit Test | 94 |

---

## 2. Clean Architecture Katmanları

```
┌─────────────────────────────────────────────────┐
│                    API Layer                      │
│    Minimal API Endpoints + Middleware Pipeline    │
├─────────────────────────────────────────────────┤
│               Application Layer                   │
│   CQRS Handlers + Behaviors + Rules + Mappings   │
├─────────────────────────────────────────────────┤
│               Domain Layer (0 bağımlılık)         │
│     Entities + Value Objects + Domain Events      │
├─────────────────────────────────────────────────┤
│             Infrastructure Layer                  │
│    EF Core + Redis + JWT + Interceptors           │
├─────────────────────────────────────────────────┤
│               Shared Layer                        │
│         DTOs + Constants + Helpers                │
└─────────────────────────────────────────────────┘
```

### Bağımlılık Kuralı

- Domain → Hiçbir şeye bağımlı değil (saf C#)
- Application → Domain + Shared
- Infrastructure → Application (transitif olarak Domain + Shared)
- API → Infrastructure + Shared
- Shared → Hiçbir şeye bağımlı değil

---

## 3. Hiyerarşik Multi-Tenant Modeli

```
🌐 System (Platform Seviyesi)
 ├── 👤 SuperAdmin (100)         → Sınırsız yetki
 ├── 👥 SystemUser (80)          → Tüm tenant'larda rol bazlı
 │
 ├── 🏢 Tenant A (Mali Müşavirlik X)
 │    ├── 👤 TenantAdmin (60)    → Kendi tenant'ında tam yetki
 │    ├── 👥 TenantUser (40)     → Alt şirketlerde yetkili
 │    │
 │    ├── 🏭 Company 1 (ABC Ltd.)
 │    │    ├── 👤 CompanyAdmin (20) → Şirket içi tam yetki
 │    │    ├── 👥 CompanyUser (10)  → Rol bazlı
 │    │    └── 👥 Member (5)        → Sınırlı erişim
 │    │
 │    └── 🏭 Company 2 (XYZ A.Ş.)
 │
 └── 🏢 Tenant B (Mali Müşavirlik Y)
```

### Çapraz Kullanıcı Kimliği

Tek kullanıcı (tek e-posta) birden fazla seviyede rol sahibi olabilir:
- Sistem kullanıcısı + 2 farklı tenant'ta TenantUser + 3 farklı şirkette CompanyUser

### Context Switching

Aynı tarayıcıda farklı sekmelerde farklı bağlamlar:
- Header: `X-Tenant-Id`, `X-Company-Id`
- Token bağlamdan bağımsız, her istek header ile bağlam belirler

---

## 4. Veritabanı Mimarisi

### İki Veritabanı Stratejisi

| Veritabanı | İçerik | Neden Ayrı? |
|------------|--------|-------------|
| cleantenant_main | Kullanıcılar, Tenant'lar, Şirketler, Roller, Oturumlar | Operasyonel veri |
| cleantenant_audit | AuditLog, SecurityLog, ApplicationLog | Yoğun INSERT, farklı retention |

### Veri İzolasyonu

- Shared Database + CompanyId filtre (Row-Level)
- Global Query Filter ile otomatik WHERE koşulu
- Şirket bazlı filtered backup desteği

### Tablo Listesi (14 Entity)

**Tenancy:** Tenants, Companies
**Identity:** Users, SystemRoles, TenantRoles, CompanyRoles
**Pivot:** UserSystemRoles, UserTenantRoles, UserCompanyRoles, UserCompanyMemberships
**Security:** UserSessions, UserAccessPolicies, UserBlocks, IpBlacklists

---

## 5. Güvenlik Mimarisi

### Kimlik Doğrulama Akışı

```
Login → Şifre (PBKDF2) → 2FA Açık mı?
                              │
                     ┌────────┴────────┐
                     │ EVET            │ HAYIR
                     ▼                 ▼
              TempToken (5dk)    AccessToken + RefreshToken
                     │
              Verify-2FA
              (TempToken + Kod)
                     │
              ┌──────┴──────┐
              │ BAŞARILI     │ BAŞARISIZ (max 3)
              ▼              ▼
        AccessToken +    TempToken silinir
        RefreshToken     → Tekrar login
```

### Token Yönetimi

| Token | Ömür | Saklama | Amaç |
|-------|------|---------|------|
| AccessToken | 15 dk | Client | API erişim |
| RefreshToken | 7 gün | Redis + DB (dual) | Token yenileme |
| TempToken | 5 dk | Sadece Redis | 2FA aracı |

### RefreshToken Dual Storage

- Redis → Hızlı doğrulama (her refresh'te)
- DB → Audit trail + Redis çökerse fallback
- Admin cache silerse → Kullanıcı otomatik logout

### Güvenlik Katmanları

1. **IP Blacklist** → Redis SET, O(1) lookup, tüm isteklerde
2. **Rate Limiting** → Sliding window, endpoint bazlı
3. **Device Fingerprint** → IP + UserAgent hash, token çalınma koruması
4. **IP Whitelist** → Kullanıcı bazlı, CIDR desteği
5. **Zaman Kısıtlaması** → Haftanın günleri + saat aralığı
6. **Anlık Bloke** → Redis flag, sonraki istekte 403
7. **Force Logout** → Redis + DB session silme

---

## 6. CQRS Pipeline

```
İstek → [ValidationBehavior]
              → Geçersiz? → Result<T>.ValidationFailure (422)
        → [LoggingBehavior]
              → Süre ölçüm, kullanıcı bilgisi, yapısal log
        → [AuthorizationBehavior]
              → [RequirePermission] → İzin yok? → Result<T>.Forbidden (403)
              → [RequireTenantAccess] → Tenant yok? → 400
              → [RequireCompanyAccess] → Şirket yok? → 400
        → [CachingBehavior]
              → ICacheableQuery? → Cache hit? → Cache'ten dön
        → Handler (İş mantığı)
        → [CacheInvalidationBehavior]
              → ICacheInvalidator? → Başarılı? → Cache key'leri sil
```

---

## 7. Middleware Pipeline Sırası

```
İstek geldi
  ↓ [1] ExceptionHandling      → Tüm hataları yakala → ApiResponse
  ↓ [2] RequestLogging          → HTTP istek logla (süre, IP, tenant)
  ↓ [3] IpBlacklist             → Redis SET kontrolü → 403
  ↓ [4] RateLimit               → Sliding window → 429
  ↓ [5] Authentication          → JWT doğrula (.NET built-in)
  ↓ [6] SessionValidation       → Redis: bloke? device? oturum?
  ↓ [7] Authorization           → .NET built-in
  ↓ [8] Endpoint                → Minimal API handler
```

---

## 8. EF Core Interceptor Zinciri

SaveChanges() çağrıldığında sırasıyla:

1. **AuditableInterceptor** → CreatedBy/UpdatedBy/IP otomatik doldur
2. **SoftDeleteInterceptor** → Delete → `IsDeleted = true` dönüştür
3. **AuditTrailInterceptor** → Eski/yeni değerleri Audit DB'ye yaz

---

## 9. Docker Ortamları

### Development

| Servis | Container | Port |
|--------|-----------|------|
| PostgreSQL Main | ct-db-main | 5432 |
| PostgreSQL Audit | ct-db-audit | 5433 |
| Redis | ct-redis | 6379 |
| pgAdmin | ct-pgadmin | 5050 |
| Seq | ct-seq | 5341 |

### Production

- DB port'ları DIŞARI KAPALI
- Sadece API portu açık (5000)
- Multi-stage Docker build (SDK → Runtime)
- Non-root user
