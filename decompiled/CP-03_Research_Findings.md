# CP-03 Research Findings — Skill Panel Bonus Display

**Date:** 2026-07-09  
**Source:** live `assembly_valheim.dll` via `ilspycmd`  
**Staged under:** `D:\ValheimProjects\Skuld\decompiled\`

## 1. Skill list panel renderer

**Class:** `SkillsDialog`  
**Method:** `SkillsDialog.Setup(Player player)`  
**Opened from:** `InventoryGui.OnOpenSkills()` → `m_skillsDialog.Setup(...)`

Each skill row is built from `m_elementPrefab` children:
- `icon`, `name`, `leveltext`, `bonustext`, `levelbar`, `levelbar_total`, `currentlevel`

## 2. How the blue "+15" is computed and shown

Confirmed path (not a separate UI system):

1. Base level comes from `skill.m_level` (raw stored skill).
2. Displayed effective level comes from `player.GetSkills().GetSkillLevel(skillType)`.
3. `Skills.GetSkillLevel` does:
   - `float level = GetSkill(skillType).m_level;`
   - `m_player.GetSEMan().ModifySkillLevel(skillType, ref level);`
   - `return Mathf.Floor(level);`
4. `SEMan.ModifySkillLevel` iterates active status effects and calls each `StatusEffect.ModifySkillLevel(skill, ref level)`.
5. Base `StatusEffect.ModifySkillLevel` is an empty virtual — concrete status effects override it to add bonuses.

In `SkillsDialog.Setup`:

```csharp
float skillLevel = player.GetSkills().GetSkillLevel(skill.m_info.m_skill);
Utils.FindChild(obj.transform, "leveltext").GetComponent<TMP_Text>().text = ((int)skill.m_level).ToString();
TMP_Text component = Utils.FindChild(obj.transform, "bonustext").GetComponent<TMP_Text>();
bool flag = skillLevel != Mathf.Floor(skill.m_level);
component.gameObject.SetActive(flag);
if (flag)
{
    component.text = (skillLevel - skill.m_level).ToString("+0");
}
Utils.FindChild(obj.transform, "levelbar_total").GetComponent<GuiBar>().SetValue(skillLevel / 100f);
Utils.FindChild(obj.transform, "levelbar").GetComponent<GuiBar>().SetValue(skill.m_level / 100f);
```

So:
- `leveltext` = raw base level
- `bonustext` = delta `(effective - base)` formatted with `"+0"` (forces a leading `+` for positive values)
- `levelbar_total` = effective level fill
- `levelbar` = base level fill

**SEMan.ModifySkillLevel hypothesis: confirmed.**

## 3. Can the same hook show a red "-X"?

**Partially yes for value math, no for color/format out of the box.**

- The delta math already supports negatives: if `ModifySkillLevel` reduced `level`, then `skillLevel - skill.m_level` would be negative.
- Format string is `"+0"` — for negatives this typically still renders a minus (e.g. `-6`), but it is not a dedicated red-debt style.
- Visibility gate is `skillLevel != Mathf.Floor(skill.m_level)` — a negative delta would still activate `bonustext`.
- Color is not set in code here; `bonustext` color is almost certainly prefab/serialized TMP styling (blue for the observed positive bonus). Treat that as prefab-driven, not a decompiled default to trust blindly.

**Debt should not reuse `SEMan.ModifySkillLevel` as the debt source.** Architecture already says debt must not interact with positive skill modifiers. Feeding debt through SEMan would also change gameplay skill checks (`GetSkillFactor` / `GetSkillLevel`), which is wrong for Skuld.

## 4. Recommended later implementation path

Do **not** inject debt into SEMan. Instead patch `SkillsDialog.Setup` (or postfix it) to:
1. Keep vanilla bonus path untouched for positive modifiers.
2. Separately read Skuld debt for that skill.
3. Append/set a red `"-X"` on `bonustext` (or a sibling text object if combining bonus+debt is needed).

If both a positive bonus and debt exist on the same skill, decide display policy in a later CP (show both, or debt-only, etc.).

## Gotchas

- Prefab-driven TMP color/style on `bonustext` — confirm live, don't assume from IL alone.
- `GetSkillLevel` floors the modified value; dialog compares against `Mathf.Floor(skill.m_level)`.
- No UI code written in this CP (research only).
