using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Persistence;

namespace AmorRP.Server.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => new HealthResponse("ok"));
        app.MapGet("/health/ready", async (DatabaseReadiness readiness, CancellationToken cancellationToken) =>
        {
            var ready = await readiness.IsReadyAsync(cancellationToken);
            return Results.Json(new HealthResponse(ready ? "ok" : "unavailable"),
                statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
    }
}
