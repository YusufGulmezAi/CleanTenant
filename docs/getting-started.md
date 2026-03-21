# 🚀 CleanTenant — Başlangıç Kılavuzu

## Ön Gereksinimler

- .NET 10 SDK (Stable)
- Docker Desktop
- Git
- Gmail hesabı (2FA aktif — App Password için)

## 1. Projeyi Kur

```bash
git clone https://github.com/YusufGulmezAi/CleanTenant.git
cd CleanTenant
```

## 2. Ortam Değişkenlerini Ayarla

```bash
cp .env.example .env
# .env dosyasını aç ve şifreleri doldur (özel karakter kullanma)
```

## 3. Docker Servislerini Başlat

```bash
cd docker
docker-compose --env-file ../.env up -d

# 15 saniye bekle
Start-Sleep -Seconds 15    # PowerShell
# veya: sleep 15           # Bash

docker-compose --env-file ../.env ps
```

5 servis çalışmalı:

| Servis | Port | Kontrol URL'i |
|--------|------|--------------|
| ct-db-main | 5432 | — |
| ct-db-audit | 5433 | — |
| ct-redis | 6379 | — |
| ct-pgadmin | 5050 | http://localhost:5050 |
| ct-seq | 5341 | http://localhost:5341 |

## 4. EF Core Tool Yükle (ilk seferde)

```bash
dotnet tool install --global dotnet-ef
```

## 5. Migration Oluştur ve Uygula

```bash
cd ..  # Proje kök dizinine dön

dotnet ef migrations add InitialCreate \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext \
  --output-dir Persistence/Migrations/Main

dotnet ef migrations add InitialCreate \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context AuditDbContext \
  --output-dir Persistence/Migrations/Audit

dotnet ef database update \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext

dotnet ef database update \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context AuditDbContext
```

## 6. Build ve Çalıştır

```bash
dotnet build
```

İki terminal aç:

**Terminal 1 — API:**
```bash
dotnet run --project src/CleanTenant.API
```

**Terminal 2 — Blazor UI:**
```bash
dotnet run --project src/CleanTenant.BlazorUI
```

## 7. Erişim URL'leri

| URL | Açıklama |
|-----|----------|
| http://localhost:54491/health | API health check |
| http://localhost:54491/scalar/v1 | API dokümantasyonu (Scalar UI) |
| http://localhost:54491/hangfire | Hangfire dashboard |
| https://localhost:{port} | Blazor UI (port konsolda görünür) |
| http://localhost:5050 | pgAdmin |
| http://localhost:5341 | Seq log viewer |

## 8. İlk Login

**Blazor UI'dan:**
1. E-posta: `admin@cleantenant.com`
2. Şifre: `Admin123!`
3. 2FA aktif — Gmail'inize 6 haneli kod gelir
4. Kodu girin → Dashboard açılır

**API'den (Scalar UI veya curl):**
```json
POST /api/auth/login
{"email": "admin@cleantenant.com", "password": "Admin123!"}
→ requires2FA: true, tempToken döner

POST /api/auth/verify-2fa
{"tempToken": "...", "code": "Gmail'deki 6 haneli kod"}
→ accessToken + refreshToken döner
```

## 9. Testleri Çalıştır

```bash
dotnet test
# 119 test, 119 başarılı
```

## 10. E-posta Yapılandırması (Zorunlu — 2FA için)

`src/CleanTenant.API/appsettings.Development.json` dosyasında `EmailSettings` bölümünü doldur:

```json
"EmailSettings": {
    "Enabled": true,
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your@gmail.com",
    "Password": "xxxx xxxx xxxx xxxx",
    "FromAddress": "your@gmail.com",
    "FromName": "CleanTenant",
    "UseSsl": true
}
```

**Gmail App Password alma:**
1. https://myaccount.google.com/security → 2FA aktifle
2. https://myaccount.google.com/apppasswords → App Password oluştur
3. 16 karakterlik şifreyi boşluklu olarak yapıştır

**Önemli:** `EmailSettings` root seviyede olmalı (`CleanTenant` altında DEĞİL):
```json
{
  "ConnectionStrings": { ... },
  "CleanTenant": { ... },
  "EmailSettings": { ... },    ← Burası!
  "Serilog": { ... }
}
```

## 11. pgAdmin Bağlantısı

pgAdmin: http://localhost:5050
- Login: `.env`'deki `PGADMIN_EMAIL` / `PGADMIN_PASSWORD`

Server ekleme:
- Host: `ct-db-main` (container adı)
- Port: `5432`
- Database: `cleantenant_main`
- Username: `cleantenant`
- Password: `.env`'deki `DB_PASSWORD`

## 12. Sorun Giderme

| Sorun | Çözüm |
|-------|-------|
| DB şifre hatası | Docker volume sıfırla: `docker-compose down -v && docker-compose up -d` |
| Migration pending | Migration sil + yeniden oluştur |
| Redis restart loop | `.env`'deki `REDIS_PASSWORD`'dan özel karakter kaldır |
| Token 401 | `appsettings.Development.json`'da Session.ValidateDeviceFingerprint: false |
| E-posta gönderilmiyor | `EmailSettings` root seviyede mi kontrol et |
| Blazor login sonrası takılma | `App.razor`'da prerender: false olmalı |
