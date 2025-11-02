using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text.Json;

namespace ManiacMod
{
    public class ManiacMod : BasePlugin
    {
        public override string ModuleName => "ManiacMod";
        public override string ModuleVersion => "0.0.4";
        public override string ModuleAuthor => "Maslenka";

        public PluginConfig Config { get; private set; } = null!;
        private Dictionary<string, string> translations = new();

        private bool maniacTimerRunning = false;
        private int countdownDurationSeconds = 10;
        private int mpFreezeTime = 5;
        private int mpRoundDuration = 120; // Set default value
        private DateTime roundStartTime;
        private bool maniacReleased = false;
        private int maniacReleaseTime; // time in seconds after round start when maniac releases

        private bool isFirstRound = true;
        private bool roundStarted = false;

        // Queue of players who want to be maniacs
        private List<CCSPlayerController> playersInRow = new List<CCSPlayerController>();
        // Selected maniacs for the round
        private List<CCSPlayerController> seekers = new List<CCSPlayerController>();

        private Random rnd = new Random();

        private int lastBroadcastSecond = -1;

        public override void Load(bool hotReload)
        {
            Console.WriteLine("ManiacMod Load");
            Config = EnsureConfigLoaded(ModuleName);

            LoadTranslations();

            RegisterListener<Listeners.OnTick>(OnTick);

            // Commands are registered via attribute, so remove RegisterConsoleCommand calls

            // Remove RegisterListener calls for round end and player disconnect,
            // use attribute-based event handlers instead
        }

        private void LoadTranslations()
        {
            string filePath = Path.Combine(ModuleDirectory, "lang", "ru.json");

            if (File.Exists(filePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(filePath);
                    translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? new Dictionary<string, string>();
                    Console.WriteLine($"[ManiacMod] Loaded translations from: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ManiacMod] Error loading translations from {filePath}: {ex.Message}");
                    translations = new Dictionary<string, string>();
                }
            }
            else
            {
                Console.WriteLine($"[ManiacMod] Translation file not found: {filePath}");
                translations = new Dictionary<string, string>();
            }
        }

        private string GetTranslation(string key)
        {
            if (translations.TryGetValue(key, out string? translation))
            {
                translation = translation
                    .Replace("{Default}", $"{ChatColors.Default}")
                    .Replace("{Blue}", $"{ChatColors.Blue}")
                    .Replace("{Green}", $"{ChatColors.Green}");
                return translation;
            }
            Console.WriteLine($"[ManiacMod] Missing translation key: {key}");
            return key;
        }

        private PluginConfig EnsureConfigLoaded(string moduleName)
        {
            try
            {
                var loaded = ConfigManager.Load<PluginConfig>(moduleName);
                if (loaded == null)
                {
                    loaded = new PluginConfig();
                    try
                    {
                        var configManagerType = typeof(ConfigManager);
                        var saveMethod = configManagerType.GetMethod("Save", new[] { typeof(string), loaded.GetType() });
                        if (saveMethod != null)
                        {
                            saveMethod.Invoke(null, new object[] { moduleName, loaded });
                        }
                        else
                        {
                            var alternative = configManagerType.GetMethod("SaveConfig", new[] { typeof(string), loaded.GetType() })
                                              ?? configManagerType.GetMethod("SaveAsync", new[] { typeof(string), loaded.GetType() });
                            if (alternative != null)
                            {
                                alternative.Invoke(null, new object[] { moduleName, loaded });
                            }
                            else
                            {
                                Console.WriteLine("ConfigManager.Save not found — skipping config save (API mismatch).");
                            }
                        }
                    }
                    catch { }
                }
                return loaded;
            }
            catch { return new PluginConfig(); }
        }

        private int GetConfiguredHealth()
        {
            if (Config == null) return 777;
            if (string.IsNullOrWhiteSpace(Config.Maniacshp)) return 777;
            if (int.TryParse(Config.Maniacshp, out int val)) return val;
            return 777;
        }

        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            maniacReleased = false;
            roundStarted = true;

            if (isFirstRound)
            {
                mpFreezeTime = GetMpFreezetime();
                var roundTimeCvar = ConVar.Find("mp_roundtime");
                if (roundTimeCvar != null && float.TryParse(roundTimeCvar.StringValue, out float roundTime))
                {
                    mpRoundDuration = (int)roundTime * 60;
                }
                isFirstRound = false;
            }

            StartManiacTimer();

            return HookResult.Continue;
        }

        private int GetMpFreezetime()
        {
            var conVar = ConVar.Find("mp_freezetime");
            if (conVar != null && int.TryParse(conVar.StringValue, out int freezeTime))
            {
                return freezeTime;
            }
            return 5;
        }

        private void StartManiacTimer()
        {
            if (maniacTimerRunning) return;

            roundStartTime = DateTime.Now;
            maniacTimerRunning = true;
            maniacReleaseTime = mpFreezeTime + 60; // 60 seconds after freeze time mp_freezetime ends
            lastBroadcastSecond = -1;
        }

        private void OnTick()
        {
            ApplyImmortalityOnTick();
        }

        private void ApplyImmortalityOnTick()
        {
            if (!roundStarted)
            {
                ApplyImmortalityToPlayers();
                return;
            }

            if (!maniacTimerRunning)
            {
                ApplyImmortalityToPlayers();
                return;
            }

            int elapsedSeconds = (int)(DateTime.Now - roundStartTime).TotalSeconds;
            int countdownStartTime = maniacReleaseTime - countdownDurationSeconds;

            if (elapsedSeconds < countdownStartTime)
            {
                ApplyImmortalityToPlayers();
                return;
            }

            if (elapsedSeconds >= countdownStartTime && elapsedSeconds < maniacReleaseTime)
            {
                int secondsLeft = maniacReleaseTime - elapsedSeconds;
                if (lastBroadcastSecond != secondsLeft)
                {
                    string? msg = GetTranslation("maniac_exit_countdown");
                    if (!string.IsNullOrEmpty(msg))
                    {
                        msg = msg.Replace("{N}", secondsLeft.ToString());
                        BroadcastMessageToAll(msg);
                    }
                    lastBroadcastSecond = secondsLeft;
                }
                ApplyImmortalityToPlayers();
                return;
            }

            if (elapsedSeconds >= maniacReleaseTime && !maniacReleased)
            {
                maniacReleased = true;
                maniacTimerRunning = false;

                AddTimer(0.1f, () =>
                {
                    BroadcastMessageToAll(GetTranslation("maniac_exit_message") ?? "Маньяк вышел!");
                    ApplyImmortalityToPlayers(false);
                });
            }
        }

        private void ApplyImmortalityToPlayers(bool isImmortal = true)
        {
            if (!roundStarted)
            {
                return;
            }

            if (maniacReleased)
            {
                isImmortal = false;
            }

            int healthValue = GetConfiguredHealth();

            try
            {
                var players = XHelper.GetOnlinePlayers()
                    .Where(p => p.TeamNum == (int)CsTeam.Terrorist)
                    .ToList();

                foreach (var p in players)
                {
                    try
                    {
                        if (p.PawnIsAlive && p.PlayerPawn != null && p.PlayerPawn.Value != null)
                        {
                            if (isImmortal)
                            {
                                p.PlayerPawn.Value.Health = healthValue;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void BroadcastMessageToAll(string message)
        {
            try
            {
                var players = XHelper.GetOnlinePlayers();
                foreach (var player in players)
                {
                    try
                    {
                        player.PrintToChat($"{ChatColors.Default}{message}");
                    }
                    catch { }
                }
            }
            catch
            {
                Console.WriteLine($"[Broadcast] {message}");
            }
        }

        [GameEventHandler]
        public HookResult HandlePlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            try
            {
                var player = @event.Userid;
                if (player == null) return HookResult.Continue;

                if (player.TeamNum == (int)CsTeam.Terrorist)
                {
                    if (player.PawnIsAlive && player.PlayerPawn != null && player.PlayerPawn.Value != null)
                    {
                        try
                        {
                            if (!maniacReleased)
                            {
                                player.PlayerPawn.Value.Health += @event.DmgHealth;
                                int configured = GetConfiguredHealth();
                                if (player.PlayerPawn.Value.Health < configured)
                                {
                                    player.PlayerPawn.Value.Health = configured;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return HookResult.Continue;
        }

        [ConsoleCommand("css_row")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnRowCommand(CCSPlayerController controller, CommandInfo info)
        {
            if (controller == null) return;

            var target = playersInRow.FirstOrDefault(p => p == controller);

            if (target == null)
            {
                playersInRow.Add(controller);
                controller.PrintToChat(" " + GetTranslation("PluginTag") + " " + GetTranslation("RowEnter"));
                BroadcastMessageToAll($" {GetTranslation("Server.PlayerEnterRow").Replace("{name}", controller.PlayerName)}");
                return;
            }

            playersInRow.Remove(controller);
            controller.PrintToChat(" " + GetTranslation("PluginTag") + " " + GetTranslation("RowLeave"));
            BroadcastMessageToAll($" {GetTranslation("Server.PlayerLeaveRow").Replace("{name}", controller.PlayerName)}");
        }

        [ConsoleCommand("css_rowlist")]
        public void OnRowListCommand(CCSPlayerController controller, CommandInfo info)
        {
            info.ReplyToCommand(" " + GetTranslation("PluginTag") + " " + GetTranslation("RowList"));
            foreach (var p in playersInRow)
            {
                info.ReplyToCommand(" " + ChatColors.Green + p.PlayerName);
            }
        }

        [GameEventHandler]
        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null)
            {
                playersInRow.Remove(player);
                seekers.Remove(player);
            }
            return HookResult.Continue;
        }

        [GameEventHandler(HookMode.Pre)]
        public HookResult OnRoundEndHandler(EventRoundEnd @event, GameEventInfo info)
        {
            CancelManiacTimer();

            seekers.Clear();

            var ManiacsCfg = Config.Maniacs.OrderByDescending(x => x.ManiacCount);
            int needManiacs = 1;

            List<CCSPlayerController> activePlayers = XHelper.GetOnlinePlayers().Where(p => p.TeamNum > 1).ToList();

            foreach (var m in ManiacsCfg)
            {
                if (needManiacs != 1) continue;

                if (activePlayers.Count >= m.PlayersCount)
                {
                    needManiacs = m.ManiacCount;
                }
            }

            if (needManiacs > activePlayers.Count)
            {
                needManiacs = activePlayers.Count;
            }

            while (playersInRow.Count > 0 && seekers.Count < needManiacs)
            {
                int idx = rnd.Next(0, playersInRow.Count);
                var addPlayer = playersInRow[idx];
                seekers.Add(addPlayer);
                activePlayers.Remove(addPlayer);
                playersInRow.RemoveAt(idx);
            }

            while (activePlayers.Count > 0 && seekers.Count < needManiacs)
            {
                int idx = rnd.Next(0, activePlayers.Count);
                var addPlayer = activePlayers[idx];
                seekers.Add(addPlayer);
                activePlayers.RemoveAt(idx);
            }

            foreach (var p in activePlayers)
            {
                try { p.ChangeTeam(CsTeam.CounterTerrorist); } catch { }
            }
            foreach (var p in seekers)
            {
                try { p.ChangeTeam(CsTeam.Terrorist); } catch { }
            }

            playersInRow.Clear();

            return HookResult.Continue;
        }

        private void CancelManiacTimer()
        {
            maniacTimerRunning = false;
            maniacReleased = false;
            lastBroadcastSecond = -1;
        }
    }
}