# Changelog

## 0.2.0

- **Paid-off baseline tracking** — `paidOff = baseline − currentDebt` (replaces lifetime-based progress display)
- **`MaxDebtPerSkill` config** — cap current debt per skill (0 = uncapped, default 3)
- **Paydown overshoot fix** — leftover paydown XP routes to leveling when debt would be exceeded
- **`DebtPaydownShare` range** — now 0.5–1.0 (default still 0.5)
- **Lifetime commands** — `/show debt lifetime <skill>` and `/show debt lifetime all`
- **Privacy confirmation** — debt stored in `Player.m_customData` (character save), not player ZDO
- Thunderstore release package with icon and documentation

## 0.1.9

- Remove F8 / `ShowDebtKey` hotkey surface.
- Add `/show debt all` for a readable multi-line rounded debt overview.

## 0.1.8

- Fix skill-panel debt hover by appending progress to the existing row `UITooltip` (same system as vanilla skill descriptions).
- Make F8 print a readable one-skill-per-line rounded overview to chat; exact decimals remain on `/show debt <skill>`.

## 0.1.7

- Track lifetime debt incurred per skill and show remaining + paid-off progress (4 decimals) on F8, `/show debt <skill>`, and skill-panel hover tooltip.
- Skip CP-06 exp-estimate approach in favor of this progress display.

## 0.1.6

- Show red `-X` debt text on the vanilla skill panel via `SkillsDialog.Setup` postfix (sibling of `bonustext`, does not touch SEMan).

## 0.1.5

- Persist debt in `Player.m_customData` (character save) instead of ephemeral player ZDO floats that die on respawn.
- Expand F8 debt-check diagnostics to log player identity, storage path, and per-skill keys.

## 0.1.2

- Add dev-only console commands gated by `EnableDevCommands` (default false).

## 0.1.1

- Add unconditional runtime diagnostics for death and skill patch entry.
- Add patch-registration startup logging for key Harmony targets.
- Bump version to avoid stale duplicate-plugin resolution.

## 0.1.0

- Initial scaffold and MVP debt mechanic implementation.
