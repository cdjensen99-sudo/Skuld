using HarmonyLib;

namespace Skuld.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
internal static class PlayerRaiseSkillPatch
{
    private static bool hasLoggedEntry;

    private static void Prefix(Player __instance, Skills.SkillType skill, ref float value)
    {
        if (!hasLoggedEntry)
        {
            Plugin.Log.LogInfo("Skuld patch entry: Player.RaiseSkill prefix fired.");
            hasLoggedEntry = true;
        }

        if (!ModConfig.EnableMod.Value || !SkillDebtService.IsReady)
        {
            return;
        }

        SkillDebtService.TryApplyRaiseSkillDebt(__instance, skill, ref value);
    }
}
