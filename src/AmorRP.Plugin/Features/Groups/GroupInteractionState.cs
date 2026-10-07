using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private bool policyDraftLoaded;
    private readonly FileDialogManager files = new();
    private string iconPath = "";
    private void SwitchGroup(Group entry)
    {
        ClearGroupViews(); selected = entry.Id; group = entry; refreshNeeded = true;
        if (saved != null) { saved.SelectedGroupId = selected; save(); }
    }
    private void ClearGroupViews()
    {
        icons.Clear(); group = null; members = []; policies = null; transfer = null; invitations = []; history = [];
        memberSelection = null; memberDraftEtag = null; policyDraftVersion = editGroupVersion = editCurrencyVersion = 0; membersCursor = invitationsCursor = historyCursor = null;
        createdCode = ""; policyDraftLoaded = false; showInactive = false;
        editName = editDescription = editCurrencyName = ""; iconPath = "";
        removalReason = restrictionReason = reason = delta = ""; confirmRemoval = false;
        ClearInventoryViews();
    }
    private void ApplyFormSuccess(CommandRequest request, string? response)
    {
        if (request.Method == "POST" && request.Path == "api/v1/groups/join") { invitationCode = ""; visibilityAccepted = false; }
        if (request.Method == "POST" && request.Path == "api/v1/groups")
        {
            name = description = ""; currencyName = "Coins"; symbol = "C";
            initialPoints = serviceLimits?.DefaultPotionPoints ?? 0; initialLetters = serviceLimits?.DefaultLettersPerWeek ?? 5;
        }
        if (request.Method == "POST" && request.Path is "api/v1/groups" or "api/v1/groups/join" && response != null)
            selected = JsonSerializer.Deserialize<CommandResult<Group>>(response, FoundationClient.Json)!.Result.Id;
    }
    private void DrawCurrencyIconEditor()
    {
        files.Draw();
        if (!ImGui.CollapsingHeader("Currency icon")) return;
        Wrapped("Choose a static PNG, JPG or WebP, at most 128 pixels wide and high. Images are stored with this group.");
        icons.DrawIcon();
        if (ImGui.Button("Choose image…")) files.OpenFileDialog("Currency icon", ".png,.jpg,.jpeg,.webp", (ok, path) => { if (ok) iconPath = path; });
        if (iconPath.Length > 0)
        {
            Wrapped("Selected: " + Path.GetFileName(iconPath));
            if (ImGui.Button("Upload currency icon"))
            {
                var path = iconPath; var endpoint = $"api/v1/groups/{group!.Id}/currency/icon"; var etag = ETag("currency", group.Currency.Id, group.Currency.Version);
                Start(async ct => {
                    var maximum = serviceLimits?.MaxCurrencyIconBytes ?? 262144;
                    await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    if (file.Length > maximum || file.Length <= 0) throw new InvalidOperationException($"Choose an image smaller than {maximum / 1024} KiB.");
                    var bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes, ct);
                    return () => QueueUpload(endpoint, etag, bytes);
                }, "Reading selected image…");
            }
        }
        if (group!.Currency.IconAssetId != null && ImGui.Button("Use default currency icon")) Mutate("DELETE", $"api/v1/groups/{group.Id}/currency/icon", null, ETag("currency", group.Currency.Id, group.Currency.Version));
    }
    private void QueueUpload(string path, string etag, byte[] bytes)
    {
        var request = new CommandRequest("PUT", path, null, etag, Convert.ToBase64String(bytes));
        var pending = new PendingCommand { Origin = origin!.AbsoluteUri, CharacterId = session!.Character.Id, Key = Guid.CreateVersion7(), ProtectedRequest = PendingCommand.Protect(request) };
        configuration.PendingCommands.Add(pending); save(); ExecutePending(pending, false);
    }
}
