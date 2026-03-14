using CleanTenant.Domain.Tenancy;
using CleanTenant.Shared.DTOs.Companies;

namespace CleanTenant.Application.Common.Mappings;

/// <summary>
/// Company entity ↔ DTO dönüşümleri.
/// </summary>
public static class CompanyMappings
{
    public static CompanyDto ToDto(this Company entity)
    {
        return new CompanyDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            TaxNumber = entity.TaxNumber,
            TaxOffice = entity.TaxOffice,
            IsActive = entity.IsActive,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            Address = entity.Address,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };
    }

    public static CompanyDetailDto ToDetailDto(this Company entity)
    {
        return new CompanyDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Code = entity.Code,
            TaxNumber = entity.TaxNumber,
            TaxOffice = entity.TaxOffice,
            IsActive = entity.IsActive,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            Address = entity.Address,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            Settings = entity.Settings,
            TenantName = entity.Tenant?.Name,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <summary>
    /// IQueryable projection — veritabanından sadece gerekli kolonları çeker.
    /// </summary>
    public static IQueryable<CompanyDto> ProjectToDto(this IQueryable<Company> query)
    {
        return query.Select(c => new CompanyDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            Name = c.Name,
            Code = c.Code,
            TaxNumber = c.TaxNumber,
            TaxOffice = c.TaxOffice,
            IsActive = c.IsActive,
            ContactEmail = c.ContactEmail,
            ContactPhone = c.ContactPhone,
            Address = c.Address,
            CreatedAt = c.CreatedAt,
            CreatedBy = c.CreatedBy
        });
    }

    public static IQueryable<CompanyDetailDto> ProjectToDetailDto(this IQueryable<Company> query)
    {
        return query.Select(c => new CompanyDetailDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            Name = c.Name,
            Code = c.Code,
            TaxNumber = c.TaxNumber,
            TaxOffice = c.TaxOffice,
            IsActive = c.IsActive,
            ContactEmail = c.ContactEmail,
            ContactPhone = c.ContactPhone,
            Address = c.Address,
            CreatedAt = c.CreatedAt,
            CreatedBy = c.CreatedBy,
            Settings = c.Settings,
            TenantName = c.Tenant.Name,
            UpdatedBy = c.UpdatedBy,
            UpdatedAt = c.UpdatedAt
        });
    }
}
