using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Skuld;

/// <summary>
/// Renders repayable skill debt on the vanilla overall skill level bar (maroon segment + label).
/// </summary>
internal static class SkillDebtBarOverlay
{
    private const string DebtBarName = "skuld_debtbar";
    private const string DebtTextName = "skuld_debttext";
    private const float LevelBarScale = 100f;
    private const float LabelGapPixels = 4f;

    internal static void Apply(Transform row, Player player, Skills.Skill skill)
    {
        if (row == null || player == null || skill?.m_info == null)
        {
            return;
        }

        RemoveLegacyDebtBar(Utils.FindChild(row, "currentlevel"));

        Transform levelBarTransform = Utils.FindChild(row, "levelbar");
        if (levelBarTransform == null)
        {
            return;
        }

        GuiBar levelBar = levelBarTransform.GetComponent<GuiBar>();
        if (levelBar == null)
        {
            return;
        }

        float debt = SkillDebtService.GetDebt(player, skill.m_info.m_skill);
        float levelBarFill = GetLevelBarFill(levelBar);
        float barDenominator = ResolveBarDenominator(skill.m_info.m_skill, skill.m_level, levelBarFill);
        ComputeDebtBarSegment(skill.m_level, debt, barDenominator, levelBarFill, out float segmentStart, out float segmentEnd);
        float debtFraction = segmentEnd - segmentStart;
        bool showDebt = debt > 0f && debtFraction > 0f;

        ApplyMaroonSegment(levelBarTransform, levelBar, segmentStart, segmentEnd, showDebt);
        ApplyDebtLabel(row, segmentStart, segmentEnd, debt, showDebt);
        EnsureVanillaLabelsDrawAboveDebtBar(row);
    }

    private static void EnsureVanillaLabelsDrawAboveDebtBar(Transform row)
    {
        Transform levelText = Utils.FindChild(row, "leveltext");
        if (levelText != null)
        {
            levelText.SetAsLastSibling();
        }

        Transform bonusText = Utils.FindChild(row, "bonustext");
        if (bonusText != null)
        {
            bonusText.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Debt in skill levels maps to the trailing portion of the gold level bar (0..1 fill).
    /// </summary>
    internal static void ComputeDebtBarSegment(
        float skillLevel,
        float debtLevels,
        float barDenominator,
        float levelBarFill,
        out float segmentStart,
        out float segmentEnd)
    {
        segmentStart = 0f;
        segmentEnd = 0f;

        if (debtLevels <= 0f || skillLevel <= 0f)
        {
            return;
        }

        float denominator = barDenominator > 0f ? barDenominator : LevelBarScale;
        float levelFraction = levelBarFill > 0f
            ? Mathf.Clamp01(levelBarFill)
            : Mathf.Clamp01(skillLevel / denominator);
        float debtFraction = Mathf.Min(debtLevels / denominator, levelFraction);
        segmentEnd = levelFraction;
        segmentStart = levelFraction - debtFraction;
    }

    private static float GetLevelBarFill(GuiBar levelBar)
    {
        return levelBar != null ? Mathf.Clamp01(levelBar.GetSmoothValue()) : 0f;
    }

    private static float ResolveBarDenominator(Skills.SkillType skillType, float skillLevel, float levelBarFill)
    {
        if (SkillLimitExtenderCompat.TryGetUiDenominator(skillType, out float fromExtender))
        {
            return fromExtender;
        }

        if (skillLevel > 0f && levelBarFill > 0.0001f)
        {
            return skillLevel / levelBarFill;
        }

        return LevelBarScale;
    }

    private static void ApplyMaroonSegment(
        Transform levelBarTransform,
        GuiBar levelBar,
        float segmentStart,
        float segmentEnd,
        bool showDebt)
    {
        Image debtImage = GetOrCreateDebtImage(levelBarTransform, levelBar);
        if (debtImage == null)
        {
            return;
        }

        debtImage.gameObject.SetActive(showDebt);
        if (!showDebt)
        {
            return;
        }

        debtImage.color = ModConfig.GetDebtBarColor();

        RectTransform debtRect = debtImage.rectTransform;
        debtRect.anchorMin = new Vector2(segmentStart, 0f);
        debtRect.anchorMax = new Vector2(segmentEnd, 1f);
        debtRect.offsetMin = Vector2.zero;
        debtRect.offsetMax = Vector2.zero;
        debtRect.SetAsLastSibling();
    }

    private static void ApplyDebtLabel(
        Transform row,
        float maroonStart,
        float maroonEnd,
        float debt,
        bool showDebt)
    {
        Transform levelBarTransform = Utils.FindChild(row, "levelbar");
        TMP_Text debtText = GetOrCreateDebtText(row, levelBarTransform);
        if (debtText == null)
        {
            return;
        }

        debtText.gameObject.SetActive(showDebt);
        if (!showDebt)
        {
            return;
        }

        debtText.text = (-Mathf.RoundToInt(debt)).ToString("0");
        ApplyDebtTextStyle(debtText, row);
        debtText.ForceMeshUpdate();

        RectTransform debtRect = debtText.rectTransform;
        if (levelBarTransform is RectTransform levelBarRect)
        {
            PlaceDebtLabelAtBarStart(debtText, debtRect, levelBarRect);
            debtRect.SetAsLastSibling();
            return;
        }

        // Fallback: center on the maroon segment when levelbar is unavailable.
        debtRect.SetParent(levelBarTransform, false);
        debtRect.anchorMin = new Vector2(maroonStart, 0f);
        debtRect.anchorMax = new Vector2(maroonEnd, 1f);
        debtRect.offsetMin = Vector2.zero;
        debtRect.offsetMax = Vector2.zero;
        debtRect.pivot = new Vector2(0.5f, 0.5f);
        debtRect.anchoredPosition = Vector2.zero;
        debtRect.sizeDelta = Vector2.zero;
        debtText.alignment = TextAlignmentOptions.Center;
        debtRect.SetAsLastSibling();
    }

    private static void PlaceDebtLabelAtBarStart(TMP_Text debtText, RectTransform debtRect, RectTransform levelBarRect)
    {
        Transform rowParent = levelBarRect.parent;
        debtRect.SetParent(rowParent, false);

        float labelWidth = Mathf.Max(28f, debtText.preferredWidth + 4f);
        float labelHeight = levelBarRect.rect.height > 0f ? levelBarRect.rect.height : 28f;
        Vector3 barLeftCenter = levelBarRect.TransformPoint(
            new Vector3(levelBarRect.rect.xMin + LabelGapPixels, levelBarRect.rect.center.y, 0f));
        Vector3 rowLocal = rowParent.InverseTransformPoint(barLeftCenter);

        debtRect.anchorMin = new Vector2(0.5f, 0.5f);
        debtRect.anchorMax = new Vector2(0.5f, 0.5f);
        debtRect.pivot = new Vector2(0f, 0.5f);
        debtRect.sizeDelta = new Vector2(labelWidth, labelHeight);
        debtRect.localPosition = rowLocal;
        debtText.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void RemoveLegacyDebtBar(Transform progressBarTransform)
    {
        if (progressBarTransform == null)
        {
            return;
        }

        Transform legacy = progressBarTransform.Find(DebtBarName);
        if (legacy != null)
        {
            Object.Destroy(legacy.gameObject);
        }
    }

    private static Image GetOrCreateDebtImage(Transform trackTransform, GuiBar levelBar)
    {
        Transform existing = trackTransform.Find(DebtBarName);
        if (existing != null)
        {
            return existing.GetComponent<Image>();
        }

        Image sourceImage = levelBar.m_bar != null ? levelBar.m_bar.GetComponent<Image>() : null;
        GameObject debtObject = new GameObject(DebtBarName, typeof(RectTransform), typeof(Image));
        debtObject.transform.SetParent(trackTransform, false);

        Image debtImage = debtObject.GetComponent<Image>();
        debtImage.raycastTarget = false;
        debtImage.color = ModConfig.GetDebtBarColor();

        if (sourceImage != null)
        {
            debtImage.sprite = sourceImage.sprite;
            debtImage.type = sourceImage.type;
            debtImage.material = sourceImage.material;
            debtImage.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
        }

        return debtImage;
    }

    private static TMP_Text GetOrCreateDebtText(Transform row, Transform levelBarTransform)
    {
        Transform existing = Utils.FindChild(row, DebtTextName);
        if (existing != null)
        {
            if (existing.parent == levelBarTransform)
            {
                Object.Destroy(existing.gameObject);
                existing = null;
            }
        }

        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            ApplyDebtTextStyle(existingText, row);
            return existingText;
        }

        TMP_Text fontSource = GetDebtTextFontSource(row);
        if (fontSource == null || levelBarTransform == null)
        {
            return null;
        }

        // Clone vanilla row text so TMP already has Valheim's font (avoids LiberationSans warnings).
        GameObject debtObject = Object.Instantiate(fontSource.gameObject, fontSource.transform.parent);
        debtObject.name = DebtTextName;
        debtObject.SetActive(false);
        StripNonTextComponents(debtObject);

        TMP_Text debtText = debtObject.GetComponent<TMP_Text>();
        debtText.raycastTarget = false;
        debtText.text = string.Empty;
        ApplyDebtTextStyle(debtText, row);

        if (debtObject.GetComponent<CanvasGroup>() == null)
        {
            CanvasGroup canvasGroup = debtObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        return debtText;
    }

    private static TMP_Text GetDebtTextFontSource(Transform row)
    {
        return Utils.FindChild(row, "leveltext")?.GetComponent<TMP_Text>()
            ?? Utils.FindChild(row, "bonustext")?.GetComponent<TMP_Text>();
    }

    private static void ApplyDebtTextStyle(TMP_Text debtText, Transform row)
    {
        TMP_Text fontSource = GetDebtTextFontSource(row);
        if (debtText == null || fontSource == null)
        {
            return;
        }

        debtText.font = fontSource.font;
        debtText.fontSharedMaterial = fontSource.fontSharedMaterial;
        debtText.fontStyle = fontSource.fontStyle;
        debtText.characterSpacing = fontSource.characterSpacing;
        debtText.wordSpacing = fontSource.wordSpacing;
        debtText.lineSpacing = fontSource.lineSpacing;
        debtText.paragraphSpacing = fontSource.paragraphSpacing;
        debtText.textWrappingMode = fontSource.textWrappingMode;
        debtText.overflowMode = TextOverflowModes.Overflow;
        debtText.enableAutoSizing = false;
        debtText.color = ModConfig.GetDebtTextColor();
        debtText.fontSize = ModConfig.GetDebtTextSize();
    }

    private static void StripNonTextComponents(GameObject debtObject)
    {
        Component[] components = debtObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component is Transform
                || component is RectTransform
                || component is TextMeshProUGUI
                || component is CanvasRenderer)
            {
                continue;
            }

            Object.Destroy(component);
        }
    }
}
