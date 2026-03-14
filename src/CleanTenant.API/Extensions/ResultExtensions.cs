using CleanTenant.Application.Common.Models;
using CleanTenant.Shared.DTOs.Common;

namespace CleanTenant.API.Extensions;

/// <summary>
/// Result&lt;T&gt; → IResult dönüşüm extension'ları.
/// Minimal API endpoint'lerinde handler sonucunu HTTP yanıtına çevirir.
/// 
/// <code>
/// // Endpoint'te kullanım:
/// var result = await sender.Send(command, ct);
/// return result.ToApiResponse();
/// 
/// // Dönen HTTP yanıt:
/// // 200: { isSuccess: true, data: {...}, statusCode: 200 }
/// // 404: { isSuccess: false, message: "Bulunamadı", statusCode: 404 }
/// </code>
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Result&lt;T&gt; → standart ApiResponse&lt;T&gt; IResult dönüşümü.
    /// Status code'a göre uygun HTTP yanıt tipini seçer.
    /// </summary>
    public static IResult ToApiResponse<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponse<T>.Success(result.Value!, statusCode: result.StatusCode);
            return result.StatusCode switch
            {
                201 => Results.Created((string?)null, response),
                204 => Results.NoContent(),
                _ => Results.Ok(response)
            };
        }

        // Hata yanıtı
        var errorResponse = result.Errors.Count > 0
            ? ApiResponse<T>.ValidationFailure(result.Errors)
            : ApiResponse<T>.Failure(result.Error ?? "Bir hata oluştu.", result.StatusCode);

        return result.StatusCode switch
        {
            401 => Results.Json(errorResponse, statusCode: 401),
            403 => Results.Json(errorResponse, statusCode: 403),
            404 => Results.Json(errorResponse, statusCode: 404),
            422 => Results.Json(errorResponse, statusCode: 422),
            _ => Results.Json(errorResponse, statusCode: result.StatusCode)
        };
    }
}
