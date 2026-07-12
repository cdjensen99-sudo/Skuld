using BepInEx.Configuration;
using UnityEngine;

namespace Skuld;

internal static class ModConfig
{
    internal static ConfigEntry<bool> EnableMod { get; private set; } = null!;
    internal static ConfigEntry<float> DebtPaydownShare { get; private set; } = null!;
    internal static ConfigEntry<int> MaxDebtPerSkill { get; private set; } = null!;
    internal static ConfigEntry<bool> DebugLogging { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableDebtClearedSound { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableDevCommands { get; private set; } = null!;
    internal static ConfigEntry<string> DebtBarColor { get; private set; } = null!;
    internal static ConfigEntry<string> DebtTextColor { get; private set; } = null!;
    internal static ConfigEntry<int> DebtTextSize { get; private set; } = null!;

    private static readonly Color DefaultDebtBarColor = new(0.55f, 0.12f, 0.22f, 1f);
    private static readonly Color DefaultDebtTextColor = new(0.85f, 0.18f, 0.18f, 1f);
    private const int DefaultDebtTextSize = 18;
    private const string ServerSyncedNote = " This setting is server synced.";
    private const string ClientOnlyNote = " Client-only; not server synced.";

    private static bool serverOverlayActive;
    private static bool serverEnableMod;
    private static float serverDebtPaydownShare;
    private static int serverMaxDebtPerSkill;

    private static bool UseServerOverlay =>
        serverOverlayActive && ZNet.instance != null && !ZNet.instance.IsServer();

    internal static bool IsModEnabled => UseServerOverlay ? serverEnableMod : EnableMod.Value;

    internal static void ApplyServerOverlay(bool enableMod, float paydownShare, int maxDebtPerSkill)
    {
        serverOverlayActive = true;
        serverEnableMod = enableMod;
        serverDebtPaydownShare = paydownShare;
        serverMaxDebtPerSkill = maxDebtPerSkill;
    }

    internal static void ClearServerOverlay()
    {
        serverOverlayActive = false;
    }

    internal static void Bind(ConfigFile config)
    {
        EnableMod = config.Bind(
            "General",
            "EnableMod",
            true,
            "Enable Skuld debt mechanics." + ServerSyncedNote);
        DebtPaydownShare = config.Bind(
            "General",
            "DebtPaydownShare",
            0.5f,
            new ConfigDescription(
                "Fraction of earned XP redirected to debt paydown while debt remains." + ServerSyncedNote,
                new AcceptableValueRange<float>(0.5f, 1f)));
        MaxDebtPerSkill = config.Bind(
            "General",
            "MaxDebtPerSkill",
            3,
            new ConfigDescription(
                "Maximum debt per skill, measured in deaths-worth of that skill's current level. 0 = uncapped." + ServerSyncedNote,
                new AcceptableValueRange<int>(0, 5)));
        DebugLogging = config.Bind(
            "General",
            "DebugLogging",
            false,
            "Enable verbose debt conversion/paydown logs." + ClientOnlyNote);
        EnableDebtClearedSound = config.Bind(
            "General",
            "EnableDebtClearedSound",
            false,
            "Client-side halo and sound when a skill's debt is fully repaid." + ClientOnlyNote);
        EnableDevCommands = config.Bind(
            "Dev",
            "EnableDevCommands",
            false,
            "Server/host only: enables skuld_clearcooldown and skuld_cleardebt on the world host. "
            + "Not server synced; ignored on multiplayer clients. Still requires Valheim devcommands and admin rights on dedicated servers. "
            + "Must remain false for Thunderstore releases.");

        DebtBarColor = config.Bind(
            "Visual",
            "DebtBarColor",
            "140,31,56",
            "Client-side color of the debt segment on the gold skill level bar. Use R,G,B (0-255) or #RRGGBB." + ClientOnlyNote);
        DebtTextColor = config.Bind(
            "Visual",
            "DebtTextColor",
            "217,46,46",
            "Client-side color of the -N debt label on skill rows. Use R,G,B (0-255) or #RRGGBB." + ClientOnlyNote);
        DebtTextSize = config.Bind(
            "Visual",
            "DebtTextSize",
            DefaultDebtTextSize,
            new ConfigDescription(
                "Client-side pixel font size for the -N debt label on all skill rows." + ClientOnlyNote,
                new AcceptableValueRange<int>(8, 36)));
    }

    internal static bool IsDevCommandsEnabled()
    {
        if (!EnableDevCommands.Value)
        {
            return false;
        }

        if (ZNet.instance == null)
        {
            return true;
        }

        return ZNet.instance.IsServer();
    }

    internal static float GetPaydownShare()
    {
        float raw = UseServerOverlay ? serverDebtPaydownShare : DebtPaydownShare.Value;
        return Mathf.Clamp(raw, 0.5f, 1f);
    }

    internal static int GetMaxDebtPerSkill()
    {
        int raw = UseServerOverlay ? serverMaxDebtPerSkill : MaxDebtPerSkill.Value;
        return Mathf.Clamp(raw, 0, 5);
    }

    internal static Color GetDebtBarColor()
    {
        return ModColorUtil.Parse(DebtBarColor.Value, DefaultDebtBarColor);
    }

    internal static Color GetDebtTextColor()
    {
        return ModColorUtil.Parse(DebtTextColor.Value, DefaultDebtTextColor);
    }

    internal static float GetDebtTextSize()
    {
        return Mathf.Clamp(DebtTextSize.Value, 8, 36);
    }
}
