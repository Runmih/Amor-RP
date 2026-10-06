using AmorRP.Server.Features.Capabilities;
using AmorRP.Server.Features.Health;
using AmorRP.Server.Features.Feasibility;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var migrate = args.Contains("--migrate", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
if (builder.Configuration["PORT"] is { } port && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    if (!int.TryParse(port, out var number) || number is < 1 or > 65535)
        throw new InvalidOperationException("PORT must be a valid TCP port.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
}
var connectionString = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings__Database before starting the server.");

builder.Services.AddOptions<ServicePolicyOptions>()
    .BindConfiguration("ServicePolicy").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddDbContext<AmorDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<DatabaseReadiness>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
    {
        401 => "unauthorized", 409 => "conflict", 429 => "rate_limited",
        503 => "service_unavailable", >= 500 => "server_error", _ => "invalid_request"
    };
    context.ProblemDetails.Extensions["requestId"] = context.HttpContext.TraceIdentifier;
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
// OAuth codes/state and provider grants must not appear in request/client logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddOptions<XivAuthOptions>().BindConfiguration("XivAuth")
    .Validate(options => options.IsValid(), "Enabled XIVAuth requires client credentials and the exact HTTPS feasibility callback.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IdentityProbeStore>();
builder.Services.AddHttpClient<ICharacterIdentityProvider, XivAuthProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 65536;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "Identity probe request limit reached.").ExecuteAsync(context.HttpContext);
    };
    options.AddFixedWindowLimiter("identity-probe", limiter =>
    {
        limiter.PermitLimit = 180;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.MapHealthEndpoints();
app.MapCapabilitiesEndpoints();
app.MapFeasibilityEndpoints();
await app.RunAsync();

public partial class Program;
