using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.IpBlacklist;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>IP Kara Liste yönetim endpoint'leri.</summary>
public static class IpBlacklistEndpoints
{
    public static void MapIpBlacklistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ip-blacklist")
            .WithTags("IP Blacklist")
            .RequireAuthorization();

        group.MapGet("/", GetBlacklist)
            .WithSummary("Kara listedeki IP'leri listele");

        group.MapPost("/", AddToBlacklist)
            .WithSummary("IP'yi kara listeye ekle");

        group.MapDelete("/{id:guid}", RemoveFromBlacklist)
            .WithSummary("IP'yi kara listeden kaldır");

        group.MapGet("/check/{ip}", CheckIp)
            .WithSummary("IP'nin kara listede olup olmadığını kontrol et");
    }

    private static async Task<IResult> GetBlacklist(
        ISender sender, bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetIpBlacklistQuery(includeInactive), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> AddToBlacklist(
        AddIpRequest body, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new AddIpBlacklistCommand
        {
            IpAddressOrRange = body.IpAddressOrRange,
            Reason = body.Reason,
            ExpiresInMinutes = body.ExpiresInMinutes
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> RemoveFromBlacklist(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveIpBlacklistCommand(id), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> CheckIp(string ip, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CheckIpBlacklistQuery(ip), ct);
        return result.ToApiResponse();
    }
}

public class AddIpRequest
{
    public string IpAddressOrRange { get; set; } = default!;
    public string? Reason { get; set; }
    public int? ExpiresInMinutes { get; set; }
}
