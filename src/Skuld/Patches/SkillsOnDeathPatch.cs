using HarmonyLib;

namespace Skuld.Patches;

[HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
internal static class SkillsOnDeathPatch
{
    private static bool Prefix(Skills __instance)
    {
        Plugin.Log.LogInfo("Skuld patch entry: Skills.OnDeath prefix fired.");
        SkillDebtService.LogHardDeathDiagnostics(__instance);

        if (!ModConfig.IsModEnabled || !SkillDebtService.IsReady)
        {
            return true;
        }

        bool converted = SkillDebtService.TryConvertDeathToDebt(__instance, out string deathSummary);
        if (!converted)
        {
            Plugin.Log.LogWarning("Skuld could not convert death penalty to debt. Falling back to vanilla skill loss.");
            return true;
        }

        if (!string.IsNullOrEmpty(deathSummary))
        {
            Player player = AccessTools.Field(typeof(Skills), "m_player")?.GetValue(__instance) as Player;
            player?.Message(MessageHud.MessageType.TopLeft, deathSummary);
        }

        return false;
    }
}
