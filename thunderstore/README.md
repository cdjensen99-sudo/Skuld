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
- **Focus paydown** — Voluntary 100% paydown on chosen skills via `/focus` (your choice, per character)
- **Skill panel display** — Maroon debt segment on the gold level bar plus red `-X` debt label
- **Configurable visuals** — Bar color, text color, and text size (`[Visual]`, client-only)
- **Hover progress** — Skill row tooltip shows remaining debt and paid-off progress
- **Chat commands** — Debt overview, lifetime stats, and focus status
- **Debt cap** — Optional per-skill maximum debt (scales with your level at death)
- **Soft-death clarity** — Message when a soft death occurs (no debt added)
- **Debt cleared feedback** — Optional halo + sound when a skill's debt hits zero
- **Server config sync** — Gameplay tuning from server/host while connected (see Multiplayer)
- **Character persistence** — Debt survives death, respawn, and save/load
- **Private to you** — Debt is stored in your character save data, not synced to other players

---

## How the debt system works

### Hard death only

Skuld only applies on **hard deaths** — the same deaths that trigger vanilla skill loss (roughly one hard death every 10 minutes of real time). Soft deaths do not add debt and do not trigger vanilla skill loss. On a soft death you see: *"Soft death — no skill debt"*.

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

- A configurable share goes toward **debt paydown** (server rule in multiplayer)
- The rest goes toward **normal leveling**

If the paydown share would exceed your remaining debt, only enough XP is used to zero the debt — the leftover goes to leveling (no XP is wasted).

Default split: **50% paydown / 50% leveling** (`DebtPaydownShare = 0.5`).

### Focus paydown (player choice)

Use `/focus` to voluntarily send **100%** of earned XP in a skill toward debt until that debt clears:

```
/focus woodcutting
/focus all
/focus off
/show focus
```

Focus is stored on **your character** (not server config). It clears automatically when debt for that skill reaches zero. Focus does not affect other players.

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

**Lifetime repaid** (shown on `/show debt lifetime <skill>`):

```
lifetimeRepaid = lifetimeIncurred − currentDebt
```

### Debt cap (`MaxDebtPerSkill`)

By default, current debt per skill is capped at **3× the raw debt from the most recent death**. The cap **floats with your level** — higher-level skills allow more total debt before the cap bites.

- `MaxDebtPerSkill = 3` (default) — cap is `3 × rawDebtThisDeath`
- `MaxDebtPerSkill = 0` — uncapped
- **Lifetime debt** always records the full uncapped raw amount, even when current debt is clamped

---

## In-game display

### Skill panel (Tab → Skills)

![Skuld skill panel showing maroon debt segments and red -X labels on indebted skills](https://raw.githubusercontent.com/cdjensen99-sudo/Skuld/main/Images/Skuld_Skill_view.png)

Skills with debt show:

- A **maroon segment** on the trailing portion of the gold **level bar** (debt in skill levels)
- A red **`-X`** label at the left edge of the bar (rounded whole number)

Vanilla blue `+X` set bonuses are unchanged. Skuld does not modify SEMan skill modifiers.

### Hover tooltip

Hover a skill row with debt to see exact progress appended below the vanilla skill description:

```
-1.2500  (1.2500 paid off)  [focused 100%]
```

### Post-death message

After a hard death, a top-left message summarizes debt added (e.g. `Debt: Wood Cutting +2.5, Run +1.1`).

### Debt cleared (optional)

When a skill's debt reaches zero, you get the usual center message (*"{Skill} debt repaid"*). If `EnableDebtClearedSound = true`, a short halo + sound plays (client-only).

---

## Chat commands

Type these in **chat** (leading `/` is optional).

| Command | Description |
|---------|-------------|
| `/show debt <skill>` | One skill, exact decimals |
| `/show debt all` | All skills with debt (rounded) |
| `/show debt lifetime <skill>` | Lifetime incurred **and** lifetime repaid |
| `/show debt lifetime all` | Lifetime totals for every skill |
| `/show focus` | Current focus paydown state |
| `/focus <skill>` | 100% paydown on one skill until cleared |
| `/focus all` | 100% paydown on all indebted skills |
| `/focus off` | Clear all focus |

Skill names are flexible — `woodcutting`, `Wood Cutting`, and `WoodCutting` all work.

---

## Configuration

Config file: `BepInEx/config/com.cdjensen99.skuld.cfg`

### `[General]` — gameplay

| Setting | Default | Sync | Description |
|---------|---------|------|-------------|
| `EnableMod` | `true` | **Server synced** | Master toggle for Skuld debt mechanics |
| `DebtPaydownShare` | `0.5` | **Server synced** | Fraction of earned XP directed to debt paydown. Range: **0.5 – 1.0** |
| `MaxDebtPerSkill` | `3` | **Server synced** | Max debt per skill in deaths-worth at death. **0 = uncapped**. Range: **0 – 5** |
| `EnableDebtClearedSound` | `false` | Client-only | Halo + sound when debt is fully repaid |
| `DebugLogging` | `false` | Client-only | Verbose logs in `BepInEx/LogOutput.log` |

**Server synced** means: when you join a multiplayer server, the server sends these three values for your session. Your local cfg file is **not** modified. On disconnect, your local settings apply again.

### `[Visual]` — client-only (not server synced)

| Setting | Default | Description |
|---------|---------|-------------|
| `DebtBarColor` | `140,31,56` | Maroon debt segment color (`R,G,B` or `#RRGGBB`) |
| `DebtTextColor` | `217,46,46` | Red `-N` label color |
| `DebtTextSize` | `18` | Label font size in pixels (8–36) |

### `[Dev]` — server/host only (not server synced)

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableDevCommands` | `false` | Enables test console commands on the **world host** only. Ignored on multiplayer clients. Still requires Valheim `devcommands` and admin rights on dedicated servers. **Must remain `false` for release builds.** |

### Tuning tips

- **`DebtPaydownShare = 0.5`** — Balanced; half your XP levels the skill, half pays debt
- **`DebtPaydownShare = 1.0`** — All XP goes to debt until cleared; skills won't level until debt is zero
- **`MaxDebtPerSkill = 3`** — Prevents runaway debt stacks on repeated deaths at high levels
- **`MaxDebtPerSkill = 0`** — Debt can grow without cap

---

## Dev commands (testing only)

When `EnableDevCommands = true` on the **server/host** cfg:

| Command | Effect |
|---------|--------|
| `skuld_clearcooldown` | Clears hard-death cooldown so the next death counts as a hard death |
| `skuld_cleardebt` | Clears all current debt, baseline, and lifetime tracking for the local player |

These are registered as Valheim cheat commands (`isCheat: true`), require `devcommands`, and mark your character as having used cheats. Leave `EnableDevCommands` at `false` for normal play.

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

## Multiplayer

- **All players need Skuld installed** for debt mechanics and UI on their own character.
- **Gameplay rules** (`EnableMod`, `DebtPaydownShare`, `MaxDebtPerSkill`) come from the **server/host** while connected. Clients receive a short join message with the active rules.
- **Focus paydown**, **visual settings**, and **debt-cleared sound** stay **per player** — not synced.
- Debt is stored in **`Player.m_customData`** (your character save), **not** on the networked player ZDO. Other players **cannot read** your debt values.

---

## Compatibility notes

- Replaces vanilla `Skills.OnDeath` skill loss on hard death — incompatible with mods that also patch hard-death skill reduction differently.
- XP paydown hooks `Player.RaiseSkill` — may interact with mods that alter skill gain rates.
- Skill panel display patches `SkillsDialog.Setup` — cosmetic only; does not affect SEMan bonus display.
- **Skill-cap mods (best-effort):** The debt bar scale follows the skills panel level bar. Tested on vanilla only. Compatibility with skill-cap mods (e.g. SkillLimitExtender) is **not author-tested** — please report issues on Discord or GitHub.

---

## Credits

**Skuld** by Hardwire99 / cdjensen99

Named after Skuld, one of the Norns of Norse mythology who weave the threads of fate.
