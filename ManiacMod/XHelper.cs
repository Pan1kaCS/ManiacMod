using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace ManiacMod;

public class XHelper
{
    public static List<string> GetArgsFromCommandLine(string commandLine)
    {
        List<string> args = new List<string>();
        var regex = new Regex(@"""((\\\"")|([^""]))*""|'((\\')|([^']))*'|(\S+)");
        var matches = regex.Matches(commandLine);
        foreach (Match match in matches)
        {
            args.Add(match.Value);
        }
        if (args.Count > 0)
            args.RemoveAt(0);
        return args;
    }

    public static List<CCSPlayerController> GetOnlinePlayers()
    {
        var players = Utilities.GetPlayers();

        List<CCSPlayerController> validPlayers = new List<CCSPlayerController>();

        foreach (var p in players)
        {
            if (p == null) continue;
            if (!p.IsValid) continue;
            if (p.IsBot) continue;
            if (p.Connected != PlayerConnectedState.PlayerConnected) continue;
            validPlayers.Add(p);
        }

        return validPlayers;
    }

    public static List<string> SeparateString(string str)
    {
        List<string> sepStr = str.Split("\n").ToList();
        return sepStr;
    }

    public static CCSPlayerController? GetPlayerFromArg(string identity)
    {
        CCSPlayerController? player = null;
        if (identity.StartsWith("#"))
        {
            identity = identity.Replace("#", "");
            if (identity.Length < 17)
            {
                int uid;
                if (Int32.TryParse(identity, out uid))
                {
                    foreach (var p in GetOnlinePlayers())
                    {
                        if (!p.IsBot && p.IsValid)
                        {
                            if (p.UserId == uid)
                            {
                                return p;
                            }
                        }
                    }
                }
            }

            if (identity.Length == 17)
            {
                ulong sid;
                if (UInt64.TryParse(identity, out sid))
                {
                    if (Utilities.GetPlayerFromSteamId(sid) != null)
                    {
                        player = Utilities.GetPlayerFromSteamId(sid);
                        return player;
                    }
                }
            }
        }
        if (!identity.StartsWith("#"))
            return GetOnlinePlayers().FirstOrDefault(u => u.PlayerName.Contains(identity));
        return null;
    }

    public static string? GetIdentityType(string identity)
    {
        if (!identity.StartsWith("#")) return "name";
        if (identity.StartsWith("#") && identity.Length < 17) return "uid";
        if (identity.StartsWith("#") && identity.Replace("#", "").Length == 17) return "sid";
        return null;
    }

    public static string ReplaceColors(string str)
    {
        foreach (FieldInfo field in typeof(ChatColors).GetFields())
        {
            string pattern = $"{{{field.Name}}}";
            var valueObj = field.GetValue(null);
            string valueStr = valueObj?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(valueStr)) continue;

            if (str.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                str = str.Replace(pattern, valueStr, StringComparison.OrdinalIgnoreCase);
            }
        }
        return str;
    }

    public static string GetServerIp()
    {
        var cv = ConVar.Find("ip");
        if (cv == null) return string.Empty;
        return cv.StringValue ?? string.Empty;
    }

    public static string GetServerPort()
    {
        var cv = ConVar.Find("hostport");
        if (cv == null) return "0";
        try
        {
            var val = cv.GetPrimitiveValue<int>();
            return val.ToString();
        }
        catch
        {
            return "0";
        }
    }

    public static DateTime UnixTimeStampToDateTime(ulong unixTimeStamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds((long)unixTimeStamp).DateTime.ToLocalTime();
    }

    public static string GetDateStringFromUTC(ulong unixTimeStamp)
    {
        return UnixTimeStampToDateTime(unixTimeStamp).ToString("dd.MM.yyyy HH:mm:ss");
    }

    public static bool IsControllerValid(CCSPlayerController? controller)
    {
        if (controller == null) return false;
        if (!controller.IsValid) return false;
        if (controller.IsBot) return false;
        if (controller.Connected != PlayerConnectedState.PlayerConnected) return false;
        return true;
    }

    public static ControllerParams GetControllerParams(CCSPlayerController controller)
    {
        string name = controller.PlayerName ?? string.Empty;
        string steam = controller.SteamID.ToString();
        string ip = controller.IpAddress ?? string.Empty;

        var res = new ControllerParams(name, steam, ip);
        return res;
    }
}