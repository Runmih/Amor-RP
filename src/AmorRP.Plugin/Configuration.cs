using Dalamud.Configuration;

namespace AmorRP.Plugin;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string BackendUrl { get; set; } = "http://127.0.0.1:5080/";
}
