using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CleanTenant.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CleanTenant.Infrastructure.Security;

/// <summary>
/// Mevcut HTTP isteğinden kullanıcı bilgilerini çıkaran servis.
/// ICurrentUserService'in Infrastructure implementasyonu.
/// 
/// <para><b>DEPENDENCY INVERSION:</b></para>
/// Application katmanı ICurrentUserService interface'ini tanımlar.
/// Bu sınıf HttpContext'ten bilgileri okuyarak implemente eder.
/// Application katmanı HttpContext'i hiç bilmez.
/// 
/// <para><b>CONTEXT SWITCHING:</b></para>
/// Aktif tenant ve company bilgileri HTTP header'larından okunur:
/// <code>
/// X-Tenant-Id: {guid}     → Aktif tenant bağlamı
/// X-Company-Id: {guid}    → Aktif şirket bağlamı
/// X-Active-Role: string   → Aktif rol (CompanyUser, CompanyMember vb.)
/// </code>
/// Aynı kullanıcı farklı tarayıcı sekmelerinde farklı bağlamlarda çalışabilir.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var sub = Context?.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? Context?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public string? Email =>
        Context?.User?.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? Context?.User?.FindFirstValue(ClaimTypes.Email);

    /// <inheritdoc />
    public string? IpAddress =>
        Context?.Connection?.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public string? UserAgent =>
        Context?.Request?.Headers["User-Agent"].ToString();

    /// <inheritdoc />
    public Guid? ActiveTenantId
    {
        get
        {
            var header = Context?.Request?.Headers["X-Tenant-Id"].ToString();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public Guid? ActiveCompanyId
    {
        get
        {
            var header = Context?.Request?.Headers["X-Company-Id"].ToString();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated =>
        Context?.User?.Identity?.IsAuthenticated ?? false;
}
