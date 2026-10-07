using Dalamud.Configuration;

namespace AmorRP.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public List<Services.Identity.SavedSession> SavedSessions { get; set; } = [];
    public List<Services.Identity.PendingCommand> PendingCommands { get; set; } = [];
    public string BackendUrl { get; set; } = "http://127.0.0.1:5080/";
}
