using System.Text.Json.Serialization;

namespace CleanTenant.Shared.DTOs.Common;

/// <summary>
/// Tüm API endpoint'lerinin standart dönüş tipi.
/// 
/// <para><b>NEDEN STANDART YANIT MODELİ?</b></para>
/// API'den dönen her yanıt aynı yapıda olmalıdır. Bu sayede:
/// <list type="bullet">
///   <item>UI tarafı her zaman aynı yapıyı parse eder (tutarlılık)</item>
///   <item>Hata yönetimi standartlaşır (errors listesi)</item>
///   <item>Başarı/başarısızlık durumu açıkça belirtilir (isSuccess)</item>
///   <item>Ek metadata taşınabilir (timestamp, pagination vb.)</item>
/// </list>
/// 
/// <para><b>KULLANIM ÖRNEKLERİ:</b></para>
/// <code>
/// // Başarılı yanıt (data ile)
/// ApiResponse&lt;TenantDto&gt;.Success(tenantDto, "Tenant başarıyla oluşturuldu.");
/// 
/// // Başarılı yanıt (sadece mesaj)
/// ApiResponse.Success("İşlem tamamlandı.");
/// 
/// // Hata yanıtı (tek hata)
/// ApiResponse.Failure("Tenant bulunamadı.", 404);
/// 
/// // Doğrulama hatası (birden fazla hata)
/// ApiResponse.ValidationFailure(["İsim boş olamaz.", "E-posta geçersiz."]);
/// </code>
/// </summary>
/// <typeparam name="T">Yanıt verisi tipi. Veri yoksa object kullanılır.</typeparam>
public class ApiResponse<T>
{
    /// <summary>İşlem başarılı mı?</summary>
    public bool IsSuccess { get; set; }

    /// <summary>HTTP durum kodu (200, 400, 404, 500 vb.)</summary>
    public int StatusCode { get; set; }

    /// <summary>Kullanıcıya gösterilecek mesaj.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// Yanıt verisi.
    /// Başarılı işlemlerde doldurulur. Hata durumunda null olabilir.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Hata mesajları listesi.
    /// Doğrulama hataları gibi birden fazla hata varsa burada listelenir.
    /// Başarılı işlemlerde null veya boş olur.
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Yanıt oluşturulma zamanı (UTC).
    /// Performans izleme ve debug için faydalıdır.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // ========================================================================
    // FACTORY METHODS — Yanıt oluşturma
    // Static factory method'lar ile tutarlı yanıt üretimi sağlanır.
    // ========================================================================

    /// <summary>Başarılı yanıt (data ile).</summary>
    public static ApiResponse<T> Success(T data, string? message = null, int statusCode = 200)
    {
        return new ApiResponse<T>
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Message = message,
            Data = data
        };
    }

    /// <summary>Başarılı oluşturma yanıtı (201 Created).</summary>
    public static ApiResponse<T> Created(T data, string? message = null)
    {
        return Success(data, message, 201);
    }

    /// <summary>Hata yanıtı.</summary>
    public static ApiResponse<T> Failure(string message, int statusCode = 400)
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Message = message,
            Errors = [message]
        };
    }

    /// <summary>Doğrulama hatası yanıtı (birden fazla hata).</summary>
    public static ApiResponse<T> ValidationFailure(List<string> errors)
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            StatusCode = 422,  // Unprocessable Entity
            Message = "Doğrulama hataları oluştu.",
            Errors = errors
        };
    }

    /// <summary>Bulunamadı yanıtı (404).</summary>
    public static ApiResponse<T> NotFound(string message = "Kayıt bulunamadı.")
    {
        return Failure(message, 404);
    }

    /// <summary>Yetkisiz erişim yanıtı (403).</summary>
    public static ApiResponse<T> Forbidden(string message = "Bu işlem için yetkiniz bulunmamaktadır.")
    {
        return Failure(message, 403);
    }

    /// <summary>Kimlik doğrulama hatası (401).</summary>
    public static ApiResponse<T> Unauthorized(string message = "Oturum açmanız gerekmektedir.")
    {
        return Failure(message, 401);
    }
}

/// <summary>
/// Data içermeyen API yanıtları için kısa yol sınıfı.
/// <code>
/// // Uzun yol:
/// ApiResponse&lt;object&gt;.Success(null, "İşlem başarılı.");
/// 
/// // Kısa yol:
/// ApiResponse.Success("İşlem başarılı.");
/// </code>
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object> Success(string? message = null)
        => ApiResponse<object>.Success(null!, message);

    public static ApiResponse<object> Failure(string message, int statusCode = 400)
        => ApiResponse<object>.Failure(message, statusCode);

    public static ApiResponse<object> ValidationFailure(List<string> errors)
        => ApiResponse<object>.ValidationFailure(errors);

    public static ApiResponse<object> NotFound(string message = "Kayıt bulunamadı.")
        => ApiResponse<object>.NotFound(message);
}

/// <summary>
/// Sayfalanmış sonuçlar için sarmalayıcı.
/// Büyük veri setlerinde tüm kayıtları tek seferde döndürmek yerine
/// sayfa sayfa döndürmek hem performans hem kullanıcı deneyimi açısından kritiktir.
/// </summary>
/// <typeparam name="T">Liste elemanı tipi</typeparam>
public class PaginatedResult<T>
{
    /// <summary>Mevcut sayfa numarası (1-based).</summary>
    public int PageNumber { get; set; }

    /// <summary>Sayfa başına kayıt sayısı.</summary>
    public int PageSize { get; set; }

    /// <summary>Toplam kayıt sayısı (filtrelenmiş).</summary>
    public int TotalCount { get; set; }

    /// <summary>Toplam sayfa sayısı.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Sonraki sayfa var mı?</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Önceki sayfa var mı?</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Bu sayfadaki kayıtlar.</summary>
    public List<T> Items { get; set; } = [];

    public PaginatedResult() { }

    public PaginatedResult(List<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
