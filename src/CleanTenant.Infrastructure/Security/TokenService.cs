using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CleanTenant.Infrastructure.Security;

/// <summary>
/// JWT token üretim ve doğrulama servisi.
/// 
/// <para><b>TOKEN TİPLERİ:</b></para>
/// <list type="bullet">
///   <item><b>AccessToken:</b> Kısa ömürlü JWT (15dk). Her API isteğinde gönderilir.</item>
///   <item><b>RefreshToken:</b> Uzun ömürlü rastgele string (7 gün). AccessToken yenilemek için.</item>
///   <item><b>TempToken:</b> Çok kısa ömürlü GUID (5dk). 2FA doğrulama sırasında kullanılır.</item>
/// </list>
/// 
/// <para><b>GÜVENLİK KATMANLARI:</b></para>
/// <list type="number">
///   <item>AccessToken'da minimum bilgi taşınır (userId, email) — hassas veri yok</item>
///   <item>RefreshToken hash'lenerek saklanır — DB sızıntısında bile kullanılamaz</item>
///   <item>TempToken sadece /verify-2fa endpoint'inde geçerlidir</item>
///   <item>Token rotation: Her refresh'te eski token revoke edilir</item>
/// </list>
/// </summary>
public class TokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenDays;

    public TokenService(IConfiguration configuration)
    {
        var jwtConfig = configuration.GetSection("CleanTenant:Jwt");
        _secret = jwtConfig["Secret"] ?? throw new InvalidOperationException("JWT Secret yapılandırılmamış.");
        _issuer = jwtConfig["Issuer"] ?? "CleanTenant";
        _audience = jwtConfig["Audience"] ?? "CleanTenant";
        _accessTokenMinutes = int.Parse(jwtConfig["AccessTokenExpirationMinutes"] ?? "15");
        _refreshTokenDays = int.Parse(jwtConfig["RefreshTokenExpirationDays"] ?? "7");
    }

    /// <summary>
    /// JWT Access Token üretir.
    /// Token'da minimum bilgi taşınır — detaylı rol/izin bilgileri Redis cache'tedir.
    /// </summary>
    public TokenResult GenerateAccessToken(Guid userId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResult(tokenString, expiresAt);
    }

    /// <summary>
    /// Refresh Token üretir — kriptografik olarak güvenli rastgele string.
    /// JWT DEĞİLDİR — sadece opaque bir token'dır.
    /// </summary>
    public TokenResult GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var token = Convert.ToBase64String(randomBytes);
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays);

        return new TokenResult(token, expiresAt);
    }

    /// <summary>
    /// 2FA TempToken üretir — GUID tabanlı, 5 dakika ömürlü.
    /// Gerçek bir JWT değildir. Redis'te userId ile eşleştirilir.
    /// Sadece /verify-2fa endpoint'inde kabul edilir.
    /// </summary>
    public TempTokenResult GenerateTempToken(Guid userId, TimeSpan? duration = null)
    {
        var tempToken = Guid.NewGuid().ToString("N");  // 32 karakter hex string
        var expiresAt = DateTime.UtcNow.Add(duration ?? TimeSpan.FromMinutes(5));

        return new TempTokenResult(tempToken, userId, expiresAt);
    }

    /// <summary>
    /// JWT Access Token'dan claims çıkarır.
    /// Süresi dolmuş token'lar için de çalışır (refresh akışında gerekli).
    /// </summary>
    public ClaimsPrincipal? GetPrincipalFromToken(string token, bool validateLifetime = true)
    {
        var tokenValidationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = validateLifetime,  // Refresh'te false — süresi dolmuş token kabul edilir
            ClockSkew = TimeSpan.Zero  // Saat farkı toleransı yok
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, tokenValidationParams, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return null;  // Algoritma uyumsuz — sahte token
            }

            return principal;
        }
        catch
        {
            return null;  // Geçersiz token
        }
    }

    /// <summary>
    /// String'in SHA-256 hash'ini üretir.
    /// Shared/Helpers/SecurityHelper'a delegasyon yapar — tek kaynak noktası.
    /// </summary>
    public static string HashToken(string token)
        => CleanTenant.Shared.Helpers.SecurityHelper.HashToken(token);
}

/// <summary>Access/Refresh token sonucu.</summary>
public record TokenResult(string Token, DateTime ExpiresAt);

/// <summary>TempToken sonucu — 2FA akışı için.</summary>
public record TempTokenResult(string TempToken, Guid UserId, DateTime ExpiresAt);
