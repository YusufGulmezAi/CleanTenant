# 🚀 CleanTenant — Başlangıç Rehberi

## 1. Ön Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

## 2. Kurulum

```bash
git clone https://github.com/<KULLANICI>/CleanTenant.git
cd CleanTenant
```

## 3. Ortam Değişkenlerini Yapılandır

```bash
cp .env.example .env
# .env dosyasını düzenle — development için varsayılan değerleri gir
```

## 4. Docker Servislerini Başlat

```powershell
cd docker
docker-compose --env-file ../.env up -d
```

### Temizleyip Sıfırdan Başlamak İçin

```powershell
cd docker
.\cleanup.ps1
```

## 5. Servislerin Sağlığını Kontrol Et

```powershell
docker-compose --env-file ../.env ps
```

Tüm servisler "healthy" veya "running" olmalıdır.

## 6. API'yi Çalıştır

```bash
cd ../src/CleanTenant.API
dotnet run
```

## 7. Testleri Çalıştır

```bash
cd ../..
dotnet test
```

## 8. Erişim Noktaları

| Servis | URL | Açıklama |
|--------|-----|----------|
| API | https://localhost:5001 | Minimal API |
| Scalar UI | https://localhost:5001/scalar/v1 | API Dokümantasyonu |
| pgAdmin | http://localhost:5050 | PostgreSQL Yönetim |
| Seq | http://localhost:5341 | Log Paneli |

## 9. pgAdmin'de Veritabanı Bağlantısı

1. http://localhost:5050 adresine gidin
2. .env'deki PGADMIN_EMAIL / PGADMIN_PASSWORD ile giriş
3. Add New Server:
   - **Main DB**: Host=`ct-db-main`, Port=`5432`, User=`cleantenant`
   - **Audit DB**: Host=`ct-db-audit`, Port=`5432`, User=`cleantenant`

> Not: pgAdmin Docker ağı içinden bağlanır, Host olarak container adını kullanın.
