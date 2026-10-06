using AmorRP.Plugin.Features.Diagnostics;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/amorrp";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly WindowSystem windows = new("AmorRP");
    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPlayerState playerState)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        var configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        mainWindow = new MainWindow(playerState, configuration, () => pluginInterface.SavePluginConfig(configuration));
        windows.AddWindow(mainWindow);
        commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "Open Amor RP." });
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }

    private void OnCommand(string command, string arguments) => mainWindow.Toggle();
    private void Open() => mainWindow.IsOpen = true;

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= Open;
        pluginInterface.UiBuilder.OpenConfigUi -= Open;
        commands.RemoveHandler(Command);
        windows.RemoveAllWindows();
        mainWindow.Dispose();
    }
}
