using System.Numerics;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Diagnostics;

public sealed class MainWindow : Window, IDisposable
{
    private readonly IPlayerState playerState;
    private readonly Configuration configuration;
    private readonly Action save;
    private readonly BackendProbe probe = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task<ProbeResult>? pending;
    private string backendUrl;
    private string status = "Use Check connection after starting the local server.";

    public MainWindow(IPlayerState playerState, Configuration configuration, Action save)
        : base("Amor RP###AmorRPMain")
    {
        this.playerState = playerState;
        this.configuration = configuration;
        this.save = save;
        backendUrl = configuration.BackendUrl;
        Size = new Vector2(540, 340);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        // All game reads happen here, on Dalamud's draw/framework thread.
        ImGui.TextUnformatted("Amor RP — M0 foundation");
        ImGui.Separator();
        if (playerState.IsLoaded)
        {
            var world = playerState.HomeWorld;
            ImGui.TextUnformatted($"Character: {playerState.CharacterName}");
            ImGui.TextUnformatted($"Home world: {(world.IsValid ? world.Value.Name.ToString() : "Unavailable")}");
        }
        else
            ImGui.TextUnformatted("Log into a character to display your home world.");

        ImGui.TextWrapped("Character information is local and unverified. M0 does not authenticate you or send it to the server.");
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
        ImGui.TextWrapped("Next: verified login and game integration (M1). Groups, potions, letters and trades are not available in this build.");
    }

    public void Dispose()
    {
        lifetime.Cancel();
        probe.Dispose();
        lifetime.Dispose();
    }
}
