using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Groups;

public sealed partial class GroupService
{
    public async Task<IResult> MembersAsync(HttpContext http, Guid groupId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (group, _) = await access.RequireAsync(groupId, session.CharacterId, ct);
        var dormant = http.Request.Query["includeDormant"] == "true";
        if (dormant) GroupAccess.Owner(group, session.CharacterId);
        var scope = $"members:{groupId}:{session.CharacterId}:{dormant}"; var (limit, after) = pages.Read(http, scope);
        var query = db.Memberships.Where(x => x.GroupId == groupId && (dormant || x.Status == "active"));
        if (after.HasValue) query = query.Where(x => x.CharacterId.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.CharacterId).Take(limit + 1).ToArrayAsync(ct);
        var items = new List<Member>();
        foreach (var row in rows.Take(limit))
        {
            var character = await db.Characters.SingleAsync(x => x.Id == row.CharacterId, ct);
            items.Add(new(SessionAccess.View(character), row.Status, group.OwnerCharacterId == row.CharacterId,
                group.OwnerCharacterId == row.CharacterId ? GroupAccess.GrantableActions : row.Capabilities, row.TradingRestricted, row.Version, GroupAccess.MemberETag(row)));
        }
        return Results.Json(new MemberPage(items.ToArray(), rows.Length > limit ? pages.Write(rows[limit-1].CharacterId, scope) : null, group.Version.ToString(), null));
    }
    public async Task<IResult> MemberCommandAsync(HttpContext http, Guid groupId, Guid characterId, object body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "character:" + characterId, "group:" + groupId], ct);
        var (group, _) = await access.RequireAsync(groupId, session.CharacterId, ct); GroupAccess.Owner(group, session.CharacterId);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(characterId != group.OwnerCharacterId, 409, "owner_protected", "Owner authority cannot be edited as a member grant.");
        var target = await access.TargetAsync(groupId, characterId, kind != "restore", ct);
        CommandStore.Match(http, GroupAccess.MemberETag(target));
        var op = commands.New(http, scope, body, session.CharacterId, groupId, "member." + kind); op.SubjectCharacterId = characterId;
        switch (body)
        {
            case MemberCapabilities grant:
                ApiFault.Require(grant.Capabilities != null && grant.Capabilities.Length <= GroupAccess.GrantableActions.Length
                    && grant.Capabilities.Distinct().Count() == grant.Capabilities.Length && grant.Capabilities.All(GroupAccess.GrantableActions.Contains),
                    422, "invalid_request", "Unknown or repeated capability.");
                target.Capabilities = grant.Capabilities!.Order().ToArray(); break;
            case MemberRestriction restriction:
                ApiFault.Text(restriction.Reason, 500, false); target.TradingRestricted = restriction.Restricted; op.Reason = restriction.Reason; break;
            case MemberRemoval removal:
                ApiFault.Text(removal.Reason, 500); op.Reason = removal.Reason;
                if (kind == "restore") { ApiFault.Require(target.Status == "blocked", 409, "member_not_blocked", "Only blocked members can be restored."); target.Status = "dormant"; }
                else { target.Status = "blocked"; target.Capabilities = []; await CancelTransfersForAsync(groupId, characterId, ct); }
                break;
        }
        target.Version++; group.Version++;
        var dto = new Member(SessionAccess.View(await db.Characters.SingleAsync(x => x.Id == characterId, ct)), target.Status, false,
            target.Capabilities, target.TradingRestricted, target.Version, GroupAccess.MemberETag(target));
        http.Response.Headers.ETag = dto.Etag;
        var result = await commands.SaveAsync(http, op, new CommandResult<Member>(op.Id, dto), ct); await tx.CommitAsync(ct); return result;
    }

}
