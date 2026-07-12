using System.Reflection;
using HarmonyLib;

namespace Skuld.Patches;

[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class PlayerSoftDeathClarityPatch
{
    private const string SoftDeathDebtMessage = "Soft death — no skill debt";

    private static readonly MethodInfo HardDeathMethod = AccessTools.Method(typeof(Player), "HardDeath");
    private static bool isHardDeath;

    private static void Prefix(Player __instance)
    {
        isHardDeath = HardDeathMethod != null
            && __instance != null
            && (bool)HardDeathMethod.Invoke(__instance, null);
    }

    private static void Postfix(Player __instance)
    {
        if (!ModConfig.IsModEnabled || isHardDeath || __instance != Player.m_localPlayer)
        {
            return;
        }

        __instance.Message(MessageHud.MessageType.TopLeft, SoftDeathDebtMessage);
    }
}
