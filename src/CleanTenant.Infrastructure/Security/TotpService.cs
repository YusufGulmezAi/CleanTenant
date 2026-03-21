using CleanTenant.Application.Common.Interfaces;
using OtpNet;
using QRCoder;

namespace CleanTenant.Infrastructure.Security;

/// <summary>
/// TOTP işlemleri — Google/Microsoft Authenticator uyumlu.
/// QRCoder ile PNG byte[] üretimi, Otp.NET ile doğrulama.
/// </summary>
internal sealed class TotpService : ITotpService
{
    /// <summary>20 byte'lık rastgele key üretir, Base32 olarak döner.</summary>
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    /// <summary>QR kod PNG byte[] üretir — Authenticator uygulamasıyla taranır.</summary>
    public byte[] GenerateQrCode(string secret, string email, string issuer = "CleanTenant")
    {
        var otpUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}" +
                     $":{Uri.EscapeDataString(email)}" +
                     $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}" +
                     "&digits=6&period=30&algorithm=SHA1";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);

        return qrCode.GetGraphic(20);
    }

    /// <summary>TOTP kodunu doğrular — ±1 pencere (30sn önceki ve sonraki kod da kabul).</summary>
    public bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        try
        {
            var keyBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(keyBytes);

            return totp.VerifyTotp(
                code,
                out _,
                new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Kurtarma kodları üretir — XXXX-XXXX-XXXX formatında.</summary>
    public IList<string> GenerateRecoveryCodes(int count = 8)
    {
        var codes = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            var bytes = new byte[10];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

            var hex = Convert.ToHexString(bytes);
            codes.Add($"{hex[..4]}-{hex[4..8]}-{hex[8..12]}");
        }

        return codes;
    }
}
