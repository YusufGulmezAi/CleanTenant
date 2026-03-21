namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// TOTP işlemleri — Google/Microsoft Authenticator uyumlu.
/// QR kod üretimi, secret oluşturma, kod doğrulama ve recovery kodları.
/// </summary>
public interface ITotpService
{
    /// <summary>Yeni TOTP secret key üretir (Base32).</summary>
    string GenerateSecret();

    /// <summary>QR kod PNG byte[] üretir — Authenticator uygulamasıyla taranır.</summary>
    byte[] GenerateQrCode(string secret, string email, string issuer = "CleanTenant");

    /// <summary>TOTP kodunu doğrular (±1 pencere toleransı).</summary>
    bool Verify(string secret, string code);

    /// <summary>Kurtarma kodları üretir (XXXX-XXXX-XXXX formatında).</summary>
    IList<string> GenerateRecoveryCodes(int count = 8);
}
