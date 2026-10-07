using AmorRP.Contracts.Inventory;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string definitionName = "", definitionDescription = "", definitionMessage = "", categoryName = "";
    private int definitionCost = 1, definitionCategory;
    private bool definitionEnabled = true, categoryPotions = true, categoryLetters;
    private Guid? definitionEditId;
    private int? definitionEditRevision;
    private void DrawCatalogEditor()
    {
        if (ImGui.CollapsingHeader("Categories"))
        {
            ImGui.InputText("New category name", ref categoryName, 256); ImGui.Checkbox("Accept potions", ref categoryPotions); ImGui.Checkbox("Accept letters", ref categoryLetters);
            if (ImGui.Button("Add category")) Mutate("POST", $"api/v1/groups/{group!.Id}/categories", new CategoryEdit(categoryName, new[] { categoryPotions ? "potion" : "", categoryLetters ? "letter" : "" }.Where(x => x.Length > 0).ToArray()));
            foreach (var category in categories)
            {
                ImGui.PushID(category.Id.ToString()); Wrapped(category.Name + " — " + string.Join(", ", category.AllowedTypeIds) + (category.Retired ? " (retired)" : ""));
                if (!category.Retired)
                {
                    if (ImGui.Button("Rename to entered category name")) Mutate("PATCH", $"api/v1/groups/{group!.Id}/categories/{category.Id}", new CategoryEdit(categoryName, category.AllowedTypeIds), ETag("category", category.Id, category.Version));
                    ImGui.SameLine(); if (ImGui.Button("Retire category")) Mutate("POST", $"api/v1/groups/{group!.Id}/categories/{category.Id}/retirement", null, ETag("category", category.Id, category.Version));
                }
                ImGui.PopID();
            }
        }
        if (!ImGui.CollapsingHeader("Potion recipes")) return;
        Wrapped("Definitions describe potions; creating a recipe does not mint copies. Copies keep their original revision, cost and use message.");
        foreach (var recipe in definitions)
        {
            ImGui.PushID(recipe.Id.ToString()); Wrapped($"{recipe.Name}, revision {recipe.Revision}, {recipe.Potion.CreationCost} points{(recipe.Retired ? " — retired" : recipe.Enabled ? " — enabled" : " — disabled")}");
            if (!recipe.Retired)
            {
                if (ImGui.Button("Edit recipe"))
                {
                    definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; definitionName = recipe.Name;
                    definitionDescription = recipe.Description; definitionMessage = recipe.Potion.UseMessage; definitionCost = recipe.Potion.CreationCost; definitionEnabled = recipe.Enabled;
                    definitionCategory = Array.FindIndex(categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains("potion")).ToArray(), x => x.Id == recipe.CategoryId);
                }
                ImGui.SameLine(); if (ImGui.Button("Retire recipe")) Mutate("POST", $"api/v1/groups/{group!.Id}/item-definitions/{recipe.Id}/retirement", null, ETag("definition", recipe.Id, recipe.Revision));
            }
            ImGui.PopID();
        }
        if (definitionEditId.HasValue && ImGui.Button("Start a new recipe")) { definitionEditId = null; definitionEditRevision = null; definitionName = definitionDescription = definitionMessage = ""; definitionCost = 1; }
        var choices = categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains("potion")).ToArray();
        if (choices.Length == 0) { Wrapped("Add an active potion category before defining a recipe."); return; }
        definitionCategory = Math.Clamp(definitionCategory, 0, choices.Length - 1);
        ImGui.Combo("Recipe category", ref definitionCategory, choices.Select(x => x.Name).ToArray(), choices.Length);
        ImGui.InputText("Potion name", ref definitionName, 256); ImGui.InputTextMultiline("Potion description", ref definitionDescription, 8192, new(0,90));
        ImGui.InputText("Literal use message", ref definitionMessage, 512); ImGui.InputInt("Creation difficulty / points per copy", ref definitionCost); ImGui.Checkbox("Enable production", ref definitionEnabled);
        Wrapped($"Message limit: {serviceLimits?.ChatMessageTextElements ?? 50} characters / 200 UTF-8 bytes. Players see this exact message before use.");
        var editing = definitions.FirstOrDefault(x => x.Id == definitionEditId);
        if (editing != null && editing.Revision != definitionEditRevision) Wrapped("This recipe changed while editing. Reload its editor before saving.");
        if (ImGui.Button(definitionEditId == null ? "Define potion recipe" : "Save a new recipe revision"))
        {
            var body = new DefinitionWrite("potion", definitionName, choices[definitionCategory].Id, definitionDescription, definitionEnabled, new(definitionCost, definitionMessage));
            if (definitionEditId.HasValue) Mutate("PATCH", $"api/v1/groups/{group!.Id}/item-definitions/{definitionEditId}", body, ETag("definition", definitionEditId.Value, definitionEditRevision ?? 0));
            else Mutate("POST", $"api/v1/groups/{group!.Id}/item-definitions", body);
        }
    }
}
