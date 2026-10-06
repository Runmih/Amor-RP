using AmorRP.Server.Features.Capabilities;
using AmorRP.Server.Features.Health;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddProblemDetails();

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
app.MapHealthEndpoints();
app.MapCapabilitiesEndpoints();
await app.RunAsync();

public partial class Program;
