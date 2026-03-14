# 🔄 CleanTenant — Akış Diyagramları

## 1. Login Akışı (TempToken ile 2FA)

```
[Kullanıcı]                      [API]                    [Redis]         [DB]
     │                             │                         │               │
     │── POST /auth/login ────────▶│                         │               │
     │   {email, password}         │                         │               │
     │                             │── IP Blacklist? ───────▶│               │
     │                             │── Kullanıcı bul ───────────────────────▶│
     │                             │── Şifre doğrula (PBKDF2)│               │
     │                             │── 2FA açık mı?          │               │
     │                             │                         │               │
     │        ┌── 2FA KAPALI ──────┤                         │               │
     │        │  Token üret        │── Session kaydet ──────▶│──────────────▶│
     │◀───────│  {access, refresh} │                         │               │
     │        └────────────────────┤                         │               │
     │                             │                         │               │
     │        ┌── 2FA AÇIK ────────┤                         │               │
     │        │  TempToken üret    │── TempToken kaydet ────▶│               │
     │◀───────│  {tempToken}       │   (5dk TTL)             │               │
     │        └────────────────────┤                         │               │
     │                             │                         │               │
     │── POST /auth/verify-2fa ───▶│                         │               │
     │   {tempToken, code}         │── TempToken çözümle ───▶│               │
     │                             │── Kod doğrula           │               │
     │                             │── TempToken SİL ───────▶│               │
     │                             │── Token üret            │               │
     │◀── {access, refresh} ──────│── Session kaydet ──────▶│──────────────▶│
```

## 2. Token Refresh (Rotation + Dual Storage)

```
[Kullanıcı]                      [API]                    [Redis]         [DB]
     │                             │                         │               │
     │── POST /auth/refresh ──────▶│                         │               │
     │   {accessToken, refresh}    │                         │               │
     │                             │── Bloke kontrolü ──────▶│               │
     │                             │── RefreshToken hash     │               │
     │                             │── Cache'te var mı? ────▶│               │
     │                             │                    YOK ─┤               │
     │                             │── DB'den kontrol ──────────────────────▶│
     │                             │◀── Session bulundu ────────────────────│
     │                             │                         │               │
     │                             │── ESKİ session REVOKE ─────────────────▶│
     │                             │── YENİ token çifti üret │               │
     │                             │── YENİ session kaydet ─▶│──────────────▶│
     │◀── {newAccess, newRefresh} ─│                         │               │
```

## 3. Middleware Pipeline Akışı

```
HTTP İstek Geldi
     │
     ▼
[1] ExceptionHandlingMiddleware
     │ Tüm alt katman hatalarını yakalar
     │ Exception → ApiResponse dönüşümü
     ▼
[2] RequestLoggingMiddleware
     │ HTTP method, path, süre, IP, tenant loglar
     ▼
[3] IpBlacklistMiddleware
     │ Redis SET kontrolü: SISMEMBER ct:blacklist:ips {ip}
     │ ❌ Blacklist'te → 403 Forbidden (pipeline durur)
     ▼
[4] RateLimitMiddleware
     │ Redis INCR + EXPIRE (sliding window)
     │ ❌ Limit aşıldı → 429 Too Many Requests
     ▼
[5] Authentication (.NET built-in)
     │ JWT token doğrulama
     │ Claims çıkarma (userId, email)
     ▼
[6] SessionValidationMiddleware
     │ Redis'ten oturum kontrolü
     │ ❌ Bloke → 403
     │ ❌ Device fingerprint uyumsuz → 401
     │ ❌ Session revoked → 401
     ▼
[7] Authorization (.NET built-in)
     ▼
[8] Endpoint (Minimal API Handler)
```

## 4. CQRS Pipeline Akışı

```
Endpoint (ISender.Send)
     │
     ▼
[1] ValidationBehavior
     │ FluentValidation çalıştır
     │ ❌ Geçersiz → Result<T>.ValidationFailure (422)
     ▼
[2] LoggingBehavior
     │ Stopwatch başlat
     │ Kullanıcı/IP/Tenant bilgisi logla
     │ >500ms → Warning logu
     ▼
[3] AuthorizationBehavior
     │ [RequirePermission("tenants.create")] → İzin kontrolü
     │ [RequireTenantAccess] → X-Tenant-Id header kontrolü
     │ [RequireCompanyAccess] → X-Company-Id header kontrolü
     │ ❌ Yetkisiz → Result<T>.Forbidden (403)
     ▼
[4] CachingBehavior (sadece Query'ler)
     │ ICacheableQuery? → Redis'ten kontrol
     │ ✅ Cache hit → Handler ÇALIŞMAZ, cache'ten dön
     │ ❌ Cache miss → Handler çalışsın → Sonucu cache'e yaz
     ▼
[5] Handler (İş Mantığı)
     │ Business Rules kontrolü
     │ Entity factory method / domain method
     │ SaveChanges (Interceptor zinciri tetiklenir)
     ▼
[6] CacheInvalidationBehavior (sadece Command'lar)
     │ ICacheInvalidator? → Başarılıysa cache key'leri sil
     ▼
Endpoint'e Result<T> döner → ApiResponse'a çevrilir
```

## 5. EF Core Interceptor Zinciri

```
dbContext.SaveChangesAsync() çağrıldı
     │
     ▼
[1] AuditableInterceptor
     │ Added → CreatedBy, CreatedAt, CreatedFromIp doldur
     │ Modified → UpdatedBy, UpdatedAt, UpdatedFromIp doldur
     │ CreatedBy/CreatedAt değiştirilmesini ENGELLE
     ▼
[2] SoftDeleteInterceptor
     │ Deleted state → Modified state'e çevir
     │ IsDeleted = true, DeletedAt, DeletedBy, DeletedFromIp doldur
     │ (Fiziksel DELETE yerine UPDATE çalışır)
     ▼
[3] AuditTrailInterceptor
     │ Değişen entity'leri tara
     │ OldValues (JSON), NewValues (JSON) oluştur
     │ AffectedColumns listele
     │ Audit DB'ye AuditLog kaydı yaz
     ▼
Veritabanına SQL çalıştır
```

## 6. Kullanıcı Ekleme Akışı (Çapraz Kimlik)

```
TenantAdmin: "Kullanıcı ekle"
     │
     ▼
E-posta adresini gir: user@example.com
     │
     ▼
Sistem e-postayı arar
     │
     ├── BULUNDU (mevcut kullanıcı)
     │    │
     │    ▼
     │    UserTenantRoles tablosuna yeni satır ekle:
     │    {userId, tenantId, tenantRoleId}
     │    │
     │    ▼
     │    Redis cache temizle (roller/izinler yeniden hesaplanacak)
     │    │
     │    ▼
     │    Kullanıcı artık bu tenant'ta da yetkili
     │    (Mevcut oturumu etkilenmez, sonraki istekte yeni roller yüklenir)
     │
     └── BULUNAMADI (yeni kullanıcı)
          │
          ▼
          ApplicationUser.Create(email, fullName)
          PasswordHash = SecurityHelper.HashPassword(tempPassword)
          │
          ▼
          UserTenantRoles tablosuna satır ekle
          │
          ▼
          Davet e-postası gönder (TODO)
          │
          ▼
          Kullanıcı ilk login'de şifre değiştirmeli
```

## 7. Hiyerarşik Yetki Kontrol Akışı

```
Kullanıcı bir işlem yapmak istiyor
     │
     ▼
[1] Kullanıcı login mi? (Authentication)
     │ ❌ → 401 Unauthorized
     ▼
[2] Kullanıcı bloke mi? (Redis kontrolü)
     │ ❌ → 403 Forbidden "Hesabınız bloke edilmiştir"
     ▼
[3] İzni var mı? (Cache'ten kontrol)
     │ Redis: ct:user:{id}:permissions
     │ Cache miss → DB'den hesapla → Cache'e yaz
     │
     │ İzin kontrolü:
     │   "tenants.create" == "tenants.create" → ✅ Tam eşleşme
     │   "tenants.*" match "tenants.create"   → ✅ Wildcard
     │   "*.*" match herhangi bir izin        → ✅ FullAccess
     │ ❌ → 403 Forbidden
     ▼
[4] Hedef kullanıcıya müdahale edebilir mi? (Hiyerarşi)
     │ currentLevel > targetLevel gerekli
     │
     │ SuperAdmin (100) > SystemUser (80)     → ✅
     │ TenantAdmin (60) > TenantUser (40)     → ✅
     │ CompanyUser (10) > CompanyAdmin (20)    → ❌ ALT SEVİYE!
     │ ❌ → 403 "Sizden üst seviyeye müdahale edemezsiniz"
     ▼
İşlem gerçekleştirilir
```

## 8. Force Logout / Bloke Akışı

```
Admin: "Kullanıcıyı bloke et"
     │
     ▼
[1] Hiyerarşi kontrolü (admin > hedef kullanıcı mı?)
     │
     ▼
[2] DB'ye UserBlock kaydı yaz
     │ {userId, blockType, reason, expiresAt}
     │
     ▼
[3] Redis'e bloke flag'i yaz
     │ ct:user:{id}:blocked = true (TTL ile)
     │
     ▼
[4] Tüm oturumları sonlandır
     │ Redis: session:{id}, refresh, device SİL
     │ DB: UserSessions → IsRevoked = true
     │
     ▼
Kullanıcının sonraki isteği:
     │
     ▼
SessionValidationMiddleware → Redis kontrolü
     │ ct:user:{id}:blocked = true
     │
     ▼
403 Forbidden: "Hesabınız bloke edilmiştir"
```
