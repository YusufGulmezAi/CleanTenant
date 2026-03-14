using CleanTenant.Domain.Identity;
using CleanTenant.Shared.DTOs.Users;

namespace CleanTenant.Application.Common.Mappings;

/// <summary>
/// ApplicationUser entity ↔ DTO dönüşümleri.
/// 
/// <para><b>ÖNEMLİ GÜVENLİK KURALI:</b></para>
/// Hassas alanlar (PasswordHash, AuthenticatorKey) ASLA DTO'ya aktarılmaz.
/// Mapping metodları bu alanları bilinçli olarak atlar.
/// </summary>
public static class UserMappings
{
    /// <summary>
    /// Kullanıcı entity'sini temel DTO'ya dönüştürür.
    /// Listeleme sayfaları ve özet bilgiler için kullanılır.
    /// </summary>
    public static UserDto ToDto(this ApplicationUser entity)
    {
        return new UserDto
        {
            Id = entity.Id,
            Email = entity.Email,
            FullName = entity.FullName,
            PhoneNumber = entity.PhoneNumber,
            IsActive = entity.IsActive,
            EmailConfirmed = entity.EmailConfirmed,
            TwoFactorEnabled = entity.TwoFactorEnabled,
            CreatedAt = entity.CreatedAt,
            LastLoginAt = entity.LastLoginAt,
            AvatarUrl = entity.AvatarUrl
        };
    }

    /// <summary>
    /// Kullanıcı entity'sini detay DTO'suna dönüştürür.
    /// Roller ve atamalar dahil — profil sayfası ve admin paneli için.
    /// 
    /// <para><b>DİKKAT:</b> Navigation property'ler Include ile yüklenmiş olmalıdır.</para>
    /// </summary>
    public static UserDetailDto ToDetailDto(this ApplicationUser entity)
    {
        return new UserDetailDto
        {
            // Temel bilgiler
            Id = entity.Id,
            Email = entity.Email,
            FullName = entity.FullName,
            PhoneNumber = entity.PhoneNumber,
            IsActive = entity.IsActive,
            EmailConfirmed = entity.EmailConfirmed,
            PhoneNumberConfirmed = entity.PhoneNumberConfirmed,
            TwoFactorEnabled = entity.TwoFactorEnabled,
            CreatedAt = entity.CreatedAt,
            LastLoginAt = entity.LastLoginAt,
            AvatarUrl = entity.AvatarUrl,
            PreferredLanguage = entity.PreferredLanguage,
            TimeZone = entity.TimeZone,

            // Sistem rolleri
            SystemRoles = entity.SystemRoles?
                .Select(sr => sr.SystemRole?.Name ?? "")
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList() ?? [],

            // Tenant rolleri
            TenantRoles = entity.TenantRoles?
                .Select(tr => new UserTenantRoleDto
                {
                    TenantId = tr.TenantId,
                    TenantName = tr.TenantRole?.Name ?? "",
                    RoleName = tr.TenantRole?.Name ?? "",
                    AssignedAt = tr.AssignedAt
                })
                .ToList() ?? [],

            // Şirket rolleri
            CompanyRoles = entity.CompanyRoles?
                .Select(cr => new UserCompanyRoleDto
                {
                    CompanyId = cr.CompanyId,
                    CompanyName = cr.CompanyRole?.Name ?? "",
                    RoleName = cr.CompanyRole?.Name ?? "",
                    AssignedAt = cr.AssignedAt
                })
                .ToList() ?? [],

            // Şirket üyelikleri
            CompanyMemberships = entity.CompanyMemberships?
                .Select(cm => new UserCompanyMembershipDto
                {
                    CompanyId = cm.CompanyId,
                    CompanyName = "",  // Navigation'dan alınacak
                    MembershipType = cm.MembershipType,
                    IsActive = cm.IsActive
                })
                .ToList() ?? []
        };
    }

    /// <summary>
    /// IQueryable projection — listeleme sorguları için.
    /// Roller dahil değil, sadece temel bilgiler.
    /// </summary>
    public static IQueryable<UserDto> ProjectToDto(this IQueryable<ApplicationUser> query)
    {
        return query.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            EmailConfirmed = u.EmailConfirmed,
            TwoFactorEnabled = u.TwoFactorEnabled,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            AvatarUrl = u.AvatarUrl
        });
    }
}
