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
}
