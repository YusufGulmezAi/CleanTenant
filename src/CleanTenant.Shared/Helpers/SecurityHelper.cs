using System.Security.Cryptography;
using System.Text;

namespace CleanTenant.Shared.Helpers;

/// <summary>
/// Güvenlik yardımcı sınıfı — tüm katmanlardan erişilebilir kriptografi işlemleri.
/// 
/// <para><b>NEDEN SHARED KATMANINDA?</b></para>
/// HashToken gibi metodlar hem Application (handler'larda karşılaştırma)
/// hem Infrastructure (token üretimde hash'leme) katmanlarında kullanılır.
/// Shared katmanı her iki taraftan da erişilebilir tek yerdir.
/// </summary>
public static class SecurityHelper
{
    /// <summary>
    /// String'in SHA-256 hash'ini üretir.
    /// Token'lar veritabanında düz metin yerine hash'lenerek saklanır.
    /// Veritabanı sızıntısında bile token'lar kullanılamaz.
    /// </summary>
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Şifreyi PBKDF2 ile hash'ler.
    /// BCrypt yerine PBKDF2 kullanıyoruz çünkü .NET native desteği var,
    /// ek NuGet paketi gerektirmez.
    /// 
    /// <para><b>NEDEN PBKDF2?</b></para>
    /// <list type="bullet">
    ///   <item>.NET native — ek paket gerektirmez</item>
    ///   <item>NIST onaylı (SP 800-132)</item>
    ///   <item>Iteration count ile brute-force'a dayanıklılık ayarlanabilir</item>
    ///   <item>Salt ile rainbow table saldırılarına karşı koruma</item>
    /// </list>
    /// </summary>
    /// <param name="password">Düz metin şifre</param>
    /// <returns>Salt + Hash birleşimi (Base64)</returns>
    public static string HashPassword(string password)
    {
        const int iterations = 100_000;  // OWASP önerisi: minimum 100K
        const int saltSize = 16;          // 128 bit salt
        const int hashSize = 32;          // 256 bit hash

        var salt = RandomNumberGenerator.GetBytes(saltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            hashSize);

        // Salt + Hash birleştir → Base64
        var result = new byte[saltSize + hashSize];
        salt.CopyTo(result, 0);
        hash.CopyTo(result, saltSize);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Şifre doğrulama — hash'lenmiş şifre ile düz metin şifreyi karşılaştırır.
    /// </summary>
    /// <param name="password">Kullanıcının girdiği düz metin şifre</param>
    /// <param name="hashedPassword">Veritabanındaki hash'lenmiş şifre</param>
    /// <returns>true ise şifre doğru</returns>
    public static bool VerifyPassword(string password, string hashedPassword)
    {
        const int iterations = 100_000;
        const int saltSize = 16;
        const int hashSize = 32;

        try
        {
            var decoded = Convert.FromBase64String(hashedPassword);
            if (decoded.Length != saltSize + hashSize)
                return false;

            var salt = decoded[..saltSize];
            var storedHash = decoded[saltSize..];

            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                hashSize);

            // Timing-safe karşılaştırma — side-channel saldırılarına karşı
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Rastgele doğrulama kodu üretir (2FA, e-posta doğrulama vb.)
    /// </summary>
    /// <param name="length">Kod uzunluğu (varsayılan 6)</param>
    /// <returns>Sayısal kod string'i. Örnek: "847293"</returns>
    public static string GenerateVerificationCode(int length = 6)
    {
        var max = (int)Math.Pow(10, length);
        var code = RandomNumberGenerator.GetInt32(0, max);
        return code.ToString().PadLeft(length, '0');
    }

    /// <summary>
    /// Kriptografik olarak güvenli rastgele token üretir.
    /// RefreshToken, şifre sıfırlama token'ı vb. için kullanılır.
    /// </summary>
    /// <param name="byteLength">Token uzunluğu (byte). 64 byte = 86 karakter Base64.</param>
    public static string GenerateRandomToken(int byteLength = 64)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes);
    }

    // ========================================================================
    // TOTP — Time-based One-Time Password (RFC 6238)
    // Google Authenticator, Microsoft Authenticator uyumlu
    // ========================================================================

    /// <summary>
    /// Yeni Authenticator secret key üretir.
    /// Base32 encoded — QR koda gömülür.
    /// </summary>
    public static string GenerateAuthenticatorKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(20); // 160 bit
        return Base32Encode(bytes);
    }

    /// <summary>
    /// otpauth:// URI üretir — QR kod için.
    /// Google/Microsoft Authenticator bu URI'yi okur.
    /// </summary>
    /// <param name="email">Kullanıcı e-postası (Authenticator'da görünür)</param>
    /// <param name="secretKey">Base32 encoded secret</param>
    /// <param name="issuer">Uygulama adı (varsayılan: CleanTenant)</param>
    public static string GenerateAuthenticatorUri(string email, string secretKey, string issuer = "CleanTenant")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
               $"?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";
    }

    /// <summary>
    /// TOTP kodu doğrular — ±1 zaman penceresi toleransı ile (30 saniyelik).
    /// Hem geçerli, hem önceki, hem sonraki pencereyi kontrol eder.
    /// </summary>
    /// <param name="secretKey">Base32 encoded secret</param>
    /// <param name="code">Kullanıcının girdiği 6 haneli kod</param>
    /// <returns>true ise kod geçerli</returns>
    public static bool VerifyTotpCode(string secretKey, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return false;

        var keyBytes = Base32Decode(secretKey);
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // ±1 pencere toleransı (toplam 90 saniye)
        for (var i = -1; i <= 1; i++)
        {
            var timeStep = (unixTime / 30) + i;
            var expectedCode = ComputeTotp(keyBytes, timeStep);
            if (expectedCode == code)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Verilen zaman adımı için TOTP kodu hesaplar.
    /// </summary>
    public static string ComputeTotp(byte[] key, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timeBytes);

        // Dynamic truncation (RFC 4226)
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    // ── Base32 Encoding/Decoding (RFC 4648) ─────────────────────────────

    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Byte array'ı Base32 string'e çevirir.</summary>
    public static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        var bits = 0;
        var value = 0;

        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Base32Chars[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0)
            sb.Append(Base32Chars[(value << (5 - bits)) & 0x1F]);

        return sb.ToString();
    }

    /// <summary>Base32 string'i byte array'a çevirir.</summary>
    public static byte[] Base32Decode(string base32)
    {
        base32 = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        var bits = 0;
        var value = 0;

        foreach (var c in base32)
        {
            var idx = Base32Chars.IndexOf(c);
            if (idx < 0) continue;

            value = (value << 5) | idx;
            bits += 5;

            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
