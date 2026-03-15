# 🚀 CleanTenant — Başlangıç Kılavuzu

## Ön Gereksinimler

- .NET 10 SDK
- Docker Desktop
- Git

## 1. Projeyi Kur

```bash
git clone https://github.com/YusufGulmezAi/CleanTenant.git
cd CleanTenant
```

## 2. Ortam Değişkenlerini Ayarla

```bash
cp .env.example .env
# .env dosyasını aç ve şifreleri doldur
```

## 3. Docker Servislerini Başlat

```bash
cd docker
docker-compose --env-file ../.env up -d
# 15 saniye bekle
docker-compose --env-file ../.env ps
```

5 servis çalışmalı: `ct-db-main`, `ct-db-audit`, `ct-redis`, `ct-pgadmin`, `ct-seq`

## 4. Migration Oluştur ve Uygula

```bash
cd ..
dotnet ef migrations add InitialCreate --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext --output-dir Persistence/Migrations/Main
dotnet ef migrations add InitialCreate --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext --output-dir Persistence/Migrations/Audit
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context ApplicationDbContext
dotnet ef database update --project src/CleanTenant.Infrastructure --startup-project src/CleanTenant.API --context AuditDbContext
```

## 5. Build ve Çalıştır

```bash
dotnet build
dotnet run --project src/CleanTenant.API
```

## 6. İlk Erişim

| URL | Açıklama |
|-----|----------|
| http://localhost:54491/health | Health check |
| http://localhost:54491/scalar/v1 | API dokümantasyonu (Dark mode) |
| http://localhost:5050 | pgAdmin |
| http://localhost:5341 | Seq log viewer |
| http://localhost:54491/hangfire | Hangfire dashboard |

## 7. İlk Login

```json
POST /api/auth/login
{
  "email": "admin@cleantenant.com",
  "password": "Admin123!"
}
```

SuperAdmin 2FA aktif — `verify-2fa` ile devam et (dev kodu: `123456`).

## 8. Testleri Çalıştır

```bash
dotnet test
# 99 test, 99 başarılı
```

## 9. E-posta Yapılandırması (Opsiyonel)

`appsettings.Development.json` → `EmailSettings` bölümünü doldur:

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

Gmail App Password: https://myaccount.google.com/apppasswords
