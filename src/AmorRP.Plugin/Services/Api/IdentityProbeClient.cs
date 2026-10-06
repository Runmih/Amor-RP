using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AmorRP.Contracts.Feasibility;

namespace AmorRP.Plugin.Services.Api;

public sealed class IdentityProbeClient : IDisposable
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 65536 };

    public async Task<IdentityProbeCreated> StartAsync(Uri origin, IdentityProbeStart selected, CancellationToken cancellationToken)
    {
        using var request = Request(origin, "api/v1/feasibility/login-attempts", HttpMethod.Post);
        request.Headers.Add("X-Login-Request-Credential", Secret());
        request.Content = JsonContent.Create(selected);
        var result = await SendAsync<IdentityProbeCreated>(request, cancellationToken);
        if (!TrustedAuthorizationUrl(result.AuthorizationUrl) || result.AttemptCredential.Length != 43)
            throw new InvalidOperationException("Untrusted provider response.");
        return result;
    }

    public async Task<IdentityProbeSession> FinishAsync(Uri origin, IdentityProbeCreated attempt, CancellationToken cancellationToken)
    {
        while (DateTimeOffset.UtcNow < attempt.ExpiresAt)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(attempt.PollIntervalSeconds, 3, 10)), cancellationToken);
            using var poll = Request(origin, $"api/v1/feasibility/login-attempts/{attempt.AttemptId}", HttpMethod.Get);
            poll.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
            var status = await SendAsync<IdentityProbeStatus>(poll, cancellationToken);
            if (status.Status == "pending") continue;
            if (status.Status != "verified") throw new InvalidOperationException("Login failed or expired. Check the selected verified character in XIVAuth.");
            using var exchange = Request(origin, $"api/v1/feasibility/login-attempts/{attempt.AttemptId}/exchange", HttpMethod.Post);
            exchange.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
            return await SendAsync<IdentityProbeSession>(exchange, cancellationToken);
        }
        throw new InvalidOperationException("Login expired. Start again.");
    }

    public async Task<VerifiedCharacter> ReconnectAsync(Uri origin, string token, CancellationToken cancellationToken)
    {
        using var request = Request(origin, "api/v1/feasibility/identity", HttpMethod.Get);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendAsync<VerifiedCharacter>(request, cancellationToken);
    }

    public async Task LogoutAsync(Uri origin, string token, CancellationToken cancellationToken)
    {
        using var request = Request(origin, "api/v1/feasibility/session", HttpMethod.Delete);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public static bool TrustedAuthorizationUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var url)
        && url.Scheme == "https" && url.Host == "xivauth.net" && url.IsDefaultPort
        && url.AbsolutePath == "/oauth/authorize" && url.UserInfo.Length == 0 && url.Fragment.Length == 0;

    private static HttpRequestMessage Request(Uri origin, string path, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, new Uri(origin, path));
        if (method != HttpMethod.Get) request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        return request;
    }
    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 503) throw new InvalidOperationException("XIVAuth is not configured on this server. Follow the M1 setup guide.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new InvalidOperationException("Invalid server response.");
    }
    public void Dispose() => client.Dispose();
}
