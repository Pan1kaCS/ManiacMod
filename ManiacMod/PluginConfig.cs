using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace ManiacMod;

public class PluginConfig : IBasePluginConfig
{
    [JsonPropertyName("Maniacshp")]
    public string Maniacshp { get; set; } = "777";

    [JsonPropertyName("RowEditFlag")]
    public string RowEditFlag { get; set; } = "@css/generic";

    [JsonPropertyName("Maniacs")]
    public Maniac[] Maniacs { get; set; } = new Maniac[]
    {
        new Maniac(2, 6),
        new Maniac(3, 10)
    };

    // Implementing IBasePluginConfig.Version — mapped to "ConfigVersion" in JSON
    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}