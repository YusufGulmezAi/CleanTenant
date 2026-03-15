

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Oturum bilgisi — CreateSessionAsync dönüş tipi.
/// </summary>
public record SessionInfo(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt
);
