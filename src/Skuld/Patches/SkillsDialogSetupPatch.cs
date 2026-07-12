using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Skuld.Patches;

[HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
internal static class SkillsDialogSetupPatch
{
    private static readonly FieldInfo ElementsField = AccessTools.Field(typeof(SkillsDialog), "m_elements");
    private static readonly FieldInfo TooltipAnchorField = AccessTools.Field(typeof(SkillsDialog), "m_tooltipAnchor");

    private static void Postfix(SkillsDialog __instance, Player player)
    {
        if (!ModConfig.IsModEnabled || player == null || ElementsField == null)
        {
            return;
        }

        List<GameObject> elements = ElementsField.GetValue(__instance) as List<GameObject>;
        if (elements == null)
        {
            Plugin.Log.LogWarning("Skuld skill UI: could not read SkillsDialog.m_elements.");
            return;
        }

        RectTransform tooltipAnchor = TooltipAnchorField?.GetValue(__instance) as RectTransform;
        List<Skills.Skill> skillList = player.GetSkills().GetSkillList();
        int count = Mathf.Min(skillList.Count, elements.Count);
        for (int i = 0; i < count; i++)
        {
            Skills.Skill skill = skillList[i];
            GameObject row = elements[i];
            if (skill?.m_info == null || row == null || !row.activeSelf)
            {
                continue;
            }

            ApplyDebtDisplay(row, player, skill, tooltipAnchor);
        }
    }

    private static void ApplyDebtDisplay(GameObject row, Player player, Skills.Skill skill, RectTransform tooltipAnchor)
    {
        SkillDebtBarOverlay.Apply(row.transform, player, skill);
        BindRowDebtTooltip(row, skill, tooltipAnchor);
    }

    private static void BindRowDebtTooltip(GameObject row, Skills.Skill skill, RectTransform tooltipAnchor)
    {
        UITooltip rowTooltip = row.GetComponentInChildren<UITooltip>(true);
        if (rowTooltip == null)
        {
            return;
        }

        RectTransform rowRect = row.transform as RectTransform;
        Vector2 fixedPosition = rowRect != null
            ? new Vector2(0f, Mathf.Min(255f, rowRect.localPosition.y + 10f))
            : Vector2.zero;

        DebtTooltipBinder binder = row.GetComponent<DebtTooltipBinder>();
        if (binder == null)
        {
            binder = row.AddComponent<DebtTooltipBinder>();
        }

        binder.Initialize(skill.m_info.m_skill, skill, rowTooltip, skill.m_info.m_description, tooltipAnchor, fixedPosition);
    }
}
