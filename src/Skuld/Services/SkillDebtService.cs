using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Skuld;

internal static class SkillDebtService
{
    private static readonly FieldInfo SkillsDataField = AccessTools.Field(typeof(Skills), "m_skillData");
    private static readonly FieldInfo DeathLowerFactorField = AccessTools.Field(typeof(Skills), "m_DeathLowerFactor");
    private static readonly FieldInfo SkillsPlayerField = AccessTools.Field(typeof(Skills), "m_player");
    private static readonly Type SkillType = AccessTools.Inner(typeof(Skills), "Skill");
    private static readonly FieldInfo SkillLevelField = SkillType != null ? AccessTools.Field(SkillType, "m_level") : null;
    private static readonly FieldInfo SkillInfoField = SkillType != null ? AccessTools.Field(SkillType, "m_info") : null;
    private static readonly Type SkillDefType = AccessTools.Inner(typeof(Skills), "SkillDef");
    private static readonly FieldInfo SkillIncreaseStepField = SkillDefType != null ? AccessTools.Field(SkillDefType, "m_increseStep") : null;

    internal static void Initialize()
    {
        if (SkillsDataField == null || DeathLowerFactorField == null || SkillsPlayerField == null || SkillLevelField == null || SkillInfoField == null || SkillIncreaseStepField == null)
        {
            Plugin.Log.LogError("Skuld failed to bind reflection fields; debt features may not work.");
        }

        Plugin.Log.LogInfo(
            "Skuld debt storage: Player.m_customData (character save). Not written to player ZDO — other players cannot read your debt keys.");
    }

    internal static bool IsReady => SkillsDataField != null && DeathLowerFactorField != null && SkillsPlayerField != null && SkillLevelField != null && SkillInfoField != null && SkillIncreaseStepField != null;

    internal static void LogHardDeathDiagnostics(Skills skills)
    {
        if (!IsReady)
        {
            Plugin.Log.LogInfo("Skuld hard-death diagnostic: reflection bindings not ready.");
            return;
        }

        float deathLowerFactor = (float)DeathLowerFactorField.GetValue(skills);
        float skillReductionRate = Game.m_skillReductionRate;
        float factor = deathLowerFactor * skillReductionRate;
        Plugin.Log.LogInfo(
            $"Skuld hard-death diagnostic: m_DeathLowerFactor={deathLowerFactor:F6}, Game.m_skillReductionRate={skillReductionRate:F6}, factor={factor:F6}");

        IDictionary map = SkillsDataField.GetValue(skills) as IDictionary;
        if (map == null)
        {
            Plugin.Log.LogInfo("Skuld hard-death diagnostic: m_skillData is null.");
            return;
        }

        foreach (DictionaryEntry entry in map)
        {
            try
            {
                Skills.SkillType skillType = (Skills.SkillType)entry.Key;
                if (entry.Value == null)
                {
                    continue;
                }

                float level = GetSkillLevel(entry.Value);
                float lossAmount = level * factor;
                Plugin.Log.LogInfo($"Skuld hard-death diagnostic: skill={skillType}, level={level:F3}, lossAmount={lossAmount:F6}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Skuld hard-death diagnostic failed for skill entry {entry.Key}: {ex}");
            }
        }
    }

    internal static bool TryConvertDeathToDebt(Skills skills, out string deathSummary)
    {
        deathSummary = string.Empty;
        Player player = SkillsPlayerField?.GetValue(skills) as Player;
        if (player == null)
        {
            Plugin.Log.LogWarning("Skuld death conversion: Skills.m_player was null.");
            return false;
        }

        float factor = GetDeathFactor(skills);
        if (factor <= 0f)
        {
            return true;
        }

        IDictionary map = SkillsDataField?.GetValue(skills) as IDictionary;
        if (map == null)
        {
            return false;
        }

        int maxDebtPerSkill = ModConfig.GetMaxDebtPerSkill();
        List<string> debtEntries = new();
        int debtedCount = 0;
        Plugin.Log.LogInfo(
            $"Skuld death conversion: starting per-skill customData writes. player={DescribePlayer(player)}, MaxDebtPerSkill={maxDebtPerSkill}");

        foreach (DictionaryEntry entry in map)
        {
            try
            {
                Skills.SkillType skillType = (Skills.SkillType)entry.Key;
                if (entry.Value == null)
                {
                    continue;
                }

                float level = GetSkillLevel(entry.Value);
                if (level <= 0f)
                {
                    continue;
                }

                float rawDebtThisDeath = level * factor;
                if (rawDebtThisDeath <= 0f)
                {
                    continue;
                }

                EnsureProgressConsistency(player, skillType);
                float currentDebt = GetDebt(player, skillType) + rawDebtThisDeath;
                float lifetime = GetIncurred(player, skillType) + rawDebtThisDeath;

                if (maxDebtPerSkill > 0)
                {
                    float capInLevels = maxDebtPerSkill * rawDebtThisDeath;
                    currentDebt = Mathf.Min(currentDebt, capInLevels);
                }

                SetDebt(player, skillType, currentDebt);
                SetIncurred(player, skillType, lifetime);
                SetBaseline(player, skillType, currentDebt);

                float readback = GetDebt(player, skillType);
                debtedCount++;
                debtEntries.Add($"{FormatSkillName(skillType)} +{rawDebtThisDeath:0.0}");
                Plugin.Log.LogInfo(
                    $"Skuld death conversion: skill={skillType}, raw={rawDebtThisDeath:F4}, current={currentDebt:F4}, baseline={currentDebt:F4}, lifetime={lifetime:F4}, customDataReadback={readback:F4}, key={GetDebtKey(skillType)}");
                LogDebug($"Death debt added: {skillType} raw +{rawDebtThisDeath:F3} (current {currentDebt:F3}, lifetime {lifetime:F3})");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Skuld death conversion failed for skill entry {entry.Key}: {ex}");
            }
        }

        Plugin.Log.LogInfo($"Skuld death conversion: complete, {debtedCount} skills debted.");

        if (debtEntries.Count > 0)
        {
            deathSummary = BuildDeathSummary(debtEntries);
        }

        return true;
    }

    internal static bool TryApplyRaiseSkillDebt(Player player, Skills.SkillType skillType, ref float value)
    {
        if (value <= 0f || skillType == Skills.SkillType.None || player == null)
        {
            return true;
        }

        EnsureProgressConsistency(player, skillType);
        float debt = GetDebt(player, skillType);
        if (debt <= 0f)
        {
            return true;
        }

        Skills skills = player.GetSkills();
        Skills.Skill skill = skills != null ? GetSkillFromMap(skills, skillType) : null;
        if (skill == null)
        {
            return true;
        }

        float increaseStep = GetIncreaseStep(skill);
        if (increaseStep <= 0f)
        {
            return true;
        }

        float incomingValue = value;
        float multiplier = 1f;
        player.GetSEMan().ModifyRaiseSkill(skillType, ref multiplier);
        float effectiveValue = incomingValue * multiplier;

        float accumulatorUnits = increaseStep * effectiveValue * Game.m_skillGainRate;
        float paydownShare = GetPaydownShare(player, skillType);
        float levelingShare = 1f - paydownShare;

        float nextLevelRequirement = GetNextLevelRequirement(skill.m_level);
        float intendedDebtUnits = accumulatorUnits * paydownShare;
        float intendedLevelUnits = accumulatorUnits * levelingShare;

        float debtPaidLevels = nextLevelRequirement > 0f ? intendedDebtUnits / nextLevelRequirement : 0f;
        float actualDebtPaidLevels = Mathf.Min(debt, debtPaidLevels);
        float unusedDebtUnits = intendedDebtUnits - (actualDebtPaidLevels * nextLevelRequirement);
        float actualLevelUnits = intendedLevelUnits + unusedDebtUnits;

        float denominator = increaseStep * multiplier * Game.m_skillGainRate;
        value = denominator > 0f ? actualLevelUnits / denominator : 0f;

        float newDebt = Mathf.Max(0f, debt - actualDebtPaidLevels);
        SetDebt(player, skillType, newDebt);

        LogDebug(
            $"Debt paydown: {skillType} paid {actualDebtPaidLevels:F4} (intended {debtPaidLevels:F4}), debt {debt:F4}->{newDebt:F4}, " +
            $"rerouted {unusedDebtUnits:F4} units to leveling, forwarded pre-multiplier value {value:F4}");

        if (debt > 0f && newDebt <= 0f)
        {
            ClearFocusPaydownForSkill(player, skillType);
            DebtClearedFeedback.TryPlay(player);
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, $"{skillType} debt repaid");
        }

        return true;
    }

    internal static float GetDeathFactor(Skills skills)
    {
        float deathLowerFactor = DeathLowerFactorField != null ? (float)DeathLowerFactorField.GetValue(skills) : 0f;
        return deathLowerFactor * Game.m_skillReductionRate;
    }

    private static float GetSkillLevel(object skillObj)
    {
        return SkillLevelField != null ? (float)SkillLevelField.GetValue(skillObj) : 0f;
    }

    private static float GetIncreaseStep(Skills.Skill skill)
    {
        object skillInfo = SkillInfoField?.GetValue(skill);
        return skillInfo != null && SkillIncreaseStepField != null ? (float)SkillIncreaseStepField.GetValue(skillInfo) : 0f;
    }

    private static Skills.Skill GetSkillFromMap(Skills skills, Skills.SkillType skillType)
    {
        IDictionary map = SkillsDataField?.GetValue(skills) as IDictionary;
        return map != null && map.Contains(skillType) ? map[skillType] as Skills.Skill : null;
    }

    private static string GetDebtKey(Skills.SkillType skillType)
    {
        return ModConstants.DebtKeyPrefix + skillType;
    }

    private static string GetBaselineKey(Skills.SkillType skillType)
    {
        return ModConstants.DebtBaselineKeyPrefix + skillType;
    }

    private static string GetIncurredKey(Skills.SkillType skillType)
    {
        return ModConstants.DebtIncurredKeyPrefix + skillType;
    }

    internal static float GetDebt(Player player, Skills.SkillType skillType)
    {
        return GetCustomFloat(player, GetDebtKey(skillType));
    }

    internal static float GetBaseline(Player player, Skills.SkillType skillType)
    {
        return GetCustomFloat(player, GetBaselineKey(skillType));
    }

    internal static float GetIncurred(Player player, Skills.SkillType skillType)
    {
        return GetCustomFloat(player, GetIncurredKey(skillType));
    }

    internal static float GetPaidOff(Player player, Skills.SkillType skillType)
    {
        EnsureProgressConsistency(player, skillType);
        return Mathf.Max(0f, GetBaseline(player, skillType) - GetDebt(player, skillType));
    }

    internal static string FormatProgressDetail(Player player, Skills.SkillType skillType)
    {
        EnsureProgressConsistency(player, skillType);
        float remaining = GetDebt(player, skillType);
        float paidOff = GetPaidOff(player, skillType);
        string focusSuffix = IsFocusPaydownActive(player, skillType) ? "  [focused 100%]" : string.Empty;
        return $"-{remaining:0.0000}  ({paidOff:0.0000} paid off){focusSuffix}";
    }

    internal static float GetPaydownShare(Player player, Skills.SkillType skillType)
    {
        if (IsFocusPaydownActive(player, skillType))
        {
            return ModConstants.FocusPaydownShare;
        }

        return ModConfig.GetPaydownShare();
    }

    internal static bool IsFocusAll(Player player)
    {
        return GetCustomFlag(player, ModConstants.FocusPaydownAllKey);
    }

    internal static bool IsFocusPaydownActive(Player player, Skills.SkillType skillType)
    {
        if (player == null || skillType == Skills.SkillType.None)
        {
            return false;
        }

        if (IsFocusAll(player))
        {
            return GetDebt(player, skillType) > 0f;
        }

        return GetCustomFlag(player, GetFocusPaydownKey(skillType));
    }

    internal static bool TryEnableFocusAll(Player player, out string message)
    {
        message = string.Empty;
        if (player?.m_customData == null)
        {
            return false;
        }

        ClearPerSkillFocusFlags(player);
        SetCustomFlag(player, ModConstants.FocusPaydownAllKey, true);
        message = "Focus all enabled — 100% paydown on skills with debt until cleared.";
        return true;
    }

    internal static bool TryEnableFocusSkill(Player player, Skills.SkillType skillType, out string message)
    {
        message = string.Empty;
        if (player?.m_customData == null || skillType == Skills.SkillType.None)
        {
            return false;
        }

        SetCustomFlag(player, ModConstants.FocusPaydownAllKey, false);
        SetCustomFlag(player, GetFocusPaydownKey(skillType), true);
        message = $"{FormatSkillName(skillType)} focus enabled — 100% paydown until that debt is cleared.";
        return true;
    }

    internal static void ClearAllFocusPaydown(Player player)
    {
        if (player?.m_customData == null)
        {
            return;
        }

        SetCustomFlag(player, ModConstants.FocusPaydownAllKey, false);
        ClearPerSkillFocusFlags(player);
    }

    internal static void ShowFocusOverview(Player player, Terminal context)
    {
        if (player == null)
        {
            context?.AddString("No local player.");
            return;
        }

        PruneInactiveFocusFlags(player);

        if (IsFocusAll(player))
        {
            List<string> indebted = GetIndebtedSkillNames(player);
            context.AddString("Focus all: active — 100% paydown on skills with debt.");
            if (indebted.Count > 0)
            {
                context.AddString("  Indebted: " + string.Join(", ", indebted));
            }
            else
            {
                context.AddString("  No outstanding debt right now.");
            }

            return;
        }

        List<string> focusedSkills = GetFocusedSkillNames(player);
        if (focusedSkills.Count == 0)
        {
            context.AddString("No focused paydown skills. Use /focus <skill|all> or /focus off.");
            return;
        }

        context.AddString($"Focused paydown ({focusedSkills.Count}) — 100% until cleared:");
        foreach (string entry in focusedSkills)
        {
            context.AddString("  " + entry);
        }
    }

    private static void ClearFocusPaydownForSkill(Player player, Skills.SkillType skillType)
    {
        SetCustomFlag(player, GetFocusPaydownKey(skillType), false);
        if (IsFocusAll(player) && !HasAnyDebt(player))
        {
            SetCustomFlag(player, ModConstants.FocusPaydownAllKey, false);
        }
    }

    private static string GetFocusPaydownKey(Skills.SkillType skillType)
    {
        return ModConstants.FocusPaydownKeyPrefix + skillType;
    }

    private static void ClearPerSkillFocusFlags(Player player)
    {
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            SetCustomFlag(player, GetFocusPaydownKey(skillType), false);
        }
    }

    private static void PruneInactiveFocusFlags(Player player)
    {
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            if (GetCustomFlag(player, GetFocusPaydownKey(skillType)) && GetDebt(player, skillType) <= 0f)
            {
                SetCustomFlag(player, GetFocusPaydownKey(skillType), false);
            }
        }

        if (IsFocusAll(player) && !HasAnyDebt(player))
        {
            SetCustomFlag(player, ModConstants.FocusPaydownAllKey, false);
        }
    }

    private static bool HasAnyDebt(Player player)
    {
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            if (GetDebt(player, skillType) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetIndebtedSkillNames(Player player)
    {
        List<string> names = new();
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            if (GetDebt(player, skillType) > 0f)
            {
                names.Add(FormatSkillName(skillType));
            }
        }

        return names;
    }

    private static List<string> GetFocusedSkillNames(Player player)
    {
        List<string> names = new();
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            if (!GetCustomFlag(player, GetFocusPaydownKey(skillType)))
            {
                continue;
            }

            string suffix = GetDebt(player, skillType) > 0f ? string.Empty : " (no debt)";
            names.Add(FormatSkillName(skillType) + suffix);
        }

        return names;
    }

    private static bool GetCustomFlag(Player player, string key)
    {
        if (player?.m_customData == null)
        {
            return false;
        }

        return player.m_customData.TryGetValue(key, out string raw)
            && string.Equals(raw, "1", StringComparison.Ordinal);
    }

    private static void SetCustomFlag(Player player, string key, bool value)
    {
        if (player?.m_customData == null)
        {
            return;
        }

        if (value)
        {
            player.m_customData[key] = "1";
            return;
        }

        player.m_customData.Remove(key);
    }

    internal static float GetLifetimeRepaid(Player player, Skills.SkillType skillType)
    {
        return Mathf.Max(0f, GetIncurred(player, skillType) - GetDebt(player, skillType));
    }

    internal static string FormatLifetimeDetail(Player player, Skills.SkillType skillType)
    {
        float incurred = GetIncurred(player, skillType);
        float repaid = GetLifetimeRepaid(player, skillType);
        return $"lifetime debt {incurred:0.0000}, lifetime repaid {repaid:0.0000}";
    }

    private static void EnsureProgressConsistency(Player player, Skills.SkillType skillType)
    {
        float remaining = GetDebt(player, skillType);
        float incurred = GetIncurred(player, skillType);
        float baseline = GetBaseline(player, skillType);

        if (remaining > 0f && incurred < remaining)
        {
            SetIncurred(player, skillType, remaining);
            incurred = remaining;
        }

        if (remaining > 0f && baseline <= 0f)
        {
            // Migrate pre-0.2.0 saves: baseline at last death equaled incurred before any paydown.
            SetBaseline(player, skillType, Mathf.Max(remaining, incurred));
        }
    }

    private static float GetCustomFloat(Player player, string key)
    {
        if (player?.m_customData == null)
        {
            return 0f;
        }

        if (!player.m_customData.TryGetValue(key, out string raw) || string.IsNullOrEmpty(raw))
        {
            return 0f;
        }

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? Mathf.Max(0f, value)
            : 0f;
    }

    private static void SetDebt(Player player, Skills.SkillType skillType, float value)
    {
        SetCustomFloat(player, GetDebtKey(skillType), value);
    }

    private static void SetBaseline(Player player, Skills.SkillType skillType, float value)
    {
        SetCustomFloat(player, GetBaselineKey(skillType), value);
    }

    private static void SetIncurred(Player player, Skills.SkillType skillType, float value)
    {
        SetCustomFloat(player, GetIncurredKey(skillType), value);
    }

    private static void SetCustomFloat(Player player, string key, float value)
    {
        if (player?.m_customData == null)
        {
            return;
        }

        float clamped = Mathf.Max(0f, value);
        if (clamped <= 0f)
        {
            player.m_customData.Remove(key);
            return;
        }

        player.m_customData[key] = clamped.ToString("R", CultureInfo.InvariantCulture);
    }

    internal static List<string> ClearAllDebt(Player player)
    {
        List<string> cleared = new();
        if (player?.m_customData == null)
        {
            return cleared;
        }

        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            float debt = GetDebt(player, skillType);
            float incurred = GetIncurred(player, skillType);
            float baseline = GetBaseline(player, skillType);
            if (debt <= 0f && incurred <= 0f && baseline <= 0f)
            {
                continue;
            }

            SetDebt(player, skillType, 0f);
            SetBaseline(player, skillType, 0f);
            SetIncurred(player, skillType, 0f);
            SetCustomFlag(player, GetFocusPaydownKey(skillType), false);
            if (debt > 0f)
            {
                cleared.Add($"{FormatSkillName(skillType)} {debt:0.0}");
            }
        }

        SetCustomFlag(player, ModConstants.FocusPaydownAllKey, false);
        return cleared;
    }

    internal static void ShowAllDebtOverview(Player player, Terminal context)
    {
        if (player == null)
        {
            context?.AddString("No local player.");
            return;
        }

        List<string> debtEntries = new();
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            EnsureProgressConsistency(player, skillType);
            float debt = GetDebt(player, skillType);
            if (debt <= 0f)
            {
                continue;
            }

            debtEntries.Add($"{FormatSkillName(skillType)} -{Mathf.RoundToInt(debt)}");
        }

        if (debtEntries.Count == 0)
        {
            context?.AddString("Skuld: no outstanding skill debt.");
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, "No outstanding skill debt.");
            return;
        }

        context?.AddString($"Skuld debt ({debtEntries.Count}):");
        foreach (string entry in debtEntries)
        {
            context?.AddString("  " + entry);
        }

        context?.AddString("Exact progress: /show debt <skill>");
        MessageHud.instance?.ShowMessage(
            MessageHud.MessageType.TopLeft,
            $"Debt on {debtEntries.Count} skills — list printed to chat.");
    }

    internal static void ShowAllLifetimeOverview(Player player, Terminal context)
    {
        if (player == null)
        {
            context?.AddString("No local player.");
            return;
        }

        List<string> lifetimeEntries = new();
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType == Skills.SkillType.None)
            {
                continue;
            }

            float lifetime = GetIncurred(player, skillType);
            if (lifetime <= 0f)
            {
                continue;
            }

            lifetimeEntries.Add($"{FormatSkillName(skillType)} {lifetime:0.0000}");
        }

        if (lifetimeEntries.Count == 0)
        {
            context?.AddString("Skuld: no lifetime debt recorded.");
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, "No lifetime debt recorded.");
            return;
        }

        context?.AddString($"Skuld lifetime debt ({lifetimeEntries.Count}):");
        foreach (string entry in lifetimeEntries)
        {
            context?.AddString("  " + entry);
        }

        MessageHud.instance?.ShowMessage(
            MessageHud.MessageType.TopLeft,
            $"Lifetime debt on {lifetimeEntries.Count} skills — list printed to chat.");
    }

    internal static bool TryResolveSkillType(string rawName, out Skills.SkillType skillType)
    {
        skillType = Skills.SkillType.None;
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return false;
        }

        string normalized = rawName.Replace(" ", string.Empty).Replace("_", string.Empty);
        foreach (Skills.SkillType candidate in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (candidate == Skills.SkillType.None)
            {
                continue;
            }

            string enumName = candidate.ToString();
            if (string.Equals(enumName, rawName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(enumName, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(FormatSkillName(candidate).Replace(" ", string.Empty), normalized, StringComparison.OrdinalIgnoreCase))
            {
                skillType = candidate;
                return true;
            }
        }

        return false;
    }

    internal static string FormatSkillName(Skills.SkillType skillType)
    {
        string name = skillType.ToString();
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        StringBuilder builder = new();
        builder.Append(name[0]);
        for (int i = 1; i < name.Length; i++)
        {
            char current = name[i];
            if (char.IsUpper(current) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string DescribePlayer(Player player)
    {
        if (player == null)
        {
            return "null";
        }

        return $"name={player.GetPlayerName()}, id={player.GetPlayerID()}, isLocal={player == Player.m_localPlayer}";
    }

    private static float GetNextLevelRequirement(float currentLevel)
    {
        return Mathf.Pow(Mathf.Floor(currentLevel + 1f), 1.5f) * 0.5f + 0.5f;
    }

    private static void LogDebug(string message)
    {
        if (ModConfig.DebugLogging.Value)
        {
            Plugin.Log.LogInfo(message);
        }
    }

    private static string BuildDeathSummary(List<string> entries)
    {
        const int maxEntries = 6;
        if (entries.Count <= maxEntries)
        {
            return "Debt: " + string.Join(", ", entries);
        }

        return $"Debt ({entries.Count} skills): " + string.Join(", ", entries.GetRange(0, maxEntries)) + $" (+{entries.Count - maxEntries} more; /show debt all)";
    }
}
