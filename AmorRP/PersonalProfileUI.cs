using ImGuiNET;
using AmorRP.Data;

namespace AmorRP.UI;

public class PersonalProfileUI
{
    private readonly RoleplayProfile profile;
    public bool IsOpen = true;

    public PersonalProfileUI(RoleplayProfile profile)
    {
        this.profile = profile;
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.Begin("Personal Profile", ref IsOpen, ImGuiWindowFlags.AlwaysAutoResize);

        // Save button at top left
        if (ImGui.Button("Save"))
        {
            profile.Save();
        }

        ImGui.Separator();

        ImGui.Text("Display Name:");
        string name = profile.DisplayName;
        if (ImGui.InputText("##displayname", ref name, 100))
        {
            profile.DisplayName = name;
        }

        ImGui.End();
    }
}
