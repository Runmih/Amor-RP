using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel(IPlayerState player, Configuration configuration, Action save) : IDisposable
{
    private readonly FoundationClient client = new();
    private CancellationTokenSource lifetime = new();
    private Task<Action>? work;
    private string scope = "";
    private string status = "Sign in with XIVAuth to manage your groups.";
    private Uri? origin;
    private SessionTokens? session;
    private SavedSession? saved;
    private LoginAttemptCreated? attempt;
    private PendingCommand? currentCommand;
    private bool refreshNeeded;
    private DateTimeOffset nextRenewAttempt;
    private Group[] groups = [];
    private Group? group;
    private Member[] members = [];
    private Invitation[] invitations = [];
    private HistoryEntry[] history = [];
    private SessionInfo[] devices = [];
    private GroupPolicies? policies;
    private OwnershipTransfer? transfer;
    private string? membersCursor, invitationsCursor, historyCursor;
    private Guid? selected;
    private int savedChoice;
    private string invitationCode = "";
    private bool visibilityAccepted;
    private string createdCode = "";
    private string name = "", description = "", currencyName = "Coins", symbol = "C";
    private int initialPoints, initialLetters = 5;
    private Guid? memberSelection;
    private bool grantCoins, grantPotions, grantRemoval, restricted;
    private string reason = "", delta = "";
    private string deleteConfirmation = "";
    private int nextPoints, nextLetters;
    private int inviteHours = 24, inviteUses = 5;

    public void Draw(string backendUrl)
    {
        var world = player.HomeWorld;
        var localName = player.IsLoaded ? player.CharacterName : "";
        var localWorld = player.IsLoaded && world.IsValid ? world.Value.Name.ToString() : "";
        var nextScope = backendUrl + "|" + localName + "|" + localWorld;
        if (scope != nextScope)
        {
            Clear(); scope = nextScope;
            if (BackendAddress.TryParse(backendUrl, out var parsed)) origin = parsed;
            saved = configuration.SavedSessions.LastOrDefault(x => x.Origin == origin?.AbsoluteUri && x.DisplayName == localName && x.HomeWorldName == localWorld);
            if (saved != null && localName.Length > 0) Resume(saved);
        }
        Collect();
        var busy = work != null;
        if (!busy && session != null && session.AccessExpiresAt < DateTimeOffset.UtcNow.AddMinutes(5) && saved != null && DateTimeOffset.UtcNow >= nextRenewAttempt) Resume(saved);
        if (refreshNeeded && work == null && session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow)
        { refreshNeeded = false; Refresh(); }
        ImGui.TextUnformatted("Character login");
        if (session == null)
        {
            ImGui.TextWrapped("XIVAuth verifies this character. Login is saved for this Windows user on this computer; the server renews ownership verification automatically.");
            ImGui.BeginDisabled(busy || origin == null || localName.Length == 0 || localWorld.Length == 0);
            if (ImGui.Button("Sign in with XIVAuth"))
            {
                var selectedSaved = configuration.SavedSessions.Where(x => x.Origin == origin?.AbsoluteUri).ToArray();
                var known = saved ?? (selectedSaved.Length > 0 && savedChoice < selectedSaved.Length ? selectedSaved[savedChoice] : null);
                var body = new LoginStart(localName, world.RowId.ToString(), localWorld, known?.CharacterId);
                // A saved identity is selected only explicitly after a rename; new characters use discovery.
                if (known != null && (known.DisplayName != localName || known.HomeWorldName != localWorld) && !useSavedIdentity) body = body with { KnownCharacterId = null };
                Start(async ct => { var a = await client.StartAsync(origin!, body, ct); return () => { attempt = a; status = "Open XIVAuth in your browser to verify this character."; }; }, "Preparing login…");
            }
            ImGui.EndDisabled();
            if (attempt != null)
            {
                ImGui.BeginDisabled(busy);
                if (ImGui.Button("Open XIVAuth in browser"))
                {
                    try {
                        if (!IdentityProbeClient.TrustedAuthorizationUrl(attempt.AuthorizationUrl)) throw new InvalidOperationException("Untrusted authorization page.");
                        Process.Start(new ProcessStartInfo(attempt.AuthorizationUrl) { UseShellExecute = true });
                        var loginAttempt = attempt;
                        Start(async ct => { var tokens = await client.FinishAsync(origin!, loginAttempt, ct); return () => Accept(tokens); }, "Waiting for browser verification…");
                    } catch (System.ComponentModel.Win32Exception) { status = "Could not open your default browser."; }
                }
                ImGui.EndDisabled();
            }
            var available = configuration.SavedSessions.Where(x => x.Origin == origin?.AbsoluteUri).ToArray();
            if (available.Length > 0)
            {
                ImGui.Checkbox("Use a saved identity (also for a renamed/transferred character)", ref useSavedIdentity);
                savedChoice = Math.Clamp(savedChoice, 0, available.Length-1);
                ImGui.Combo("Saved character", ref savedChoice, available.Select(x => x.DisplayName + " @ " + x.HomeWorldName).ToArray(), available.Length);
                ImGui.BeginDisabled(busy || origin == null || localName.Length == 0);
                if (ImGui.Button("Resume saved login")) Resume(available[savedChoice]);
                ImGui.EndDisabled();
            }
        }
        else
        {
            ImGui.TextUnformatted($"Verified: {session.Character.DisplayName} @ {session.Character.HomeWorldName}");
            ImGui.BeginDisabled(busy);
            if (ImGui.Button("Refresh groups")) Refresh();
            ImGui.SameLine();
            if (ImGui.Button("Sign out this device")) Mutate("DELETE", "api/v1/auth/sessions/current", null, null, signOut: true);
            ImGui.EndDisabled();
        }
        if (work != null && ImGui.Button("Cancel waiting"))
        {
            ClearWork(); status = "Waiting cancelled. Saved operations retain their original retry key.";
        }
        Wrapped(status);
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session?.Character.Id);
        if (pending != null)
        {
            ImGui.TextWrapped("A previous action has an unresolved outcome. Reconcile it before making another change.");
            ImGui.BeginDisabled(work != null || session == null);
            if (ImGui.Button("Retry original action")) ExecutePending(pending, false);
            ImGui.SameLine();
            if (ImGui.Button("Check committed receipt"))
                Start(async ct => { await client.GetAsync<Operation>(origin!, $"api/v1/operation-keys/{pending.Key}", session!.AccessToken, ct);
                    return () => { configuration.PendingCommands.Remove(pending); save(); status = "The action committed. Refreshing current state."; refreshNeeded = true; }; }, "Checking receipt…");
            ImGui.EndDisabled();
        }
        if (session == null || session.AccessExpiresAt <= DateTimeOffset.UtcNow) return;
        if (session.Character.DisplayName != localName || session.Character.HomeWorldName != localWorld)
        { ImGui.TextWrapped("The verified profile does not match the character currently in game. Wait for XIVAuth to refresh it, then resume login. Existing assets are preserved."); return; }
        ImGui.BeginDisabled(work != null || pending != null);
        if (ImGui.BeginTabBar("AmorRPProductTabs"))
        {
            if (ImGui.BeginTabItem("Groups")) { DrawGroups(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Members & currency")) { DrawMembers(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Owner settings")) { DrawOwner(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("History & devices")) { DrawHistory(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        ImGui.EndDisabled();
    }
    private bool useSavedIdentity;
    private bool Owner => group != null && session?.Character.Id == group.OwnerCharacterId;
    private bool CanCoins => Owner || group?.MyCapabilities.Contains("currency.manage") == true;
    private void Resume(SavedSession entry)
    {
        try {
            nextRenewAttempt = DateTimeOffset.UtcNow.AddMinutes(1);
            saved = entry; selected = saved.SelectedGroupId; var tokens = saved.Read();
            saved.PendingRefreshKey ??= Guid.CreateVersion7(); save(); var key = saved.PendingRefreshKey.Value;
            Start(async ct => { var renewed = await client.RenewAsync(origin!, tokens, key, ct); return () => Accept(renewed); }, "Renewing saved login…");
        } catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        { status = "Saved login cannot be opened by this Windows user. Sign in again with XIVAuth."; }
    }
    private void Accept(SessionTokens tokens)
    {
        session = tokens; attempt = null;
        saved = configuration.SavedSessions.SingleOrDefault(x => x.Origin == origin!.AbsoluteUri && x.CharacterId == tokens.Character.Id);
        if (saved == null) { saved = new SavedSession { Origin = origin!.AbsoluteUri, CharacterId = tokens.Character.Id }; configuration.SavedSessions.Add(saved); }
        saved.DisplayName = tokens.Character.DisplayName; saved.HomeWorldName = tokens.Character.HomeWorldName;
        saved.ProtectedTokens = SavedSession.Protect(tokens); saved.PendingRefreshKey = null; save();
        status = "Character login saved. Loading groups…"; refreshNeeded = true;
    }
    private void Start(Func<CancellationToken, Task<Action>> run, string message)
    { status = message; work = run(lifetime.Token); }
    private void Collect()
    {
        if (work is not { IsCompleted: true }) return;
        var finished = work; work = null;
        if (finished.IsCompletedSuccessfully) { finished.Result(); currentCommand = null; }
        else
        {
            var failure = finished.Exception?.GetBaseException();
            status = failure is FoundationApiException or InvalidOperationException ? failure.Message : "Connection failed or timed out. Retry with the saved operation key.";
            if (failure is FoundationApiException api)
            {
                if ((int)api.Status is >= 400 and < 500 && api.Status != System.Net.HttpStatusCode.TooManyRequests && api.Code != "operation_replay_expired" && currentCommand != null)
                { configuration.PendingCommands.Remove(currentCommand); save(); currentCommand = null; refreshNeeded = session != null; }
                if (api.Status == System.Net.HttpStatusCode.Unauthorized) { session = null; if (saved != null) { saved.PendingRefreshKey = null; save(); } }
            }
        }
    }
    private void Mutate(string method, string path, object? body, string? etag = null, bool signOut = false)
    {
        if (session == null || origin == null) return;
        var command = new CommandRequest(method, path, body == null ? null : JsonSerializer.Serialize(body, FoundationClient.Json), etag);
        var pending = new PendingCommand { Origin = origin.AbsoluteUri, CharacterId = session.Character.Id, Key = Guid.CreateVersion7(), ProtectedRequest = PendingCommand.Protect(command) };
        configuration.PendingCommands.Add(pending); save(); ExecutePending(pending, signOut);
    }
    private void ExecutePending(PendingCommand pending, bool signOut)
    {
        var request = pending.Read(); currentCommand = pending; var token = session!.AccessToken;
        Start(async ct => {
            var response = await client.CommandAsync(origin!, request, pending.Key, token, ct);
            return () => {
                configuration.PendingCommands.Remove(pending); save(); status = "Action completed.";
                if (request.Method == "POST" && request.Path.EndsWith("/invitations", StringComparison.Ordinal))
                    createdCode = JsonSerializer.Deserialize<CommandResult<InvitationCreated>>(response, FoundationClient.Json)!.Result.InvitationCode;
                if (signOut || request.Path == "api/v1/auth/sessions/current") { ForgetCurrent(); status = "This device session was revoked. Sign in to reconnect."; }
                else refreshNeeded = true;
            };
        }, "Saving action…");
    }
    private void Refresh()
    {
        var uri = origin!; var token = session!.AccessToken; var selectedId = selected;
        Start(async ct => {
            var list = await client.GetAsync<GroupPage>(uri, "api/v1/groups", token, ct);
            var active = list.Items.FirstOrDefault(x => x.Id == selectedId) ?? list.Items.FirstOrDefault();
            MemberPage? roster = null; InvitationPage? invites = null; GroupPolicies? limits = null; HistoryEntryPage? log = null; OwnershipTransfer? proposal = null;
            if (active != null)
            {
                var isOwner = active.OwnerCharacterId == session!.Character.Id;
                roster = await client.GetAsync<MemberPage>(uri, $"api/v1/groups/{active.Id}/members" + (isOwner ? "?includeDormant=true" : ""), token, ct);
                limits = await client.GetAsync<GroupPolicies>(uri, $"api/v1/groups/{active.Id}/policies", token, ct);
                log = await client.GetAsync<HistoryEntryPage>(uri, $"api/v1/groups/{active.Id}/" + (isOwner ? "audit" : "history"), token, ct);
                if (isOwner) invites = await client.GetAsync<InvitationPage>(uri, $"api/v1/groups/{active.Id}/invitations", token, ct);
                if (active.PendingOwnershipTransferId.HasValue && (isOwner || roster.Items.Any(x => x.Character.Id == session.Character.Id)))
                {
                    try { proposal = await client.GetAsync<OwnershipTransfer>(uri, $"api/v1/groups/{active.Id}/ownership-transfers/{active.PendingOwnershipTransferId}", token, ct); }
                    catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
                }
            }
            var sessions = await client.GetAsync<SessionInfoPage>(uri, "api/v1/me/sessions", token, ct);
            return () => {
                groups = list.Items; group = active; selected = active?.Id; members = roster?.Items ?? []; invitations = invites?.Items ?? []; history = log?.Items ?? [];
                membersCursor = roster?.NextCursor; invitationsCursor = invites?.NextCursor; historyCursor = log?.NextCursor;
                policies = limits; transfer = proposal; devices = sessions.Items;
                if (saved != null) { saved.SelectedGroupId = selected; save(); }
                if (limits != null) { nextPoints = limits.Next.PotionPoints; nextLetters = limits.Next.Letters; }
                memberSelection = null; status = "Groups refreshed.";
            };
        }, "Loading groups…");
    }
    private void ForgetCurrent()
    { if (saved != null) configuration.SavedSessions.Remove(saved); save(); session = null; saved = null; group = null; groups = []; members = []; }
    private void ClearWork()
    {
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        if (work != null) _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        work = null; currentCommand = null;
    }
    private void Clear()
    { ClearWork(); origin = null; session = null; saved = null; attempt = null; selected = null; group = null; groups = []; members = []; invitations = []; history = []; devices = []; transfer = null; refreshNeeded = false; createdCode = ""; }
    private static void Wrapped(string message) { ImGui.PushTextWrapPos(); ImGui.TextUnformatted(message); ImGui.PopTextWrapPos(); }
    public void Dispose() { ClearWork(); lifetime.Dispose(); client.Dispose(); }
}
