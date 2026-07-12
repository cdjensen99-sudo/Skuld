using UnityEngine;

namespace Skuld;

/// <summary>
/// Keeps the skill-row vanilla UITooltip text fresh with debt progress while the panel is open.
/// </summary>
internal sealed class DebtTooltipBinder : MonoBehaviour
{
    private Skills.SkillType skillType;
    private Skills.Skill skill;
    private UITooltip tooltip;
    private RectTransform tooltipAnchor;
    private Vector2 fixedPosition;
    private string baseDescription = string.Empty;

    internal void Initialize(
        Skills.SkillType type,
        Skills.Skill skillRef,
        UITooltip tip,
        string description,
        RectTransform anchor,
        Vector2 position)
    {
        skillType = type;
        skill = skillRef;
        tooltip = tip;
        baseDescription = description ?? string.Empty;
        tooltipAnchor = anchor;
        fixedPosition = position;
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (tooltip == null || Player.m_localPlayer == null)
        {
            return;
        }

        if (skill != null)
        {
            SkillDebtBarOverlay.Apply(transform, Player.m_localPlayer, skill);
        }

        float remaining = SkillDebtService.GetDebt(Player.m_localPlayer, skillType);
        if (remaining <= 0f)
        {
            tooltip.Set(string.Empty, baseDescription, tooltipAnchor, fixedPosition);
            return;
        }

        string detail = SkillDebtService.FormatProgressDetail(Player.m_localPlayer, skillType);
        string text = string.IsNullOrEmpty(baseDescription)
            ? detail
            : baseDescription + "\n\n" + detail;
        tooltip.Set(string.Empty, text, tooltipAnchor, fixedPosition);
    }
}
