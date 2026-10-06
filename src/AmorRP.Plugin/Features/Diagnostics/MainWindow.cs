using System.Numerics;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Diagnostics;

public sealed class MainWindow : Window, IDisposable
{
    private readonly IPlayerState playerState;
    private readonly Configuration configuration;
    private readonly Action save;
    private readonly FeasibilityPanel feasibility;
    private readonly BackendProbe probe = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task<ProbeResult>? pending;
    private string backendUrl;
    private string status = "Use Check connection after starting the local server.";

    public MainWindow(IPlayerState playerState, IPartyList party, ChatConsentWindow chatConsent, Configuration configuration, Action save)
        : base("Amor RP###AmorRPMain")
    {
        this.playerState = playerState;
        this.configuration = configuration;
        this.save = save;
        feasibility = new(playerState, party, chatConsent);
        backendUrl = configuration.BackendUrl;
        Size = new Vector2(650, 720);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        // All game reads happen here, on Dalamud's draw/framework thread.
        ImGui.TextUnformatted("Amor RP — M1 integration tests");
        ImGui.Separator();
        if (playerState.IsLoaded)
        {
            var world = playerState.HomeWorld;
            ImGui.TextUnformatted($"Character: {playerState.CharacterName}");
            ImGui.TextUnformatted($"Home world: {(world.IsValid ? world.Value.Name.ToString() : "Unavailable")}");
        }
        else
            ImGui.TextUnformatted("Log into a character to display your home world.");

        ImGui.TextWrapped("This display is local and unverified. Connection checks send no character information.");
        ImGui.Spacing();
        ImGui.InputText("Server address", ref backendUrl, 2048);
        var busy = pending is { IsCompleted: false };
        ImGui.BeginDisabled(busy);
        if (ImGui.Button("Check connection"))
        {
            if (!BackendAddress.TryParse(backendUrl, out var origin))
                status = "Enter an HTTPS origin, or HTTP localhost for local testing. No credentials, path, query or fragment.";
            else
            {
                configuration.BackendUrl = origin!.AbsoluteUri;
                save();
                status = "Checking server…";
                pending = probe.CheckAsync(origin, lifetime.Token);
            }
        }
        ImGui.EndDisabled();
        if (pending is { IsCompleted: true })
        {
            status = pending.IsCompletedSuccessfully ? pending.Result.Summary : "Connection check cancelled or failed.";
            pending = null;
        }
        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(status);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        ImGui.Separator();
        feasibility.Draw(backendUrl);
    }

    public void SelectTarget(CharacterTarget target) { feasibility.SelectTarget(target); IsOpen = true; }

    public void Dispose()
    {
        lifetime.Cancel();
        feasibility.Dispose();
        probe.Dispose();
        lifetime.Dispose();
    }
}
