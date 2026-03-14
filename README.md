# 🏗️ CleanTenant

> **Hierarchical Multi-Tenant Enterprise Framework for .NET 10**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791)](https://www.postgresql.org)
[![Redis](https://img.shields.io/badge/Redis-7-DC382D)](https://redis.io)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

## 🎯 Nedir?

CleanTenant, **3 katmanlı hiyerarşik multi-tenant** yapıda enterprise uygulamalar için hazır bir altyapı framework'üdür.

```
🌐 System (Platform)
 └── 🏢 Tenant (örn: Mali Müşavirlik Firması)
      └── 🏭 Company (örn: Müşteri Şirketi)
           └── 👥 Members
```

## ✨ Özellikler

**Mimari:** Clean Architecture, SOLID, CQRS + MediatR, Result Pattern, Custom Mapping

**Güvenlik:** 2FA (SMS/Email/Authenticator + Fallback), JWT + TempToken, Device Fingerprint, IP Blacklist/Whitelist, Rate Limiting, Zaman Kısıtlaması, Anlık Bloke/Force Logout, RefreshToken Rotation + Dual Storage

**Hiyerarşik Yetki:** SuperAdmin → SystemUser → TenantAdmin → TenantUser → CompanyAdmin → CompanyUser → CompanyMember, Attribute bazlı pipeline authorization

**Audit:** Entity değişiklikleri (eski/yeni değer JSONB), Güvenlik logları, Yapısal loglama (Serilog + Seq), Ayrı audit veritabanı

**Altyapı:** PostgreSQL 17, Redis 7, Docker Compose (Dev/Test/Demo/Production), MudBlazor UI, Hangfire Background Jobs, Şirket bazlı yedekleme

## 🚀 Hızlı Başlangıç

```bash
# 1. Klonla
git clone https://github.com/CleanTenant/CleanTenant.git
cd CleanTenant

# 2. Ortam değişkenlerini yapılandır
cp .env.example .env

# 3. Docker servislerini başlat
cd docker && docker-compose --env-file ../.env up -d

# 4. API'yi çalıştır
cd ../src/CleanTenant.API && dotnet run
```

📖 Detaylı kurulum için [Başlangıç Rehberi](docs/getting-started.md)

## 🏛️ Proje Yapısı

```
src/
├── CleanTenant.Domain          → Entity'ler, Enum'lar (sıfır bağımlılık)
├── CleanTenant.Application     → CQRS, Behaviors, Rules, Mappings
├── CleanTenant.Shared          → DTO'lar, Sabitler (API ↔ UI ortak)
├── CleanTenant.Infrastructure  → EF Core, Redis, JWT, Interceptors
├── CleanTenant.API             → Minimal API, Middleware pipeline
└── CleanTenant.BlazorUI        → MudBlazor arayüz (gelecek faz)

tests/
├── CleanTenant.Domain.Tests
├── CleanTenant.Application.Tests
├── CleanTenant.Infrastructure.Tests
└── CleanTenant.API.IntegrationTests
```

## 🔒 Güvenlik

- Gizli bilgiler `.env` dosyasında (Git'e commit edilmez)
- Production ayarları environment variable ile override edilir
- JWT Secret minimum 64 karakter
- Tüm DateTime'lar UTC olarak saklanır
- Soft delete ile veri korunur
- Kapsamlı audit trail

## 📝 Lisans

[MIT License](LICENSE)
