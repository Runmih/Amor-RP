using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private Category[] categories = [];
    private Definition[] definitions = [];
    private Holding[] holdings = [], compact = [], memberHoldings = [];
    private Quotas? quotas;
    private HoldingDetails? details;
    private Guid? holdingSelection;
    private string? holdingCursor, compactCursor, memberHoldingCursor;
    private Guid? memberInventoryFor;
    private string search = "";
    private int filterType, filterCategory, sort;
    private bool discardConfirmed;
    private int discardQuantity = 1, removeQuantity = 1;
    private string itemRemovalReason = "";
    private bool removeItemsConfirmed;
    private HashSet<Guid> pinned = [];
    private sealed record InventoryState(Category[] Categories, Definition[] Definitions, HoldingPage Holdings, HoldingPage Compact, Quotas Quotas, HoldingDetails? Details);
    private string InventoryQuery()
    {
        var args = new List<string> { "sort=" + new[] { "name", "type", "category" }[sort] };
        if (search.Length > 0) args.Add("search=" + Uri.EscapeDataString(search));
        if (filterType > 0) args.Add("typeId=" + new[] { "", "potion", "letter" }[filterType]);
        if (filterCategory > 0 && filterCategory <= categories.Length) args.Add("categoryId=" + categories[filterCategory - 1].Id);
        return string.Join("&", args);
    }
    private async Task<InventoryState> LoadInventoryAsync(Uri uri, string token, Guid id, CancellationToken ct)
    {
        var prefix = $"api/v1/groups/{id}/";
        var cats = new List<Category>(); string? cursor = null;
        do { var page = await client.GetAsync<CategoryPage>(uri, prefix + "categories" + (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), token, ct); cats.AddRange(page.Items); cursor = page.NextCursor; } while (cursor != null);
        var recipes = new List<Definition>(); cursor = null;
        do { var page = await client.GetAsync<DefinitionPage>(uri, prefix + "item-definitions" + (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), token, ct); recipes.AddRange(page.Items); cursor = page.NextCursor; } while (cursor != null);
        var pageHoldings = await client.GetAsync<HoldingPage>(uri, prefix + "inventory?" + InventoryQuery(), token, ct);
        var potions = await client.GetAsync<HoldingPage>(uri, prefix + "inventory?typeId=potion&sort=name", token, ct);
        var allowance = await client.GetAsync<Quotas>(uri, prefix + "quotas", token, ct);
        HoldingDetails? selectedDetails = null;
        if (holdingSelection.HasValue)
        {
            try { selectedDetails = await client.GetAsync<HoldingDetails>(uri, prefix + "inventory/" + holdingSelection, token, ct); }
            catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
        }
        return new(cats.ToArray(), recipes.ToArray(), pageHoldings, potions, allowance, selectedDetails);
    }
    private void AcceptInventoryState(InventoryState? state)
    {
        if (state == null) { ClearInventoryViews(); return; }
        var filterId = filterCategory > 0 && filterCategory <= categories.Length ? (Guid?)categories[filterCategory - 1].Id : null;
        var currentRecipes = definitions.Where(x => !x.Retired && x.Enabled).ToArray();
        var recipeId = recipeChoice < currentRecipes.Length ? (Guid?)currentRecipes[recipeChoice].Id : null;
        categories = state.Categories; definitions = state.Definitions; holdings = state.Holdings.Items; holdingCursor = state.Holdings.NextCursor;
        compact = state.Compact.Items; compactCursor = state.Compact.NextCursor; quotas = state.Quotas;
        filterCategory = filterId.HasValue ? Array.FindIndex(categories, x => x.Id == filterId) + 1 : 0;
        if (recipeId.HasValue) recipeChoice = Math.Max(0, Array.FindIndex(definitions.Where(x => !x.Retired && x.Enabled).ToArray(), x => x.Id == recipeId));
        details = state.Details;
        holdingSelection = details?.Holding.Id;
        memberHoldings = []; memberInventoryFor = null; memberHoldingCursor = null;
        LoadPins();
    }
    private void ClearInventoryViews()
    {
        categories = []; definitions = []; holdings = []; compact = []; memberHoldings = []; quotas = null; details = null;
        holdingSelection = memberInventoryFor = null; holdingCursor = compactCursor = memberHoldingCursor = null;
        pinned = []; filterCategory = filterType = sort = 0; search = "";
        discardConfirmed = removeItemsConfirmed = false; discardQuantity = removeQuantity = 1; itemRemovalReason = "";
        ClearCreationDrafts(); lastUse = null;
    }
    private void DrawQuotas()
    {
        if (quotas == null) return;
        foreach (var q in quotas.Items) Wrapped($"{(q.Kind == "potion_points" ? "Potion points" : "Letters")}: {q.Remaining}/{q.Limit} remaining; resets {q.ResetAt.LocalDateTime:g}.");
    }
    private void ReadHolding(Holding row)
    {
        holdingSelection = row.Id; details = null; discardConfirmed = false;
        var path = $"api/v1/groups/{group!.Id}/inventory/{row.Id}"; var token = session!.AccessToken;
        Start(async ct => { var loaded = await client.GetAsync<HoldingDetails>(origin!, path, token, ct); return () => { details = loaded; }; }, "Reading selected item…");
    }
    private void DrawInventory()
    {
        if (group == null) { Wrapped("Join or create a group first."); return; }
        DrawQuotas();
        ImGui.InputText("Search inventory", ref search, 400);
        ImGui.Combo("Item type", ref filterType, new[] { "All", "Potions", "Letters" }, 3);
        var catNames = new[] { "All categories" }.Concat(categories.Select(x => x.Name)).ToArray(); filterCategory = Math.Clamp(filterCategory, 0, catNames.Length - 1);
        ImGui.Combo("Category filter", ref filterCategory, catNames, catNames.Length);
        ImGui.Combo("Sort by", ref sort, new[] { "Name", "Type", "Category" }, 3);
        if (ImGui.Button("Apply filters")) { details = null; refreshNeeded = true; }
        foreach (var item in holdings)
            if (ImGui.Selectable($"{item.Name} — {item.TypeId} — {item.Available} available ({item.Reserved} reserved)###holding{item.Id}", holdingSelection == item.Id)) ReadHolding(item);
        if (holdings.Length == 0) Wrapped("No items match this view. Create a letter or an authorized potion copy.");
        LoadMoreInventory(false);
        DrawDetails();
    }
    private void LoadMoreInventory(bool small)
    {
        var cursor = small ? compactCursor : holdingCursor;
        if (cursor == null || !ImGui.Button(small ? "More consumables" : "More inventory items")) return;
        var path = $"api/v1/groups/{group!.Id}/inventory?" + (small ? "typeId=potion&sort=name" : InventoryQuery()) + "&cursor=" + Uri.EscapeDataString(cursor);
        var token = session!.AccessToken;
        Start(async ct => { var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct); return () => {
            if (small) { compact = [..compact, ..page.Items]; compactCursor = page.NextCursor; }
            else { holdings = [..holdings, ..page.Items]; holdingCursor = page.NextCursor; }
        }; }, "Loading inventory page…");
    }
    private void DrawDetails()
    {
        if (details == null) return;
        var item = details.Holding; ImGui.Separator(); Wrapped($"{item.Name} — {item.Available} available; group {group!.Name}.");
        Wrapped("Category: " + (categories.FirstOrDefault(x => x.Id == item.CategoryId)?.Name ?? "Retained category"));
        if (details.Definition != null) { Wrapped(details.Definition.Description); Wrapped("Recipe revision: " + details.Definition.Revision); DrawPotionUse(details); }
        if (details.Letter != null) DrawLetterDetails(details.Letter);
        DrawResend();
        ImGui.InputInt("Discard quantity", ref discardQuantity);
        ImGui.Checkbox("Confirm discard; creation allowance is not refunded", ref discardConfirmed);
        ImGui.BeginDisabled(!discardConfirmed || discardQuantity < 1 || discardQuantity > item.Available);
        if (ImGui.Button("Discard selected items")) Mutate("POST", $"api/v1/groups/{group.Id}/inventory/{item.Id}/discard", new DiscardRequest(discardQuantity, item.Version));
        ImGui.EndDisabled();
    }
    private void DrawCreation()
    {
        if (group == null) { Wrapped("Join or create a group first."); return; }
        DrawQuotas();
        if (ImGui.CollapsingHeader("Create potion copies")) DrawPotionCreation();
        if (ImGui.CollapsingHeader("Write a letter")) DrawLetterCreation();
    }
    private void DrawMemberInventory(Member target)
    {
        if (!(Owner || group!.MyCapabilities.Contains("inventory.remove"))) return;
        if (ImGui.Button("Inspect holding summaries for removal"))
        {
            var path = $"api/v1/groups/{group!.Id}/members/{target.Character.Id}/holdings"; var token = session!.AccessToken;
            Start(async ct => { var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct); return () => { memberHoldings = page.Items; memberInventoryFor = target.Character.Id; memberHoldingCursor = page.NextCursor; }; }, "Reading holding summaries…");
        }
        if (memberInventoryFor != target.Character.Id) return;
        if (memberHoldingCursor != null && ImGui.Button("More holding summaries"))
        {
            var path = $"api/v1/groups/{group!.Id}/members/{target.Character.Id}/holdings?cursor=" + Uri.EscapeDataString(memberHoldingCursor); var token = session!.AccessToken;
            Start(async ct => { var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct); return () => { memberHoldings = [..memberHoldings, ..page.Items]; memberHoldingCursor = page.NextCursor; }; }, "Reading more summaries…");
        }
        Wrapped("Summaries only; management authority does not reveal letter contents. Inactive holdings cannot be removed.");
        ImGui.InputInt("Items to remove", ref removeQuantity); ImGui.InputText("Item removal reason", ref itemRemovalReason, 2048);
        ImGui.Checkbox("Confirm item removal without quota refund", ref removeItemsConfirmed);
        foreach (var item in memberHoldings)
        {
            Wrapped($"{item.Name} ({item.TypeId}): {item.Available} available"); ImGui.PushID(item.Id.ToString());
            ImGui.BeginDisabled(target.Status != "active" || !removeItemsConfirmed || string.IsNullOrWhiteSpace(itemRemovalReason) || removeQuantity < 1 || removeQuantity > item.Available);
            if (ImGui.Button("Remove these items")) Mutate("POST", $"api/v1/groups/{group!.Id}/inventory/removals", new InventoryRemoval(target.Character.Id, item.Id, removeQuantity, item.Version, itemRemovalReason));
            ImGui.EndDisabled(); ImGui.PopID();
        }
    }
    private string PinScope => origin!.AbsoluteUri + "|" + session!.Character.Id + "|" + group!.Id;
    private void LoadPins() { if (group != null) pinned = configuration.PinnedHoldings.TryGetValue(PinScope, out var pins) ? pins.ToHashSet() : []; }
    private void TogglePin(Guid id) { if (!pinned.Add(id)) pinned.Remove(id); configuration.PinnedHoldings[PinScope] = pinned.ToArray(); save(); }
    private void ApplyInventorySuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        if (request.Method == "POST" && request.Path.EndsWith("/item-definitions", StringComparison.Ordinal))
        {
            var recipe = JsonSerializer.Deserialize<CommandResult<Definition>>(response.Json, FoundationClient.Json)!.Result;
            definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision;
        }
        if (request.Method == "PATCH" && request.Path.Contains("/item-definitions/", StringComparison.Ordinal))
            definitionEditRevision = JsonSerializer.Deserialize<CommandResult<Definition>>(response.Json, FoundationClient.Json)!.Result.Revision;
        if (request.Method == "PATCH" && request.Path.Contains("/letters/", StringComparison.Ordinal))
            letterEditVersion = JsonSerializer.Deserialize<CommandResult<Letter>>(response.Json, FoundationClient.Json)!.Result.Version;
        if (request.Path.EndsWith("/inventory/letters", StringComparison.Ordinal)) { letterTitle = letterBody = ""; }
        if (!request.Path.EndsWith("/use", StringComparison.Ordinal)) return;
        var result = JsonSerializer.Deserialize<CommandResult<ConsumptionResult>>(response.Json, FoundationClient.Json)!.Result;
        lastUse = result; resendConfirmed = false;
        var destination = DestinationIndex(result.Destination);
        var submitted = !recovering && !response.Replayed && CanPost(result.Message, destination);
        if (submitted) lastPost = DateTimeOffset.UtcNow;
        actionResult = submitted && GameChatSender.Send(result.Message, destination)
            ? "Potion consumed. Message submitted to the chosen game channel; delivery is not confirmed."
            : "Potion consumed. Chat was not submitted by this response. Review the outcome before a manual resend.";
    }
}
