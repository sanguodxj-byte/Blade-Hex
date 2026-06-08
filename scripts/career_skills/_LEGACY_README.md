# v0.8 Legacy Career Skill Lua Scripts

This directory contains **v0.8 legacy** career skill Lua scripts.

## Status: DEPRECATED — Not used by v1.0

Since **v1.0**, career skills are registered and executed entirely through C#:

- **Registration**: `BladeHexCore/src/SkillTree/CareerSkillRegistry.cs` — all 63 skills inline
- **Passive hooks**: `BladeHexFrontend/src/View/Combat/CareerPassiveHooks.cs`
- **Active execution**: `BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`
- **Data model**: `BladeHexCore/src/SkillTree/CareerSkillData.cs`

## Why keep these files?

These Lua scripts are preserved for historical reference only. They document the original v0.8 design intent and were used to derive the v1.0 C# implementation. No gameplay code reads them.

## Removal

These files can be safely deleted once the v1.0 system is fully validated in-game.
