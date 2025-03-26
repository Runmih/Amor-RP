using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AmorRP.Windows;
using AmorRP;
using AmorRP.Data;
using AmorRP.UI;
using System;

namespace AmorRP;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/amorp";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("AmorRP");
    private MainWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    private RoleplayProfile Profile { get; init; }
    public PersonalProfileUI? ProfileUI;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "/amorp profile - Open or close the personal RP profile window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");

        // Load RP profile and setup profile UI
        Profile = RoleplayProfile.Load();
        ProfileUI = new PersonalProfileUI(Profile);
        PluginInterface.UiBuilder.Draw += Draw;
    }

    private void Draw()
    {
        ProfileUI?.Draw();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("profile", StringComparison.OrdinalIgnoreCase))
        {
            if (ProfileUI is { } ui)
            {
                ui.IsOpen = !ui.IsOpen;
                Log.Information($"Toggled Personal Profile window: {(ui.IsOpen ? "Open" : "Closed")}");
            }
        }
        else
        {
            ToggleMainUI();
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
