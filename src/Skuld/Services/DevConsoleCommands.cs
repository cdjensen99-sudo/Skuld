using System.Collections.Generic;

namespace Skuld;

internal static class DevConsoleCommands
{
    private static bool registered;

    internal static void Register()
    {
        if (registered)
        {
            Plugin.Log.LogWarning("Skuld dev console commands already registered; skipping duplicate registration.");
            return;
        }

        new Terminal.ConsoleCommand(
            "skuld_clearcooldown",
            "Skuld dev: force the next death to count as a hard death.",
            ClearCooldown,
            isCheat: true,
            isNetwork: false,
            onlyServer: true,
            isSecret: true);

        new Terminal.ConsoleCommand(
            "skuld_cleardebt",
            "Skuld dev: clear all outstanding skill debt for the local player.",
            ClearDebt,
            isCheat: true,
            isNetwork: false,
            onlyServer: true,
            isSecret: true);

        registered = true;
        string state = ModConfig.IsDevCommandsEnabled()
            ? "enabled on server/host"
            : "disabled (set EnableDevCommands=true on the server/host cfg)";
        Plugin.Log.LogInfo($"Skuld dev console commands registered ({state}): skuld_clearcooldown, skuld_cleardebt.");
    }

    private static void ClearCooldown(Terminal.ConsoleEventArgs args)
    {
        if (!ModConfig.IsDevCommandsEnabled())
        {
            args.Context.AddString(
                "Skuld dev commands are disabled. Set EnableDevCommands=true in the server/host BepInEx config.");
            return;
        }

        Player player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context.AddString("No local player.");
            return;
        }

        player.ClearHardDeath();
        args.Context.AddString("Skuld: hard-death cooldown cleared. Next death will generate debt.");
    }

    private static void ClearDebt(Terminal.ConsoleEventArgs args)
    {
        if (!ModConfig.IsDevCommandsEnabled())
        {
            args.Context.AddString(
                "Skuld dev commands are disabled. Set EnableDevCommands=true in the server/host BepInEx config.");
            return;
        }

        Player player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context.AddString("No local player.");
            return;
        }

        List<string> cleared = SkillDebtService.ClearAllDebt(player);
        if (cleared.Count == 0)
        {
            args.Context.AddString("Skuld: no debt to clear.");
            return;
        }

        args.Context.AddString("Skuld: cleared debt for " + string.Join(", ", cleared) + ".");
    }
}
