using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Users.Commands;
using CleanTenant.Application.Features.Users.Queries;
using CleanTenant.Shared.DTOs.Users;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>Kullanıcı yönetimi Minimal API endpoint'leri.</summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/", GetAll).WithSummary("Kullanıcıları listele (sayfalı)");
        group.MapGet("/{id:guid}", GetById).WithSummary("Kullanıcı detayı (roller dahil)");
        group.MapPost("/", Create).WithSummary("Kullanıcı oluştur/ekle (e-posta ile arama)");
        group.MapPut("/{id:guid}", Update).WithSummary("Kullanıcı profilini güncelle");
        group.MapDelete("/{id:guid}", Delete).WithSummary("Kullanıcıyı sil (soft delete)");
        group.MapPost("/{id:guid}/block", Block).WithSummary("Kullanıcıyı bloke et");
        group.MapPost("/{id:guid}/force-logout", ForceLogout).WithSummary("Kullanıcıyı zorla çıkış yaptır");
    }

    private static async Task<IResult> GetAll(
        ISender sender, int pageNumber = 1, int pageSize = 20,
        string? search = null, bool? isActive = null, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery
        {
            PageNumber = pageNumber, PageSize = pageSize,
            Search = search, IsActive = isActive
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> GetById(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Create(
        CreateUserDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CreateUserCommand(dto), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Update(
        Guid id, UpdateUserProfileDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateUserCommand(id, dto), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Delete(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteUserCommand(id), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Block(
        Guid id, BlockUserRequest body, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new BlockUserCommand
        {
            UserId = id, Reason = body.Reason, ExpiresAt = body.ExpiresAt
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> ForceLogout(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new ForceLogoutCommand(id), ct);
        return result.ToApiResponse();
    }
}

/// <summary>Block endpoint body.</summary>
public class BlockUserRequest
{
    public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
