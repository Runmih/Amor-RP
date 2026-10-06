using System.Diagnostics;
using AmorRP.Contracts.Feasibility;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Diagnostics;

public sealed class FeasibilityPanel(IPlayerState player, IPartyList party, ChatConsentWindow chatConsent) : IDisposable
{
    private readonly IdentityProbeClient client = new();
    private CancellationTokenSource login = new();
    private Task<IdentityProbeCreated>? starting;
    private Task<IdentityProbeSession>? finishing;
    private Task<string>? checking;
    private IdentityProbeCreated? attempt;
    private IdentityProbeSession? session;
    private string identityScope = "";
    private string status = "M1 login requires a configured XIVAuth server.";
    private CharacterTarget? target;
    private string message = "uses a test potion.";
    private int destination;
    private DateTimeOffset lastPost;

    public void SelectTarget(CharacterTarget selected) => target = selected;

    public void Draw(string backendUrl)
    {
        var world = player.HomeWorld;
        var name = player.IsLoaded ? player.CharacterName : "";
        var worldName = player.IsLoaded && world.IsValid ? world.Value.Name.ToString() : "";
        var scope = backendUrl + "|" + name + "|" + worldName;
        if (scope != identityScope)
        {
            Clear();
            status = "Character or server changed. Local login credentials cleared.";
            identityScope = scope;
        }
        Collect();
        if (session is not null && session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Clear();
            status = "The five-minute test session expired. Sign in again.";
        }
        var valid = BackendAddress.TryParse(backendUrl, out var origin) && name.Length > 0 && worldName.Length > 0;
        ImGui.Separator();
        ImGui.TextUnformatted("Verified login test");
        ImGui.TextWrapped("Signing in sends this character's name and home world to your configured server. The browser lets you choose a verified character. Test credentials stay in memory and expire after five minutes.");
        ImGui.BeginDisabled(!valid || starting is not null || finishing is not null || checking is not null);
        if (ImGui.Button("Start XIVAuth login"))
        {
            Clear();
            status = "Starting login…";
            starting = client.StartAsync(origin!, new(name, worldName), login.Token);
        }
        ImGui.EndDisabled();
        if (attempt is not null && finishing is null && session is null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Open XIVAuth in browser"))
            {
                try
                {
                    if (!IdentityProbeClient.TrustedAuthorizationUrl(attempt.AuthorizationUrl)) throw new InvalidOperationException();
                    Process.Start(new ProcessStartInfo(attempt.AuthorizationUrl) { UseShellExecute = true });
                    finishing = client.FinishAsync(origin!, attempt, login.Token);
                    status = "Waiting for browser consent…";
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                { status = "Could not open the trusted XIVAuth page. Check your default browser."; }
            }
        }
        if (starting is not null || finishing is not null || session is not null || attempt is not null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Cancel / sign out"))
            {
                var token = session?.AccessToken;
                Clear();
                if (token is not null && origin is not null)
                    checking = LogoutAsync(origin, token, login.Token);
                status = "Local credentials cleared.";
            }
        }
        if (session is not null)
        {
            ImGui.TextUnformatted($"Verified: {session.Character.DisplayName} @ {session.Character.HomeWorld}");
            ImGui.BeginDisabled(checking is not null);
            if (ImGui.Button("Check authenticated reconnect")) checking = ReconnectAsync(origin!, session.AccessToken, login.Token);
            ImGui.EndDisabled();
        }
        Wrapped(status);

        ImGui.Separator();
        ImGui.TextUnformatted("Character context-menu test");
        Wrapped(target is null ? "Right-click a player and select Amor RP: inspect character (M1)."
            : $"Selected: {target.Name}; home-world ID: {target.HomeWorldId}. This is an unverified target hint. Trading arrives in M4.");

        ImGui.Separator();
        ImGui.TextUnformatted("Public RP message test");
        ImGui.TextWrapped("This test sends a real in-game message. It does not create or consume an item. Choose the exact destination; unavailable channels may be rejected by the game. No fallback or automatic retry is used.");
        ImGui.InputText("Message (50 characters)", ref message, 512);
        ImGui.Combo("Destination", ref destination, ChatCommand.Destinations, ChatCommand.Destinations.Length);
        var safe = ChatCommand.TryCreate(message, destination, out var command);
        Wrapped(safe ? "Preview: " + command : "Use 1–50 characters (up to 200 UTF-8 bytes). Controls, formatting codes and <macro placeholders> are disallowed.");
        if (chatConsent.Answered)
        {
            var allowed = chatConsent.Allowed;
            if (ImGui.Checkbox("Chat setting: enable RP posting for this startup", ref allowed)) chatConsent.Allowed = allowed;
        }
        else
            ImGui.TextUnformatted("Answer the startup chat permission question before posting.");
        var canPost = safe && chatConsent.Allowed && player.IsLoaded && (destination != 2 || party.Length > 0)
            && DateTimeOffset.UtcNow - lastPost >= TimeSpan.FromSeconds(3);
        ImGui.BeginDisabled(!canPost);
        if (ImGui.Button("Post test message"))
        {
            lastPost = DateTimeOffset.UtcNow;
            status = GameChatSender.Send(message, destination)
                ? "Message submitted to the game. Check the selected channel; delivery is not confirmed."
                : "The message could not be submitted. Review the preview before trying again.";
        }
        ImGui.EndDisabled();
    }

    private void Collect()
    {
        if (starting is { IsCompleted: true })
        {
            if (starting.IsCompletedSuccessfully) { attempt = starting.Result; status = "Ready. Open XIVAuth to verify the current character."; }
            else status = Failure(starting);
            starting = null;
        }
        if (finishing is { IsCompleted: true })
        {
            if (finishing.IsCompletedSuccessfully) { session = finishing.Result; status = "Verified login succeeded."; }
            else status = Failure(finishing);
            finishing = null;
            attempt = null;
        }
        if (checking is { IsCompleted: true })
        {
            status = checking.IsCompletedSuccessfully ? checking.Result : Failure(checking);
            if (!checking.IsCompletedSuccessfully) session = null;
            checking = null;
        }
    }
    private static string Failure(Task task)
    {
        var error = task.Exception?.GetBaseException(); // Observe faults, never print tokens/URLs.
        return error is InvalidOperationException ? "Login is unavailable or the selected character was not verified. Check the M1 setup and retry."
            : "Request failed, timed out or was cancelled. Check the server and start again.";
    }
    private async Task<string> ReconnectAsync(Uri origin, string token, CancellationToken cancellationToken)
    {
        await client.ReconnectAsync(origin, token, cancellationToken);
        return "Authenticated reconnect succeeded.";
    }
    private async Task<string> LogoutAsync(Uri origin, string token, CancellationToken cancellationToken)
    {
        await client.LogoutAsync(origin, token, cancellationToken);
        return "Local credentials cleared and server test session revoked.";
    }
    private void Clear()
    {
        login.Cancel();
        login.Dispose();
        login = new();
        Observe(starting); Observe(finishing); Observe(checking);
        starting = null; finishing = null; checking = null;
        attempt = null; session = null;
    }
    private static void Observe(Task? task)
    {
        if (task is not null)
            _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private static void Wrapped(string text) { ImGui.PushTextWrapPos(); ImGui.TextUnformatted(text); ImGui.PopTextWrapPos(); }
    public void Dispose() { Clear(); login.Dispose(); client.Dispose(); }
}
