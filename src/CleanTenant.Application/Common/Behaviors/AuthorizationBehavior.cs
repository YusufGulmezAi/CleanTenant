using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Common.Behaviors;

/// <summary>
/// Yetkilendirme marker attribute'u.
/// Handler'a ulaşmadan önce belirtilen izinlerin kontrolünü tetikler.
/// 
/// <para><b>KULLANIM:</b></para>
/// <code>
/// // Command/Query sınıfına attribute olarak ekle:
/// [RequirePermission("tenants.create")]
/// public record CreateTenantCommand(...) : IRequest&lt;Result&lt;TenantDto&gt;&gt;;
/// 
/// // Birden fazla izin (hepsi gerekli):
/// [RequirePermission("tenants.read")]
/// [RequirePermission("companies.read")]
/// public record GetTenantWithCompaniesQuery(...) : IRequest&lt;...&gt;;
/// 
/// // Tenant bağlamı zorunlu:
/// [RequireTenantAccess]
/// public record GetCompaniesQuery(...) : IRequest&lt;...&gt;;
/// 
/// // Şirket bağlamı zorunlu:
/// [RequireCompanyAccess]
/// public record GetInvoicesQuery(...) : IRequest&lt;...&gt;;
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute
{
    public string Permission { get; }

    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
    }
}

/// <summary>
/// Aktif tenant bağlamı zorunlu kılan attribute.
/// X-Tenant-Id header'ının gönderilmiş olması gerekir.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequireTenantAccessAttribute : Attribute;

/// <summary>
/// Aktif şirket bağlamı zorunlu kılan attribute.
/// X-Tenant-Id ve X-Company-Id header'larının gönderilmiş olması gerekir.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequireCompanyAccessAttribute : Attribute;

/// <summary>
/// Yetkilendirme pipeline behavior'ı — attribute tabanlı izin kontrolü.
/// 
/// <para><b>ÇALIŞMA SIRASI:</b></para>
/// <code>
/// İstek → [Validation] → [Logging] → [Authorization ← BU] → [Caching] → Handler
/// </code>
/// 
/// <para><b>HİYERARŞİK KONTROL:</b></para>
/// <list type="number">
///   <item>Kullanıcı login mi? (Authentication)</item>
///   <item>RequirePermission varsa → izin kontrolü (cache'ten)</item>
///   <item>RequireTenantAccess varsa → tenant erişim kontrolü</item>
///   <item>RequireCompanyAccess varsa → şirket erişim kontrolü</item>
/// </list>
/// 
/// Tüm kontroller geçerse handler'a devam eder.
/// Herhangi biri başarısız olursa Result.Forbidden/Unauthorized döner.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly Common.Rules.AuthorizationRules _authRules;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        ICurrentUserService currentUser,
        Common.Rules.AuthorizationRules authRules,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUser = currentUser;
        _authRules = authRules;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);

        // Attribute'ları oku
        var permissionAttributes = requestType
            .GetCustomAttributes(typeof(RequirePermissionAttribute), true)
            .Cast<RequirePermissionAttribute>()
            .ToList();

        var requireTenant = requestType
            .GetCustomAttributes(typeof(RequireTenantAccessAttribute), true)
            .Any();

        var requireCompany = requestType
            .GetCustomAttributes(typeof(RequireCompanyAccessAttribute), true)
            .Any();

        // Hiçbir yetki attribute'u yoksa direkt geç
        if (permissionAttributes.Count == 0 && !requireTenant && !requireCompany)
            return await next();

        // Authentication kontrolü
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            _logger.LogWarning(
                "[AUTH] {RequestName} — Kimlik doğrulaması yapılmamış kullanıcı engellendi",
                requestType.Name);

            return CreateFailureResponse(
                Result<object>.Unauthorized("Bu işlem için oturum açmanız gerekmektedir."));
        }

        // Permission kontrolü
        foreach (var attr in permissionAttributes)
        {
            var permResult = await _authRules.EnsureHasPermissionAsync(attr.Permission, cancellationToken);

            if (permResult.IsFailure)
            {
                _logger.LogWarning(
                    "[AUTH] {RequestName} — İzin reddedildi: {Permission} | User: {UserId}",
                    requestType.Name, attr.Permission, _currentUser.UserId);

                return CreateFailureResponse(permResult);
            }
        }

        // Tenant erişim kontrolü
        if (requireTenant)
        {
            if (_currentUser.ActiveTenantId is null)
            {
                return CreateFailureResponse(
                    Result<object>.Failure("Bu işlem için aktif tenant seçilmelidir (X-Tenant-Id header).", 400));
            }

            var tenantResult = await _authRules.EnsureHasTenantAccessAsync(
                _currentUser.ActiveTenantId.Value, cancellationToken);

            if (tenantResult.IsFailure)
            {
                _logger.LogWarning(
                    "[AUTH] {RequestName} — Tenant erişimi reddedildi: {TenantId} | User: {UserId}",
                    requestType.Name, _currentUser.ActiveTenantId, _currentUser.UserId);

                return CreateFailureResponse(tenantResult);
            }
        }

        // Company erişim kontrolü
        if (requireCompany)
        {
            if (_currentUser.ActiveTenantId is null || _currentUser.ActiveCompanyId is null)
            {
                return CreateFailureResponse(
                    Result<object>.Failure(
                        "Bu işlem için aktif tenant ve şirket seçilmelidir " +
                        "(X-Tenant-Id ve X-Company-Id header).", 400));
            }

            var companyResult = await _authRules.EnsureHasCompanyAccessAsync(
                _currentUser.ActiveTenantId.Value,
                _currentUser.ActiveCompanyId.Value,
                cancellationToken);

            if (companyResult.IsFailure)
            {
                _logger.LogWarning(
                    "[AUTH] {RequestName} — Şirket erişimi reddedildi: {CompanyId} | User: {UserId}",
                    requestType.Name, _currentUser.ActiveCompanyId, _currentUser.UserId);

                return CreateFailureResponse(companyResult);
            }
        }

        // Tüm kontroller geçti → handler'a devam
        return await next();
    }

    /// <summary>
    /// Result&lt;T&gt; tipindeki hata sonucunu TResponse tipine dönüştürür.
    /// Reflection ile Result&lt;T&gt;.Failure veya Unauthorized çağrılır.
    /// </summary>
    private static TResponse CreateFailureResponse<T>(Result<T> failureResult)
    {
        var responseType = typeof(TResponse);

        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = responseType.GetGenericArguments()[0];
            var method = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(failureResult.StatusCode == 401
                    ? nameof(Result<object>.Unauthorized)
                    : nameof(Result<object>.Forbidden));

            if (method is not null)
            {
                var result = method.Invoke(null, [failureResult.Error ?? "Yetkiniz yok."]);
                return (TResponse)result!;
            }
        }

        throw new UnauthorizedAccessException(failureResult.Error);
    }
}
