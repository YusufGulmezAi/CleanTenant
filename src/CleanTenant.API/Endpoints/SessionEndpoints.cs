using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Sessions;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>
/// Oturum izleme ve yönetim endpoint'leri.
/// Admin kullanıcılar aktif oturumları izleyebilir ve sonlandırabilir.
/// </summary>
public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Sessions")
            .RequireAuthorization();

        group.MapGet("/", GetActiveSessions)
            .WithSummary("Aktif oturumları listele");
        group.MapGet("/user/{userId:guid}", GetUserSessions)
            .WithSummary("Belirli kullanıcının aktif oturumlarını listele");
        group.MapDelete("/user/{userId:guid}", RevokeUserSessions)
            .WithSummary("Kullanıcının tüm oturumlarını sonlandır");
    }

    private static async Task<IResult> GetActiveSessions(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetActiveSessionsQuery(), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> GetUserSessions(
        Guid userId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetActiveSessionsQuery { FilterByUserId = userId }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> RevokeUserSessions(
        Guid userId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new RevokeSessionCommand(userId), ct);
        return result.ToApiResponse();
    }
}
