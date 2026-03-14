namespace CleanTenant.Shared.DTOs.Tenants;

/// <summary>
/// Tenant listeleme ve detay görüntüleme için DTO.
/// Entity'den farklı olarak sadece UI'ın ihtiyacı olan alanları taşır.
/// Hassas bilgiler (Settings JSON vb.) burada yer almaz.
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Identifier { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public bool IsActive { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>Bu tenant'a ait şirket sayısı (listeleme için).</summary>
    public int CompanyCount { get; set; }
}

/// <summary>
/// Tenant oluşturma isteği DTO'su.
/// API endpoint'ine gelen JSON body bu sınıfa deserialize edilir.
/// FluentValidation ile doğrulanır.
/// </summary>
public class CreateTenantDto
{
    public string Name { get; set; } = default!;
    public string Identifier { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}

/// <summary>
/// Tenant güncelleme isteği DTO'su.
/// Identifier değiştirilemez — sadece Name ve iletişim bilgileri güncellenebilir.
/// </summary>
public class UpdateTenantDto
{
    public string Name { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}

/// <summary>
/// Tenant detay DTO'su — ayarlar dahil, sadece yetkili kullanıcılara döner.
/// </summary>
public class TenantDetailDto : TenantDto
{
    public string? Settings { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
