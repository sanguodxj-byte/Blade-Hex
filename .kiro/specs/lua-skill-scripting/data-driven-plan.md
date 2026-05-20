# 数据驱动化 + Mod 支持 — 剩余硬编码盘点

## 已完成

| 系统 | 状态 | Mod 支持 |
|------|------|----------|
| 技能效果逻辑 | ✅ Lua 脚本化 (49个) | ✅ `user://mods/skills/` |
| 物品（武器/护甲/消耗品/箭筒/饰品） | ✅ JSON 驱动 | ✅ `user://mods/items/` |
| 技能配置（AP/范围/名称） | ✅ SkillRegistry C# 字典 | — |
| 敌人模板 | ✅ enemies.json | — |
| 起源问答 | ✅ origin_questions.json | — |
| 任务模板 | ✅ quest_templates.json | — |
| 建筑数据 | ✅ buildings.json (有 fallback) | — |
| 遭遇描述 | ✅ encounter_descriptions.json | — |

## 仍然硬编码（按优先级排序）

### P1: 应该尽快外置

| 模块 | 文件 | 内容 | 建议格式 |
|------|------|------|----------|
| **种族数据** | `RaceData.cs` | 5个种族的属性修正、特性、好感度、招募难度 | `races.json` |
| **坐骑数据** | `MountData.cs` | 6种坐骑的速度/HP/特性 | `mounts.json` |
| **武器子类型基础数值** | `WeaponRegistry.cs` | 各武器子类型的骰子/AP/射程/重量 | `weapon_subtypes.json` |
| **SkillRegistry 技能配置** | `SkillRegistry.cs` | ~100个技能的 AP 消耗/目标类型/名称/描述 | `skill_configs.json` |

### P2: 可以外置但不紧急

| 模块 | 文件 | 内容 | 建议格式 |
|------|------|------|----------|
| QuestManager 样例任务 | `QuestManager.cs` | 2个占位任务 | 已有 quest_templates.json，只需切换加载源 |
| TipsData 加载提示 | `TipsData.cs` | 硬编码回退文本 | 已有 tips.json，删除 fallback |
| BuildingDataLoader 回退 | `BuildingDataLoader.cs` | 硬编码建筑数据 | 已有 buildings.json，删除 fallback |
| FogOfWar 区域揭示 | `FogOfWar.cs` | 种族初始可见区域 | `race_regions.json` |

### P3: 保持硬编码合理

| 模块 | 原因 |
|------|------|
| RPGRuleEngine | 核心数学公式，不适合外置（XP表、属性修正公式） |
| HexUtils | 纯数学工具 |
| CombatResolver | 战斗结算核心逻辑 |
| AI 策略类 | 复杂行为逻辑，未来可考虑 Lua 化但不紧急 |

## Mod 目录结构（已实现 + 规划）

```
user://mods/
├── skills/              ← ✅ 已实现
│   └── my_custom_skill.lua
├── items/               ← ✅ 已实现
│   ├── weapons_mod.json
│   ├── armors_mod.json
│   └── accessories_mod.json
├── races/               ← 规划中
│   └── custom_race.json
├── enemies/             ← 规划中
│   └── custom_enemies.json
└── quests/              ← 规划中
    └── custom_quests.json
```

## 下一步建议

1. **种族 JSON 化** — 影响面最广（CharacterGenerator、OriginSelect、EncounterSpawner 都引用）
2. **SkillRegistry JSON 化** — 让技能的 AP/范围/名称也能 Mod
3. **WeaponRegistry JSON 化** — 让武器子类型基础数值可配置
4. **坐骑 JSON 化** — 最简单，独立模块
