# 战斗架构优化 — Design

## 设计总则

- **接口优先**：先定义抽象，再迁移实现，最后切换调用点。
- **零行为变更**：每个改动有可观察的等价性证据（测试、手动验证、日志对比）。
- **可逆性**：每个阶段可独立 revert，不留残骸。
- **强类型 > 字符串约定**：能用编译期检查的不用运行期检查。
- **不创新**：使用 .NET 标准库与既有 Godot 能力，不引入新框架。

## 现状盘点

### 大文件（> 500 行，Combate 子系统）

| 文件 | 行数 | 核心问题 |
|------|------|----------|
| `Scenes/combat/CombatSceneBase.cs` | 2303 | 上帝类：环境/相机/小地图/输入/高亮/部署/行动/AI/结算全在一个 partial |
| `View/Combat/CareerSkillExecutor.cs` | 875 | 31 个 case 的巨型 switch，与 SkillEffectExecutor 平行 |
| `View/Combat/LuaSkillBridge.cs` | 631 | Lua ↔ C# 桥，直接操作 SkillTree 字段 |
| `View/Combat/PassiveSkillResolver.cs` | 607 | static 方法直查 SkillTree，调用点散落 |
| `View/Combat/AI/AIController.cs` | 558 | 规划+执行+表现三位一体 |
| `View/Combat/CombatResolver.cs` | 518 | 9 个修正项手工累加，与规则引擎职责重叠 |
| `Core/src/Combat/Headless/HeadlessCombatLoop.cs` | 1202 | 明确放弃 LOS/侧翼/士气/法术/状态 |

### 依赖关系

```
[Core — 纯逻辑]
  CombatRuleEngine         ← 纯静态，命中/伤害/暴击穿甲公式
  CombatStats              ← 派生属性（AP/AC/暴击阈值/STR 加成）
  BattleUnitModel          ← 单位运行时模型（HP/DR/装备解析）
  DamagePenetrationTable   ← 穿甲分流表
  CombatStateMachine       ← Init→Deploy→Player↔Enemy→End
  InitiativeQueue          ← 先攻队列
  Buff/*                   ← BuffSystem / DamageCalcPipeline / Hooks
  Abilities/*              ← 装备能力（lifesteal/thorns/...）
  Headless/*               ← HeadlessCombatLoop（无 Godot 模拟器）
  LosCore                  ← 路径命中惩罚（无 Node）
  ProjectileSystem         ← 投射物逻辑（无 View）
  IBattleField/IFightable  ← 解耦接口

[Frontend — View / Scene]
  Scenes.combat
    CombatSceneBase.cs     ← 2303 行，95% 战斗交互
    CombatScene.cs         ← 362 行（初始化数据 + 战利品）
    QuickCombatScene.cs    ← 随机生成 + 快速战斗结束
  View.Combat
    CombatManager.cs       ← Facade，协调 Registry/Turns/Result/Buff
    CombatResolver.cs      ← View 适配 + 调 Core 规则
    AI/AIController.cs     ← 决策 + 动画 + 寻路
    CareerSkillExecutor.cs ← 职业技能执行
    PassiveSkillResolver.cs← 被动技能查询
    LuaSkillBridge.cs      ← Lua 技能桥
    ...（渲染辅助一堆）
```

### 已做好的基础

- `CombatRuleEngine` 纯静态，Frontend ↔ Headless 共享（`DamageResolutionParityTest` 守住了等价性）。
- `CombatManager` 是 Facade，对外暴露清晰 API。
- `TurnManager` 已切先攻制，`InitiativeQueue` 在 Core 层独立。
- `ICombatSceneAdapter` / `ITickScheduler` 把 AI/Projectile 与 SceneTree 解耦。
- 命令系统（`AttackCommand` / `MoveCommand` / `UseSkillCommand` / `CommandHistory`）骨架已在。

---

## C1 — CombatSceneBase 组件化 Design

### 拆分目标

```
[Node3D] CombatSceneBase
  ├─ [Node] CombatCameraController        ← 相机、AABB、UI insets
  ├─ [Node] CombatInputController         ← OnCellClicked / 长按 / 部署点击
  ├─ [Node] CombatHighlightController       ← 移动/攻击范围 / hover / 叠加层
  ├─ [Node] CombatDeploymentController      ← 部署阶段、确认按钮
  ├─ [Node] CombatActionDispatcher          ← OnActionSelected 的 switch → 命令分派
  ├─ [Node] CombatResultPresenter           ← 结算面板、BGM 切换
  └─ [Node] CombatMinimapController         ← 小地图渲染（可选）
```

### 主类职责

```csharp
public partial class CombatSceneBase : Node3D, ICombatSceneAdapter
{
    [Export] public CombatCameraController CameraCtrl = null!;
    [Export] public CombatInputController InputCtrl = null!;
    [Export] public CombatHighlightController HighlightCtrl = null!;
    [Export] public CombatDeploymentController DeployCtrl = null!;
    [Export] public CombatActionDispatcher ActionDispatcher = null!;
    [Export] public CombatResultPresenter ResultPresenter = null!;

    public override void _Ready()
    {
        // 编排顺序，不做具体逻辑
        CameraCtrl.Initialize(this);
        InputCtrl.Initialize(this, HighlightCtrl);
        ActionDispatcher.Initialize(this, _combatManager);
        ResultPresenter.Initialize(this, _combatManager);
        // ...
    }
}
```

### 组件间通信

- **首选**：`EventBus` 强类型事件
- **次选**：组件持有需要的 sibling 引用（编译期可见）
- **禁止**：跨组件直接访问私有字段

---

## C2 — 死亡 SSOT Design

### 统一入口

```csharp
// CombatManager.cs — 唯一清理入口
public void HandleUnitKilled(Unit unit)
{
    if (unit == null || unit.CurrentHp > 0) return;
    if (_deadUnits.Contains(unit)) return; // 幂等

    // 1. 标记
    _deadUnits.Add(unit);

    // 2. 维护先攻队列
    Turns.RemoveFromQueue(unit);

    // 3. 移除格子占用
    var cell = _hexGrid.GetCell(unit.GridPos);
    if (cell?.Occupant == unit) cell.Occupant = null;

    // 4. 刷 UI
    EventBus.Publish(new UnitDiedEvent(unit, unit.Data?.IsEnemy == false));

    // 5. 检查战斗结束
    CheckCombatEnd();
}
```

### 订阅者模式

```csharp
// CombatResultPresenter（原 CombatSceneBase）
EventBus.Subscribe<UnitDiedEvent>(evt =>
{
    _combatUi.RemoveUnit(evt.Unit);
    _floatingNumbers.Show(evt.Unit.GlobalPosition, "击杀！", Color.Red);
    PlayDeathSfx(evt.Unit);
});
```

---

## C3 — 技能结果类型化 Design

### 新类型定义

```csharp
namespace BladeHex.Combat.Skills;

public sealed record SkillExecutionResult(
    bool Success,
    string? FailureReason,
    IReadOnlyList<SkillSubResult> SubResults);

public abstract record SkillSubResult;

public sealed record DamageEvent(Unit Target, int Damage, bool WasKillingBlow) : SkillSubResult;
public sealed record TeleportEvent(Unit Unit, Vector2I Destination, HexCell? PreviousCell) : SkillSubResult;
public sealed record StatusEffectApplication(string EffectId, Unit Target, int Duration) : SkillSubResult;
public sealed record ResultText(string Text, Color? Color = null) : SkillSubResult;
public sealed record HealEvent(Unit Target, int Amount) : SkillSubResult;
public sealed record BuffApplication(string BuffId, Unit Target) : SkillSubResult;
```

### 兼容适配

```csharp
// 旧 API → 新 API 的薄 wrapper（标记 [Obsolete]）
[Obsolete("Use the typed SkillExecutionResult overload", error: false)]
public Godot.Collections.Dictionary UseSkillLegacy(Unit caster, string skillId, Vector2I target)
    => UseSkill(caster, skillId, target).ToDictionary();

// 扩展方法
public static Godot.Collections.Dictionary ToDictionary(this SkillExecutionResult result)
{
    var dict = new Godot.Collections.Dictionary
    {
        ["success"] = result.Success,
        ["failure_reason"] = result.FailureReason ?? "",
        ["sub_results"] = new Godot.Collections.Array(result.SubResults.Select(r => r.ToGodotDict()))
    };
    return dict;
}
```

---

## C4 — AI 三段式分离 Design

### 目标架构

```
AIPlanner (纯函数，Core 层或 Frontend 不依赖 Godot)
  ↓ 输出 AIAction (Move/Attack/UseSkill/Wait)
AICommandTranslator
  ↓ 输出 ICommand，入 CommandHistory
AIPresenter (订阅 EventBus，处理动画/日志/镜头)
```

### AIAction 类型

```csharp
public abstract record AIAction;
public sealed record MoveAction(List<Vector2I> Path) : AIAction;
public sealed record AttackAction(Unit Target) : AIAction;
public sealed record UseSkillAction(string SkillId, Vector2I TargetCell) : AIAction;
public sealed record WaitAction : AIAction;
```

### AIPlanner 接口

```csharp
public interface IAIPlanner
{
    AIAction DecideActionForUnit(Unit unit, CombatStateSnapshot state);
}

// 纯函数，无 Godot 依赖
public sealed class CombatStateSnapshot
{
    public IReadOnlyList<UnitSnapshot> AllUnits { get; init; } = Array.Empty<UnitSnapshot>();
    public IReadOnlyDictionary<Vector2I, CellSnapshot> Grid { get; init; } = new Dictionary<Vector2I, CellSnapshot>();
    public int RoundNumber { get; init; }
}
```

### AICommandTranslator

```csharp
public sealed class AICommandTranslator
{
    private readonly CommandHistory _history;

    public ICommand Translate(AIAction action, Unit unit, CombatManager manager)
    {
        return action switch
        {
            MoveAction m => new MoveCommand(unit, m.Path, manager),
            AttackAction a => new AttackCommand(unit, a.Target, manager),
            UseSkillAction s => new UseSkillCommand(unit, s.SkillId, s.TargetCell, manager),
            WaitAction _ => new WaitCommand(unit),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
```

---

## C5 — 修正项流水线 Design

### IAttackModifier 接口

```csharp
public interface IAttackModifier
{
    string Name { get; }
    void Apply(AttackContext ctx, ref AttackInput input);
}

public sealed class AttackContext
{
    public Unit Attacker { get; init; } = null!;
    public Unit Defender { get; init; } = null!;
    public HexGrid? Grid { get; init; }
    public bool IsCharge { get; init; }
    public bool IsAoo { get; init; }
    public Unit[]? AttackerAllies { get; init; }
}

public sealed class AttackInput
{
    public int AttackBonus { get; set; }
    public bool HasAdvantage { get; set; }
    public bool HasDisadvantage { get; set; }
    public List<string> AppliedModifiers { get; } = new();
    public float NodePassiveScale { get; set; } = 1.0f;
}
```

### CombatResolver 装配

```csharp
public static class CombatResolver
{
    private static readonly IReadOnlyList<IAttackModifier> DefaultModifiers = new List<IAttackModifier>
    {
        new HighGroundModifier(),
        new ChargeModifier(),
        new MoraleModifier(),
        new CoverModifier(),
        new FlankingModifier(),
        new HeightDifferenceModifier(),
        new RiverCrossingModifier(),
        new EncirclementModifier(),
        new NodeCritModifier()
    };

    public static AttackResult ResolveAttack(AttackContext ctx)
    {
        var input = new AttackInput { AttackBonus = ctx.Attacker.Model.GetAttackBonus() };

        foreach (var mod in DefaultModifiers)
            mod.Apply(ctx, ref input);

        return CombatRuleEngine.RollAttack(ctx.Attacker, ctx.Defender, input);
    }
}
```

---

## C6 — LOS/Facing/Morale Core 回收 Design

### 目标

| 现有 Frontend 类 | Core 迁移目标 | Frontend 保留 |
|------------------|---------------|---------------|
| `LineOfSight` | `LosCore` | thin adapter |
| `FacingSystem` | `FacingCore` | 可视化叠加层 |
| `MoraleSystem` | `MoraleCore` | UI 表现 |

### LosCore（已存在，扩展）

```csharp
// Core/src/Combat/LosCore.cs（已存在，无 Node 依赖）
public static class LosCore
{
    public static int GetPathPenalty(Vector2I start, Vector2I end, IReadOnlyDictionary<Vector2I, CellData> grid);
    public static bool HasLineOfSight(Vector2I start, Vector2I end, IReadOnlyDictionary<Vector2I, CellData> grid);
}
```

### FacingCore（新增）

```csharp
// Core/src/Combat/FacingCore.cs
public static class FacingCore
{
    public static FlankDirection GetFlankDirection(UnitSnapshot attacker, UnitSnapshot defender);
    public static bool IsFlanking(Vector2I attackerPos, Vector2I defenderPos, HexDirection facing);
    public static int GetFlankBonus(FlankDirection dir) => dir switch
    {
        FlankDirection.Rear => 4,
        FlankDirection.Flank => 2,
        _ => 0
    };
}
```

### MoraleCore（新增）

```csharp
// Core/src/Combat/MoraleCore.cs
public static class MoraleCore
{
    public static MoraleEffects CalculateEffects(UnitSnapshot unit, IReadOnlyList<UnitSnapshot> allies, IReadOnlyList<UnitSnapshot> enemies);
}
```

### Headless 可选开关

```csharp
// HeadlessCombatLoop.cs
var simulationConfig = new SimulationConfig
{
    EnableLineOfSight = true,
    EnableFlanking = true,
    EnableMorale = true,
    EnableSpells = false, // Phase 3
    EnableStatusEffects = false // Phase 3
};
```

---

## C7 — 技能执行器收编 Design

### ISkillEffectHandler 统一接口

```csharp
public interface ISkillEffectHandler
{
    string EffectId { get; }
    SkillExecutionResult Execute(SkillHandlerContext ctx);
}

public sealed class SkillHandlerRegistry
{
    private readonly Dictionary<string, ISkillEffectHandler> _handlers = new();

    public void Register(ISkillEffectHandler handler) => _handlers[handler.EffectId] = handler;

    public SkillExecutionResult Execute(string effectId, SkillHandlerContext ctx)
    {
        if (_handlers.TryGetValue(effectId, out var handler))
            return handler.Execute(ctx);
        throw new ArgumentException($"Unknown skill effect: {effectId}");
    }
}
```

### PassiveSkillResolver 折叠

```csharp
// Unit.cs 新增
public PassiveModifierSet GetPassiveModifiers()
    => PassiveSkillResolver.Query(this); // 内部聚合所有 static 查询
```

### Lua 桥调用路径

```csharp
// LuaSkillBridge.cs — 不再直接读 SkillTree
// 改为：
LuaCombatAPI.Register(lua, _skillRegistry); // 注册表只暴露已注册 handler
```

---

## C8 — 清理 Design

### 清理清单

| 项目 | 处理方案 | 文件 |
|------|----------|------|
| `UpdateFov` | 直接删除，无功能 | `CombatSceneBase.cs` |
| `CombatManager.ChangeState` | 确认无调用点后删除 | `CombatManager.cs` |
| `SpawnHardcodedPlayer/Enemies` | 改为调用 `CharacterGenerator` + `EquipmentGenerator` | `CombatScene.cs` |
| `CombatScene.GenerateLoot` | 抽进 `LootTable` | `CombatScene.cs`, `LootTable.cs` |
| `OnAiDone` / `OnAiSingleUnitDone` | 确认只留一个 | `AIController.cs` |

---

## 实施顺序与依赖

```
阶段 1
  C1 (场景拆分) ─┐
                 ├→ C2 (死亡 SSOT) ──→ C3 (技能类型化)
阶段 2
  C4 (AI 三段) ──┐
                 ├→ C5 (修正流水线) ──→ C6 (Core 回收)
阶段 3
  C7 (技能收编) ─┐
                 ├→ C8 (清理)
```

**实际推进顺序建议：**

1. **Sprint 1 (C1)** — CombatSceneBase 拆分（最重，先拆完减轻认知负担）
2. **Sprint 2 (C2)** — 死亡 SSOT（与 C1 有耦合，紧跟其后）
3. **Sprint 3 (C3)** — 技能结果类型化（C1/C2 完成后，拆到处都是 Dictionary 的路径）
4. **Sprint 4 (C4)** — AI 三段分离（可利用 C3 的类型化结果）
5. **Sprint 5 (C5 + C6)** — 修正流水线 + Core 回收 LOS（可并行）
6. **Sprint 6 (C7)** — 技能执行器收编
7. **Sprint 7 (C8)** — 清理（穿插在各 sprint 间的轻量任务）

每个 Sprint 结束做一次完整流程手测。

---

## 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| CombatSceneBase 拆分破坏 .tscn 场景文件引用 | 高 | 保留 `partial` 桥接字段直到场景文件更新完成 |
| 技能类型化导致 Lua 桥中断 | 中 | 保留 Dictionary 兼容路径到 Phase 3 |
| AI 三段分离导致 AI 行为变化 | 中 | 引入 AI 行为回归测试（已有 `AIBehaviorRegressionTests.cs`） |
| Core 回收 LOS/Facing 引入 Core → Map 循环依赖 | 低 | 确保新 Core 类只依赖 `Vector2I` 和 `HexCell` 数据，不依赖 Node |
| Career/Passive 收编漏掉某些技能效果 | 中 | 全局搜索 `EffectId` / `skill.` 字符串，建立映射表后再迁移 |
