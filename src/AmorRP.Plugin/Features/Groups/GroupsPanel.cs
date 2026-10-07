using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Features.Diagnostics;
using AmorRP.Plugin.Services.Game;
using AmorRP.Plugin.Services.Media;
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

public sealed partial class GroupsPanel(IPlayerState player, Configuration configuration, Action save, IPartyList party, ChatConsentWindow consent, ITextureProvider textures) : IDisposable
{
    private readonly FoundationClient client = new();
    private CancellationTokenSource lifetime = new();
    private Task<Action>? work;
    private string scope = "";
    private readonly CurrencyIconCache icons = new(textures);
    private string actionResult = "", actionDiagnostic = "";
    private DateTimeOffset lastRefresh, nextRefresh;
    private bool showInactive;
    private string? memberDraftEtag;
    private int policyDraftVersion, editGroupVersion, editCurrencyVersion;
    private string removalReason = "", restrictionReason = "";
    private bool confirmRemoval;
    private string editName = "", editDescription = "", editCurrencyName = "";
    private PublicLimits? serviceLimits;
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

    public void Draw(string backendUrl, bool compactMode = false)
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
        busy = work != null;
        if (compactMode && session == null) { Wrapped("Open /amorrp to sign in and select your character."); return; }
        if (!compactMode)
        {
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
            if (ImGui.Button("Refresh data")) Refresh();
            ImGui.SameLine();
            if (ImGui.Button("Sign out this device")) Mutate("DELETE", "api/v1/auth/sessions/current", null, null, signOut: true);
            ImGui.EndDisabled();
        }
        }
        if (compactMode && session != null)
        {
            ImGui.TextUnformatted($"{session.Character.DisplayName} @ {session.Character.HomeWorldName}");
            ImGui.BeginDisabled(work != null); if (ImGui.Button("Refresh data")) Refresh(); ImGui.EndDisabled();
        }
        if (work != null && ImGui.Button("Cancel waiting"))
        {
            ClearWork(); status = "Waiting cancelled. Saved operations retain their original retry key.";
        }
        Wrapped(status);
        if (lastRefresh != default) Wrapped($"Last update: {lastRefresh.LocalDateTime:T}{(DateTimeOffset.UtcNow - lastRefresh > TimeSpan.FromMinutes(2) ? " — data may be stale" : "")}");
        if (actionResult.Length > 0)
        {
            Wrapped(actionResult);
            if (actionDiagnostic.Length > 0 && ImGui.CollapsingHeader("Action troubleshooting")) Wrapped(actionDiagnostic);
            if (ImGui.SmallButton("Dismiss action result")) { actionResult = ""; actionDiagnostic = ""; }
        }
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session?.Character.Id);
        if (pending != null)
        {
            ImGui.TextWrapped("A previous action has an unresolved outcome. Reconcile it before making another change.");
            ImGui.BeginDisabled(work != null || session == null);
            if (ImGui.Button("Retry original action")) ExecutePending(pending, false, recovering: true);
            ImGui.SameLine();
            if (ImGui.Button("Check committed receipt"))
                Start(async ct => { var receipt = await client.GetAsync<Operation>(origin!, $"api/v1/operation-keys/{pending.Key}", session!.AccessToken, ct);
                    return () => { configuration.PendingCommands.Remove(pending); save(); ApplyFormSuccess(pending.Read(), null);
                        if (pending.Read().Path is "api/v1/groups" or "api/v1/groups/join") selected = receipt.GroupId;
                        actionResult = "The action committed. Chat is never posted by receipt recovery."; status = "Refreshing current state."; refreshNeeded = true; }; }, "Checking receipt…");
            ImGui.EndDisabled();
        }
        if (session == null || session.AccessExpiresAt <= DateTimeOffset.UtcNow) return;
        if (session.Character.DisplayName != localName || session.Character.HomeWorldName != localWorld)
        { ImGui.TextWrapped("The verified profile does not match the character currently in game. Wait for XIVAuth to refresh it, then resume login. Existing assets are preserved."); return; }
        if (work == null && pending == null && DateTimeOffset.UtcNow >= nextRefresh && !refreshNeeded) Refresh();
        icons.Draw();
        ImGui.BeginDisabled(work != null || pending != null);
        if (ImGui.BeginCombo("Group", group?.Name ?? "Choose or create a group"))
        {
            foreach (var entry in groups)
                if (ImGui.Selectable(entry.Name + (entry.OwnerCharacterId == session.Character.Id ? " (owner)" : "") + "###group" + entry.Id, selected == entry.Id)) SwitchGroup(entry);
            ImGui.EndCombo();
        }
        if (group != null) { icons.DrawIcon(); ImGui.SameLine(); ImGui.TextUnformatted($"{group.MyBalance.Available} {group.Currency.Name} — {group.Name}"); }
        if (compactMode) { DrawConsumables(); ImGui.EndDisabled(); return; }
        if (ImGui.BeginTabBar("AmorRPProductTabs"))
        {
            if (ImGui.BeginTabItem("Groups")) { DrawGroups(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Members & currency")) { DrawMembers(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Consumables")) { DrawConsumables(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Inventory")) { DrawInventory(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Create items")) { DrawCreation(); ImGui.EndTabItem(); }
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
    { status = message; var token = lifetime.Token; work = Task.Run(() => run(token), token); }
    private void Collect()
    {
        if (work is not { IsCompleted: true }) return;
        var finished = work; work = null;
        if (finished.IsCompletedSuccessfully) { currentCommand = null; finished.Result(); }
        else
        {
            var failure = finished.Exception?.GetBaseException();
            nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1);
            status = failure is FoundationApiException or InvalidOperationException ? failure.Message : "Connection failed or timed out. Retry with the saved operation key.";
            if (currentCommand != null)
            {
                actionResult = status;
                actionDiagnostic = failure is FoundationApiException a ? $"HTTP {(int)a.Status}; code: {a.Code}; request: {a.RequestId ?? "unavailable"}; operation key: {currentCommand.Key}" : $"Outcome unresolved; operation key: {currentCommand.Key}";
            }
            if (failure is FoundationApiException api)
            {
                var commandFailed = currentCommand != null;
                if (api.OutcomeUncertain && commandFailed) actionResult += " An earlier attempt may have committed; keep this key and check the receipt after reconnecting.";
                if (!api.OutcomeUncertain && (int)api.Status is >= 400 and < 500 && api.Status != System.Net.HttpStatusCode.TooManyRequests && api.Code != "operation_replay_expired" && currentCommand != null)
                { configuration.PendingCommands.Remove(currentCommand); save(); currentCommand = null; refreshNeeded = session != null; }
                if (api.Status == System.Net.HttpStatusCode.NotFound && !commandFailed && group != null) { ClearGroupViews(); refreshNeeded = true; }
                if (api.Status == System.Net.HttpStatusCode.Unauthorized) { ClearGroupViews(); session = null; if (saved != null) { saved.PendingRefreshKey = null; save(); } }
            }
        }
    }
    private void Mutate(string method, string path, object? body, string? etag = null, bool signOut = false)
    {
        if (session == null || origin == null || work != null) return;
        var command = new CommandRequest(method, path, body == null ? null : JsonSerializer.Serialize(body, FoundationClient.Json), etag);
        var pending = new PendingCommand { Origin = origin.AbsoluteUri, CharacterId = session.Character.Id, Key = Guid.CreateVersion7(), ProtectedRequest = PendingCommand.Protect(command) };
        configuration.PendingCommands.Add(pending); save(); ExecutePending(pending, signOut);
    }
    private void ExecutePending(PendingCommand pending, bool signOut, bool recovering = false)
    {
        var request = pending.Read(); currentCommand = pending; var token = session!.AccessToken;
        Start(async ct => {
            var response = await client.CommandWithMetadataAsync(origin!, request, pending.Key, token, ct);
            return () => {
                configuration.PendingCommands.Remove(pending); save(); status = "Action completed."; actionResult = "Action completed."; actionDiagnostic = "Operation key: " + pending.Key;
                ApplyFormSuccess(request, response.Json);
                ApplyInventorySuccess(request, response, recovering);
                if (request.Path.Contains("/members/", StringComparison.Ordinal)) memberDraftEtag = JsonSerializer.Deserialize<CommandResult<Member>>(response.Json, FoundationClient.Json)!.Result.Etag;
                if (request.Path.EndsWith("/policies", StringComparison.Ordinal)) policyDraftVersion = JsonSerializer.Deserialize<CommandResult<GroupPolicies>>(response.Json, FoundationClient.Json)!.Result.Version;
                if (request.Method == "PATCH" && request.Path.EndsWith("/currency", StringComparison.Ordinal)) editCurrencyVersion = JsonSerializer.Deserialize<CommandResult<AmorRP.Contracts.Currency.Currency>>(response.Json, FoundationClient.Json)!.Result.Version;
                if (request.Method == "PATCH" && request.Path.Split('/').Length == 4) editGroupVersion = JsonSerializer.Deserialize<CommandResult<Group>>(response.Json, FoundationClient.Json)!.Result.Version;
                if (request.Method == "POST" && request.Path.EndsWith("/invitations", StringComparison.Ordinal))
                    createdCode = JsonSerializer.Deserialize<CommandResult<InvitationCreated>>(response.Json, FoundationClient.Json)!.Result.InvitationCode;
                if (signOut || request.Path == "api/v1/auth/sessions/current") { ForgetCurrent(); status = "This device session was revoked. Sign in to reconnect."; }
                else refreshNeeded = true;
            };
        }, "Saving action…");
    }
    private void Refresh()
    {
        var uri = origin!; var token = session!.AccessToken; var selectedId = selected;
        var actorId = session.Character.Id; var includeInactive = showInactive; var selectedMemberId = memberSelection;
        nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1);
        Start(async ct => {
            var capability = await client.GetAsync<CapabilitiesResponse>(uri, "api/v1/capabilities", token, ct);
            var groupList = new List<Group>(); string? groupCursor = null;
            do { var page = await client.GetAsync<GroupPage>(uri, "api/v1/groups" + (groupCursor == null ? "" : "?cursor=" + Uri.EscapeDataString(groupCursor)), token, ct); groupList.AddRange(page.Items); groupCursor = page.NextCursor; } while (groupCursor != null);
            var list = new GroupPage(groupList.ToArray(), null, "read", null);
            var active = list.Items.FirstOrDefault(x => x.Id == selectedId) ?? list.Items.FirstOrDefault();
            MemberPage? roster = null; InvitationPage? invites = null; GroupPolicies? limits = null; HistoryEntryPage? log = null; OwnershipTransfer? proposal = null;
            if (active != null)
            {
                var isOwner = active.OwnerCharacterId == actorId;
                roster = await client.GetAsync<MemberPage>(uri, $"api/v1/groups/{active.Id}/members" + (isOwner && includeInactive ? "?includeDormant=true" : ""), token, ct);
                if (selectedMemberId.HasValue && !roster.Items.Any(x => x.Character.Id == selectedMemberId))
                {
                    try {
                        var current = await client.GetAsync<Member>(uri, $"api/v1/groups/{active.Id}/members/{selectedMemberId}", token, ct);
                        if (current.Status == "active" || (isOwner && includeInactive)) roster = roster with { Items = [..roster.Items, current] };
                    } catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
                }
                limits = await client.GetAsync<GroupPolicies>(uri, $"api/v1/groups/{active.Id}/policies", token, ct);
                log = await client.GetAsync<HistoryEntryPage>(uri, $"api/v1/groups/{active.Id}/" + (isOwner ? "audit" : "history"), token, ct);
                if (isOwner) invites = await client.GetAsync<InvitationPage>(uri, $"api/v1/groups/{active.Id}/invitations", token, ct);
                if (active.PendingOwnershipTransferId.HasValue && (isOwner || roster.Items.Any(x => x.Character.Id == actorId)))
                {
                    try { proposal = await client.GetAsync<OwnershipTransfer>(uri, $"api/v1/groups/{active.Id}/ownership-transfers/{active.PendingOwnershipTransferId}", token, ct); }
                    catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
                }
            }
            var inventoryState = active == null ? null : await LoadInventoryAsync(uri, token, active.Id, ct);
            byte[]? iconBytes = null;
            if (active?.Currency.IconAssetId != null && !icons.Matches(uri.AbsoluteUri + actorId + active.Id + active.Currency.IconAssetId))
                iconBytes = await client.GetIconAsync(uri, $"api/v1/groups/{active.Id}/currency/icon", token, ct);
            var sessions = await client.GetAsync<SessionInfoPage>(uri, "api/v1/me/sessions", token, ct);
            return () => {
                var changedGroup = group?.Id != active?.Id;
                serviceLimits = capability.Limits;
                if (lastRefresh == default) { initialPoints = capability.Limits.DefaultPotionPoints; initialLetters = capability.Limits.DefaultLettersPerWeek; }
                if (changedGroup) ClearGroupViews();
                groups = list.Items; group = active; selected = active?.Id; members = roster?.Items ?? []; invitations = invites?.Items ?? []; history = log?.Items ?? [];
                membersCursor = roster?.NextCursor; invitationsCursor = invites?.NextCursor; historyCursor = log?.NextCursor;
                policies = limits; transfer = proposal; devices = sessions.Items;
                if (saved != null) { saved.SelectedGroupId = selected; save(); }
                if (limits != null && (changedGroup || !policyDraftLoaded)) { policyDraftLoaded = true; policyDraftVersion = limits.Version; nextPoints = limits.Next.PotionPoints; nextLetters = limits.Next.Letters; }
                if (!members.Any(x => x.Character.Id == memberSelection)) memberSelection = null;
                AcceptInventoryState(inventoryState);
                if (active != null) icons.Update(uri.AbsoluteUri + actorId + active.Id + active.Currency.IconAssetId, iconBytes, active.Currency.IconAssetId != null);
                lastRefresh = DateTimeOffset.UtcNow; nextRefresh = lastRefresh.AddMinutes(1); status = "Data refreshed.";
            };
        }, "Loading groups…");
    }
    private void ForgetCurrent()
    { if (saved != null) configuration.SavedSessions.Remove(saved); save(); session = null; saved = null; group = null; groups = []; members = []; ClearGroupViews(); }
    private void ClearWork()
    {
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        if (work != null) _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        work = null; currentCommand = null;
    }
    private void Clear()
    { ClearWork(); origin = null; session = null; saved = null; attempt = null; selected = null; group = null; groups = []; members = []; invitations = []; history = []; devices = []; transfer = null; refreshNeeded = false; createdCode = ""; actionResult = ""; actionDiagnostic = ""; lastRefresh = default; nextRefresh = default; ClearGroupViews(); }
    private static void Wrapped(string message) { ImGui.PushTextWrapPos(); ImGui.TextUnformatted(message); ImGui.PopTextWrapPos(); }
    public void Dispose() { ClearWork(); lifetime.Dispose(); icons.Dispose(); client.Dispose(); }
}
