# 架构优化 — Design

## 设计总则

- **接口优先**：先定义抽象，再迁移实现，最后切换调用点
- **零行为变更**：每个改动有可观察的等价性证据（测试、手动验证、日志对比）
- **可逆性**：每个阶段可独立 revert，不留残骸
- **强类型 > 字符串约定**：能用编译期检查的不用运行期检查
- **不创新**：使用 .NET 标准库与既有 Godot 能力，不引入新框架

## 现状盘点

### Autoload（`project.godot`）
```
GlobalState, SkillTreeManager, EventBus, AudioManager,
AudioEventReactor, UITheme, DebugConsole, GameMenuManager
```

### 非 Autoload 的 `static Instance`
```
QuestManager, VFXManager, HexCellMultiMeshBatcher, UITheme（也是 Autoload）
```

### 关键大文件
- `WorldCreator.cs` ~1900 行 / 30+ 方法
- `OriginSelect.cs` ~1500 行
- `OverworldScene3D.*.cs` ~9 个 partial 文件
- `Unit.cs` ~600 行（含视觉 + 逻辑）

---

## 全局架构调整后的目标形态

```
┌─────────────────────────────────────────────────┐
│  Autoload（真单例，跨场景）                       │
│  ┌──────────────────────────────────────────┐   │
│  │ GlobalState (Aggregate Root)             │   │
│  │  ├─ SaveContext                          │   │
│  │  ├─ WorldGenContext                      │   │
│  │  ├─ QuickCombatContext                   │   │
│  │  ├─ WeatherContext                       │   │
│  │  └─ PlayerOriginContext                  │   │
│  │                                          │   │
│  │ EventBus (强类型 + 弱类型兼容)            │   │
│  │ AudioManager / UITheme / DebugConsole    │   │
│  │ GameMenuManager / AudioEventReactor      │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
                       ▲
                       │
┌─────────────────────────────────────────────────┐
│  Scene Services（场景级）                         │
│  ┌──────────────────────────────────────────┐   │
│  │ OverworldScene3D                         │   │
│  │  ├─ DayNightController                   │   │
│  │  ├─ EntityRegistry                       │   │
│  │  ├─ FogController                        │   │
│  │  ├─ InteractionDispatcher                │   │
│  │  ├─ NavigationController                 │   │
│  │  ├─ POIController                        │   │
│  │  ├─ RoadRenderer                         │   │
│  │  ├─ WeatherController                    │   │
│  │  └─ WorldRendererBridge                  │   │
│  │                                          │   │
│  │ CombatScene                              │   │
│  │  ├─ CombatManager (existing)             │   │
│  │  └─ ...                                  │   │
│  │                                          │   │
│  │ QuestManager (Scene Service, 不再 static)│   │
│  │ SkillTreeManager (Scene Service)         │   │
│  │ VFXManager (Scene Service)               │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
                       ▲
                       │
┌─────────────────────────────────────────────────┐
│  Domain (BladeHexCore)                           │
│  ┌──────────────────────────────────────────┐   │
│  │ WorldPipeline                            │   │
│  │  ├─ TerrainStage                         │   │
│  │  ├─ RiverStage                           │   │
│  │  ├─ IslandStage                          │   │
│  │  ├─ POIStage                             │   │
│  │  ├─ RoadStage                            │   │
│  │  └─ EncounterDensityStage                │   │
│  │                                          │   │
│  │ Combat Rules (existing)                  │   │
│  │ TriggerEngine / QuestGenerator / ...     │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

---

## R1 — 单例治理 Design

### 全局对象分类

| 类别 | 标识 | 生命周期 | 访问方式 | 候选 |
|------|------|----------|----------|------|
| Autoload Singleton | `[Autoload]` | 应用生命周期 | `XxxManager.Instance` | EventBus, AudioManager, GlobalState, UITheme, DebugConsole, GameMenuManager, AudioEventReactor |
| Scene Service | `[SceneService]` 注释标记 | 当前场景 | 通过场景树或父节点引用 | QuestManager, SkillTreeManager, VFXManager, HexCellMultiMeshBatcher |
| Plain Helper | 普通 static class | 无 | 类名直接调用 | HexUtils, CoordConverter |

### Steering 文件

新建 `.kiro/steering/global-objects.md`，包含：
- 三类对象的明确定义
- 每个具体对象的归类与说明
- 「新增全局对象」的判定流程（决策树：是否跨场景需要 → 是 → Autoload；是否场景内单实例 → 是 → Scene Service；否则 → Plain Helper）
- 测试时如何替换/重置

### Scene Service 模式

去掉 `static Instance`，改为通过场景树查找：

```csharp
// Before
public static QuestManager Instance { get; private set; }

// After
public static QuestManager? FindIn(Node sceneRoot)
    => sceneRoot.GetNodeOrNull<QuestManager>("QuestManager")
       ?? sceneRoot.FindChild("QuestManager", recursive: true) as QuestManager;
```

或在父场景持有引用：

```csharp
public partial class OverworldScene3D : Node3D
{
    [Export] public QuestManager QuestManager { get; set; }
}
```

### 迁移路径

1. `QuestManager` → Scene Service（OverworldScene3D 持有）
2. `SkillTreeManager` → Scene Service（OverworldScene3D 持有）
   - 注：当前是 Autoload，要从 `project.godot` 移除
3. `VFXManager` → Scene Service（CombatScene 持有）
4. `HexCellMultiMeshBatcher` → Scene Service（HexGrid 持有）
5. 保留为 Autoload 的类：补完头部注释
   ```csharp
   /// <summary>
   /// [Autoload Singleton]
   /// 生命周期：应用全局
   /// 注册位置：project.godot [autoload]
   /// 测试替换：通过 EventBus.OverrideForTest(...)
   /// </summary>
   ```

### 测试可替换性

EventBus 增加测试钩子：
```csharp
public static EventBus? Instance { get; private set; }

#if DEBUG
public static void OverrideForTest(EventBus? mock) => Instance = mock;
#endif
```

---

## R2 — 跨场景状态访问统一 Design

### GlobalState 重构

```csharp
[GlobalClass]
public partial class GlobalState : Node
{
    public static GlobalState Instance { get; private set; } = null!;
    public static GlobalState Get() => Instance ?? throw new InvalidOperationException(
        "GlobalState 未初始化，请确保 project.godot 注册了 Autoload");

    public override void _EnterTree() { Instance = this; }

    // 子上下文（按职责拆分）
    public SaveContext Save { get; } = new();
    public WorldGenContext WorldGen { get; } = new();
    public QuickCombatContext QuickCombat { get; } = new();
    public WeatherContext Weather { get; } = new();
    public PlayerOriginContext PlayerOrigin { get; } = new();

    // 设置访问保留为顶层（高频跨域）
    public GameSettings GetSettings() { ... }
    public void ApplySettings(GameSettings s) { ... }
}
```

### 子上下文示例

```csharp
public sealed class SaveContext
{
    public bool IsLoadingSave { get; set; }
    public string? CurrentSaveId { get; set; }
    public Godot.Collections.Dictionary LoadedData { get; set; } = new();
}

public sealed class QuickCombatContext
{
    public bool IsQuickGame { get; set; }
    public string Template { get; set; } = "";
    public int Size { get; set; }
    public int PlayerCount { get; set; } = 2;
    public int EnemyCount { get; set; } = 3;
    public int Difficulty { get; set; } = 1;
    public int PlayerLevel { get; set; } = 1;
    public int EnemyType { get; set; }
}
```

### 调用点迁移

```csharp
// Before
var gs = GetNode<GlobalState>("/root/GlobalState");
gs.QuickCombatPlayerCount = 4;

// After
GlobalState.Get().QuickCombat.PlayerCount = 4;
```

### 兼容期保护

为旧字段提供过渡 setter（用 `[Obsolete]`）：
```csharp
[Obsolete("Use QuickCombat.PlayerCount")]
public int QuickCombatPlayerCount
{
    get => QuickCombat.PlayerCount;
    set => QuickCombat.PlayerCount = value;
}
```
分批迁移调用点，全部完成后删除兼容字段。

---

## R3 — World Pipeline Design

### RNG 现状（已盘点，2026-05）

WorldCreator 的随机性已天然隔离，**不是单条共享 RNG 流**：

| 阶段 | RNG 来源 |
|------|---------|
| GenerateAllTerrain / ChunkGenerator | 无 `Random`，用 `FastNoiseLite`（确定性噪声，seed + 偏移） |
| SmoothIsolatedTerrainPatches | 无 RNG |
| GenerateRiversDirect | `new Random(seed ^ 0x52495645)` "RIVE" |
| GenerateIslands | `new Random(seed ^ 0x49534C44)` "ISLD" |
| PlaceIslandPOIs | `new Random(seed ^ 0x49504F49)` "IPOI" |
| PlacePOIs | `new Random(seed ^ 0x504F49)` "POI" |
| BuildNearestNeighborRoads | 接收 seed 但内部是 Prim MST，**未实际使用 RNG** |
| SpecialCharacterGenerator | 内部独立构造 |
| PrecomputeEncounterDensity | 待复核 |

**重构含义：**
- 不需要新设计 RNG 派生方案，沿用既有魔数即可
- 阶段间无 RNG 状态依赖，可以自由重排（顺序不变即可，行为不变）
- 跨阶段共享状态只有 `_islandCenters`（GenerateIslands 写、PlaceIslandPOIs 读），需要提到 Context 中
- `ChunkGenerator` 是有状态对象（持有 5 个 noise instance），Stage 内必须复用同一实例

### 核心抽象

```csharp
namespace BladeHex.Strategic.WorldGen;

public interface IWorldStage
{
    string Name { get; }
    float ProgressWeight { get; }
    void Execute(WorldBuildContext ctx);
}

public sealed class WorldBuildContext
{
    public int Seed { get; }
    public WorldCreationConfig Config { get; }

    // 各 stage 累积写入的中间结果
    public Dictionary<Vector2I, ChunkData> Chunks { get; } = new();
    public List<BiomeZone> Zones { get; set; } = new();
    public Dictionary<string, NationTerritory> Territories { get; set; } = new();
    public List<OverworldPOI> Pois { get; } = new();
    public List<SpecialCharacter> SpecialCharacters { get; set; } = new();

    // 跨阶段共享状态（原 WorldCreator 私有字段）
    public List<Vector2I> IslandCenters { get; } = new();

    public Action<float, string>? OnProgress { get; set; }

    // RNG 工厂：每个 Stage 自己构造，沿用既有魔数
    public Random NewRng(int magic) => new(Seed ^ magic);
}
```

### Pipeline 协调器

```csharp
public sealed class WorldPipeline
{
    private readonly IReadOnlyList<IWorldStage> _stages;

    public static WorldPipeline Default() => new(new IWorldStage[]
    {
        new TerrainStage(),
        new TerrainSmoothingStage(),
        new BiomeZoneStage(),
        new NationAllocationStage(),
        new RiverStage(),
        new IslandStage(),
        new POIStage(),
        new IslandPOIStage(),
        new FerryRouteStage(),
        new RoadStage(),
        new SpecialCharacterStage(),
        new EncounterDensityStage(),
    });

    public WorldData Build(int seed, WorldCreationConfig config, Action<float, string>? onProgress = null)
    {
        var ctx = new WorldBuildContext(seed, config) { OnProgress = onProgress };
        float cumulative = 0f;
        float totalWeight = _stages.Sum(s => s.ProgressWeight);

        foreach (var stage in _stages)
        {
            ctx.OnProgress?.Invoke(cumulative / totalWeight, stage.Name);
            stage.Execute(ctx);
            cumulative += stage.ProgressWeight;
        }
        ctx.OnProgress?.Invoke(1f, "完成");

        return new WorldData
        {
            Seed = ctx.Seed,
            WorldChunksW = config.WorldChunksW,
            WorldChunksH = config.WorldChunksH,
            Chunks = ctx.Chunks,
            Pois = ctx.Pois,
            Zones = ctx.Zones,
            Territories = ctx.Territories,
            Nations = config.Nations,
            SpecialCharacters = ctx.SpecialCharacters,
        };
    }
}
```

### WorldCreator 退化

```csharp
public class WorldCreator
{
    public Action<float, string>? OnProgress;

    public WorldData CreateWorld(int seed, WorldCreationConfig config)
        => WorldPipeline.Default().Build(seed, config, OnProgress);
}
```

### 文件布局

```
BladeHexCore/src/Strategic/
  WorldCreator.cs             (< 50 行 wrapper)
  WorldGen/
    WorldPipeline.cs
    WorldBuildContext.cs
    IWorldStage.cs
    Stages/
      TerrainStage.cs
      TerrainSmoothingStage.cs
      BiomeZoneStage.cs
      NationAllocationStage.cs
      RiverStage.cs
      IslandStage.cs
      POIStage.cs
      IslandPOIStage.cs
      FerryRouteStage.cs
      RoadStage.cs
      SpecialCharacterStage.cs
      EncounterDensityStage.cs
    Internal/
      RiverPathfinder.cs       (从 WorldCreator 抽出的私有 helper)
      RoadAStar.cs
      ...
```

### 等价性保证

- 引入 **golden seed test**：固定 3 个种子，重构前生成 WorldData → 序列化 hash → 重构后重生成 → 比对 hash 一致
- 各阶段 RNG 沿用既有魔数（`seed ^ 0x52495645` 等），不引入新派生方案
- ChunkGenerator 在 `TerrainStage` 内一次性构造并复用，不每 chunk new 实例

---

## R4 — Origin Select Design

### 数据 Schema

`BladeHexCore/src/Data/origin/origin_questions.json`：

```json
{
  "version": 1,
  "races": [
    {
      "raceId": "Human",
      "questions": [
        {
          "id": "human_q1",
          "text": "你的人类童年是怎样度过的？",
          "choices": [
            {
              "id": "farm",
              "text": "在田间劳作，帮父母收割庄稼",
              "summary": "田间劳作",
              "attrMods": { "str": 2, "intel": -1, "cha": -1 },
              "itemReward": "镰刀",
              "illustId": "human_childhood_farm"
            }
          ]
        }
      ]
    }
  ],
  "companionQuestion": {
    "text": "当你将要出发时，你最忠实的伙伴追了上来。那是谁？",
    "choices": [
      {
        "id": "dog",
        "text": "一条忠诚的猎犬",
        "summary": "忠犬相随",
        "illustId": "companion_dog"
      }
    ]
  }
}
```

### 数据加载器

```csharp
public sealed class OriginQuestionLoader
{
    public OriginQuestionData Load(string path = "res://BladeHexCore/src/Data/origin/origin_questions.json");
}

public sealed record OriginQuestionData(
    int Version,
    IReadOnlyDictionary<RaceData.Race, IReadOnlyList<OriginQuestion>> RaceQuestions,
    OriginQuestion CompanionQuestion);
```

### View / Controller 拆分

```
BladeHexFrontend/src/View/UI/MainMenu/
  OriginSelect.cs               (剩余：CanvasLayer 入口 + 信号桥接, < 100 行)
  Origin/
    OriginSelectView.cs         (UI 构建 + 控件管理, < 600 行)
    OriginSelectController.cs   (状态机：Phase1 → Phase2 → Confirm)
    OriginItemRegistry.cs       (运行期物品/插图 lookup)
```

OriginSelectView 暴露事件：
```csharp
public event Action<RaceData> RaceChanged;
public event Action ConfirmRequested;
public event Action<ChoiceData> ChoiceSelected;
```

Controller 订阅事件 → 更新内部状态 → 调用 View 刷新方法。

---

## R5 — 场景控制器组件化 Design

### OverworldScene3D 拆分目标

```
[Node3D] OverworldScene3D
  ├─ [Node] DayNightController        ← OverworldScene3D.DayNight.cs
  ├─ [Node] EntityRegistry            ← OverworldScene3D.Entities.cs
  ├─ [Node] FogController             ← OverworldScene3D.Fog.cs
  ├─ [Node] InteractionDispatcher     ← OverworldScene3D.Interaction.cs
  ├─ [Node] NavigationController      ← OverworldScene3D.Navigation.cs
  ├─ [Node] POIController             ← OverworldScene3D.POI.cs
  ├─ [Node] RoadRenderer              ← OverworldScene3D.Roads.cs
  ├─ [Node] WeatherController         ← OverworldScene3D.Weather.cs
  └─ [Node] WorldRendererBridge       ← OverworldScene3D.World.cs
```

### 主类职责

```csharp
public partial class OverworldScene3D : Node3D, IOverworldContext
{
    // 通过 [Export] 在场景中显式装配
    [Export] public DayNightController DayNight = null!;
    [Export] public EntityRegistry Entities = null!;
    [Export] public FogController Fog = null!;
    // ... 其他

    public override void _Ready()
    {
        // 编排顺序，不做具体逻辑
        Entities.Initialize(this);
        Fog.AttachTo(Entities);
        DayNight.Start();
        // ...
    }
}
```

### 组件间通信

- **首选**：通过 EventBus 发布事件
- **次选**：组件持有需要的 sibling 引用（编译期可见，避免反射）
- **禁止**：跨组件访问私有字段（partial 的副作用）

### 渐进迁移

1. 先把 OverworldScene3D.X.cs 的字段全部加上 `partial` 关键词（已有）+ `field` 标记
2. 把每个 partial 的字段转成属性，由独立 Component 持有
3. 保留 `partial class` 的桥接代理（forward to component）
4. 调用点逐步切换，最后删除 partial 文件

CombatScene 复杂度低于 Overworld，等 Overworld 模式确立后用同套方法。

---

## R6 — 事件总线类型化 Design

### 强类型 API

```csharp
public sealed class EventBus : Node
{
    private readonly Dictionary<Type, List<Delegate>> _typedHandlers = new();
    private readonly Dictionary<string, List<Action<Godot.Collections.Dictionary>>> _legacyHandlers = new();

    // 强类型路径
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (!_typedHandlers.TryGetValue(typeof(TEvent), out var list))
            _typedHandlers[typeof(TEvent)] = list = new();
        list.Add(handler);
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (_typedHandlers.TryGetValue(typeof(TEvent), out var list))
            list.Remove(handler);
    }

    public void Publish<TEvent>(TEvent ev) where TEvent : notnull
    {
        if (_typedHandlers.TryGetValue(typeof(TEvent), out var list))
            foreach (var h in list.ToArray())
                try { ((Action<TEvent>)h)(ev); }
                catch (Exception e) { GD.PrintErr($"[EventBus<{typeof(TEvent).Name}>] {e}"); }
    }

    // 兼容路径（保留现有 Publish(string, Dictionary)）
    [Obsolete("使用 Publish<TEvent> 强类型接口", error: false)]
    public void Publish(string signal, Godot.Collections.Dictionary? data = null) { /* 旧实现保留 */ }
}
```

### 事件类型定义

`BladeHexFrontend/src/View/Events/Payloads/`：

```csharp
namespace BladeHex.Events.Payloads;

public sealed record UnitDamagedEvent(Node3D Unit, int Damage, int RemainingHp);
public sealed record UnitDiedEvent(Node3D Unit, bool IsPlayer);
public sealed record SkillUsedEvent(Node3D Caster, string SkillEffect, bool Success);
public sealed record CombatEndedEvent(BattleOutcome Outcome);
public sealed record DayPassedEvent(int DaysPassed, int Year, int Month, int Day);
```

### 桥接策略

新 API 上线后，旧代码继续工作。Publisher 同时发布两路：

```csharp
public void PublishUnitDied(Node3D unit, bool isPlayer)
{
    Publish(new UnitDiedEvent(unit, isPlayer));  // 强类型
    Publish(Signals.UnitDied, new() { ["unit"] = unit, ["is_player"] = isPlayer });  // 兼容
}
```

### 内部 API 类型化（技能/伤害）

`UseSkill` / `UseCareerSkill` 当前返回 `Godot.Collections.Dictionary`：

```csharp
// 新增强类型 result
public sealed record SkillExecutionResult(
    bool Success,
    string? FailureReason,
    IReadOnlyList<DamageEvent> Damages,
    IReadOnlyList<TeleportEvent> Teleports,
    IReadOnlyList<StatusEffectApplication> StatusEffects);

// 同时保留 Dictionary 版本作为薄 wrapper
public Godot.Collections.Dictionary UseSkillLegacy(...) =>
    UseSkill(...).ToDictionary();
```

---

## R7 — 测试覆盖 Design

### 测试框架

继续使用项目现有约定（`BladeHexCore/tests/` + 自定义 `TestRunner`），不引入 NUnit/xUnit（除非现有 runner 不够用）。

如果现有 runner 不足以承载更多测试，统一切换到 xUnit + .NET Test SDK：
```xml
<PackageReference Include="xunit" Version="2.x" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.x" />
```

待 R7 启动时再决定。

### 测试组织

```
BladeHexCore/tests/
  Combat/
    CombatRuleEngineTests.cs       (命中/暴击/穿透)
    DamagePenetrationTableTests.cs
    WeaponMasteryTests.cs
  Map/
    HexOverworldAStarTests.cs
    ChunkAStarTests.cs
  Strategic/
    SaveSystemRoundtripTests.cs
    TriggerEngineTests.cs
    WorldPipelineGoldenSeedTests.cs   (与 R3 协同)
  Quest/
    QuestGeneratorTests.cs
```

### 关键测试样例

```csharp
[Test]
public void GoldenSeed_Seed42_WorldHashStable()
{
    var pipeline = WorldPipeline.Default();
    var world = pipeline.Build(seed: 42, config: TestConfig.Small());

    var hash = WorldHasher.Hash(world);
    Assert.Equal("expected-hash-for-seed-42", hash);
}

[Test]
public void Damage_Crit_Doubles()
{
    var attacker = TestUnits.Knight(str: 16);
    var defender = TestUnits.Goblin(ac: 14);
    var rng = new SeededRng(1);

    var result = CombatRuleEngine.ResolveAttack(attacker, defender, rng, naturalRoll: 20);

    Assert.True(result.IsCritical);
    Assert.Equal(expected: result.BaseDamage * 2, actual: result.FinalDamage);
}
```

### 不依赖 Godot 场景树

所有测试在 `BladeHexCore/tests/`（纯 C#），不引用 `BladeHexFrontend`。涉及 Godot 类型时使用：
- `Godot.Vector2I` / `Godot.Vector2` / `Godot.Color`：允许（数学类型）
- `Godot.Resource`：允许（数据基类）
- `Godot.Node` / `Texture2D` / `Material`：禁止（被 steering 约束）

---

## R8 — 项目根目录清理 Design

### 处理方案

| 文件类型 | 处理 |
|----------|------|
| `fix_*.py` / `clean_*.py` / `find_*.py` / `revert_*.py` / `show_*.py` / `remove_*.py` / `check_build.py` | 移到 `tools/scripts/legacy/`，附 README 说明用途和最后使用场景；如果确认无用，直接删除 |
| `build_*.txt` / `godot_*.txt` / `remaining_errors*.txt` | 全部删除 |
| `.gitignore` | 添加 `build_*.txt`、`godot_*.txt`、`remaining_errors*.txt` 模式 |

### legacy/README.md 模板

```markdown
# Legacy Scripts

这些脚本是项目早期一次性维护工具，保留作为历史参考。
新代码请勿依赖。

| 脚本 | 用途 | 最后使用 |
|------|------|----------|
| fix_audio_calls.py | 修复 GDScript 时代的音频 API 调用 | 2025-XX |
| clean_all_gd_refs.py | GDScript 迁移期间清理 .gd 引用 | 2025-XX |
| ... | ... | ... |
```

---

## R9 — SaveManager 退役 Design

### 步骤

1. 全局搜索 `SaveManager.` 与 `new SaveManager(`，列出所有引用点
2. 确认每处都已切到 V2
3. 加载若干旧存档手动测试：V2 是否能直接读取，或需要迁移代码
4. 如果格式兼容，删除 `SaveManager.cs`
5. 如果格式不兼容，在 `SaveManagerV2` 中实现 `MigrateFromV1(string v1Json)`，保留兼容期至少 1 个版本
6. `SaveManagerV2` 重命名为 `SaveManager`（V2 变成正名）

### 决策点
- **格式兼容**：直接删 v1
- **格式不兼容**：先实现迁移，再删 v1

---

## R10 — Unit 视图组件化 Design

### 目标拆分

```
[Node3D] Unit (核心状态 + 规则委托)
  ├─ [Node3D] CharacterRenderNode (existing)
  ├─ [Node3D] UnitHealthBarComponent  (新)
  └─ [Node3D] UnitAnimationComponent  (新)
```

### Unit.cs 保留内容

- `Data` / `Model` / `CommandHistory`
- `CurrentHp` / `GridPos` / `IsPlayerSide` / `HasMoved` / `HasActed` / `CurrentAp`
- `IFightable` 接口实现
- `MoveAlongPath`
- 规则委托方法（GetMaxHp, GetAc, RollDamage 等）
- `TakeDamage` / `Heal` / `Die`（保留为高层入口，内部委派给组件）

### Unit.cs 移出内容

- `_hpBarBg` / `_hpBarFill` / `_armorBarBg` / `_armorBarFill` 字段
- `SetupHpBar` / `UpdateHpBar` / `UpdateArmorBar`
- `CreateBarSprite` / `CreateColorTexture`
- `BarPixelWidth` 等常量
- `PlayAttackLunge`

### UnitHealthBarComponent

```csharp
[GlobalClass]
public partial class UnitHealthBarComponent : Node3D
{
    [Export] public Unit Owner = null!;

    public override void _Ready() { /* 创建 4 个 Sprite3D */ }

    public void Refresh()  // Unit.UpdateHpBar 改为转发到此
    {
        UpdateHpBar();
        UpdateArmorBar();
    }

    private void UpdateHpBar() { ... }
    private void UpdateArmorBar() { ... }
}
```

### Unit 转发

```csharp
public void UpdateHpBar() => _healthBar?.Refresh();
```

---

## 实施顺序与依赖

```
阶段 1
  R1 ─┐
       ├─→ 阶段 2
  R2 ─┘     R3 (无依赖)
            R4 (无依赖)
                     ┌─→ 阶段 3
                     │     R5 (依赖 R1 单例治理)
                     │     R6 (无依赖)
                     │              ┌─→ 阶段 4
                     │              │     R7 (建议在 R3 之前启动 golden test)
                     │              │     R8 (无依赖, 可任何时候做)
                     │              │     R9 (无依赖)
                     │              │     R10 (依赖 R6 类型化事件)
```

**实际推进顺序建议：**

1. **Sprint 1 (R1 + R2)** — 单例 + GlobalState 拆分
2. **Sprint 2 (R7 部分 + R3)** — 先补 golden seed 测试，再拆 WorldPipeline
3. **Sprint 3 (R4)** — Origin 数据外置
4. **Sprint 4 (R8 + R9)** — 工程清理 + SaveManager 退役（轻量任务穿插）
5. **Sprint 5 (R6)** — EventBus 类型化
6. **Sprint 6 (R5)** — 场景组件化（最重）
7. **Sprint 7 (R10 + R7 补完)** — Unit 拆分 + 测试补完

每个 Sprint 结束做完整流程手测：主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档。

---

## 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| WorldPipeline 重构改变随机数序列，世界变了 | 高 → **低**（已盘点 RNG 已天然隔离） | golden seed test 兜底；保留各阶段魔数派生 |
| 组件化导致 Godot 序列化破坏（场景文件引用旧字段） | 中 | partial → component 阶段保留代理字段，让旧 .tscn 仍可加载 |
| GlobalState 拆分期间外部代码混合新旧路径 | 低 | `[Obsolete]` 兼容期 + grep 跟踪 |
| EventBus 双路径导致事件重复触发 | 中 | publisher 内部去重；订阅者一次只订一边 |
| 测试框架切换 | 低 | 本 Spec 不强制切换，按需在 R7 启动时决定 |

---

## 文档约束

每个 Sprint 完成后：
- 更新本 design.md，记录实际偏差
- 在 `.kiro/specs/architecture-optimization/notes.md`（运行时创建）记录决策日志
- 必要时更新 steering 文件（如新增 `global-objects.md`）
