# 🔧 CleanTenant — Dosya Ayrıştırma (Refactoring) Rehberi

## Bu Patch İle Yapılanlar

### ✅ Zaten Ayrıştırıldı (ZIP'te mevcut)

| Eski Dosya | Yeni Dosyalar |
|---|---|
| `Domain/Security/SecurityEntities.cs` | `UserSession.cs`, `UserAccessPolicy.cs`, `UserBlock.cs`, `IpBlacklist.cs` |
| `Application/Interfaces/IAuditDbContext.cs` | `IAuditDbContext.cs`, `AuditLog.cs`, `SecurityLog.cs` |

**ÖNEMLİ:** Eski `SecurityEntities.cs` ve eski `IAuditDbContext.cs` dosyalarını **SİLMENİZ** gerekiyor!

### 📋 Manuel Ayrıştırma Rehberi (opsiyonel)

Aşağıdaki dosyalar birden fazla class içeriyor. İsterseniz aynı mantıkla ayrıştırabilirsiniz.
Namespace'ler aynı kaldığı sürece build etkilenmez.

#### Domain Katmanı

**`Identity/Roles.cs`** → 7 dosyaya ayrılabilir:
```
Identity/
  ├── SystemRole.cs
  ├── TenantRole.cs
  ├── CompanyRole.cs
  ├── UserSystemRole.cs
  ├── UserTenantRole.cs
  ├── UserCompanyRole.cs
  └── UserCompanyMembership.cs
```

**`Identity/ApplicationUser.cs`** → 3 dosyaya ayrılabilir:
```
Identity/
  ├── ApplicationUser.cs         (ana entity)
  ├── ApplicationUserEvents.cs   (domain event record'lar)
  └── (enum'lar zaten SecurityEnums.cs'de)
```

**`Enums/SecurityEnums.cs`** → 3 dosyaya ayrılabilir:
```
Enums/
  ├── TwoFactorMethod.cs
  ├── BlockType.cs
  └── SecurityEventType.cs
```

**`Tenancy/Tenant.cs`** → 2 dosyaya ayrılabilir:
```
Tenancy/
  ├── Tenant.cs
  └── TenantEvents.cs
```

**`Tenancy/Company.cs`** → 2 dosyaya ayrılabilir:
```
Tenancy/
  ├── Company.cs
  └── CompanyEvents.cs
```

#### Application Katmanı

**`Common/Interfaces/IServices.cs`** → önerilen bölünme:
```
Common/Interfaces/
  ├── ICurrentUserService.cs
  ├── ICacheService.cs
  ├── ISessionManager.cs
  ├── IEmailService.cs
  ├── IBackupService.cs
  ├── ISmsProvider.cs
  ├── EmailMessage.cs
  ├── EmailAttachment.cs
  └── SessionInfo.cs
```

**Feature dosyaları** (RoleFeatures.cs, CompanyFeatures.cs vb.):
```
Bu dosyalar CQRS pattern'ında feature bazlı gruplanmış.
Ayrıştırma opsiyoneldir — her handler ayrı dosyaya konulabilir:

Features/Roles/
  ├── Commands/
  │   ├── CreateTenantRoleCommand.cs
  │   ├── AssignTenantRoleCommand.cs
  │   └── ...
  └── Queries/
      ├── GetTenantRolesQuery.cs
      └── ...

Ama mevcut tek dosya yapısı da kabul edilir — feature başına 1 dosya.
Tercih sizin.
```

#### Shared Katmanı

**`DTOs/Auth/AuthDtos.cs`** → 16 DTO class, önerilen:
```
DTOs/Auth/
  ├── LoginRequestDto.cs
  ├── LoginResponseDto.cs
  ├── TwoFactorVerifyDto.cs
  ├── RefreshTokenDto.cs
  ├── ChangePasswordDto.cs
  ├── Enable2FAEmailDto.cs
  ├── SetupAuthenticatorResponseDto.cs
  ├── VerifyAuthenticatorDto.cs
  ├── Disable2FADto.cs
  ├── TwoFactorStatusDto.cs
  ├── UserContextDto.cs
  ├── ConfirmEmailDto.cs
  └── ...
```

## Ayrıştırma Kuralları

1. **Namespace DEĞİŞMEZ** — dosya adı değişir, namespace aynı kalır
2. **using'ler kopyalanır** — her yeni dosyaya gerekli using'ler eklenir
3. **Eski dosya SİLİNİR** — yoksa duplicate class hatası alırsınız
4. **Build ile doğrula** — her ayrıştırmadan sonra `dotnet build`
5. **Tek seferde hepsini yapma** — gruplar halinde ilerle, her grupta build et
