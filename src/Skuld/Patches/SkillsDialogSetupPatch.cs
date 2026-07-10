using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Skuld.Patches;

[HarmonyPatch(typeof(SkillsDialog), nameof(SkillsDialog.Setup))]
internal static class SkillsDialogSetupPatch
{
    private const string DebtTextName = "skuld_debttext";
    private static readonly Color DebtColor = new(0.85f, 0.18f, 0.18f, 1f);
    private static readonly FieldInfo ElementsField = AccessTools.Field(typeof(SkillsDialog), "m_elements");
    private static readonly FieldInfo TooltipAnchorField = AccessTools.Field(typeof(SkillsDialog), "m_tooltipAnchor");

    private static void Postfix(SkillsDialog __instance, Player player)
    {
        if (!ModConfig.EnableMod.Value || player == null || ElementsField == null)
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

            ApplyDebtLabel(row, player, skill, tooltipAnchor);
        }
    }

    private static void ApplyDebtLabel(GameObject row, Player player, Skills.Skill skill, RectTransform tooltipAnchor)
    {
        Skills.SkillType skillType = skill.m_info.m_skill;
        Transform bonusTransform = Utils.FindChild(row.transform, "bonustext");
        if (bonusTransform == null)
        {
            return;
        }

        TMP_Text bonusText = bonusTransform.GetComponent<TMP_Text>();
        TMP_Text debtText = GetOrCreateDebtText(bonusTransform, bonusText);
        if (debtText == null)
        {
            return;
        }

        float debt = SkillDebtService.GetDebt(player, skillType);
        bool showDebt = debt > 0f;
        debtText.gameObject.SetActive(showDebt);
        if (showDebt)
        {
            debtText.text = (-Mathf.RoundToInt(debt)).ToString("0");
            debtText.color = DebtColor;

            RectTransform debtRect = debtText.rectTransform;
            RectTransform bonusRect = bonusTransform as RectTransform;
            if (debtRect != null && bonusRect != null)
            {
                float xOffset = bonusTransform.gameObject.activeSelf ? bonusRect.rect.width + 8f : 0f;
                debtRect.anchoredPosition = bonusRect.anchoredPosition + new Vector2(xOffset, 0f);
            }
        }

        // Hook the same row UITooltip vanilla already uses for skill descriptions.
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

        binder.Initialize(skill.m_info.m_skill, rowTooltip, skill.m_info.m_description, tooltipAnchor, fixedPosition);
    }

    private static TMP_Text GetOrCreateDebtText(Transform bonusTransform, TMP_Text bonusText)
    {
        Transform existing = bonusTransform.parent.Find(DebtTextName);
        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        GameObject debtObject = Object.Instantiate(bonusTransform.gameObject, bonusTransform.parent);
        debtObject.name = DebtTextName;

        UITooltip clonedTooltip = debtObject.GetComponent<UITooltip>();
        if (clonedTooltip != null)
        {
            Object.Destroy(clonedTooltip);
        }

        TMP_Text debtText = debtObject.GetComponent<TMP_Text>();
        if (debtText == null)
        {
            Object.Destroy(debtObject);
            return null;
        }

        if (bonusText != null)
        {
            debtText.font = bonusText.font;
            debtText.fontSize = bonusText.fontSize;
            debtText.fontStyle = bonusText.fontStyle;
            debtText.alignment = bonusText.alignment;
            debtText.enableAutoSizing = bonusText.enableAutoSizing;
        }

        // Debt label is visual-only; hover uses the row's existing UITooltip.
        CanvasGroup canvasGroup = debtObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = debtObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        debtText.color = DebtColor;
        debtObject.SetActive(false);
        return debtText;
    }
}
