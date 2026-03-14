using CleanTenant.Domain.Tenancy;
using CleanTenant.Shared.DTOs.Tenants;

namespace CleanTenant.Application.Common.Mappings;

/// <summary>
/// Tenant entity ↔ DTO dönüşümleri.
/// 
/// <para><b>CUSTOM MAPPING YAKLAŞIMI:</b></para>
/// AutoMapper/Mapster yerine extension method'lar kullanıyoruz.
/// 
/// <list type="bullet">
///   <item><b>Açıklık:</b> Her property'nin nereden geldiği tek bakışta görülür</item>
///   <item><b>Derleme güvenliği:</b> Property adı değişirse derleme hatası verir</item>
///   <item><b>Performans:</b> Reflection yok, doğrudan property erişimi</item>
///   <item><b>Debug:</b> Breakpoint koyulabilir, adım adım izlenebilir</item>
/// </list>
/// 
/// <para><b>İKİ TİP MAPPİNG:</b></para>
/// <list type="number">
///   <item>
///     <b>ToDto():</b> Bellekteki entity'yi DTO'ya çevirir.
///     Entity zaten yüklendiğinde kullanılır (Create/Update sonrası).
///   </item>
///   <item>
///     <b>ProjectToDto():</b> IQueryable üzerinde LINQ projection yapar.
///     EF Core bunu SQL SELECT ifadesine çevirir — sadece gerekli
///     kolonlar veritabanından çekilir (performans).
///   </item>
/// </list>
/// </summary>
public static class TenantMappings
{
    // ========================================================================
    // ENTITY → DTO (bellekte dönüşüm)
    // ========================================================================

    /// <summary>
    /// Tenant entity'sini DTO'ya dönüştürür.
    /// Create/Update sonrası entity zaten bellekte olduğunda kullanılır.
    /// </summary>
    public static TenantDto ToDto(this Tenant entity)
    {
        return new TenantDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Identifier = entity.Identifier,
            TaxNumber = entity.TaxNumber,
            IsActive = entity.IsActive,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            CompanyCount = entity.Companies?.Count ?? 0
        };
    }

    /// <summary>
    /// Tenant entity'sini detay DTO'suna dönüştürür (Settings dahil).
    /// </summary>
    public static TenantDetailDto ToDetailDto(this Tenant entity)
    {
        return new TenantDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Identifier = entity.Identifier,
            TaxNumber = entity.TaxNumber,
            IsActive = entity.IsActive,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            CompanyCount = entity.Companies?.Count ?? 0,
            Settings = entity.Settings,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }

    // ========================================================================
    // IQueryable PROJECTION (veritabanı seviyesinde dönüşüm)
    // ========================================================================

    /// <summary>
    /// IQueryable üzerinde projection yapar.
    /// 
    /// <para><b>NEDEN ProjectToDto?</b></para>
    /// <code>
    /// // ❌ KÖTÜ — tüm kolonlar çekilir, bellekte dönüştürülür
    /// var tenants = await db.Tenants.ToListAsync();
    /// var dtos = tenants.Select(t => t.ToDto());
    /// 
    /// // ✅ İYİ — sadece gerekli kolonlar SQL'de SELECT edilir
    /// var dtos = await db.Tenants.ProjectToDto().ToListAsync();
    /// 
    /// // Üretilen SQL:
    /// // SELECT t."Id", t."Name", t."Identifier", t."IsActive", ...
    /// // FROM "Tenants" AS t
    /// // (Settings, PasswordHash gibi gereksiz kolonlar ÇEKİLMEZ)
    /// </code>
    /// </para>
    /// </summary>
    public static IQueryable<TenantDto> ProjectToDto(this IQueryable<Tenant> query)
    {
        return query.Select(t => new TenantDto
        {
            Id = t.Id,
            Name = t.Name,
            Identifier = t.Identifier,
            TaxNumber = t.TaxNumber,
            IsActive = t.IsActive,
            ContactEmail = t.ContactEmail,
            ContactPhone = t.ContactPhone,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            CompanyCount = t.Companies.Count
        });
    }

    /// <summary>
    /// Detay DTO'su projection'ı — Settings dahil.
    /// Sadece yetkili kullanıcıların eriştiği detay endpointlerinde kullanılır.
    /// </summary>
    public static IQueryable<TenantDetailDto> ProjectToDetailDto(this IQueryable<Tenant> query)
    {
        return query.Select(t => new TenantDetailDto
        {
            Id = t.Id,
            Name = t.Name,
            Identifier = t.Identifier,
            TaxNumber = t.TaxNumber,
            IsActive = t.IsActive,
            ContactEmail = t.ContactEmail,
            ContactPhone = t.ContactPhone,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            CompanyCount = t.Companies.Count,
            Settings = t.Settings,
            UpdatedBy = t.UpdatedBy,
            UpdatedAt = t.UpdatedAt
        });
    }
}
