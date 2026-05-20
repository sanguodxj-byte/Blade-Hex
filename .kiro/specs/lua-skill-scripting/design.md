# Lua 技能脚本化 — Design

## 目标

将技能效果执行逻辑从 C# 硬编码迁移到 Lua 脚本，实现：
1. 热重载：修改 Lua 文件后无需重新编译即可生效
2. 数据驱动：技能逻辑与 C# 引擎解耦，策划可独立编辑
3. Mod 支持：玩家可通过 Lua 脚本添加自定义技能

## 技术选型

### NLua 1.7.6（选定）

- 原生 Lua 5.4 绑定（通过 KeraLua）
- NuGet 包：`NLua` 1.7.6，目标 **net8.0**
- 性能优秀：原生 Lua VM，比纯 C# 解释器快一个数量级
- 跨平台：KeraLua 自带 win-x64/linux-x64/osx-arm64/android-arm64 原生库
- 原生库体积小：lua54.so 约 300-500KB/架构
- C# 对象直接暴露给 Lua（无需手动绑定）
- 引入位置：**BladeHexCore**（纯逻辑层）

### 为什么不选其他方案

| 方案 | 否决原因 |
|------|----------|
| MoonSharp | 停更（2022），只有 netstandard1.6/2.1，Godot .NET SDK 兼容性差 |
| NeoLua | 用 DLR 编译，API 风格偏 .NET 不像 Lua |
| GDScript Expression | 只能做简单表达式，无法表达分支/循环逻辑 |
| Roslyn Scripting | 运行时编译 C#，启动慢、内存大、沙箱困难 |

---

## 架构设计

### 层次关系

```
┌─────────────────────────────────────────────────────────┐
│  BladeHexFrontend (Godot Node 层)                       │
│  ┌───────────────────────────────────────────────────┐  │
│  │ SkillEffectExecutor (分发器，保持不变)              │  │
│  │   ├─ C# Handler (保留少量性能关键技能)             │  │
│  │   └─ LuaSkillBridge.Execute(skillId, ctx)  ←NEW   │  │
│  └───────────────────────────────────────────────────┘  │
│                          │                               │
│                          ▼                               │
│  ┌───────────────────────────────────────────────────┐  │
│  │ LuaSkillBridge (Frontend, 负责 Godot↔Lua 转换)    │  │
│  │   - 将 SkillHandlerContext → LuaTable             │  │
│  │   - 将 Lua 返回值 → Godot.Collections.Dictionary  │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│  BladeHexCore (纯逻辑层)                                │
│  ┌───────────────────────────────────────────────────┐  │
│  │ LuaScriptEngine (VM 生命周期管理)                  │  │
│  │   - Script 缓存池 (skillId → DynValue)            │  │
│  │   - 沙箱配置 (禁 IO/OS/require)                   │  │
│  │   - 错误处理 + 日志                               │  │
│  │   - 热重载 (Reload / ReloadAll)                   │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │ LuaCombatAPI (注册到 Lua 全局表的 C# 函数)         │  │
│  │   - combat.resolve_attack(...)                    │  │
│  │   - combat.roll_dice(count, sides)                │  │
│  │   - combat.get_stat_mod(stat_value)               │  │
│  │   - hex.get_neighbors(q, r)                       │  │
│  │   - hex.distance(q1, r1, q2, r2)                  │  │
│  │   - unit.find_at(q, r, "enemies"|"allies")        │  │
│  │   - unit.heal(target, amount)                     │  │
│  │   - unit.take_damage(target, amount)              │  │
│  │   - result.add_attack(attack_result)              │  │
│  │   - result.add_effect(target, effect_id, ...)     │  │
│  │   - result.fail(reason)                           │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│  res://scripts/skills/  (Lua 脚本目录)                   │
│    double_attack.lua                                     │
│    whirlwind.lua                                         │
│    chain_lightning.lua                                    │
│    ...                                                   │
└─────────────────────────────────────────────────────────┘
```

### 核心类职责

| 类 | 项目 | 职责 |
|----|------|------|
| `LuaScriptEngine` | Core | VM 池管理、脚本加载/缓存/热重载、沙箱配置 |
| `LuaCombatAPI` | Core | 战斗相关 C# 函数注册到 Lua 全局表 |
| `LuaSkillBridge` | Frontend | SkillHandlerContext ↔ Lua 数据转换、调用入口 |
| `LuaUnitProxy` | Frontend | Unit (Node3D) 的轻量 Lua 代理，只暴露数据字段 |

### Lua 脚本约定

每个技能一个 `.lua` 文件，必须定义 `execute(ctx)` 函数：

```lua
-- scripts/skills/double_attack.lua

function execute(ctx)
    local target = unit.find_at(ctx.target_q, ctx.target_r, "enemies")
    if not target then
        result.fail("目标格没有敌人")
        return
    end

    local r1 = combat.resolve_attack(ctx.attacker, target)
    result.add_attack(r1)

    local r2 = combat.resolve_attack(ctx.attacker, target, { hit_mod = -3 })
    result.add_attack(r2)
end
```

### ctx 表结构

```lua
ctx = {
    attacker = <LuaUnitProxy>,   -- 施放者代理
    target_q = int,              -- 目标格 Q 坐标
    target_r = int,              -- 目标格 R 坐标
    grid = <GridProxy>,          -- 网格代理（可能为 nil）
    enemies = <UnitListProxy>,   -- 敌方列表代理
    allies = <UnitListProxy>,    -- 友方列表代理
}
```

### LuaUnitProxy 暴露的字段/方法

```lua
unit_proxy.hp           -- 当前 HP (读写)
unit_proxy.max_hp       -- 最大 HP (只读)
unit_proxy.mana         -- 当前 Mana (读写)
unit_proxy.q            -- 格子 Q 坐标
unit_proxy.r            -- 格子 R 坐标
unit_proxy.level        -- 等级
unit_proxy.str          -- 力量
unit_proxy.dex          -- 敏捷
unit_proxy.con          -- 体质
unit_proxy.int          -- 智力
unit_proxy.wis          -- 感知
unit_proxy.cha          -- 魅力
unit_proxy.is_enemy     -- 是否敌方
unit_proxy.facing       -- 朝向
unit_proxy.has_effect(effect_id)  -- 是否有某状态
unit_proxy.has_skill(skill_id)    -- 是否有某被动技能
unit_proxy.runtime      -- 运行时数据子表
```

---

## 关键设计决策

### D1: 混合模式 — C# 与 Lua 共存

不强制所有技能迁移到 Lua。`SkillEffectExecutor` 的注册表分发逻辑保持不变：
1. 先查 C# Handler 注册表
2. 未找到 → 尝试加载 `scripts/skills/{skillId}.lua`
3. 都没有 → 返回"技能未注册"错误

这允许渐进迁移，性能关键的技能（如 AI 高频评估的）可保留 C#。

### D2: 沙箱安全

MoonSharp 沙箱配置：
- 禁用：`io`, `os`, `file`, `debug`, `load`, `loadfile`, `dofile`, `require`
- 允许：`math`, `string`, `table`, `pairs`, `ipairs`, `type`, `tostring`, `tonumber`, `print`
- `print` 重定向到 `GD.Print("[Lua] ...")`

### D3: 性能策略

- Script 实例缓存：每个 skillId 只 parse 一次，缓存 `DynValue` (closure)
- 单 VM 实例：所有技能共享一个 `Script` 对象（MoonSharp 线程安全由调用方保证，回合制无并发）
- 预热：游戏启动时扫描 `scripts/skills/` 目录，预加载所有 `.lua` 文件
- 基准目标：单次技能执行 < 1ms（回合制无帧压力）

### D4: 热重载机制

- 开发模式（`#if DEBUG`）：DebugConsole 命令 `lua_reload [skillId]`
- 文件监视：不做自动监视（避免复杂度），手动触发即可
- 重载粒度：单文件重载 or 全量重载

### D5: 错误处理

- Lua 运行时错误 → `GD.PushError` + 技能执行返回 `{ success = false, reason = "Lua error: ..." }`
- 不崩溃、不中断战斗流程
- 错误信息包含：脚本文件名、行号、错误消息

### D6: Godot Dictionary 转换策略

**问题：** MoonSharp 不认识 `Godot.Collections.Dictionary`/`Array`。

**方案：** `LuaSkillBridge` 负责双向转换：
- C# → Lua：`SkillHandlerContext` 拆解为 Lua table（不传 Godot 对象）
- Lua → C#：Lua 不直接构造 Godot Dictionary；通过 `result.*` API 函数由 C# 侧构造

这样 Lua 脚本完全不接触 Godot 类型，保持 Core 层纯净。

---

## 目录结构

```
Blade&Hex/
├── BladeHexCore/
│   └── src/
│       └── Scripting/           ← NEW
│           ├── LuaScriptEngine.cs
│           ├── LuaCombatAPI.cs
│           └── LuaUnitProxy.cs
│
├── BladeHexFrontend/
│   └── src/
│       └── View/
│           └── Combat/
│               └── LuaSkillBridge.cs  ← NEW
│
└── scripts/                     ← NEW (Godot res:// 目录)
    └── skills/
        ├── _lib.lua             (公共工具函数)
        ├── double_attack.lua
        ├── whirlwind.lua
        ├── ...
        └── README.md            (Lua API 文档)
```

---

## 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| MoonSharp 与 Godot .NET SDK 版本冲突 | 编译失败 | Sprint 0 先做 PoC 验证 |
| Android 上 MoonSharp 性能不足 | 技能执行卡顿 | 基准测试；关键技能保留 C# |
| Lua 脚本错误导致战斗状态不一致 | 游戏逻辑错误 | result API 做防御性校验 |
| 开发者不熟悉 Lua | 开发效率下降 | 提供模板 + 详细 API 文档 |
