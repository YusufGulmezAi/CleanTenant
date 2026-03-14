namespace CleanTenant.Shared.Helpers;

/// <summary>
/// DateTime dönüşüm yardımcı sınıfı — UTC ↔ Yerel saat çevrimi.
/// 
/// <para><b>ALTIN KURAL:</b></para>
/// <code>
/// Veritabanı  → Her zaman UTC saklar
/// API Katmanı → Her zaman UTC döner (ISO 8601: "2026-03-13T10:30:00Z")
/// UI Katmanı  → Kullanıcının timezone'una göre yerel saat gösterir
/// </code>
/// 
/// <para><b>NEDEN UTC?</b></para>
/// <list type="bullet">
///   <item>Farklı ülkelerdeki kullanıcılar doğru saati görür</item>
///   <item>Yaz/kış saati değişikliklerinden etkilenmez</item>
///   <item>Sıralama ve karşılaştırma her zaman tutarlıdır</item>
///   <item>Uluslararası standartlara uyar (ISO 8601)</item>
/// </list>
/// 
/// <para><b>KULLANIM:</b></para>
/// <code>
/// // API'den gelen UTC tarih:
/// var utcDate = DateTime.Parse("2026-03-13T10:30:00Z");
/// 
/// // Kullanıcının timezone'u (ApplicationUser.TimeZone):
/// var userTimeZone = "Europe/Istanbul";
/// 
/// // Yerel saate çevir (UI'da göstermek için):
/// var localDate = DateTimeHelper.ToLocal(utcDate, userTimeZone);
/// // Sonuç: 2026-03-13 13:30:00 (UTC+3)
/// 
/// // Yerel saatten UTC'ye çevir (UI'dan gelen input):
/// var utcFromLocal = DateTimeHelper.ToUtc(localDate, userTimeZone);
/// // Sonuç: 2026-03-13 10:30:00 UTC
/// </code>
/// </summary>
public static class DateTimeHelper
{
    /// <summary>Varsayılan timezone — Türkiye.</summary>
    public const string DefaultTimeZone = "Europe/Istanbul";

    /// <summary>
    /// UTC tarihini kullanıcının yerel saatine çevirir.
    /// Blazor UI'da tarih gösterirken kullanılır.
    /// </summary>
    /// <param name="utcDateTime">UTC DateTime (veritabanından gelen)</param>
    /// <param name="timeZoneId">IANA timezone ID. Örnek: "Europe/Istanbul"</param>
    /// <returns>Yerel saat olarak DateTime</returns>
    public static DateTime ToLocal(DateTime utcDateTime, string? timeZoneId = null)
    {
        var tz = GetTimeZoneInfo(timeZoneId ?? DefaultTimeZone);
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), tz);
    }

    /// <summary>
    /// Nullable UTC tarihini yerel saate çevirir.
    /// null girişte null döner.
    /// </summary>
    public static DateTime? ToLocal(DateTime? utcDateTime, string? timeZoneId = null)
    {
        return utcDateTime.HasValue
            ? ToLocal(utcDateTime.Value, timeZoneId)
            : null;
    }

    /// <summary>
    /// Yerel saatten UTC'ye çevirir.
    /// UI'dan gelen tarih input'larını veritabanına kaydetmeden önce kullanılır.
    /// </summary>
    /// <param name="localDateTime">Yerel saat (kullanıcıdan gelen)</param>
    /// <param name="timeZoneId">IANA timezone ID</param>
    /// <returns>UTC DateTime</returns>
    public static DateTime ToUtc(DateTime localDateTime, string? timeZoneId = null)
    {
        var tz = GetTimeZoneInfo(timeZoneId ?? DefaultTimeZone);
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), tz);
    }

    /// <summary>
    /// Nullable yerel saatten UTC'ye çevirir.
    /// </summary>
    public static DateTime? ToUtc(DateTime? localDateTime, string? timeZoneId = null)
    {
        return localDateTime.HasValue
            ? ToUtc(localDateTime.Value, timeZoneId)
            : null;
    }

    /// <summary>
    /// Şu anki zamanı belirtilen timezone'da döner.
    /// UI'da "Şu an saat kaç?" göstermek için.
    /// </summary>
    public static DateTime NowInTimeZone(string? timeZoneId = null)
    {
        return ToLocal(DateTime.UtcNow, timeZoneId);
    }

    /// <summary>
    /// Tarih farkını okunabilir formatta döner.
    /// "3 dakika önce", "2 saat önce", "Dün" gibi.
    /// UI'da "son giriş zamanı" gösterirken kullanılır.
    /// </summary>
    public static string ToRelativeTime(DateTime utcDateTime, string? timeZoneId = null)
    {
        var localNow = NowInTimeZone(timeZoneId);
        var localDate = ToLocal(utcDateTime, timeZoneId);
        var diff = localNow - localDate;

        return diff.TotalSeconds switch
        {
            < 60 => "Az önce",
            < 3600 => $"{(int)diff.TotalMinutes} dakika önce",
            < 86400 => $"{(int)diff.TotalHours} saat önce",
            < 172800 => "Dün",
            < 2592000 => $"{(int)diff.TotalDays} gün önce",
            _ => localDate.ToString("dd.MM.yyyy HH:mm")
        };
    }

    /// <summary>
    /// Tarihi Türkiye formatında string'e çevirir.
    /// </summary>
    public static string Format(DateTime utcDateTime, string? timeZoneId = null, string format = "dd.MM.yyyy HH:mm:ss")
    {
        return ToLocal(utcDateTime, timeZoneId).ToString(format);
    }

    /// <summary>
    /// IANA timezone ID'sinden TimeZoneInfo nesnesi oluşturur.
    /// .NET 6+ hem Windows hem Linux'ta IANA ID'leri destekler.
    /// </summary>
    private static TimeZoneInfo GetTimeZoneInfo(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Geçersiz timezone ID → varsayılan Türkiye timezone'u kullan
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZone);
        }
    }
}
