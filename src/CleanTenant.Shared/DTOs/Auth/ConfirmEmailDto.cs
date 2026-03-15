namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>E-posta doğrulama kodu onaylama isteği.</summary>
public class ConfirmEmailDto
{
    public string Email { get; set; } = default!;
    public string Code { get; set; } = default!;
}
