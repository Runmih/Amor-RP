using AmorRP.Contracts.Common;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Capabilities;

public static class CapabilitiesEndpoints
{
    public static void MapCapabilitiesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/capabilities", (IOptions<ServicePolicyOptions> policy) =>
            new CapabilitiesResponse("1", "0.0.2", "0.0.1", false,
                "M1 feasibility probes. Durable authentication, groups, inventory and trading are not available yet.",
                [], [], [], policy.Value.ToContract()));
    }
}
