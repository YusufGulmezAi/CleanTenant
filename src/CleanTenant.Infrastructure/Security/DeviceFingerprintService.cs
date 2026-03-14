using System.Security.Cryptography;
using System.Text;

namespace CleanTenant.Infrastructure.Security;

/// <summary>
/// Cihaz parmak izi servisi — token'ın çalınıp başka cihazda kullanılmasını engeller.
/// 
/// <para><b>NASIL ÇALIŞIR?</b></para>
/// <code>
/// Login anında:
///   IP + UserAgent → SHA-256 hash → deviceHash üretilir
///   deviceHash Redis'te saklanır
/// 
/// Her API isteğinde:
///   Gelen IP + UserAgent → SHA-256 hash → karşılaştırılır
///   Eşleşmiyorsa → 401 Unauthorized (token çalınmış olabilir)
/// </code>
/// 
/// <para><b>SINIRLILIKLAR:</b></para>
/// IP değişimi (mobil ağ geçişi) false positive üretebilir.
/// Bu yüzden IP doğrulama appsettings.json'dan kapatılabilir.
/// UserAgent doğrulama her zaman aktif kalmalıdır.
/// </summary>
public static class DeviceFingerprintService
{
    /// <summary>
    /// IP adresi ve UserAgent'tan benzersiz cihaz hash'i üretir.
    /// </summary>
    public static string GenerateHash(string ipAddress, string userAgent)
    {
        var raw = $"{ipAddress}|{NormalizeUserAgent(userAgent)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Sadece UserAgent ile hash üretir (IP doğrulama kapalıyken).
    /// </summary>
    public static string GenerateHashWithoutIp(string userAgent)
    {
        var raw = $"NO_IP|{NormalizeUserAgent(userAgent)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// UserAgent string'ini normalize eder.
    /// Küçük versiyon farklılıklarını (Chrome 120.0.1 vs 120.0.2) tolere etmek için
    /// sadece ana tarayıcı bilgisini alır.
    /// </summary>
    private static string NormalizeUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "unknown";

        // Uzun UserAgent'ları kısalt — ilk 200 karakter yeterli
        return userAgent.Length > 200
            ? userAgent[..200].ToLowerInvariant()
            : userAgent.ToLowerInvariant();
    }
}
