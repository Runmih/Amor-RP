using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private int recipeChoice, productionQuantity = 1, destination;
    private ConsumptionResult? lastUse;
    private bool resendConfirmed;
    private DateTimeOffset lastPost;
    private void DrawConsumables()
    {
        if (group == null) { Wrapped("Join or create a group first."); return; }
        DrawQuotas();
        ImGui.Checkbox("Show pinned potions only", ref onlyPinned);
        foreach (var item in compact.Where(x => !onlyPinned || pinned.Contains(x.Id)))
        {
            ImGui.PushID(item.Id.ToString());
            if (ImGui.SmallButton(pinned.Contains(item.Id) ? "Unpin" : "Pin")) TogglePin(item.Id);
            ImGui.SameLine(); if (ImGui.Selectable($"{item.Name} ×{item.Available} (revision {item.DefinitionRevision})", holdingSelection == item.Id)) ReadHolding(item);
            ImGui.PopID();
        }
        if (compact.Length == 0) Wrapped("You have no potions. Authorized creation uses your own weekly points.");
        LoadMoreInventory(true);
        if (details?.Definition != null) { Wrapped(details.Definition.Description); DrawPotionUse(details); }
        DrawResend();
    }
    private bool onlyPinned;
    private void DrawPotionCreation()
    {
        if (!(Owner || group!.MyCapabilities.Contains("items.potion.create"))) { Wrapped("The owner must authorize potion creation for this character."); return; }
        var recipes = definitions.Where(x => !x.Retired && x.Enabled).ToArray();
        if (recipes.Length == 0) { Wrapped("The owner has not enabled a potion recipe yet."); return; }
        recipeChoice = Math.Clamp(recipeChoice, 0, recipes.Length - 1);
        ImGui.Combo("Potion recipe", ref recipeChoice, recipes.Select(x => x.Name + " (revision " + x.Revision + ")").ToArray(), recipes.Length);
        var recipe = recipes[recipeChoice]; Wrapped(recipe.Description); Wrapped("Use message: " + recipe.Potion.UseMessage);
        ImGui.InputInt("Copies to create", ref productionQuantity);
        var cost = (long)productionQuantity * recipe.Potion.CreationCost; Wrapped($"Cost: {recipe.Potion.CreationCost} points each; {cost} points total.");
        ImGui.BeginDisabled(productionQuantity < 1 || productionQuantity > (serviceLimits?.MaxCreationQuantity ?? 100) || cost > (quotas?.Items.FirstOrDefault(x => x.Kind == "potion_points")?.Remaining ?? 0));
        if (ImGui.Button("Create potion copies")) Mutate("POST", $"api/v1/groups/{group!.Id}/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, productionQuantity));
        ImGui.EndDisabled();
    }
    private static string DestinationId(int index) => index switch { 0 => "emote", 1 => "say", 2 => "party", >= 3 and <= 10 => "linkshell:" + (index - 2), _ => "crossworld-linkshell:" + (index - 10) };
    private static int DestinationIndex(string id) => Enumerable.Range(0, ChatCommand.Destinations.Length).FirstOrDefault(i => DestinationId(i) == id, -1);
    private bool CanPost(string message, int channel) => consent.Allowed && ChatDestination.Available(player, party, channel)
        && ChatCommand.TryCreate(message, channel, out _) && DateTimeOffset.UtcNow - lastPost >= TimeSpan.FromSeconds(3);
    private void DrawPotionUse(HoldingDetails item)
    {
        if (item.Definition == null) return;
        if (consent.Answered) { var allowed = consent.Allowed; if (ImGui.Checkbox("Enable RP chat for this startup", ref allowed)) consent.Allowed = allowed; }
        else Wrapped("Answer the startup permission question to use potions.");
        ImGui.Combo("Potion destination", ref destination, ChatCommand.Destinations, ChatCommand.Destinations.Length);
        var message = item.Definition.Potion.UseMessage;
        Wrapped($"Uses one {item.Holding.Name}; group {group!.Name}; character {session!.Character.DisplayName}.");
        Wrapped(ChatCommand.TryCreate(message, destination, out var preview) ? "Exact message: " + preview : "This message cannot be posted safely.");
        if (destination == 2 && party.Length == 0) Wrapped("Join a party before using the party channel.");
        if (!ChatDestination.Available(player, party, destination)) Wrapped("The selected channel is unavailable. Choose a joined linkshell or available channel before consuming.");
        ImGui.BeginDisabled(item.Holding.Available < 1 || !CanPost(message, destination));
        if (ImGui.Button("Use one potion")) { Mutate("POST", $"api/v1/groups/{group.Id}/inventory/{item.Holding.Id}/use", new ConsumptionRequest(item.Holding.Version, consent.Allowed, DestinationId(destination))); }
        ImGui.EndDisabled();
    }
    private void DrawResend()
    {
        if (lastUse == null) return;
        Wrapped("Last consumed message: " + lastUse.Message + " → " + lastUse.Destination);
        ImGui.Checkbox("Resend may duplicate a message already delivered", ref resendConfirmed);
        ImGui.BeginDisabled(!resendConfirmed || !CanPost(lastUse.Message, DestinationIndex(lastUse.Destination)));
        if (ImGui.Button("Manually resend last consumed message"))
        {
            lastPost = DateTimeOffset.UtcNow; resendConfirmed = false;
            actionResult = GameChatSender.Send(lastUse.Message, DestinationIndex(lastUse.Destination)) ? "Message submitted again; check the chosen channel." : "Message could not be submitted. The item remains consumed.";
        }
        ImGui.EndDisabled();
    }
}
