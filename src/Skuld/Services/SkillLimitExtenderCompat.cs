using System;
using System.Reflection;

namespace Skuld;

/// <summary>
/// Optional soft dependency on SkillLimitExtender's per-skill UI denominator API.
/// </summary>
internal static class SkillLimitExtenderCompat
{
    private static bool lookupAttempted;
    private static MethodInfo getUiDenominatorMethod;

    internal static bool TryGetUiDenominator(Skills.SkillType skillType, out float denominator)
    {
        denominator = 0f;
        if (!TryResolveMethod())
        {
            return false;
        }

        try
        {
            object result = getUiDenominatorMethod.Invoke(null, new object[] { skillType });
            if (result is float value && value > 0f)
            {
                denominator = value;
                return true;
            }

            if (result is double doubleValue && doubleValue > 0d)
            {
                denominator = (float)doubleValue;
                return true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"SkillLimitExtender UI denominator lookup failed for {skillType}: {ex.Message}");
        }

        return false;
    }

    private static bool TryResolveMethod()
    {
        if (lookupAttempted)
        {
            return getUiDenominatorMethod != null;
        }

        lookupAttempted = true;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            if (assemblyName.IndexOf("SkillLimitExtender", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Type configManager = assembly.GetType("SkillLimitExtender.SkillConfigManager")
                ?? assembly.GetType("SkillConfigManager");
            if (configManager == null)
            {
                continue;
            }

            getUiDenominatorMethod = configManager.GetMethod(
                "GetUiDenominatorForSkillSafe",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(object) },
                null);
            if (getUiDenominatorMethod != null)
            {
                return true;
            }
        }

        return false;
    }
}
