using System;
using System.IO;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace AmorRP.Data;

public class RoleplayProfile
{
    public string DisplayName { get; set; } = "Unnamed Inmate";

    [JsonIgnore]
    private static string SavePath => Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "RoleplayProfile.json");

    public void Save()
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(SavePath, json);
    }

    public static RoleplayProfile Load()
    {
        if (!File.Exists(SavePath))
            return new RoleplayProfile();

        var json = File.ReadAllText(SavePath);
        try
        {
            return JsonConvert.DeserializeObject<RoleplayProfile>(json) ?? new RoleplayProfile();
        }
        catch (Exception e)
        {
            Plugin.Log.Error($"Failed to load RoleplayProfile: {e.Message}");
            return new RoleplayProfile();
        }
    }
}
