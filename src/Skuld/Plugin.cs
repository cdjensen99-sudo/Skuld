using System.Reflection;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Skuld;

[BepInPlugin(ModConstants.ModGuid, ModConstants.ModName, ModConstants.ModVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    internal static Plugin Instance { get; private set; } = null!;
    private Harmony harmony;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        ModConfig.Bind(Config);
        SkillDebtService.Initialize();

        harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModConstants.ModGuid);
        LogPatchStatus();
        DevConsoleCommands.Register();
        PlayerChatCommands.Register();
        Log.LogInfo($"{ModConstants.ModName} {ModConstants.ModVersion} loaded.");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }

    private static void LogPatchStatus()
    {
        LogOriginalPatchStatus(typeof(Skills), nameof(Skills.OnDeath));
        LogOriginalPatchStatus(typeof(Player), nameof(Player.RaiseSkill));
        LogOriginalPatchStatus(typeof(Player), "OnDeath");
        LogOriginalPatchStatus(typeof(SkillsDialog), nameof(SkillsDialog.Setup));
    }

    private static void LogOriginalPatchStatus(System.Type type, string methodName)
    {
        MethodInfo original = AccessTools.Method(type, methodName);
        if (original == null)
        {
            Log.LogWarning($"Skuld patch diagnostic: could not resolve original {type.FullName}.{methodName}.");
            return;
        }

        HarmonyLib.Patches patchInfo = Harmony.GetPatchInfo(original);
        if (patchInfo == null)
        {
            Log.LogWarning($"Skuld patch diagnostic: no Harmony patch info for {type.FullName}.{methodName}.");
            return;
        }

        string owners = string.Join(", ", patchInfo.Owners.Distinct());
        Log.LogInfo($"Skuld patch diagnostic: {type.FullName}.{methodName} patched. Owners=[{owners}]");
    }
}
