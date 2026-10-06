namespace AmorRP.Server.Features.Feasibility;

public sealed class XivAuthOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackUrl { get; set; } = "";

    public bool IsValid() => !Enabled || (!string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && Uri.TryCreate(CallbackUrl, UriKind.Absolute, out var callback)
        && callback.Scheme == "https" && callback.UserInfo.Length == 0
        && callback.AbsolutePath == "/auth/xivauth/feasibility-callback"
        && callback.Query.Length == 0 && callback.Fragment.Length == 0);
}
