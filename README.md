# Skuld

Valheim BepInEx mod — replaces hard-death skill loss with repayable per-skill debt.

**Current version:** 0.3.2

## Quick links

- **Thunderstore README:** [thunderstore/README.md](thunderstore/README.md) — full player documentation
- **GitHub:** https://github.com/cdjensen99-sudo/Skuld
- **Discord:** https://discord.gg/cCNG8xKXMn

## Build

```powershell
.\build.ps1                          # Build Release → artifacts\Skuld.dll
.\build.ps1 -Deploy                  # Build + deploy to r2modman Testing profile
.\build.ps1 -Package                 # Build + Thunderstore zip
.\build.ps1 -Deploy -Package         # All of the above
```

## Dev testing

Set `EnableDevCommands = true` in `BepInEx/config/com.cdjensen99.skuld.cfg` for `skuld_clearcooldown` and `skuld_cleardebt`. These register as Valheim cheat commands and require `devcommands`; using them marks the character as cheated for achievements. Must remain `false` for Thunderstore releases.
