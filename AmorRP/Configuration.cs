using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AmorRP;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    // Personal code
    public string CustomDisplayName { get; set; } = "Unnamed Inmate";


    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
