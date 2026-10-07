using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.History;

public sealed class HistoryService(AmorDbContext db, SessionAccess sessions, GroupAccess groups, PageCursor pages)
{
    public async Task<IResult> ListAsync(HttpContext http, Guid groupId, bool audit, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        if (audit) { var (g, _) = await groups.RequireAsync(groupId, session.CharacterId, ct); GroupAccess.Owner(g, session.CharacterId); }
        else ApiFault.Require(await db.Memberships.AnyAsync(x => x.GroupId == groupId && x.CharacterId == session.CharacterId, ct), 404, "not_found", "History not found.");
        var scope = $"history:{groupId}:{session.CharacterId}:{audit}"; var (limit, after) = pages.Read(http, scope);
        var query = db.Operations.Where(x => x.GroupId == groupId && (audit || x.ActorCharacterId == session.CharacterId || x.SubjectCharacterId == session.CharacterId));
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) < 0);
        var rows = await query.OrderByDescending(x => x.Id).Take(limit+1).ToArrayAsync(ct);
        return Results.Json(new HistoryEntryPage(rows.Take(limit).Select(x => new HistoryEntry(x.Id, x.Id, x.Kind, x.ActorCharacterId!.Value,
            x.SubjectCharacterId, x.OccurredAt, x.Summary, x.Reason)).ToArray(), rows.Length > limit ? pages.Write(rows[limit-1].Id, scope) : null, rows.FirstOrDefault()?.Id.ToString() ?? "0", null));
    }
    public async Task<IResult> OperationAsync(HttpContext http, Guid id, bool byKey, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var op = await db.Operations.SingleOrDefaultAsync(x => x.ActorCharacterId == session.CharacterId
            && (byKey ? x.Key == id && x.Scope == "character:" + session.CharacterId : x.Id == id), ct);
        ApiFault.Require(op != null, 404, "not_found", "Operation not found.");
        return Results.Json(new Operation(op!.Id, op.GroupId, op.Kind, op.ActorCharacterId, op.OccurredAt, []));
    }
}
public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/history", (HttpContext h, Guid groupId, HistoryService s, CancellationToken ct) => s.ListAsync(h, groupId, false, ct));
        app.MapGet("/api/v1/groups/{groupId}/audit", (HttpContext h, Guid groupId, HistoryService s, CancellationToken ct) => s.ListAsync(h, groupId, true, ct));
        app.MapGet("/api/v1/operations/{operationId}", (HttpContext h, Guid operationId, HistoryService s, CancellationToken ct) => s.OperationAsync(h, operationId, false, ct));
        app.MapGet("/api/v1/operation-keys/{idempotencyKey}", (HttpContext h, Guid idempotencyKey, HistoryService s, CancellationToken ct) => s.OperationAsync(h, idempotencyKey, true, ct));
    }
}
