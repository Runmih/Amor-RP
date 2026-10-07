using AmorRP.Contracts.Inventory;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string letterTitle = "", letterBody = "", letterEditTitle = "", letterEditBody = "";
    private Guid? editingLetter;
    private int letterEditVersion;
    private int letterCategory;
    private void DrawLetterCreation()
    {
        var choices = categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains("letter")).ToArray();
        if (choices.Length == 0) { Wrapped("Ask the owner to add a category that accepts letters."); return; }
        letterCategory = Math.Clamp(letterCategory, 0, choices.Length - 1);
        ImGui.Combo("Letter category", ref letterCategory, choices.Select(x => x.Name).ToArray(), choices.Length);
        ImGui.InputText("Letter title", ref letterTitle, 4096); ImGui.InputTextMultiline("Letter body", ref letterBody, 65536, new(0,160));
        Wrapped($"Title limit: {serviceLimits?.LetterTitleTextElements ?? 80}; body limit: {serviceLimits?.LetterBodyTextElements ?? 5000}. Creating spends one letter allowance; edits and reads do not.");
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(letterTitle) || (quotas?.Items.FirstOrDefault(x => x.Kind == "letters")?.Remaining ?? 0) < 1);
        if (ImGui.Button("Create letter")) Mutate("POST", $"api/v1/groups/{group!.Id}/inventory/letters", new LetterCreate(choices[letterCategory].Id, letterTitle, letterBody));
        ImGui.EndDisabled();
    }
    private void DrawLetterDetails(Letter letter)
    {
        Wrapped($"Author: {letter.Author.DisplayName} @ {letter.Author.HomeWorldName}; {(letter.Locked ? "locked" : "untraded")}; version {letter.Version}.");
        Wrapped(letter.Body);
        if (letter.Author.Id != session!.Character.Id || letter.Locked) return;
        if (ImGui.Button("Edit this letter")) { editingLetter = letter.Id; letterEditVersion = letter.Version; letterEditTitle = letter.Title; letterEditBody = letter.Body; }
        if (editingLetter != letter.Id) return;
        if (letter.Version != letterEditVersion) Wrapped("Letter changed while editing. Click Edit this letter to review its current contents before saving.");
        ImGui.InputText("Edited title", ref letterEditTitle, 4096); ImGui.InputTextMultiline("Edited body", ref letterEditBody, 65536, new(0,140));
        if (ImGui.Button("Save letter edit")) Mutate("PATCH", $"api/v1/groups/{group!.Id}/letters/{letter.Id}", new LetterEdit(letterEditTitle, letterEditBody), ETag("letter", letter.Id, letterEditVersion));
    }
    private void ClearCreationDrafts()
    {
        letterTitle = letterBody = letterEditTitle = letterEditBody = ""; editingLetter = null; letterCategory = recipeChoice = 0; productionQuantity = 1;
        definitionEditId = null; definitionName = definitionDescription = definitionMessage = categoryName = ""; definitionCost = 1; definitionEnabled = true;
    }
}
