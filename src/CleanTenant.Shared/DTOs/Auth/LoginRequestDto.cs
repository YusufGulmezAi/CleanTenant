

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Login isteği.</summary>
public class LoginRequestDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;

    /// <summary>
    /// Cihaz parmak izi — UI tarafından oluşturulur.
    /// IP + UserAgent + ekran çözünürlüğü vb. hash'lenir.
    /// </summary>
    public string? DeviceFingerprint { get; set; }
}
