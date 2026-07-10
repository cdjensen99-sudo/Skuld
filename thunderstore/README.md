# Skuld

**Death no longer erases your skills — it puts them in debt.**

Skuld is a Valheim BepInEx mod that replaces vanilla **hard-death skill loss** with **repayable per-skill debt**. When you die on a hard death, the skill levels you would have lost become debt instead. Your actual skill levels stay where they are. While debt remains, earned XP is split between normal leveling and paying that debt down. Once a skill's debt reaches zero, it earns XP at full speed again.

Named after **Skuld**, the Norn who weaves the future — your fate is not erased on death, only deferred.

---

## Support & feedback

Bug reports, feature requests, and questions:

- **[Team Extreme Discord](https://discord.gg/cCNG8xKXMn)** — setup help, testing, and updates
- **[GitHub — Skuld](https://github.com/cdjensen99-sudo/Skuld)** — source code and issues

When reporting a problem, include your mod version, single-player or multiplayer, and any other skill or death-related mods.

---

## Features

- **Debt instead of loss** — Hard-death skill reduction becomes per-skill debt; levels are not lowered
- **XP split paydown** — While debt exists, earned XP is shared between leveling and debt repayment
- **Skill panel display** — Red `-X` debt indicator next to each skill (separate from vanilla blue bonus text)
- **Hover progress** — Skill row tooltip shows remaining debt and paid-off progress
- **Chat commands** — Check debt for one skill, all skills, or lifetime totals
- **Debt cap** — Optional per-skill maximum debt (scales with your level at death)
- **Character persistence** — Debt survives death, respawn, and save/load
- **Private to you** — Debt is stored in your character save data, not synced to other players

---

## How the debt system works

### Hard death only

Skuld only applies on **hard deaths** — the same deaths that trigger vanilla skill loss (roughly one hard death every 10 minutes of real time). Soft deaths do not add debt and do not trigger vanilla skill loss.

### What happens on a hard death

For each skill with a level above zero, Skuld calculates the vanilla loss amount:

```
rawDebt = skillLevel × deathFactor
```

That amount is **not** subtracted from your skill level. Instead:

1. **Current debt** increases by the raw amount (subject to cap — see below)
2. **Lifetime debt** increases by the full raw amount (never decreases)
3. **Paid-off baseline** resets to the new current debt (paid-off progress starts over for this death cycle)

You also get a short summary message listing which skills gained debt.

### Paying debt down

Whenever you earn XP in a skill that has debt, Skuld splits the gain:

- A configurable share goes toward **debt paydown**
- The rest goes toward **normal leveling**

If the paydown share would exceed your remaining debt, only enough XP is used to zero the debt — the leftover goes to leveling (no XP is wasted).

Default split: **50% paydown / 50% leveling** (`DebtPaydownShare = 0.5`).

### Three tracked values per skill

| Value | What it means |
|-------|----------------|
| **Current debt** | How much you still owe on this skill |
| **Paid-off baseline** | Snapshot of current debt when it was last added on death; used to calculate progress |
| **Lifetime debt** | Total raw debt ever incurred on this skill across all deaths (never shrinks) |

**Paid-off progress** (shown in tooltips and `/show debt`):

```
paidOff = baseline − currentDebt
```

Lifetime debt is only shown via `/show debt lifetime` commands.

### Worked example

You are **level 50** in Wood Cutting and die on a hard death:

| | Value |
|---|------|
| Current debt | 2.5000 |
| Baseline | 2.5000 |
| Lifetime | 2.5000 |
| Paid off | 0.0000 |

You earn enough XP to gain **2.5 levels** of progress at a 50/50 split. Your displayed level rises to ~51.25, and **1.25** levels of debt are paid off:

| | Value |
|---|------|
| Current debt | 1.2500 |
| Baseline | 2.5000 (unchanged) |
| Lifetime | 2.5000 |
| Paid off | 1.2500 |

You die again at level 51.25. Raw debt this death is **2.5625**:

| | Value |
|---|------|
| Current debt | 3.8125 |
| Baseline | 3.8125 (reset) |
| Lifetime | 5.0625 |
| Paid off | 0.0000 |

### Debt cap (`MaxDebtPerSkill`)

By default, current debt per skill is capped at **3× the raw debt from the most recent death**. The cap **floats with your level** — higher-level skills allow more total debt before the cap bites.

- `MaxDebtPerSkill = 3` (default) — cap is `3 × rawDebtThisDeath`
- `MaxDebtPerSkill = 0` — uncapped
- **Lifetime debt** always records the full uncapped raw amount, even when current debt is clamped

---

## In-game display

### Skill panel (Tab → Skills)

Skills with debt show a red **`-X`** next to the level (rounded whole number), sibling to vanilla's blue `+X` bonus text. Skuld does not modify bonus effects or SEMan skill modifiers.

### Hover tooltip

Hover a skill row with debt to see exact progress appended below the vanilla skill description:

```
-1.2500  (1.2500 paid off)
```

### Post-death message

After a hard death, a top-left message summarizes debt added (e.g. `Debt: Wood Cutting +2.5, Run +1.1`).

---

## Chat commands

Type these in **chat** (leading `/` is optional — Valheim routes them through the same command system as the console).

### `/show debt <skill>` — one skill, exact decimals

Shows remaining debt and paid-off progress for a single skill.

**Examples:**

```
/show debt woodcutting
→ Wood Cutting: -1.2500  (1.2500 paid off)

/show debt knives
→ Knives: -2.5000  (0.0000 paid off)
```

Skill names are flexible — `woodcutting`, `Wood Cutting`, and `WoodCutting` all work.

### `/show debt all` — every skill with debt (rounded)

Prints a multi-line list to chat with **rounded whole numbers** (same rounding as the red skill-panel text). Use this for a quick overview.

**Example:**

```
/show debt all
→ Skuld debt (3):
    Wood Cutting -3
    Run -1
    Jump -2
  Exact progress: /show debt <skill>
```

### `/show debt lifetime <skill>` — lifetime total for one skill

Shows cumulative raw debt ever incurred on that skill. This is the **only** place lifetime appears in normal play.

**Example:**

```
/show debt lifetime woodcutting
→ Wood Cutting lifetime: 5.0625 levels (lifetime)
```

### `/show debt lifetime all` — lifetime totals for every skill

**Example:**

```
/show debt lifetime all
→ Skuld lifetime debt (2):
    Wood Cutting 5.0625
    Run 3.0000
```

---

## Configuration

Config file: `BepInEx/config/com.cdjensen99.skuld.cfg`

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableMod` | `true` | Master toggle for Skuld debt mechanics |
| `DebtPaydownShare` | `0.5` | Fraction of earned XP directed to debt paydown while debt remains. Range: **0.5 – 1.0** |
| `MaxDebtPerSkill` | `3` | Maximum debt per skill, measured in deaths-worth of that skill's level at death. **0 = uncapped**. Range: **0 – 5** |
| `DebugLogging` | `false` | Verbose debt conversion and paydown logs in `BepInEx/LogOutput.log` |
| `EnableDevCommands` | `false` | Enables dev console commands (see below). **Must remain `false` for release builds** |

### Tuning tips

- **`DebtPaydownShare = 0.5`** — Balanced; half your XP levels the skill, half pays debt
- **`DebtPaydownShare = 1.0`** — All XP goes to debt until cleared; skills won't level until debt is zero
- **`MaxDebtPerSkill = 3`** — Prevents runaway debt stacks on repeated deaths at high levels
- **`MaxDebtPerSkill = 0`** — Vanilla-like stacking; debt can grow without cap

---

## Dev commands (testing only)

When `EnableDevCommands = true`, the in-game console exposes:

| Command | Effect |
|---------|--------|
| `skuld_clearcooldown` | Clears hard-death cooldown so the next death counts as a hard death |
| `skuld_cleardebt` | Clears all current debt, baseline, and lifetime tracking for the local player |

These are for **testing only**. Leave `EnableDevCommands` at `false` for normal play and Thunderstore releases.

---

## Installation

**Requirements:**

- [BepInEx Pack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

Install via r2modman, Thunderstore Mod Manager, or manually:

1. Install BepInEx Pack for Valheim
2. Install Skuld
3. Launch the game

No Jotunn or other framework dependencies.

---

## Multiplayer & privacy

- Skuld is **client-side**. Each player needs the mod installed for their own debt mechanics.
- Debt is stored in **`Player.m_customData`** (your character save file), **not** on the networked player ZDO.
- Other players **cannot read** your debt values.

---

## Compatibility notes

- Replaces vanilla `Skills.OnDeath` skill loss on hard death — incompatible with mods that also patch hard-death skill reduction differently.
- XP paydown hooks `Player.RaiseSkill` — may interact with mods that alter skill gain rates.
- Skill panel display patches `SkillsDialog.Setup` — cosmetic only; does not affect SEMan bonus display.

---

## Credits

**Skuld** by Hardwire99 / cdjensen99

Named after Skuld, one of the Norns of Norse mythology who weave the threads of fate.
