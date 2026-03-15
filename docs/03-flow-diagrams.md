# 🔄 CleanTenant — Akış Diyagramları

## 1. Login Akışı (2FA + Access Policy Kontrolü)

```
[Kullanıcı]                    [API]                    [Redis]         [DB]
     │                           │                         │               │
     │── POST /auth/login ──────▶│                         │               │
     │                           │── IP Blacklist? ───────▶│               │
     │                           │── Kullanıcı bul ──────────────────────▶│
     │                           │── Şifre doğrula (PBKDF2)│               │
     │                           │                         │               │
     │                           │── Access Policy kontrol ─────────────▶│
     │                           │   Politika var mı?      │               │
     │                           │   DenyAllIps?            │               │
     │                           │   IP listesinde mi?     │               │
     │                           │   DenyAllTimes?          │               │
     │                           │   Gün/Saat uygun mu?    │               │
     │                           │   ❌ → 403 Erişim Engel │               │
     │                           │   ✅ → Devam            │               │
     │                           │                         │               │
     │       ┌── 2FA KAPALI ─────┤                         │               │
     │       │  Token üret       │── Session kaydet ──────▶│──────────────▶│
     │◀──────│  {access, refresh}│                         │               │
     │       └───────────────────┤                         │               │
     │                           │                         │               │
     │       ┌── 2FA AÇIK ───────┤                         │               │
     │       │  TempToken üret   │── TempToken kaydet ────▶│ (5dk TTL)     │
     │◀──────│  {tempToken}      │                         │               │
     │       └───────────────────┤                         │               │
     │                           │                         │               │
     │── POST /auth/verify-2fa ─▶│                         │               │
     │   {tempToken, code}       │── TOTP/Email doğrula ──▶│               │
     │                           │── TempToken SİL ───────▶│               │
     │◀── {access, refresh} ─────│── Session kaydet ──────▶│──────────────▶│
```

## 2. Token Refresh (Rotation + Dual Storage)

```
[Kullanıcı]                    [API]                    [Redis]         [DB]
     │                           │                         │               │
     │── POST /auth/refresh ────▶│                         │               │
     │   {accessToken, refresh}  │── Bloke kontrolü ──────▶│               │
     │                           │── RefreshToken hash     │               │
     │                           │── Cache'te var mı? ────▶│               │
     │                           │   YOK → DB'den kontrol ──────────────▶│
     │                           │                         │               │
     │                           │── ESKİ session REVOKE ─────────────▶│
     │                           │── YENİ token çifti üret │               │
     │                           │── YENİ session kaydet ─▶│──────────────▶│
     │◀── {newAccess, newRefresh}│                         │               │

Not: Token süresi uzatılmaz! 15dk dolunca UI refresh yapar.
Refresh Token her kullanımda yenisi üretilir (rotation).
```

## 3. Middleware Pipeline

```
HTTP İstek Geldi
     │
     ▼
[1] ExceptionHandlingMiddleware
     │ Tüm alt katman hatalarını yakalar
     ▼
[2] RequestLoggingMiddleware
     │ HTTP method, path, süre, IP loglar
     ▼
[3] IpBlacklistMiddleware
     │ Redis SET: SISMEMBER ct:blacklist:ips {ip}
     │ ❌ Blacklist'te → 403
     ▼
[4] RateLimitMiddleware
     │ Redis INCR + EXPIRE (sliding window)
     │ ❌ Limit aşıldı → 429
     ▼
[5] Authentication (.NET built-in)
     │ JWT token doğrulama, Claims çıkarma
     ▼
[6] SessionValidationMiddleware
     │ Redis'ten oturum kontrolü
     │ ❌ Bloke/Revoked/Device uyumsuz → 401/403
     ▼
[7] Authorization (.NET built-in)
     ▼
[8] Endpoint (Minimal API Handler)
```

## 4. CQRS Pipeline

```
Endpoint (ISender.Send)
     │
     ▼
[1] ValidationBehavior
     │ FluentValidation → ❌ 422
     ▼
[2] LoggingBehavior
     │ Stopwatch, >500ms → Warning
     ▼
[3] AuthorizationBehavior
     │ [RequirePermission], [RequireTenantAccess] → ❌ 403
     ▼
[4] CachingBehavior (Query)
     │ Redis cache hit → Handler ÇALIŞMAZ
     ▼
[5] Handler (İş Mantığı)
     │ Business Rules + SaveChanges
     ▼
[6] CacheInvalidationBehavior (Command)
     │ Başarılıysa cache key'leri sil
     ▼
Result<T> → ApiResponse
```

## 5. Access Policy Kontrol Akışı

```
Kullanıcı giriş yapıyor
     │
     ▼
Kullanıcının atanmış politikası var mı?
     │
  ┌──┴──┐
  │ YOK  │ VAR
  ▼      ▼
REDDET  Politika aktif mi?
(403)        │
        ┌────┴────┐
        │ HAYIR    │ EVET
        ▼          ▼
      REDDET   DenyAllIps = true?
      (403)        │
             ┌─────┴─────┐
             │ EVET       │ HAYIR
             ▼            ▼
          REDDET     IP listesinde mi? (CIDR kontrolü)
          (403)           │
                    ┌─────┴─────┐
                    │ HAYIR      │ EVET
                    ▼            ▼
                 REDDET     DenyAllTimes = true?
                 (403)           │
                           ┌─────┴─────┐
                           │ EVET       │ HAYIR
                           ▼            ▼
                        REDDET     Gün uygun mu? (Pzt=1, Paz=7)
                        (403)           │
                                  ┌─────┴─────┐
                                  │ HAYIR      │ EVET
                                  ▼            ▼
                               REDDET     Saat uygun mu?
                               (403)           │
                                         ┌─────┴─────┐
                                         │ HAYIR      │ EVET
                                         ▼            ▼
                                      REDDET      GİRİŞ OK ✅
                                      (403)

Her REDDET → SecurityLog'a detaylı kayıt
```

## 6. Settings Hiyerarşik Okuma

```
GetSetting("Jwt.AccessTokenExpirationMinutes", tenantId, companyId)
     │
     ▼
[1] Company ayarı var mı? (companyId + key)
     │
  ┌──┴──┐
  │ VAR  │ YOK
  ▼      ▼
DÖNÜŞ  [2] Tenant ayarı var mı? (tenantId + key)
              │
           ┌──┴──┐
           │ VAR  │ YOK
           ▼      ▼
         DÖNÜŞ  [3] System ayarı var mı? (key)
                       │
                    ┌──┴──┐
                    │ VAR  │ YOK
                    ▼      ▼
                  DÖNÜŞ  [4] appsettings.json fallback
                                │
                             ┌──┴──┐
                             │ VAR  │ YOK
                             ▼      ▼
                           DÖNÜŞ   null

Her seviyede Redis cache (5dk TTL)
```

## 7. E-posta Gönderim Akışı

```
emailService.SendAsync(message)
     │
     ▼
EmailLog oluştur (PostgreSQL — Status: Queued)
     │
     ├── SendInBackground = true?
     │        │
     │        ▼
     │   Hangfire.Enqueue(job)
     │   EmailLog.HangfireJobId = jobId
     │   → Hangfire Worker:
     │        SMTP gönder
     │        ✅ → EmailLog.Status = Sent
     │        ❌ → Retry (30s, 120s, 300s)
     │             3 başarısız → EmailLog.Status = Failed
     │
     └── SendInBackground = false?
              │
              ▼
         SMTP gönder (senkron)
         EmailLog.Status = Sending → Sent/Failed
```

## 8. Kullanıcı Oluşturma + Default Policy Atama

```
POST /api/users
     │
     ▼
Default politika var mı? (seviyeye göre)
     │
  ┌──┴──┐
  │ YOK  │ VAR
  ▼      ▼
400    Kullanıcı oluştur
"Default    │
 politika   ▼
 yok!"   Default politika otomatik ata
              │
              ▼
         Kullanıcı hazır (ama giriş yapamaz!)
              │
              ▼
         Admin özel politika oluşturup atar
              │
              ▼
         Kullanıcı artık giriş yapabilir ✅
```
