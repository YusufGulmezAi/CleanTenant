# 🔄 CleanTenant — Akış Diyagramları

## 1. Login Akışı (Gerçek 2FA E-posta Kodu)

```
[Kullanıcı]                    [API]                    [Redis]         [DB]      [SMTP]
     │                           │                         │               │          │
     │── POST /auth/login ──────▶│                         │               │          │
     │   {email, password}       │── IP Blacklist? ───────▶│               │          │
     │                           │── Kullanıcı bul ──────────────────────▶│          │
     │                           │── PBKDF2 şifre doğrula  │               │          │
     │                           │── 2FA açık mı?          │               │          │
     │                           │                         │               │          │
     │      ┌── 2FA KAPALI ──────┤                         │               │          │
     │      │  Token üret        │── Session kaydet ──────▶│──────────────▶│          │
     │◀─────│  {access, refresh} │                         │               │          │
     │                           │                         │               │          │
     │      ┌── 2FA AÇIK ────────┤                         │               │          │
     │      │  6 haneli kod üret │── Kod Redis'e ─────────▶│               │          │
     │      │  TempToken üret    │── TempToken Redis'e ───▶│               │          │
     │      │                    │── E-posta gönder ────────────────────────────────▶│
     │◀─────│  {tempToken}       │                         │               │          │
     │                           │                         │               │          │
     │── POST /auth/verify-2fa ─▶│                         │               │          │
     │   {tempToken, code}       │── TempToken çözümle ───▶│               │          │
     │                           │── Redis'ten kod al ────▶│               │          │
     │                           │── Kod eşleşiyor mu?     │               │          │
     │                           │   ❌ → "Kalan deneme: X"│               │          │
     │                           │   ✅ → Kod + TempToken SİL ──────────▶│          │
     │                           │── Token üret            │               │          │
     │◀── {access, refresh} ─────│── Session kaydet ──────▶│──────────────▶│          │
```

## 2. Token Refresh (Rotation)

```
Access Token süresi: 15dk (parametrik — DB Settings)
Refresh Token süresi: 7 gün
Token her istekte uzatılMAZ — dolunca UI refresh yapar.

[Kullanıcı]                    [API]                    [Redis]         [DB]
     │                           │                         │               │
     │── POST /auth/refresh ────▶│                         │               │
     │   {accessToken, refresh}  │── RefreshToken hash     │               │
     │                           │── DB'den session bul  ──────────────▶│
     │                           │── Süresi dolmuş mu?     │               │
     │                           │── Bloke kontrolü ──────▶│               │
     │                           │── ESKİ session REVOKE ──────────────▶│
     │                           │── YENİ token çifti üret │               │
     │                           │── YENİ session kaydet ─▶│──────────────▶│
     │◀── {newAccess, newRefresh}│                         │               │
```

## 3. Authenticator 2FA Akışı

```
[Kullanıcı]                    [API]                    [Authenticator App]
     │                           │                              │
     │── POST /2fa/setup ───────▶│                              │
     │                           │── Secret key üret            │
     │                           │── QR URI oluştur             │
     │◀── {secretKey, qrCodeUrl} │                              │
     │                           │                              │
     │── QR'ı tarat ─────────────────────────────────────────▶│
     │                           │                              │
     │── POST /2fa/verify ──────▶│                              │
     │   {secretKey, code}       │── TOTP doğrula (±30sn)      │
     │                           │   ✅ → DB'ye key kaydet     │
     │◀── "2FA aktif" ──────────│                              │
     │                           │                              │
     │── POST /auth/login ──────▶│                              │
     │   (sonraki login)         │── 2FA gerekli               │
     │◀── {tempToken}            │                              │
     │                           │                              │
     │── Authenticator'dan kod al ◀────────────────────────────│
     │                           │                              │
     │── POST /auth/verify-2fa ─▶│                              │
     │   {tempToken, code}       │── TOTP doğrula (gerçek)     │
     │◀── {access, refresh} ─────│                              │
```

## 4. Middleware Pipeline

```
HTTP İstek Geldi
     │
     ▼
[1] ExceptionHandlingMiddleware
     │ Tüm alt katman hatalarını yakalar → ApiResponse
     ▼
[2] RequestLoggingMiddleware
     │ HTTP method, path, süre, IP, tenant loglar
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
     │ Redis oturum, bloke, device fingerprint kontrolü
     │ ❌ Bloke/Revoked/Device uyumsuz → 401/403
     ▼
[7] Authorization (.NET built-in)
     ▼
[8] Endpoint (Minimal API Handler)
```

## 5. Access Policy Kontrol Akışı (Açık Kapı YOK)

```
Kullanıcı giriş yapıyor
     │
     ▼
Atanmış politikası var mı?
     │
  ┌──┴──┐
  │ YOK  │ VAR
  ▼      ▼
REDDET  DenyAllIps = true?
(403)        │
       ┌─────┴─────┐
       │ EVET       │ HAYIR
       ▼            ▼
    REDDET     IP listesinde mi? (CIDR)
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
Default politika: DenyAllIps=true, DenyAllTimes=true → her şeyi reddeder
```

## 6. Settings Hiyerarşik Okuma

```
GetSetting("Jwt.AccessTokenExpirationMinutes", tenantId, companyId)
     │
     ▼
[1] Redis cache kontrol (ct:settings:{level}:{id}:{key})
     │ HIT → anında dönüş (5dk TTL)
     │ MISS ↓
     ▼
[2] Company ayarı var mı? (companyId + key)
     │ VAR → cache'e yaz, dönüş
     │ YOK ↓
     ▼
[3] Tenant ayarı var mı? (tenantId + key)
     │ VAR → cache'e yaz, dönüş
     │ YOK ↓
     ▼
[4] System ayarı var mı? (key)
     │ VAR → cache'e yaz, dönüş
     │ YOK ↓
     ▼
[5] appsettings.json → CleanTenant:{Key} (nokta→: dönüşümü)
```

## 7. E-posta Gönderim Akışı

```
emailService.SendAsync(message)
     │
     ▼
EmailLog oluştur (PostgreSQL Audit DB — Status: Queued)
     │
     ├── SendInBackground = true?
     │        ▼
     │   Hangfire.Enqueue(job)
     │   EmailLog.HangfireJobId = jobId
     │   → Hangfire Worker:
     │        MailKit SMTP gönder
     │        ✅ → EmailLog.Status = Sent
     │        ❌ → Retry (30s, 120s, 300s)
     │             3 başarısız → Status = Failed
     │
     └── SendInBackground = false?
              ▼
         MailKit SMTP gönder (senkron)
         EmailLog.Status = Sending → Sent/Failed
```

## 8. Blazor UI Login Akışı

```
[Tarayıcı]                     [Blazor Server]              [API]
     │                              │                          │
     │── /login sayfası ───────────▶│                          │
     │◀── Login formu ──────────────│                          │
     │                              │                          │
     │── E-posta + Şifre gir ─────▶│                          │
     │                              │── POST /auth/login ─────▶│
     │                              │◀── {requires2FA, temp}  │
     │◀── 2FA kod ekranı ──────────│                          │
     │                              │                          │
     │── 6 haneli kod gir ─────────▶│                          │
     │                              │── POST /verify-2fa ─────▶│
     │                              │◀── {accessToken}        │
     │                              │── LocalStorage'a kaydet  │
     │                              │── AuthState güncelle     │
     │◀── Dashboard'a yönlendir ───│                          │
```
