# Lua 技能脚本化 — Notes

## MoonSharp 关键 API 备忘

```csharp
// 创建沙箱 VM
var script = new Script(CoreModules.Preset_SoftSandbox);

// 注册 C# 类型
UserData.RegisterType<LuaUnitProxy>();

// 注册全局函数
script.Globals["combat"] = new Table(script);
script.Globals.Get("combat").Table["roll_dice"] = (Func<int, int, int>)RPGRuleEngine.RollDice;

// 加载并缓存脚本
DynValue chunk = script.LoadFile(path);  // 不执行，只编译
script.Call(chunk);                       // 执行顶层代码（定义 function）

// 调用 Lua 函数
DynValue executeFunc = script.Globals.Get("execute");
script.Call(executeFunc, ctxTable);
```

## Godot FileAccess → MoonSharp 加载

MoonSharp 的 `LoadFile` 默认用 System.IO，但 Godot 的 `res://` 路径不是真实文件系统路径。
需要自定义加载：

```csharp
// 方案：用 Godot FileAccess 读取文本，再用 LoadString
string luaSource = ReadGodotFile("res://scripts/skills/double_attack.lua");
DynValue chunk = script.LoadString(luaSource, null, "double_attack");
```

## CombatResolver.ResolveAttack 参数映射

C# 签名（简化）：
```csharp
static Dictionary ResolveAttack(
    Unit attacker, Unit target, HexGrid? grid,
    bool hasAdvantage = false, bool hasDisadvantage = false,
    int hitModifier = 0, float damageMultiplier = 1.0f,
    string? overrideDamageType = null, float nodeFlatDmgScale = 1.0f)
```

Lua 调用约定：
```lua
-- 基础调用
local r = combat.resolve_attack(attacker, target)

-- 带选项
local r = combat.resolve_attack(attacker, target, {
    advantage = true,
    hit_mod = -3,
    damage_mult = 2.0,
    node_flat_scale = 0.5,
})
```

## 已知的 MoonSharp 限制

1. **不支持 Lua 5.3+ 整数除法 `//`** — 用 `math.floor(a/b)`
2. **不支持 goto** — 用 if/else 替代
3. **协程可用但不推荐** — 技能执行应该是同步的
4. **Table 遍历顺序** — 数组部分有序，hash 部分无序（与标准 Lua 一致）

## 性能注意事项

- `script.Call()` 每次调用约 0.01-0.05ms（空函数）
- 复杂技能（ChainLightning 级别）约 0.1-0.3ms
- 避免在 Lua 中做大量字符串拼接（GC 压力）
- Unit 列表传递用 proxy array，不要每次都重建

## 迁移检查清单（每个技能）

1. [ ] 翻译 C# 逻辑到 Lua
2. [ ] 确认所有 API 调用都有对应绑定
3. [ ] 处理边界情况（target 为 nil、grid 为 nil）
4. [ ] 从 C# 注册表注释掉对应条目
5. [ ] 快速战斗中实际使用该技能
6. [ ] 确认 AI 也能正常使用
7. [ ] 删除 C# 注册表条目（Sprint 2 末尾统一删）
