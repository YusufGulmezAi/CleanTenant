

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>2FA durum bilgisi.</summary>
public class TwoFactorStatusDto
{
    public bool IsEnabled { get; set; }
    public string Method { get; set; } = "None";
    public bool HasAuthenticator { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }
}
