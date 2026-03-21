# 🚀 CleanTenant — Başlangıç Kılavuzu

**Hedef Kitle:** Geliştiriciler, DevOps Mühendisleri
**Son Güncelleme:** Mart 2026

Bu doküman projeyi sıfırdan çalıştırmak için gereken tüm adımları içerir.

---

## 1. Ön Gereksinimler

Aşağıdaki araçlar kurulu olmalıdır:

| Araç | Versiyon | Kontrol Komutu | Yükleme |
|------|----------|----------------|---------|
| .NET SDK | 10.0+ | `dotnet --version` | https://dotnet.microsoft.com/download |
| Docker Desktop | 24+ | `docker --version` | https://www.docker.com/products/docker-desktop |
| Git | — | `git --version` | https://git-scm.com |
| EF Core Tools | — | `dotnet ef --version` | `dotnet tool install --global dotnet-ef` |

**Gmail SMTP (2FA e-posta için):**
- Gmail hesabında 2FA (iki adımlı doğrulama) aktif olmalıdır
- App Password oluşturulmalıdır: https://myaccount.google.com/apppasswords
- Normal Gmail şifresi çalışmaz — App Password gereklidir

---

## 2. Projeyi Klonla

```bash
git clone https://github.com/YusufGulmezAi/CleanTenant.git
cd CleanTenant
```

---

## 3. Ortam Değişkenlerini Ayarla

```bash
cp .env.example .env
```

`.env` dosyasını düzenleyin. Şifrelerde özel karakter (`!`, `@`, `#` vb.) kullanmayın — Docker Compose parse edemiyor.

```env
# Örnek .env
DB_PASSWORD=CleanTenantDev2026
AUDIT_DB_PASSWORD=CleanTenantAudit2026
REDIS_PASSWORD=CleanTenantRedis2026
PGADMIN_EMAIL=admin@cleantenant.com
PGADMIN_PASSWORD=admin123
```

---

## 4. Docker Servislerini Başlat

```bash
cd docker
docker-compose --env-file ../.env up -d
```

15 saniye bekleyin (PostgreSQL'in başlatılması için):

```powershell
Start-Sleep -Seconds 15    # PowerShell
# veya
sleep 15                   # Bash/Linux
```

Servislerin çalıştığını doğrulayın:

```bash
docker-compose --env-file ../.env ps
```

5 servis "Up (healthy)" durumunda olmalıdır:

| Servis | Container | Port | Doğrulama |
|--------|-----------|------|-----------|
| Ana DB | ct-db-main | 5432 | `docker exec ct-db-main pg_isready` |
| Audit DB | ct-db-audit | 5433 | `docker exec ct-db-audit pg_isready` |
| Redis | ct-redis | 6379 | `docker exec ct-redis redis-cli ping` |
| pgAdmin | ct-pgadmin | 5050 | http://localhost:5050 |
| Seq | ct-seq | 5341 | http://localhost:5341 |

---

## 5. Veritabanı Migration

```bash
cd ..  # Proje kök dizinine dön

# Ana veritabanı migration
dotnet ef migrations add InitialCreate \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext \
  --output-dir Persistence/Migrations/Main

# Audit veritabanı migration
dotnet ef migrations add InitialCreate \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context AuditDbContext \
  --output-dir Persistence/Migrations/Audit

# Migration'ları uygula
dotnet ef database update \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext

dotnet ef database update \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context AuditDbContext
```

**NOT:** Migration zaten varsa `add` komutunu atlayıp doğrudan `update` yapabilirsiniz.

---

## 6. E-posta Yapılandırması (Zorunlu)

`src/CleanTenant.API/appsettings.Development.json` dosyasında `EmailSettings` bölümünü doldurun.

**KRİTİK:** `EmailSettings` root seviyede olmalı — `CleanTenant` bloğunun İÇİNDE DEĞİL.

```json
{
  "ConnectionStrings": {
    "MainDatabase": "Host=localhost;Port=5432;...",
    "AuditDatabase": "Host=localhost;Port=5433;...",
    "Redis": "localhost:6379,password=CleanTenantRedis2026,abortConnect=false"
  },
  "CleanTenant": {
    "Jwt": { "Secret": "..." },
    "Session": {
      "ValidateDeviceFingerprint": false,
      "ValidateIpAddress": false
    }
  },
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
}
```

**Gmail App Password Alma:**
1. https://myaccount.google.com/security → "İki adımlı doğrulama" açın
2. https://myaccount.google.com/apppasswords → "Uygulama şifreleri" oluşturun
3. 16 karakterlik şifreyi boşluklu olarak `Password` alanına yapıştırın

---

## 7. Build ve Çalıştır

```bash
dotnet build
```

Tüm projeler başarıyla build olmalıdır. Ardından iki ayrı terminal açın:

**Terminal 1 — API:**
```bash
dotnet run --project src/CleanTenant.API
```

API konsol çıktısında şunları görmelisiniz:
- "Hangfire SQL objects installed."
- "Now listening on: http://localhost:54491"
- "Seed data oluşturuldu" (ilk çalıştırmada)

**Terminal 2 — Blazor UI:**
```bash
dotnet run --project src/CleanTenant.BlazorUI
```

---

## 8. Erişim URL'leri

| URL | Açıklama |
|-----|----------|
| http://localhost:54491/health | API health check |
| http://localhost:54491/scalar/v1 | API dokümantasyonu (Scalar UI — tüm endpoint'ler) |
| http://localhost:54491/hangfire | Hangfire dashboard (arka plan job'ları) |
| https://localhost:{port} | Blazor UI (port konsolda yazan değer) |
| http://localhost:5050 | pgAdmin (DB yönetimi) |
| http://localhost:5341 | Seq (log izleme) |

---

## 9. İlk Login

**Varsayılan SuperAdmin hesabı:**
- E-posta: `admin@cleantenant.com`
- Şifre: `Admin123!`
- 2FA: E-posta ile aktif — Gmail'e 6 haneli kod gelir

**Blazor UI'dan giriş:**
1. Blazor URL'ini tarayıcıda açın
2. E-posta ve şifreyi girin
3. Gmail'e gelen 6 haneli kodu girin
4. Dashboard açılır

**API'den giriş (Scalar UI veya curl):**
```bash
# 1. Login
curl -X POST http://localhost:54491/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@cleantenant.com","password":"Admin123!"}'
# → requires2FA: true, tempToken: "..." döner

# 2. 2FA doğrulama (Gmail'deki kodu girin)
curl -X POST http://localhost:54491/api/auth/verify-2fa \
  -H "Content-Type: application/json" \
  -d '{"tempToken":"...","code":"123456"}'
# → accessToken, refreshToken döner

# 3. Authenticated istek
curl http://localhost:54491/api/auth/me \
  -H "Authorization: Bearer {accessToken}"
```

---

## 10. Testleri Çalıştır

```bash
dotnet test
# 119 test, 119 başarılı, 0 başarısız
```

---

## 11. pgAdmin'de Veritabanına Bağlanma

1. http://localhost:5050 adresini açın
2. `.env`'deki `PGADMIN_EMAIL` / `PGADMIN_PASSWORD` ile giriş yapın
3. "Add New Server" tıklayın:
   - Name: `CleanTenant Main`
   - Host: `ct-db-main` (Docker container adı)
   - Port: `5432`
   - Username: `cleantenant`
   - Password: `.env`'deki `DB_PASSWORD`

---

## 12. Yaygın Sorunlar ve Çözümleri

| Sorun | Neden | Çözüm |
|-------|-------|-------|
| DB şifre hatası (FATAL: password) | Docker volume eski şifreyi tutuyor | `docker-compose down -v && docker-compose up -d` |
| Migration pending hatası | Yeni property eklendi, migration yok | `dotnet ef migrations add ... && dotnet ef database update` |
| Redis bağlantı hatası | Redis başlamamış veya şifre yanlış | `docker exec ct-redis redis-cli -a PASSWORD ping` |
| Token 401 hatası | Device fingerprint uyumsuz | `appsettings.Development.json`'da `ValidateDeviceFingerprint: false` |
| E-posta gönderilmiyor | EmailSettings yanlış yerde | Root seviyede mi kontrol et (CleanTenant altında DEĞİL) |
| Blazor login sonrası takılma | Pre-rendering sorunu | `App.razor`'da `prerender: false` olmalı |
| EF Core versiyon çakışması | Farklı paketler farklı versiyon çekiyor | `Directory.Build.props`'ta versiyon sabitlendi |
| Hangfire AddHangfire bulunamıyor | Hangfire.AspNetCore paketi eksik | csproj'a `Hangfire.AspNetCore` ekle |
| Gmail "Authentication Required" | Normal şifre kullanılmış | App Password oluştur |
| 2FA kodu hatalı | Redis'ten okuma sorunu | `docker exec ct-redis redis-cli KEYS "ct:2fa*"` ile kontrol et |

---

## 13. Migration Yönetimi (Referans)

```bash
# Yeni migration ekle
dotnet ef migrations add {İsim} \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext \
  --output-dir Persistence/Migrations/Main

# Migration uygula
dotnet ef database update \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext

# Son migration'ı geri al
dotnet ef migrations remove \
  --project src/CleanTenant.Infrastructure \
  --startup-project src/CleanTenant.API \
  --context ApplicationDbContext --force

# DB'yi sıfırla (TÜM VERİ SİLİNİR)
cd docker && docker-compose --env-file ../.env down -v && docker-compose --env-file ../.env up -d && cd ..
# 15 saniye bekle, sonra migration tekrar uygula
```

---

## 14. Geliştirme İpuçları

- **Hot Reload:** `dotnet watch run --project src/CleanTenant.API` ile otomatik yeniden başlatma
- **Seq Loglama:** http://localhost:5341 üzerinden tüm logları filtreleyebilirsiniz
- **Redis İzleme:** `docker exec ct-redis redis-cli MONITOR` ile gerçek zamanlı Redis trafiğini görün
- **Scalar API Docs:** http://localhost:54491/scalar/v1 üzerinden tüm endpoint'leri test edin
- **Hangfire Dashboard:** http://localhost:54491/hangfire üzerinden e-posta job'larını izleyin
