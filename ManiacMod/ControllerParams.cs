using CounterStrikeSharp.API.Modules.Entities;

namespace ManiacMod;

public class ControllerParams
{
    public string PlayerName;
    public string SteamID;
    public string IpAddress;


    public ControllerParams(string name, string sid, string? ip)
    {
        PlayerName = name ?? string.Empty;
        SteamID = sid ?? string.Empty;
        IpAddress = ip ?? string.Empty;
    }
}