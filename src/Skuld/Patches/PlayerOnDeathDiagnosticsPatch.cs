using HarmonyLib;

namespace Skuld.Patches;

[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class PlayerOnDeathDiagnosticsPatch
{
    private static readonly System.Reflection.FieldInfo TimeSinceDeathField = AccessTools.Field(typeof(Player), "m_timeSinceDeath");
    private static readonly System.Reflection.MethodInfo HardDeathMethod = AccessTools.Method(typeof(Player), "HardDeath");

    private static void Prefix(Player __instance)
    {
        float timeSinceDeath = TimeSinceDeathField != null ? (float)TimeSinceDeathField.GetValue(__instance) : -1f;
        float cooldown = __instance != null ? __instance.m_hardDeathCooldown : -1f;
        bool hardDeath = HardDeathMethod != null && __instance != null && (bool)HardDeathMethod.Invoke(__instance, null);

        Plugin.Log.LogInfo($"Skuld diagnostic: Player.OnDeath entered. hardDeath={hardDeath}, m_timeSinceDeath={timeSinceDeath:F3}, m_hardDeathCooldown={cooldown:F3}");
    }
}
