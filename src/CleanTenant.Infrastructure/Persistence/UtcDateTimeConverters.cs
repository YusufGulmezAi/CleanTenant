using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CleanTenant.Infrastructure.Persistence;

/// <summary>
/// DateTime → UTC dönüştürücü.
/// 
/// <para><b>NEDEN GEREKLİ?</b></para>
/// Npgsql 6+ sürümünde <c>timestamptz</c> kolonlarına yazarken
/// <c>DateTime.Kind</c> = <c>Utc</c> ZORUNLUDUR. Aksi halde exception fırlatır.
/// 
/// Bu converter iki yönlü çalışır:
/// <list type="bullet">
///   <item><b>Yazarken:</b> Kind belirtilmemişse (Unspecified) → UTC olarak işaretler</item>
///   <item><b>Okurken:</b> DB'den gelen değere Kind = Utc ekler</item>
/// </list>
/// 
/// <para><b>ConfigureConventions ile kullanımı:</b></para>
/// Tüm DateTime property'lerine otomatik uygulanır — entity bazında
/// tek tek yapılandırmaya gerek yoktur.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            // Veritabanına yazarken: UTC olduğundan emin ol
            v => v.Kind == DateTimeKind.Utc
                ? v
                : DateTime.SpecifyKind(v, DateTimeKind.Utc),

            // Veritabanından okurken: UTC olarak işaretle
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

/// <summary>
/// Nullable DateTime → UTC dönüştürücü.
/// DateTime? property'leri için aynı mantık — null değerler olduğu gibi geçer.
/// </summary>
public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Utc
                    ? v
                    : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                : v,

            v => v.HasValue
                ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                : v)
    {
    }
}
