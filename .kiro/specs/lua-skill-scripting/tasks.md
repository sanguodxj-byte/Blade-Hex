# Lua 技能脚本化 — Tasks

## 使用说明

- 任务按 Sprint 组织，Sprint 0 为 PoC 验证，Sprint 1~3 为正式实施
- 复选框前缀：`[ ]` 待办、`[~]` 进行中、`[x]` 完成
- 每个任务挂关联的需求 ID（R1~R10）
- 每个 Sprint 结束需做 **战斗流程手测**：快速战斗 → 使用迁移后的技能 → 验证效果正确

---

## Sprint 0 — PoC 验证（1 天）

**目标：** 验证 MoonSharp 能在 Godot .NET 项目中正常工作，确认无 SDK 冲突。

### 0.1 NuGet 引入 [R1]

- [x] 0.1.1 在 `BladeHexCore.csproj` 添加 `<PackageReference Include="MoonSharp" Version="2.0.0" />`
- [x] 0.1.2 `dotnet build BladeHex.sln` 编译通过
- [ ] 0.1.3 在 Godot 编辑器中运行项目，确认无加载错误

### 0.2 最小执行验证 [R1]

- [x] 0.2.1 创建 `BladeHexCore/src/Scripting/LuaScriptEngine.cs` — 最小实现：创建 Script 实例、执行 `return 1+1`、返回结果
- [x] 0.2.2 在 DebugConsole 添加临时命令 `lua_test`，调用 LuaScriptEngine 执行简单脚本并打印结果
- [ ] 0.2.3 Godot 编辑器内运行，DebugConsole 输入 `lua_test`，确认输出 `2`
- [ ] 0.2.4 Android 导出测试（如有设备）或确认 MoonSharp 无原生依赖

### 0.3 性能基准 [R9]

- [ ] 0.3.1 编写基准脚本：循环调用 1000 次简单 Lua 函数，计时
- [ ] 0.3.2 确认单次调用 < 0.1ms（远低于 1ms 目标）
- [ ] 0.3.3 删除临时测试代码（保留 LuaScriptEngine 骨架）

---

## Sprint 1 — 核心引擎 + API 绑定（3-4 天）

**目标：** 完成 Lua 引擎基础设施和战斗 API 绑定，能执行第一个 Lua 技能。

### 1.1 LuaScriptEngine 完整实现 [R1, R4, R8]

- [x] 1.1.1 沙箱配置：设置 `CoreModules` 白名单（math/string/table/基础函数）
- [x] 1.1.2 脚本加载：从 `res://scripts/skills/` 读取 `.lua` 文件（通过 Godot FileAccess）
- [x] 1.1.3 缓存机制：`Dictionary<string, DynValue>` 存储已编译的 closure
- [x] 1.1.4 公共库预加载：加载 `_lib.lua` 到全局环境
- [x] 1.1.5 print 重定向：`script.Options.DebugPrint = msg => GD.Print("[Lua] " + msg)`
- [x] 1.1.6 错误处理：try-catch `ScriptRuntimeException` / `SyntaxErrorException`，格式化错误信息

### 1.2 LuaUnitProxy [R3]

- [x] 1.2.1 创建 `BladeHexCore/src/Scripting/LuaUnitProxy.cs`
- [x] 1.2.2 实现 `[MoonSharpUserData]` 标注的代理类，字段映射：hp, max_hp, mana, q, r, level, str/dex/con/int/wis/cha, is_enemy, facing
- [x] 1.2.3 实现方法：`has_effect(string)`, `has_skill(string)`
- [x] 1.2.4 实现 runtime 子表：extra_actions, life_circle_used, life_shield_used, heroic_call_used
- [x] 1.2.5 实现写入拦截：hp/mana 写入时同步回 Unit 实例

### 1.3 LuaCombatAPI [R2]

- [x] 1.3.1 创建 `BladeHexCore/src/Scripting/LuaCombatAPI.cs`
- [x] 1.3.2 注册 `combat` 全局表：
  - `combat.resolve_attack(attacker, target, opts?)` → 调用 CombatResolver，返回 Lua table
  - `combat.roll_dice(count, sides)` → RPGRuleEngine.RollDice
  - `combat.get_stat_mod(stat_value)` → RPGRuleEngine.GetStatModifier
  - `combat.get_proficiency(level)` → RPGRuleEngine.GetProficiencyBonus
  - `combat.make_save(stat, prof, has_advantage, dc)` → RPGRuleEngine.MakeSave
- [x] 1.3.3 注册 `hex` 全局表：
  - `hex.neighbors(q, r)` → HexUtils.GetNeighbors，返回 Lua array of {q,r}
  - `hex.distance(q1, r1, q2, r2)` → HexUtils.Distance
  - `hex.get_neighbor(q, r, direction)` → HexUtils.GetNeighbor
- [x] 1.3.4 注册 `unit` 全局表：
  - `unit.find_at(q, r, side)` → 从当前 enemies/allies 列表查找
  - `unit.heal(proxy, amount)` → 调用 Unit.Heal，返回实际治疗量
  - `unit.take_damage(proxy, amount)` → 调用 Unit.TakeDamage
  - `unit.change_morale(proxy, amount)` → MoraleSystem.ChangeMorale
- [x] 1.3.5 注册 `result` 全局表：
  - `result.add_attack(attack_table)` → 转换并追加到 Result["results"]
  - `result.add_damage(target, value, damage_type?)` → 构造伤害条目
  - `result.add_heal(target, value)` → 构造治疗条目
  - `result.add_effect(target, effect_id, duration, modifiers?)` → 追加到 Result["status_effects"]
  - `result.add_remove_effect(target, effect_ids)` → 构造移除效果条目
  - `result.fail(reason)` → 设置 success=false

### 1.4 LuaSkillBridge [R5]

- [x] 1.4.1 创建 `BladeHexFrontend/src/View/Combat/LuaSkillBridge.cs`
- [x] 1.4.2 实现 `Execute(string skillId, SkillHandlerContext ctx)` 方法：
  - 构造 ctx Lua table（attacker proxy, target_q, target_r, enemies/allies proxy 列表）
  - 调用 LuaScriptEngine 执行对应脚本的 `execute(ctx)`
  - 返回 bool 表示是否成功执行
- [x] 1.4.3 实现 Godot Dictionary ↔ Lua table 转换工具方法
- [x] 1.4.4 在 SkillEffectExecutor 中添加 Lua fallback 路径：Handler 未注册时调用 LuaSkillBridge

### 1.5 第一个 Lua 技能验证 [R5, R10]

- [x] 1.5.1 创建 `scripts/skills/` 目录
- [x] 1.5.2 编写 `scripts/skills/double_attack.lua`（从 C# DoubleAttack 翻译）
- [ ] 1.5.3 从 SkillEffectExecutor 注册表中**注释掉** `double_attack` 的 C# 条目
- [ ] 1.5.4 快速战斗中使用连击技能，验证效果与 C# 版本一致
- [ ] 1.5.5 恢复 C# 条目（Sprint 1 不正式迁移，只验证通路）

### 1.6 Sprint 1 收尾

- [ ] 1.6.1 战斗流程手测：快速战斗 → 使用 Lua 版连击 → 验证伤害/命中正确
- [ ] 1.6.2 错误路径测试：故意写错 Lua 语法，确认不崩溃、日志正确
- [ ] 1.6.3 提交 + 打 tag `lua-scripting-sprint-1`

---

## Sprint 2 — 批量迁移 + 热重载（5-6 天）

**目标：** 将大部分技能迁移到 Lua，实现热重载，建立 Lua 技能开发工作流。

### 2.1 公共库 _lib.lua [R4]

- [ ] 2.1.1 创建 `scripts/skills/_lib.lua`，提取公共模式：
  - `require_target(q, r, side)` — 查找目标，失败自动 fail
  - `aoe_neighbors(q, r, side, callback)` — 遍历邻格执行回调
  - `aoe_area(center_q, center_r, side, callback)` — 中心+邻格 AOE
  - `check_mana(attacker, cost)` — 检查并扣除魔力

### 2.2 近战技能迁移 [R10]

- [ ] 2.2.1 `whirlwind.lua` — 旋风斩
- [ ] 2.2.2 `battle_cry.lua` — 战斗怒吼
- [ ] 2.2.3 `blood_vortex.lua` — 血腥漩涡
- [ ] 2.2.4 `bloodthirst.lua` — 嗜血
- [ ] 2.2.5 `sword_dance.lua` — 剑舞
- [ ] 2.2.6 `shield_bash.lua` — 盾击
- [ ] 2.2.7 从 C# 注册表移除对应条目，验证全部走 Lua

### 2.3 远程技能迁移 [R10]

- [ ] 2.3.1 `aimed_shot.lua` — 精准射击
- [ ] 2.3.2 `double_shot.lua` — 连射
- [ ] 2.3.3 `scatter_shot.lua` — 散射
- [ ] 2.3.4 `multi_shot.lua` — 连珠箭
- [ ] 2.3.5 `blind_arrow.lua` — 致盲箭
- [ ] 2.3.6 `trick_arrow.lua` — 诡计箭
- [ ] 2.3.7 `meteor_shower.lua` — 流星雨

### 2.4 潜行技能迁移 [R10]

- [ ] 2.4.1 `stealth.lua` — 潜行
- [ ] 2.4.2 `shadow_clone.lua` — 影分身
- [ ] 2.4.3 `poison_blade.lua` — 淬毒
- [ ] 2.4.4 `shadow_strike.lua` — 暗影突袭
- [ ] 2.4.5 `trap_master.lua` — 陷阱大师

### 2.5 刺客技能迁移 [R10]

- [ ] 2.5.1 `mana_surge.lua` — 魔力涌动
- [ ] 2.5.2 `head_shot.lua` — 爆头
- [ ] 2.5.3 `assassinate.lua` — 暗杀

### 2.6 法术技能迁移 [R10]

- [ ] 2.6.1 `mana_shield.lua` — 法力护盾
- [ ] 2.6.2 `time_warp.lua` — 时间扭曲
- [ ] 2.6.3 `arcane_burst.lua` — 奥术爆发
- [ ] 2.6.4 `mana_drain.lua` — 法力吸取
- [ ] 2.6.5 `chain_lightning.lua` — 连锁闪电（复杂逻辑，重点测试）
- [ ] 2.6.6 `arcane_bomb.lua` — 奥术炸弹
- [ ] 2.6.7 `void_gate.lua` — 虚空之门

### 2.7 治疗/辅助技能迁移 [R10]

- [ ] 2.7.1 `basic_heal.lua` — 基础治疗
- [ ] 2.7.2 `field_medic.lua` — 战地医疗
- [ ] 2.7.3 `group_heal.lua` — 群体治疗
- [ ] 2.7.4 `life_circle.lua` — 生命之环
- [ ] 2.7.5 `blessing.lua` — 祝福
- [ ] 2.7.6 `unyielding_bulwark.lua` — 不屈壁垒
- [ ] 2.7.7 `life_shield.lua` — 生命之盾
- [ ] 2.7.8 `guardian_spirit.lua` — 守护之灵

### 2.8 领导/CHA 技能迁移 [R10]

- [ ] 2.8.1 `war_cry.lua` — 战吼
- [ ] 2.8.2 `inspire.lua` — 鼓舞
- [ ] 2.8.3 `taunt.lua` — 嘲讽
- [ ] 2.8.4 `command.lua` — 指挥
- [ ] 2.8.5 `rally.lua` — 集结
- [ ] 2.8.6 `shadow_deal.lua` — 暗影交易
- [ ] 2.8.7 `intimidate.lua` — 威吓
- [ ] 2.8.8 `heroic_call.lua` — 英雄号召

### 2.9 奥术攻击技能迁移 [R10]

- [ ] 2.9.1 `purifying_flame.lua` — 净化之焰
- [ ] 2.9.2 `arcane_judgment.lua` — 奥术审判
- [ ] 2.9.3 `oracle.lua` — 神谕
- [ ] 2.9.4 `elemental_storm.lua` — 元素风暴

### 2.10 热重载实现 [R6]

- [ ] 2.10.1 `LuaScriptEngine.Reload(string skillId)` — 清除单个缓存并重新加载
- [ ] 2.10.2 `LuaScriptEngine.ReloadAll()` — 清除全部缓存
- [ ] 2.10.3 DebugConsole 注册命令 `lua_reload <id>` 和 `lua_reload_all`
- [ ] 2.10.4 验证：修改 Lua 文件 → `lua_reload` → 下次使用技能生效

### 2.11 Sprint 2 收尾

- [ ] 2.11.1 全量战斗手测：每个迁移的技能至少使用一次
- [ ] 2.11.2 AI 回合测试：确认 AI 使用 Lua 技能无异常
- [ ] 2.11.3 性能验证：AI 回合（6+ 敌人）总 Lua 开销 < 16ms
- [ ] 2.11.4 删除已迁移技能的 C# Handler 代码（MeleeSkillHandlers 等文件瘦身）
- [ ] 2.11.5 提交 + 打 tag `lua-scripting-sprint-2`

---

## Sprint 3 — 清理 + 文档 + Mod 基础（2 天）

**目标：** 清理残留代码，编写 Lua API 文档，为 Mod 支持打基础。

### 3.1 代码清理 [R10]

- [ ] 3.1.1 删除空的 C# Handler 文件（如果所有方法都已迁移）
- [ ] 3.1.2 SkillEffectExecutor 注册表只保留未迁移的条目
- [ ] 3.1.3 确认 `AssassinSkillHandlers.cs` 等文件无残留引用

### 3.2 API 文档 [R4]

- [ ] 3.2.1 创建 `scripts/skills/README.md` — Lua 技能开发指南
  - ctx 表结构说明
  - 全部 API 函数签名 + 示例
  - 常见模式（单体攻击、AOE、治疗、增益、控制）
  - 错误处理约定
- [ ] 3.2.2 创建 `scripts/skills/_template.lua` — 新技能模板文件

### 3.3 Mod 目录支持 [R4]

- [ ] 3.3.1 LuaScriptEngine 支持额外扫描 `user://mods/skills/` 目录
- [ ] 3.3.2 Mod 脚本优先级低于内置脚本（同名不覆盖，除非显式配置）
- [ ] 3.3.3 Mod 加载日志：`[Lua] Loaded mod skill: xxx from user://mods/skills/xxx.lua`

### 3.4 Sprint 3 收尾

- [ ] 3.4.1 完整流程手测：主菜单 → 新游戏 → 大地图 → 遭遇战斗 → 使用各类技能
- [ ] 3.4.2 快速战斗全难度测试
- [ ] 3.4.3 提交 + 打 tag `lua-scripting-v1.0`
