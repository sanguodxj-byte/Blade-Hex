# Lua 技能脚本化 — Requirements

## R1: Lua 运行时集成

MoonSharp 作为 NuGet 依赖引入 BladeHexCore，提供沙箱化的 Lua 5.2 执行环境。
VM 实例在游戏启动时创建，生命周期与进程一致。

## R2: 战斗 API 绑定

将技能 handler 所需的全部 C# 能力注册为 Lua 全局函数：
- 攻击结算（ResolveAttack）
- 骰子/属性修正（RollDice, GetStatModifier, GetProficiencyBonus, MakeSave）
- 六角格工具（GetNeighbors, Distance, GetNeighbor）
- 单位查询（FindUnitAt, 遍历 enemies/allies）
- 单位操作（Heal, TakeDamage, ChangeMorale）
- 结果构造（add_attack, add_effect, add_damage, fail）

## R3: Unit 代理层

Lua 不直接持有 Godot Node3D 引用。通过 LuaUnitProxy 暴露只读/受控读写的数据视图：
- 属性（6 维 + HP/Mana/Level）
- 位置（GridPos）
- 运行时状态（ActiveStatusEffects, ExtraActionsThisTurn, 各 UsedThisCombat 标记）
- 被动技能查询（HasSkillEffect）

## R4: 脚本加载与缓存

- 从 `res://scripts/skills/` 目录加载 `.lua` 文件
- 文件名 = 技能 ID（如 `double_attack.lua`）
- 首次加载后缓存编译结果，后续调用直接执行
- 支持 `_lib.lua` 公共库预加载（工具函数复用）

## R5: 混合分发

SkillEffectExecutor 保持现有注册表机制：
- C# Handler 优先（已注册的技能走 C#）
- 未注册的技能 fallback 到 Lua 脚本查找
- 两者都没有 → 返回错误

允许渐进迁移：可以一个一个技能从 C# 移到 Lua，不需要一次性全迁。

## R6: 热重载

开发模式下支持通过 DebugConsole 命令重载 Lua 脚本：
- `lua_reload <skill_id>` — 重载单个技能
- `lua_reload_all` — 重载全部技能
- 重载后下次技能执行即使用新逻辑

## R7: 错误隔离

Lua 脚本执行错误不得导致游戏崩溃或战斗状态不一致：
- 捕获所有 Lua 异常
- 记录错误日志（文件名 + 行号 + 消息）
- 技能执行返回 `{ success = false, reason = "..." }`
- 战斗流程正常继续（跳过该技能效果）

## R8: 安全沙箱

Lua 环境禁止访问文件系统、操作系统、网络等危险 API：
- 白名单：math, string, table, pairs, ipairs, type, tostring, tonumber, print
- print 重定向到引擎日志
- 禁止 require/load/dofile（防止加载任意代码）

## R9: 性能基准

- 单次技能执行 < 1ms（桌面端）
- 脚本预加载在启动时完成，不影响战斗中帧率
- AI 回合连续调用多个技能时总 Lua 开销 < 16ms

## R10: 技能迁移

将现有 63 个 C# handler 中的大部分迁移到 Lua 实现：
- 优先迁移模式化技能（单体攻击、AOE、治疗、增益）
- 保留复杂/性能敏感技能在 C#（如需要深度访问 Grid 状态的）
- 迁移后删除对应 C# handler 注册条目
