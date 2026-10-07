using AmorRP.Contracts.Common;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Capabilities;

public static class CapabilitiesEndpoints
{
    public static void MapCapabilitiesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/capabilities", (IOptions<ServicePolicyOptions> policy) =>
            new CapabilitiesResponse("1", "0.0.4", "0.0.4", false,
                "M3: saved login, groups, currency icons, potion recipes, per-character weekly allowances, letters and inventory. Trading arrives in M4.",
                [new("potion", 1, true, true), new("letter", 1, false, false)], AmorRP.Server.Features.Groups.GroupAccess.GrantableActions, ["emote", "say", "party", "linkshell", "crossworld-linkshell"], policy.Value.ToContract()));
    }
}
