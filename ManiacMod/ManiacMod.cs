using System;
using System.Linq;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration; // <-- для [GameEventHandler]

namespace ManiacMod;
public class ManiacMod : BasePlugin
{
    public override string ModuleName => "ManiacMod";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "Maslenka";

    // Config holder (optional — loaded via EnsureConfigLoaded)
    public PluginConfig Config { get; private set; } = null!;

    public override void Load(bool hotReload)
    {
        Console.WriteLine("ManiacMod Load");

        // Ensure config is loaded when plugin loads
        Config = EnsureConfigLoaded(ModuleName);

        // Example: register a per-tick handler to apply immortality (depends on host API support)
        RegisterListener<Listeners.OnTick>(() =>
        {
            // Apply immortality each tick (main-thread)
            ApplyImmortalityOnTick();
        });
    }

    public void OnConfigParsed(PluginConfig config)
    {
        Config = ConfigManager.Load<PluginConfig>(ModuleName);
    }

    /// <summary>
    /// Попытаться загрузить конфигурацию плагина; при отсутствии — создать и сохранить дефолт.
    /// Возвращает загруженный (или созданный) PluginConfig.
    /// </summary>
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
                    // Try to call ConfigManager.Save(moduleName, loaded) if the method exists in the current API.
                    var configManagerType = typeof(ConfigManager);
                    var saveMethod = configManagerType.GetMethod("Save", new[] { typeof(string), loaded.GetType() });
                    if (saveMethod != null)
                    {
                        saveMethod.Invoke(null, new object[] { moduleName, loaded });
                    }
                    else
                    {
                        // If there's no Save method, try common alternatives by name (optional)
                        var alternative = configManagerType.GetMethod("SaveConfig", new[] { typeof(string), loaded.GetType() })
                                          ?? configManagerType.GetMethod("SaveAsync", new[] { typeof(string), loaded.GetType() });
                        if (alternative != null)
                        {
                            alternative.Invoke(null, new object[] { moduleName, loaded });
                        }
                        else
                        {
                            // No save available in this API version — skip saving (config will still be used in-memory)
                            Console.WriteLine("ConfigManager.Save not found — skipping config save (API mismatch).");
                        }
                    }
                }
                catch
                {
                    // Игнорируем ошибки при сохранении конфигурации; вернём объект в памяти
                }
            }
            return loaded;
        }
        catch
        {
            return new PluginConfig();
        }
    }

    /// <summary>
    /// Получить число здоровья из конфига. Если невалидно — вернуть дефолт 777.
    /// </summary>
    private int GetConfiguredHealth()
    {
        if (Config == null) return 777;
        if (string.IsNullOrWhiteSpace(Config.Maniacshp)) return 777;
        if (int.TryParse(Config.Maniacshp, out int val))
        {
            return val;
        }
        return 777;
    }

    /// <summary>
    /// Устанавливает всем живым игрокам команды Terrorist здоровье в указанное значение из конфига.
    /// Вызывается при каждом тике (пример использования OnTick).
    /// </summary>
    private void ApplyImmortalityOnTick()
    {
        int healthValue = GetConfiguredHealth();

        try
        {
            // Применяется к Terrorist
            var players = XHelper.GetOnlinePlayers()
                .Where(p => p.TeamNum == (int)CsTeam.Terrorist)
                .ToList();

            foreach (var p in players)
            {
                try
                {
                    if (p.PawnIsAlive && p.PlayerPawn != null && p.PlayerPawn.Value != null)
                    {
                        p.PlayerPawn.Value.Health = healthValue;
                    }
                }
                catch
                {
                    // игнорировать исключения при манипуляции pawn
                }
            }
        }
        catch
        {
            // общий игнор для защиты от падения плагина
        }
    }

    /// <summary>
    /// Обработчик получения урона: восстанавливает урон игрокам Terrorist согласно значению из конфига.
    /// Подключите как игровой обработчик OnPlayerHurt.
    /// </summary>
    [GameEventHandler]
    public HookResult HandlePlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        try
        {
            var player = @event.Userid;
            if (player == null) return HookResult.Continue;

            // Теперь восстанавливаем урон террористам
            if (player.TeamNum == (int)CsTeam.Terrorist)
            {
                if (player.PawnIsAlive && player.PlayerPawn != null && player.PlayerPawn.Value != null)
                {
                    try
                    {
                        // При получении урона увеличиваем здоровье на величину урона,
                        // чтобы нейтрализовать эффект урона (как в исходной реализации).
                        player.PlayerPawn.Value.Health += @event.DmgHealth;

                        // Также гарантированно выставляем минимум конфигное значение, на случай, если нужно зафиксировать HP.
                        int configured = GetConfiguredHealth();
                        if (player.PlayerPawn.Value.Health < configured)
                        {
                            player.PlayerPawn.Value.Health = configured;
                        }
                    }
                    catch
                    {
                        // игнорируем ошибки при установке здоровья
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return HookResult.Continue;
    }
}