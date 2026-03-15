

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Authenticator 2FA kurulum yanıtı — QR kod bilgileri.</summary>
public class SetupAuthenticatorResponseDto
{
    /// <summary>Base32 encoded secret key — elle girmek için.</summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>otpauth:// URI — QR kod oluşturmak için.</summary>
    public string QrCodeUri { get; set; } = default!;
	// Mevcut property'lerin altına ekle:
	public string QrCodeImageUrl { get; set; } = default!;
}
