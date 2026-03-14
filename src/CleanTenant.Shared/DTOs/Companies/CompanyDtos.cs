namespace CleanTenant.Shared.DTOs.Companies;

/// <summary>
/// Şirket listeleme ve detay DTO'su.
/// </summary>
public class CompanyDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public bool IsActive { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>Şirketteki kullanıcı sayısı.</summary>
    public int UserCount { get; set; }

    /// <summary>Şirketteki üye sayısı.</summary>
    public int MemberCount { get; set; }
}

/// <summary>Şirket oluşturma isteği.</summary>
public class CreateCompanyDto
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}

/// <summary>Şirket güncelleme isteği. Code değiştirilemez.</summary>
public class UpdateCompanyDto
{
    public string Name { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}

/// <summary>Şirket detay DTO'su — ayarlar dahil.</summary>
public class CompanyDetailDto : CompanyDto
{
    public string? Settings { get; set; }
    public string? TenantName { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
