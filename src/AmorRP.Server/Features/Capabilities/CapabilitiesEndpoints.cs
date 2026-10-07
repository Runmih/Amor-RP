using AmorRP.Contracts.Common;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Capabilities;

public static class CapabilitiesEndpoints
{
    public static void MapCapabilitiesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/capabilities", (IOptions<ServicePolicyOptions> policy) =>
            new CapabilitiesResponse("1", "0.0.3", "0.0.3", false,
                "M2: saved character login, groups, invitations, action grants, weekly policies and currency. Inventory and trading arrive in later milestones.",
                [], AmorRP.Server.Features.Groups.GroupAccess.GrantableActions, [], policy.Value.ToContract()));
    }
}
