using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Infrastructure.Persistence;
using Bazaar.Infrastructure.Returns;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminReturnEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/returns").WithTags("Admin: Returns").RequireAuthorization("Admin");
        group.MapGet("/", ListReturns);
        group.MapPost("/{id:guid}/approve", Approve);
        group.MapPost("/{id:guid}/reject", Reject);
        return app;
    }

    private static async Task<IResult> ListReturns(
        BazaarDbContext db, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNumber, size) = Paging.Clamp(page, pageSize, DefaultPageSize, MaxPageSize);

        var query = from r in db.ReturnRequests.AsNoTracking().Include(r => r.Lines)
                    join o in db.Orders.AsNoTracking() on r.OrderId equals o.Id
                    select new { r, o.Number, o.Email };

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReturnStatus>(status, true, out var parsed))
            query = query.Where(x => x.r.Status == parsed);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.r.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        var items = rows.Select(x => x.r.ToAdminDto(x.Number, x.Email)).ToList();
        return Results.Ok(new PagedResult<AdminReturnDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> Approve(BazaarDbContext db, ReturnService returns, Guid id, CancellationToken ct)
    {
        var outcome = await returns.ApproveAsync(id, ct);
        return await Respond(db, outcome, ct);
    }

    private static async Task<IResult> Reject(
        BazaarDbContext db, ReturnService returns, Guid id, RejectReturnRequest request, CancellationToken ct)
    {
        var outcome = await returns.RejectAsync(id, request.Reason, ct);
        return await Respond(db, outcome, ct);
    }

    private static async Task<IResult> Respond(BazaarDbContext db, ReturnDecisionOutcome outcome, CancellationToken ct)
    {
        if (outcome.Status != ReturnDecisionStatus.Ok)
            return outcome.Status switch
            {
                ReturnDecisionStatus.NotFound => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status404NotFound, title: "Return not found"),
                ReturnDecisionStatus.NotPending => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status409Conflict, title: "Already decided"),
                ReturnDecisionStatus.RefundFailed => Results.Problem(outcome.Detail, statusCode: StatusCodes.Status402PaymentRequired, title: "Refund failed"),
                _ => Results.Problem(outcome.Detail),
            };

        var request = outcome.Return!;
        var order = await db.Orders.AsNoTracking().Where(o => o.Id == request.OrderId)
            .Select(o => new { o.Number, o.Email }).FirstAsync(ct);
        return Results.Ok(request.ToAdminDto(order.Number, order.Email));
    }
}
