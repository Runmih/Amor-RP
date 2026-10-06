using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AmorRP.Plugin.Features.Diagnostics;

public sealed class ChatConsentWindow : Window
{
    public bool Answered { get; private set; }
    public bool Allowed { get; set; }

    public ChatConsentWindow()
        : base("Amor RP chat permission###AmorRPStartupChatConsent", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        IsOpen = true;
        ShowCloseButton = false;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Allow Amor RP to post RP messages when you use an item?");
        ImGui.TextWrapped("Chat posting is required for potion use. You choose the destination and can see the message before using it. M1 provides a test-post button. Messages are sent only when you click Use or Post test message.");
        ImGui.TextWrapped("This permission lasts until the plugin restarts. Messages, channel changes and character switches do not ask again. You can change the permission in Amor RP settings.");
        if (ImGui.Button("Allow for this startup")) Answer(true);
        ImGui.SameLine();
        if (ImGui.Button("Decline")) Answer(false);
    }

    private void Answer(bool allowed)
    {
        Allowed = allowed;
        Answered = true;
        IsOpen = false;
    }
}
