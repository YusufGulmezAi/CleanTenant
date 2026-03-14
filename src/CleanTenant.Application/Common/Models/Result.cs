namespace CleanTenant.Application.Common.Models;

/// <summary>
/// İş operasyonu sonucu — Exception yerine Result pattern.
/// 
/// <para><b>NEDEN EXCEPTION KULLANMIYORUZ?</b></para>
/// Exception'lar "istisnai durumlar" içindir (veritabanı çöktü, ağ kesildi).
/// İş kuralı ihlalleri istisna değildir — beklenen durumlardır:
/// <list type="bullet">
///   <item>"E-posta zaten kayıtlı" — beklenen bir durum, exception değil</item>
///   <item>"Tenant bulunamadı" — beklenen bir durum, exception değil</item>
///   <item>"Yetki yetersiz" — beklenen bir durum, exception değil</item>
/// </list>
/// 
/// Exception'lar:
/// <list type="bullet">
///   <item>Yavaştır (stack trace oluşturma maliyeti)</item>
///   <item>Try-catch ile yakalanmalıdır (kod karmaşıklığı)</item>
///   <item>Flow control için kullanılmamalıdır (anti-pattern)</item>
/// </list>
/// 
/// Result pattern ile:
/// <list type="bullet">
///   <item>Başarı/başarısızlık açıkça ifade edilir</item>
///   <item>Hata mesajları tip güvenli taşınır</item>
///   <item>Performans cezası yoktur</item>
///   <item>Railway-oriented programming desteklenir</item>
/// </list>
/// 
/// <para><b>KULLANIM:</b></para>
/// <code>
/// // Handler'da:
/// public async Task&lt;Result&lt;TenantDto&gt;&gt; Handle(...)
/// {
///     var tenant = await db.Tenants.FindAsync(id);
///     if (tenant is null)
///         return Result&lt;TenantDto&gt;.Failure("Tenant bulunamadı.", 404);
///     
///     return Result&lt;TenantDto&gt;.Success(mapper.Map(tenant));
/// }
/// 
/// // Endpoint'te:
/// var result = await sender.Send(query);
/// return result.ToApiResponse();  // ApiResponse'a otomatik dönüşüm
/// </code>
/// </summary>
/// <typeparam name="T">Başarılı sonuçta dönen veri tipi</typeparam>
public class Result<T>
{
    /// <summary>İşlem başarılı mı?</summary>
    public bool IsSuccess { get; }

    /// <summary>İşlem başarısız mı? (IsSuccess'in tersi — okunabilirlik için)</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Başarılı sonuçta dönen veri.</summary>
    public T? Value { get; }

    /// <summary>Hata mesajı (başarısız durumda).</summary>
    public string? Error { get; }

    /// <summary>HTTP durum kodu önerisi (endpoint'e yardımcı).</summary>
    public int StatusCode { get; }

    /// <summary>Hata listesi (doğrulama hataları için).</summary>
    public List<string> Errors { get; } = [];

    // Private constructor — factory method'lar üzerinden oluşturulur
    private Result(bool isSuccess, T? value, string? error, int statusCode, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    /// <summary>Başarılı sonuç (data ile).</summary>
    public static Result<T> Success(T value, int statusCode = 200)
        => new(true, value, null, statusCode);

    /// <summary>Başarılı oluşturma sonucu (201).</summary>
    public static Result<T> Created(T value)
        => new(true, value, null, 201);

    /// <summary>Hata sonucu.</summary>
    public static Result<T> Failure(string error, int statusCode = 400)
        => new(false, default, error, statusCode);

    /// <summary>Doğrulama hatası sonucu (birden fazla hata).</summary>
    public static Result<T> ValidationFailure(List<string> errors)
        => new(false, default, "Doğrulama hataları oluştu.", 422, errors);

    /// <summary>Bulunamadı sonucu (404).</summary>
    public static Result<T> NotFound(string error = "Kayıt bulunamadı.")
        => new(false, default, error, 404);

    /// <summary>Yetkisiz sonuç (403).</summary>
    public static Result<T> Forbidden(string error = "Bu işlem için yetkiniz bulunmamaktadır.")
        => new(false, default, error, 403);

    /// <summary>Kimlik doğrulama hatası (401).</summary>
    public static Result<T> Unauthorized(string error = "Oturum açmanız gerekmektedir.")
        => new(false, default, error, 401);
}

/// <summary>
/// Data içermeyen operasyonlar için Result yardımcı sınıfı.
/// Örnek: Silme, güncelleme gibi sonuç dönmeyen işlemler.
/// <code>
/// return Result.Success();
/// return Result.NoContent();
/// return Result.Failure("Hata", 400);
/// </code>
/// </summary>
public static class Result
{
    public static Result<object> Success(int statusCode = 200)
        => Result<object>.Success(null!, statusCode);

    public static Result<object> NoContent()
        => Result<object>.Success(null!, 204);

    public static Result<object> Failure(string error, int statusCode = 400)
        => Result<object>.Failure(error, statusCode);

    public static Result<object> NotFound(string error = "Kayıt bulunamadı.")
        => Result<object>.NotFound(error);

    public static Result<object> Forbidden(string error = "Bu işlem için yetkiniz bulunmamaktadır.")
        => Result<object>.Forbidden(error);
}
