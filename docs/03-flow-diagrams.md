# 🔄 CleanTenant — Akış Diyagramları

**Hedef Kitle:** Tüm Teknik Ekip
**Son Güncelleme:** Mart 2026

Bu doküman sistemdeki kritik iş akışlarını adım adım gösterir. Her diyagram, hangi bileşenlerin hangi sırayla iletişim kurduğunu açıklar.

---

## 1. Login Akışı (Çoklu 2FA)

Bu akış bir kullanıcının sisteme giriş yapmasını, şifre doğrulamasını ve iki faktörlü doğrulama sürecini kapsar.

```
Kullanıcı                    Blazor UI                    API                     Redis           DB           SMTP
   │                            │                          │                        │              │              │
   │── E-posta + Şifre ────────▶│                          │                        │              │              │
   │                            │── POST /auth/login ─────▶│                        │              │              │
   │                            │                          │── IP Blacklist? ──────▶│              │              │
   │                            │                          │   (SISMEMBER)          │              │              │
   │                            │                          │── Kullanıcı bul ───────────────────▶│              │
   │                            │                          │── PBKDF2 doğrula       │              │              │
   │                            │                          │                        │              │              │
   │                            │                          │── 2FA aktif mi?        │              │              │
   │                            │                          │                        │              │              │
   │              ┌── 2FA KAPALI ──────────────────────────┤                        │              │              │
   │              │                                        │── Session oluştur ────▶│─────────────▶│              │
   │◀── Dashboard │◀── {accessToken, refreshToken} ───────│                        │              │              │
   │                                                       │                        │              │              │
   │              ┌── 2FA AÇIK ────────────────────────────┤                        │              │              │
   │              │                                        │── TempToken Redis'e ──▶│              │              │
   │              │   Primary = Authenticator?              │                        │              │              │
   │              │     → Kod istenmez, uygulama üretir    │                        │              │              │
   │              │   Primary = Email veya SMS?             │                        │              │              │
   │              │     → 6 haneli kod üret                │                        │              │              │
   │              │     → Kodu Redis'e kaydet ────────────▶│              │              │
   │              │     → E-posta / SMS gönder ──────────────────────────────────────────────────▶│
   │              │                                        │                        │              │              │
   │◀── 2FA Ekranı│◀── {tempToken, primary, methods[]} ──│                        │              │              │
   │                                                       │                        │              │              │
   │   (Ekranda yöntem seçimi:)                            │                        │              │              │
   │   ○ Authenticator (uygulama kodu)                     │                        │              │              │
   │   ○ E-posta ile kod gönder                            │                        │              │              │
   │   ○ SMS ile kod gönder                                │                        │              │              │
   │                                                       │                        │              │              │
   │── 6 haneli kodu gir ──────▶│                          │                        │              │              │
   │                            │── POST /verify-2fa ─────▶│                        │              │              │
   │                            │                          │── TempToken çözümle ──▶│              │              │
   │                            │                          │── Authenticator?        │              │              │
   │                            │                          │   → Otp.NET TOTP doğrula             │              │
   │                            │                          │── Email/SMS?            │              │              │
   │                            │                          │   → Redis'ten kod al ─▶│              │              │
   │                            │                          │   → Eşleşiyor mu?      │              │              │
   │                            │                          │                        │              │              │
   │                            │                          │── ❌ Yanlış kod        │              │              │
   │                            │                          │   → Deneme sayısı++    │              │              │
   │                            │                          │   → 3 deneme aşıldı?   │              │              │
   │                            │                          │     → TempToken SİL    │              │              │
   │                            │                          │     → "Tekrar login"   │              │              │
   │                            │                          │                        │              │              │
   │                            │                          │── ✅ Doğru kod         │              │              │
   │                            │                          │   → TempToken SİL ────▶│              │              │
   │                            │                          │   → Kod SİL ──────────▶│              │              │
   │                            │                          │   → Session oluştur ──▶│─────────────▶│              │
   │◀── Dashboard ──────────────│◀── {accessToken} ───────│                        │              │              │
```

**Önemli noktalar:**
- TempToken 5 dakika geçerlidir (Redis TTL)
- Yanlış kod 3 kez girilirse TempToken silinir, kullanıcı baştan login yapmalıdır
- Authenticator kodu API tarafından gönderilmez — kullanıcı uygulamasından okur
- Email/SMS kodu Redis'te saklanır, doğrulama sonrası silinir (tek kullanımlık)

---

## 2. Token Refresh (Rotation)

Access Token 15 dakikada bir süresi dolduğunda Blazor UI otomatik olarak yenileme yapar.

```
Blazor UI                          API                         Redis           DB
   │                                │                            │              │
   │── API isteği (401 döndü) ─────▶│                            │              │
   │                                │  Access Token süresi dolmuş│              │
   │◀── 401 Unauthorized ──────────│                            │              │
   │                                │                            │              │
   │── POST /auth/refresh ─────────▶│                            │              │
   │   {accessToken, refreshToken}  │                            │              │
   │                                │── RefreshToken SHA-256 ───▶│              │
   │                                │── DB'den session bul ──────────────────▶│
   │                                │── Süresi dolmuş mu?        │              │
   │                                │── Kullanıcı bloke mu? ────▶│              │
   │                                │                            │              │
   │                                │── ESKİ session REVOKE ─────────────────▶│
   │                                │── YENİ token çifti üret    │              │
   │                                │── YENİ session kaydet ────▶│─────────────▶│
   │                                │                            │              │
   │◀── {newAccessToken} ──────────│                            │              │
   │                                │                            │              │
   │── Önceki API isteğini tekrarla │                            │              │
```

**Güvenlik notu:** Refresh Token rotation sayesinde her yenileme işleminde eski refresh token geçersiz olur. Eğer bir saldırgan eski token'ı çalmışsa, gerçek kullanıcı yenileme yaptığında eski token artık çalışmaz.

---

## 3. Authenticator Kurulumu (QRCoder)

```
Kullanıcı                          API                         QRCoder
   │                                │                            │
   │── POST /2fa/setup-authenticator▶│                            │
   │                                │── Otp.NET: 20 byte key ──▶│
   │                                │── Base32 encode             │
   │                                │── otpauth:// URI oluştur   │
   │                                │── QRCoder: PNG byte[] ────▶│
   │                                │◀── PNG (300x300px)         │
   │                                │── Recovery kodları üret     │
   │                                │   (8 adet XXXX-XXXX-XXXX)  │
   │◀── {secretKey, qrCodeImage(base64), recoveryCodes[]} ─────│
   │                                │                            │
   │── Authenticator uygulamasıyla  │                            │
   │   QR kodu tarat               │                            │
   │                                │                            │
   │── POST /2fa/verify-authenticator│                           │
   │   {secretKey, code: "482937"} │                            │
   │                                │── Otp.NET: TOTP doğrula    │
   │                                │   (±30 saniye pencere)     │
   │                                │── ✅ → DB'ye key kaydet   │
   │                                │── EnabledMethods += "Auth" │
   │◀── "Authenticator aktif" ─────│                            │
```

**UI'da QR kod gösterimi:**
```html
<img src="data:image/png;base64,{qrCodeImage}" />
```

---

## 4. Erişim Politikası Kontrol Akışı

Bu akış her API isteğinde SessionValidationMiddleware tarafından çalıştırılır.

```
Kullanıcı API isteği gönderdi
        │
        ▼
Atanmış politikası var mı?
        │
   ┌────┴────┐
   │  YOK    │  VAR
   ▼         ▼
REDDET    Politika aktif mi?
(403)         │
         ┌────┴────┐
         │ HAYIR   │ EVET
         ▼         ▼
      REDDET    DenyAllIps = true?
      (403)         │
               ┌────┴────┐
               │ EVET    │ HAYIR
               ▼         ▼
            REDDET    IP adresi AllowedIpRanges listesinde mi?
            (403)     (CIDR desteği: 192.168.1.0/24)
                           │
                      ┌────┴────┐
                      │ HAYIR   │ EVET
                      ▼         ▼
                   REDDET    DenyAllTimes = true?
                   (403)         │
                            ┌────┴────┐
                            │ EVET    │ HAYIR
                            ▼         ▼
                         REDDET    Bugün AllowedDays listesinde mi?
                         (403)     (Pazartesi=1, Pazar=7)
                                        │
                                   ┌────┴────┐
                                   │ HAYIR   │ EVET
                                   ▼         ▼
                                REDDET    Şu an AllowedTimeStart-End arasında mı?
                                (403)     (örn: 08:00 — 18:00)
                                               │
                                          ┌────┴────┐
                                          │ HAYIR   │ EVET
                                          ▼         ▼
                                       REDDET    GİRİŞ İZİN VERİLDİ ✅
                                       (403)

Her REDDET → SecurityLog'a detaylı kayıt yazılır:
  - Kullanıcı ID, E-posta, IP adresi
  - Hangi kontrolde reddedildi
  - Politika detayları
```

---

## 5. Ayar Hiyerarşik Okuma Akışı

```
SettingsService.GetAsync("Jwt.AccessTokenExpirationMinutes", tenantId, companyId)
        │
        ▼
[1] Redis cache kontrol
    Key: ct:settings:{level}:{id}:{key}
        │
   ┌────┴────┐
   │  HIT    │  MISS
   ▼         ▼
Anında    [2] DB: Company ayarı var mı? (companyId + key)
döndür         │
          ┌────┴────┐
          │  VAR    │  YOK
          ▼         ▼
        Cache'e   [3] DB: Tenant ayarı var mı? (tenantId + key)
        yaz,           │
        döndür    ┌────┴────┐
                  │  VAR    │  YOK
                  ▼         ▼
                Cache'e   [4] DB: System ayarı var mı? (key only)
                yaz,           │
                döndür    ┌────┴────┐
                          │  VAR    │  YOK
                          ▼         ▼
                        Cache'e   [5] appsettings.json fallback
                        yaz,       CleanTenant:{Key} (nokta → : dönüşümü)
                        döndür     Örn: "Jwt.AccessTokenExpirationMinutes"
                                   → CleanTenant:Jwt:AccessTokenExpirationMinutes

Cache TTL: 5 dakika
```

---

## 6. E-posta Gönderim Akışı

```
EmailService.SendAsync(message)
        │
        ▼
[1] EmailLog oluştur (Audit DB)
    Status: Queued
        │
        ├── Arka planda mı? (varsayılan: evet)
        │         │
        │    ┌────┴────┐
        │    │  EVET   │  HAYIR (acil)
        │    ▼         ▼
        │  Hangfire   MailKit SMTP
        │  .Enqueue   doğrudan gönder
        │    │         │
        │    │    ┌────┴────┐
        │    │    │ BAŞARILI │ BAŞARISIZ
        │    │    ▼         ▼
        │    │  Status:   Status:
        │    │  Sent      Failed
        │    │
        │    ▼
        │  Hangfire Worker çalışır:
        │    │
        │    ▼
        │  Status: Sending
        │  AttemptCount++
        │    │
        │  MailKit SMTP ile gönder:
        │    - Host: smtp.gmail.com:587
        │    - TLS/StartTLS
        │    - Authenticate (App Password)
        │    - CC, BCC, ekler
        │    │
        │    ├── ✅ Başarılı → Status: Sent, SentAt kaydedilir
        │    │
        │    └── ❌ Başarısız → Retry mekanizması:
        │         Deneme 1: 30 saniye sonra tekrar
        │         Deneme 2: 120 saniye sonra tekrar
        │         Deneme 3: 300 saniye sonra tekrar
        │         Hepsi başarısız → Status: Failed, ErrorMessage kaydedilir
```

---

## 7. Middleware Pipeline (HTTP İstek Yaşam Döngüsü)

Bu diyagram bir HTTP isteğinin API'ye geldiğinde hangi katmanlardan sırasıyla geçtiğini gösterir.

```
İstemci (Blazor UI / Postman / curl)
        │
        ▼
┌───────────────────────────────────┐
│ [1] ExceptionHandlingMiddleware   │  try-catch ile tüm hataları yakalar
│     Hata varsa → ApiResponse<T>   │  500 Internal Server Error → JSON
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [2] RequestLoggingMiddleware      │  Serilog ile HTTP loglar
│     Method, Path, Süre, IP       │  > 500ms → Warning log
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [3] IpBlacklistMiddleware         │  Redis: SISMEMBER ct:blacklist:ips
│     Kara listede → 403 Forbidden  │  O(1) hızında kontrol
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [4] RateLimitMiddleware           │  Redis: INCR + EXPIRE (sliding window)
│     Limit aşıldı → 429 Too Many  │  IP bazlı, dakikalık pencere
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [5] Authentication (.NET)         │  JWT Bearer doğrulama
│     Claims çıkarma (UserId, etc)  │  Token geçersiz → 401
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [6] SessionValidationMiddleware   │  Redis: Oturum geçerli mi?
│     Bloke mu? Device uyumlu mu?   │  Revoke edilmiş → 401
│     Erişim politikası kontrolü    │  Bloke → 403
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [7] Authorization (.NET)          │  [Authorize] attribute kontrolü
│     İzin yok → 403 Forbidden     │  
└───────────────┬───────────────────┘
                ▼
┌───────────────────────────────────┐
│ [8] Endpoint (Minimal API)        │  MediatR.Send(command/query)
│     → CQRS Pipeline              │  → Validation → Auth → Cache → Handler
│     → Result<T> → ApiResponse<T> │  → HTTP response
└───────────────────────────────────┘
```

---

## 8. Blazor UI Login Akışı (Detaylı)

```
┌─────────────────────────────────────────────────────────┐
│                    TARAYICI                               │
│                                                          │
│  /login sayfası yüklenir (LoginLayout)                   │
│  ┌────────────────────────────────┐                      │
│  │  E-posta: [_______________]    │                      │
│  │  Şifre:   [_______________]    │                      │
│  │  [     Giriş Yap      ]       │                      │
│  └────────────────────────────────┘                      │
│           │                                              │
│           ▼ Giriş Yap tıklandı                          │
│  ApiClient → POST /auth/login                            │
│           │                                              │
│     ┌─────┴──────┐                                       │
│     │ 2FA KAPALI  │ 2FA AÇIK                             │
│     ▼             ▼                                      │
│  Token al      ┌────────────────────────────────┐        │
│  LocalStorage   │  2FA Doğrulama Ekranı          │        │
│  kaydet        │                                │        │
│  Dashboard'a    │  Primary: Authenticator ise:   │        │
│  yönlendir      │  "Authenticator kodunu giriniz"│        │
│                 │                                │        │
│                 │  Primary: Email ise:            │        │
│                 │  "E-postanıza kod gönderildi"  │        │
│                 │                                │        │
│                 │  [______] ← 6 haneli kod        │        │
│                 │  [  Doğrula  ]                  │        │
│                 │                                │        │
│                 │  ─── Başka yöntem kullan ───   │        │
│                 │  ○ E-posta ile kod gönder       │        │
│                 │  ○ SMS ile kod gönder           │        │
│                 │  ○ Authenticator kodu gir       │        │
│                 └────────────────────────────────┘        │
│                          │                               │
│                          ▼ Kod doğrulandı                │
│                 Token → LocalStorage                      │
│                 AuthState güncelle                         │
│                 Nav.NavigateTo("/")                        │
│                          │                               │
│                          ▼                               │
│  ┌──────────────────────────────────────────────────┐    │
│  │  Dashboard                                        │    │
│  │  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐    │    │
│  │  │Tenant:3│ │Firma:12│ │User:47 │ │Oturum:8│    │    │
│  │  └────────┘ └────────┘ └────────┘ └────────┘    │    │
│  └──────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```
