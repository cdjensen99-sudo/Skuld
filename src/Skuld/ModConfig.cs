using BepInEx.Configuration;
using UnityEngine;

namespace Skuld;

internal static class ModConfig
{
    internal static ConfigEntry<bool> EnableMod { get; private set; } = null!;
    internal static ConfigEntry<float> DebtPaydownShare { get; private set; } = null!;
    internal static ConfigEntry<int> MaxDebtPerSkill { get; private set; } = null!;
    internal static ConfigEntry<bool> DebugLogging { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableDevCommands { get; private set; } = null!;

    internal static void Bind(ConfigFile config)
    {
        EnableMod = config.Bind("General", "EnableMod", true, "Enable Skuld debt mechanics.");
        DebtPaydownShare = config.Bind(
            "General",
            "DebtPaydownShare",
            0.5f,
            new ConfigDescription(
                "Fraction of earned XP redirected to debt paydown while debt remains.",
                new AcceptableValueRange<float>(0.5f, 1f)));
        MaxDebtPerSkill = config.Bind(
            "General",
            "MaxDebtPerSkill",
            3,
            new ConfigDescription(
                "Maximum debt per skill, measured in deaths-worth of that skill's current level. 0 = uncapped.",
                new AcceptableValueRange<int>(0, 5)));
        DebugLogging = config.Bind("General", "DebugLogging", false, "Enable verbose debt conversion/paydown logs.");
        EnableDevCommands = config.Bind("Dev", "EnableDevCommands", false, "Enable Skuld test console commands. Must remain false for Thunderstore releases.");
    }

    internal static float GetPaydownShare()
    {
        return Mathf.Clamp(DebtPaydownShare.Value, 0.5f, 1f);
    }

    internal static int GetMaxDebtPerSkill()
    {
        return Mathf.Clamp(MaxDebtPerSkill.Value, 0, 5);
    }
}
