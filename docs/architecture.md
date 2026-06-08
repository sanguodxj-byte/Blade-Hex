# Blade & Hex 架构文档

> 供 AI Agent 快速获取项目架构、关键类、命名空间和设计模式信息。

## 1. 技术栈

| 项目 | 技术 |
|------|------|
| 引擎 | Godot 4.6.2 (C# / .NET 8.0) |
| 物理 | Jolt Physics 3D |
| 渲染 | Mobile renderer, D3D12 |
| 脚本扩展 | NLua (Lua 5.4) |
| 分辨率 | 1920x1080, canvas_items stretch |

## 2. 项目结构

```
Blade&Hex/
├── BladeHexCore/              ← 纯逻辑层（无渲染、无 Node 继承）
│   ├── src/
│   │   ├── Character/         角色生成、命名、种族
│   │   ├── Combat/            战斗规则引擎、状态机、伤害解算
│   │   ├── Data/              数据模型（UnitData, ItemData, WeaponData, ArmorData, QuestData）
│   │   ├── Interaction/       交互选项、设施类型（TownFacility）
│   │   ├── Map/               六边形网格、Chunk 系统、地形生成
│   │   ├── Quest/             任务模板加载、动态生成器
│   │   ├── Scripting/         Lua 脚本绑定
│   │   ├── SkillTree/         技能盘数据、称号解析
│   │   └── Strategic/         战略层（经济、实体、招募、声望、封地、世界生成）
│   └── tests/                 单元测试（Godot headless 运行）
│
├── BladeHexFrontend/          ← 渲染/UI/场景层
│   └── src/
│       ├── Scenes/
│       │   ├── combat/        CombatSceneBase, CombatScene, QuickCombatScene
│       │   ├── overworld/     OverworldScene3D（partial class, 12+ 文件）
│       │   └── test/          测试场景
│       └── View/
│           ├── Audio/         AudioManager, AudioEventReactor
│           ├── Camera/        OverworldCamera3D, CombatCamera
│           ├── Combat/        AI 策略、命令模式、技能处理器、状态效果、回合管理
│           ├── Data/          GlobalState, Globals, SaveManager, EconomyManager
│           ├── Debug/         DebugConsole
│           ├── Environment/   WeatherManager, 昼夜系统
│           ├── Events/        EventBus, 强类型事件载荷
│           ├── Map/           HexOverworldRenderer3D, PropRenderer, MultiMesh 合批
│           ├── Quest/         QuestManager（运行时追踪）
│           ├── Strategic/     OverworldTown, OverworldEnemy, OverworldParty
│           ├── Transitions/   场景切换动画
│           ├── UI/            所有 UI 面板和 HUD
│           └── Unit/          单位节点、装备渲染、Sprite 管线
│
├── assets/                    生成的图片资源（图标、装备、消耗品）
├── docs/                      设计文档
├── scripts/                   Lua 技能脚本
├── src/                       Godot 场景文件（.tscn）和旧 UI 资源
└── tools/                     构建/测试/迁移脚本
```

## 3. 构建配置

| 文件 | 作用 |
|------|------|
| `BladeHexFrontend.csproj`（根目录） | Godot 运行时加载的主 csproj，编译所有 .cs |
| `BladeHexCore/BladeHexCore.csproj` | Core 层独立编译（IDE/测试用） |
| `BladeHex.sln` | 包含两个 csproj 的解决方案 |
| `Directory.Build.props` | 共享 NLua/KeraLua DLL 引用 |

构建命令：
```bash
dotnet build BladeHexFrontend.sln --no-restore
```

## 4. Autoload 单例

通过 `project.godot [autoload]` 注册，全局生命周期：

| 名称 | 类 | 职责 |
|------|----|------|
| GlobalState | `BladeHex.Data.GlobalState` | 跨场景状态聚合根（世界生成参数、玩家数据） |
| EventBus | `BladeHex.Events.EventBus` | 全局事件总线 |
| AudioManager | `BladeHex.Audio.AudioManager` | 音频播放 |
| AudioEventReactor | — | 监听 EventBus 播放音效 |
| UITheme | `BladeHex.UI.UITheme` | 设计令牌（颜色/字号/间距） |
| SkillTreeManager | — | 技能盘进度持久化 |
| GameMenuManager | `BladeHex.UI.Global.GameMenuManager` | ESC 菜单、存档、返回主菜单 |
| WeatherManager | `BladeHex.View.Environment.WeatherManager` | 天气状态机 |
| CursorManager | — | 鼠标光标管理 |
| DebugConsole | — | 开发期调试控制台 |

统一访问入口：`BladeHex.Data.Globals` 静态类
```csharp
var gs = Globals.State;          // GlobalState
var bus = Globals.Events;        // EventBus
var audio = Globals.Audio;       // AudioManager
var menu = Globals.GameMenu;     // GameMenuManager
// OrNull 变体用于生命周期早期
var gs = Globals.StateOrNull;
```

## 5. 核心设计模式

### 5.1 Core/View 分层

| 规则 | 说明 |
|------|------|
| Core 禁渲染类型 | 不得出现 Texture2D, Material, Mesh 等 |
| Core 禁 Node 继承 | 不得继承 Node/Node3D/Control（Resource 除外） |
| Core 只持 string ID | 资源引用用 `string IconId` / `string SpriteFramesId` |
| View 不持规则 | 伤害/属性计算不在 View 层 |
| 数据流单向 | Core → View，View 通过委托方法修改运行时状态 |

### 5.2 Partial Class 大场景拆分

`OverworldScene3D` 拆分为 12+ 文件：
```
OverworldScene3D.cs           主文件（_Ready, _Process）
OverworldScene3D.Audio.cs     音频
OverworldScene3D.DayNight.cs  昼夜循环
OverworldScene3D.Entities.cs  实体管理、战斗入口
OverworldScene3D.Fog.cs       战争迷雾
OverworldScene3D.Input.cs     输入处理、快捷键
OverworldScene3D.Interaction.cs  POI 交互、面板路由
OverworldScene3D.Minimap.cs   小地图
OverworldScene3D.Navigation.cs  寻路
OverworldScene3D.POI.cs       POI 检测
OverworldScene3D.Roads.cs     道路渲染
OverworldScene3D.UI.cs        HUD 初始化
OverworldScene3D.Weather.cs   天气
OverworldScene3D.World.cs     世界生成
```

### 5.3 POI 面板基类模式

`POIPanelBase` (CanvasLayer) 提供固定布局脚手架：
```
插画区 → 信息行 → 描述文本 → 功能列表(ScrollContainer) → 结果反馈 → 离开按钮
```

子类只重写数据填充方法：
```csharp
protected override Color GetIllustrationColor() => ...;
protected override string GetIllustrationText() => ...;
protected override string GetInfoText() => ...;
protected override string GetDescriptionText() => ...;
protected override string GetLeaveButtonText() => ...;
protected override void PopulateActions(VBoxContainer container) { ... }
```

具体面板：TownPanel, SmithyPanel, TemplePanel, ArenaPanel, RestPanel, RecruitPanel, QuestBoardPanel, PortPanel

### 5.4 EventBus 双轨事件

```csharp
// 强类型（推荐）
bus.Subscribe<UnitDamagedEvent>(OnDamaged);
bus.Publish(new UnitDamagedEvent(unit, 10, 80));

// 弱类型（兼容期）
bus.Subscribe(Signals.UnitDamaged, dict => ...);
bus.Publish(Signals.UnitDamaged, new Dictionary { ... });
```

信号常量定义在 `EventBus.Signals` 静态类中。

### 5.5 场景切换

```csharp
// 标准切换（清理游离节点 + 解除暂停）
SceneTransition.ChangeSceneTo(tree, "res://path/to/scene.tscn");

// 带加载屏切换
LoadingScreen.LoadScene("res://path/to/scene.tscn", LoadingScreen.PhaseType.QuickGame);

// 战斗切换（手动管理场景树）
EnterCombatScene(ctx);  // 移除大地图 → 添加战斗场景
OnCombatFinished(victory, combatScene);  // 移除战斗 → 恢复大地图
```

`SceneTransition.Persistent` 集合定义不被清理的节点名（所有 Autoload + LoadingScreen）。

### 5.6 设施路由

```
TownPanel.FacilitySelected(int type)
  → OverworldScene3D.OnFacilitySelected(type)
    → switch (FacilityType) {
         Market → OpenTradePanel()
         Tavern → OpenRecruitPanel()
         Smithy → OpenSmithyPanel()
         Temple → OpenTemplePanel()
         Arena  → OpenArenaPanel()
         QuestBoard → OpenQuestPanel()
         Rest   → OpenRestPanel()
         Port   → OpenPortPanel()
       }
```

二级面板关闭 → `OnSecondaryPanelClosed()` → 重新打开 TownPanel。

## 6. 关键数据模型

### UnitData (Resource)
```
六维属性: Str, Dex, Con, Intel, Wis, Cha
装备槽: PrimaryMainHand, SecondaryMainHand, Armor, Helmet, Gauntlets, Boots, Shield, Accessories[]
运行时: Runtime.CurrentHp, Runtime.CurrentMana, Runtime.StatusEffects
敌方: IsEnemy, enemyType, aiStrategy, ThreatLevel
```

### PartyRoster (Resource)
```
Members: List<UnitData>  (队长始终 index 0)
Capacity: int (默认 6)
方法: Add, Remove, GetDeployableMembers, ApplyBattleResult, RestoreHp, Serialize/Deserialize
```

### EconomyManager (Node, Autoload 级别)
```
资源: Gold, Food, MaxFood, DailyWage
时间: CurrentHour, DaysPassed, Month, Year, Season
背包: PlayerInventory (Array<ItemData>)
方法: AddGold, SpendGold, AddFood, ConsumeFood, AdvanceTime, AddItem, RemoveItem
信号: ResourcesChanged, InventoryChanged
```

### BattleContext (Resource)
```
地形: Terrain (TerrainType)
规模: Size (Mercenary/Knight/Lord/Stronghold)
交战: Engagement (Normal/Ambush/Ambushed)
部署: AttackerDeployment[], DefenderDeployment[]
工厂: Create(), CreateFromEncounter(), CreateFromNoise()
```

### TownFacility (Resource)
```
枚举: Castle, Market, Tavern, Arena, Smithy, Temple, QuestBoard, Rest, Port
工厂: CreateDefaultFacilities(), CreatePortFacilities(), CreateVillageFacilities(), ...
```

## 7. 测试基础设施

- 框架：Godot headless 内置（无 xUnit/NUnit）
- 入口：`TerrainTestRunner` (Node)
- 模式：`TEST_MODE` 环境变量 (unit/terrain/golden_record/golden_verify/ui/sim)
- 约定：静态类 + `RunAll()` 返回 `(int passed, int failed, List<string> details)`

运行命令：
```powershell
$env:GODOT = "C:\path\to\godot_console.exe"
.\test.bat -Mode unit
```

测试套件：CombatRuleEngineTests, CombatStateMachineTests, LosCoreTests, HexOverworldAStarTests, ChunkAStarTests, OverworldSamplerTests, BattleProjectionTests, TerrainEnumAlignmentTests, SaveSystemRoundtripTests, TriggerEngineTests, QuestGeneratorTests, UIConnectivityTests, POIPanelTests

## 8. 命名空间映射

| 命名空间 | 位置 | 职责 |
|----------|------|------|
| `BladeHex.Data` | Core/src/Data + Frontend/View/Data | 数据模型、全局状态 |
| `BladeHex.Strategic` | Core/src/Strategic | 战略层逻辑 |
| `BladeHex.Combat` | Core/src/Combat | 战斗规则 |
| `BladeHex.Map` | Core/src/Map | 六边形网格 |
| `BladeHex.Events` | Frontend/View/Events | 事件总线 |
| `BladeHex.Scenes.Overworld` | Frontend/Scenes/overworld | 大地图场景 |
| `BladeHex.Scenes` | Frontend/Scenes/combat | 战斗场景 |
| `BladeHex.View.UI.Overworld` | Frontend/View/UI/Overworld | 大地图 UI 面板 |
| `BladeHex.UI` | Frontend/View/UI | UIFactory, UITheme |
| `BladeHex.Audio` | Frontend/View/Audio | 音频系统 |
| `BladeHex.View.Map` | Frontend/View/Map | 地图渲染 |
| `BladeHex.View.Environment` | Frontend/View/Environment | 天气、昼夜 |
| `BladeHex.Debug` | Frontend/View/Debug | 调试工具 |

## 9. 常用操作速查

| 操作 | 方法 |
|------|------|
| 扣金币 | `EconomyMgr.SpendGold(amount)` → bool |
| 加金币 | `EconomyMgr.AddGold(amount)` |
| 推进时间 | `EconomyMgr.AdvanceTime(hours)` |
| 添加队员 | `roster.Add(unitData)` → bool |
| 恢复 HP | `roster.RestoreHp(amount)` |
| 获取当前 HP | `PartyRoster.GetCurrentHp(unit)` |
| 设置当前 HP | `PartyRoster.SetCurrentHp(unit, hp)` |
| 生成敌人 | `EncounterUnitFactory.BuildEnemyUnitsFromEntity(entity)` |
| 进入战斗 | `EnterCombatScene(battleContext)` |
| 生成任务 | `questGenerator.GetAvailableQuests(poiId, day)` |
| 接取任务 | `questGenerator.AcceptQuest(poiId, index, day)` |
| 打开面板 | `panel.ShowPanel()` / `panel.HidePanel()` |
| 发布事件 | `Globals.Events.Publish(new XxxEvent(...))` |
| 场景切换 | `SceneTransition.ChangeSceneTo(tree, path)` |
| 带加载屏切换 | `LoadingScreen.LoadScene(path, phaseType)` |

## 10. 编码规范

- 语言：C# 12, nullable enabled
- 注释：中文（中文游戏项目）
- UI 文本：全中文，禁止 emoji
- 类标注：需暴露给 Godot 的类加 `[GlobalClass]`
- 跨语言边界：用 `Godot.Collections.Dictionary`，内部用 `System.Collections.Generic`
- 信号：用 `[Signal] public delegate void XxxEventHandler(...)` 声明
- 资源引用：Core 层用 string ID，View 层通过 ResourceRegistry 解析
- 大类拆分：用 partial class + 按职责命名的文件后缀


## 11. 战斗系统详解

### 11.1 战斗场景层次

```
CombatSceneBase (abstract, Node3D)
├── CombatScene          正式战斗（从大地图进入）
└── QuickCombatScene     快速战斗（主菜单直接进入）
```

子类必须实现：
- `GenerateBattlefield()` — 生成战斗地图
- `SpawnUnits()` — 部署双方单位
- `HandleCombatEnd(bool victory)` — 处理战斗结束

### 11.2 战斗子系统

| 子系统 | 类 | 层 | 职责 |
|--------|----|----|------|
| 流程总控 | `CombatManager` | Frontend | Facade，协调所有子系统 |
| 状态机 | `CombatStateMachine` | Core | 纯 C# 状态机 (Init→PlayerTurn↔EnemyTurn→CombatEnd) |
| 回合管理 | `TurnManager` | Frontend | 包装 CombatStateMachine，重置单位 AP |
| 单位注册 | `UnitRegistry` | Frontend | 管理玩家/敌方单位列表 |
| 规则引擎 | `CombatRuleEngine` | Core | 攻击检定、伤害管道（纯静态） |
| AI 控制器 | `AIController` | Frontend | 驱动敌方回合 AI 决策 |
| 法术管理 | `SpellManager` | Frontend | 法术施放、魔力、冷却 |
| 状态效果 | `StatusEffectManager` | Frontend | Buff/Debuff 管理 |
| 命令系统 | `CommandHistory` | Frontend | ICommand 模式，支持 Undo |
| 战果构建 | `CombatResultBuilder` | Frontend | 构建战斗结果数据 |

### 11.3 攻击检定流程

```
CombatRuleEngine.RollAttack(AttackInput) → AttackRollResult
  1. 优劣势互抵 → 掷 d20
  2. 总攻击值 = roll + AttackBonus + AccuracyMod
  3. 暴击判定: roll >= CritThreshold
  4. 命中判定: 暴击 || (非大失败 && total >= AC + CoverBonus)
  5. 擦伤: 未命中但差值 ≤ 2 → 半伤命中
  6. 独立暴击概率: BonusCritChance 追加暴击机会
```

### 11.4 伤害管道

```
CombatRuleEngine.CalculateDamage(DamageInput) → DamageCalcResult
  基础伤害 → 擦伤减半 → 暴击倍率 → 偷袭加成 → 被动近战加成
  → 包夹倍率 → 冲锋倍率 → 骑乘加成 → 被动减免 → 最终倍率
  → max(1, result)
```

### 11.5 AI 策略系统

```
AIStrategyBase (abstract)
├── AIStrategyCautious    谨慎型（优先远程/掩体）
├── AIStrategyInstinct    本能型（最近目标直冲）
├── AIStrategyReckless    鲁莽型（无视 HP 全力攻击）
├── AIStrategyTactical    战术型（评估包夹/高地/掩体）
├── AIStrategyCunning     狡诈型（优先弱目标）
└── AIStrategyTerritorial 领地型（守卫区域）
```

决策模板方法：
```
DecideAction(actor, playerUnits, enemyUnits, hexGrid)
  1. 士气强制行为（溃逃）
  2. HP 过低撤退检查
  3. 目标评估（AITargetEvaluator）
  4. 策略特定决策（子类实现）
```

### 11.6 命令模式

```csharp
ICommand { Execute(ctx), Undo(ctx), CanUndo, Description }
├── AttackCommand
├── MoveCommand
├── UseSkillCommand
├── SwitchWeaponCommand
└── WaitCommand

CommandHistory.Execute(cmd, ctx)  // 执行并入栈
CommandHistory.TryUndoLast(ctx)   // 悔棋
CommandHistory.MarkTurnBoundary() // 回合边界（Undo 不越过）
```

### 11.7 法术系统

```
SpellManager.CastSpell(caster, spell, targetCell, grid)
  1. CanCastSpell 检查（触媒/冷却/魔力）
  2. 扣除魔力 + 设置冷却
  3. SpellShapeResolver 计算影响范围
  4. 按 ResolutionType 解算:
     - AttackRoll: d20 + CastingMod + Prof vs AC
     - Save: DC vs 目标豁免（半伤）
     - AutoHit: 自动命中（治疗/必中伤害）
```

## 12. 战斗地图生成

### 12.1 BattleMapGenerator

| 规模 | 六边形半径 | 约 Cell 数 |
|------|-----------|-----------|
| Mercenary | 7 | 169 |
| Knight | 8 | 217 |
| Lord | 11 | 397 |
| Stronghold | 14 | 631 |

25+ 地图模板：plain_field, forest_ambush, mountain_pass, swamp_battle, coastal_ambush, desert_skirmish, dragon_lair, ancient_tomb, goblin_camp, kobold_mine, minotaur_fortress, shadow_cult_hideout, village_defense, castle_siege, bandit_stronghold...

### 12.2 大地图战斗生成管线

```
1. OverworldSampler.Sample() — 采样大地图地形
2. BattleProjector.Project() — 投影到战斗尺度
3. Voronoi + 水体处理
4. 桥梁生成
5. 结构物放置（墙/废墟）
6. 天气覆盖
```

### 12.3 部署区域 (DeploymentZone)

- Normal: 按 approachDirection 半平面切分（有方向）或 q 轴对半切（无方向）
- Ambush: 玩家分散有利位置
- Ambushed: 玩家集中不利位置

## 13. 装备与物品生成

### 13.1 EquipmentGenerator

```csharp
EquipmentGenerator.GenerateEquipment(baseItem, rarity, itemLevel, difficulty)
EquipmentGenerator.GenerateRandomWeapon(pool, rarity, itemLevel, difficulty)
EquipmentGenerator.GenerateRandomArmor(pool, rarity, itemLevel, difficulty)
EquipmentGenerator.RollRarity(difficulty) → Rarity
```

### 13.2 LootTable

```csharp
LootTable.GenerateLoot(enemyData) → List<ItemData>
```

掉落概率基于：
- 基础掉率（武器 15%, 护甲 12%, 消耗品 30%, 金币 80%）
- CR 倍率（CR 0.125→0.5x, CR 20→4.0x）
- 敌人类型偏差（野兽不掉武器/护甲，龙 3x 金币）

### 13.3 PrototypeData

```csharp
PrototypeData.GetWeapons()      // 171 种武器
PrototypeData.GetArmors()       // 19 种护甲
PrototypeData.GetConsumables()  // 10 种消耗品
PrototypeData.GetQuivers()      // 3 种箭筒
// 全部从 JSON 加载（ItemDataLoader）
```

## 14. 世界生成

### 14.1 WorldPipeline 阶段

```
1. TerrainStage          地形噪声生成
2. TerrainSmoothingStage 地形平滑
3. BiomeZoneStage        生态区划分
4. NationAllocationStage 国家领土分配
5. RiverStage            河流生成
6. IslandStage           岛屿生成
7. POIStage              城镇/据点放置
8. IslandPOIStage        岛屿 POI
9. FerryRouteStage       渡船航线
10. RoadStage            道路连接
11. SpecialCharacterStage 特殊角色生成
12. EncounterDensityStage 遭遇密度
```

### 14.2 WorldGenerator（运行时协调器）

```
WorldGenerator.InitializeChunkWorld(seed, raceId)
  → ChunkManager 初始化
  → EncounterSpawner 初始化
  → ChunkFogOfWar 初始化
  → RiverRoadGenerator 生成骨架
  → TriggerEngine 注册默认触发条件
```

### 14.3 ChunkManager

- Chunk 大小: 16x16 hex tiles
- 加载半径: 2 chunks（切比雪夫距离）
- 卸载半径: 3 chunks
- 加载优先级: 内存缓存 → 磁盘存档 → 重新生成

## 15. 角色生成

### 15.1 CharacterGenerator

```csharp
CharacterGenerator.GenerateCharacter(race, level, seed)
  → 属性分配（25 基础点 + 等级加成）
  → 种族修正
  → 特质随机
  → 命名（NameGenerator）
  → 初始装备（布衣 + 皮靴 + 随机 T1 武器）

CharacterGenerator.GenerateRandomEnemy(cr, enemyType, strategy)
  → CR→等级转换
  → 按敌人类型分配属性
  → 装备生成（按等级/CR 缩放）
```

### 15.2 NameGenerator

- 5 种族名池（Human/Elf/Dwarf/HalfOrc/HalfElf）
- 双语支持（中/英）
- 5 级以上获得称号（前缀+后缀组合，200+ 变体）
- 种族偏好称号池

## 16. 大地图实体系统

### 16.1 OverworldEntity 类型

| 类型 | 行为 | 敌对 |
|------|------|------|
| Adventurer | 巡逻/友好 | 否 |
| RaidingParty | 追击/掠夺 | 是 |
| BanditParty | 追击/伏击 | 是 |
| Caravan | 城镇间移动 | 否 |
| EpicMonster | 领地巡逻 | 是 |
| LordArmy | 巡逻/围攻/回援 | 视势力 |

### 16.2 EncounterEntitySpawner

```
Tick(delta, playerPos, entities, playerLevel, day)
  1. 累积玩家移动距离
  2. 检查 spawn 条件（间隔 8s + 距离 600px + 上限 5）
  3. TrySpawnEntity（视野外 800-1600px）
  4. UpdateChaseAI（视野内追击/视野外巡逻）
```

实体类型权重：掠夺队 35%, 山贼 25%, 劫匪 10%, 冒险者 15%, 商队 10%, 巨兽 5%

### 16.3 OverworldAIResolver（AI 间自动结算）

```csharp
OverworldAIResolver.ResolveBattle(attacker, defender)  // 野外战斗
OverworldAIResolver.ResolveSiege(attacker, poi)        // 围攻
OverworldAIResolver.ResolveRaid(attacker, village)     // 掠夺
```

## 17. 存档系统

### 17.1 数据模型 (GameSaveData)

```
GameSaveData v2.0.0
├── WorldSaveData (玩家位置, seed, 实体列表, POI 列表)
├── PartySaveData (单位列表含完整属性/装备ID/法术/精通)
├── EconomySaveData (金币, 食物, 时间)
├── QuestSaveData (活跃/完成任务, 进度)
└── InventoryItemSaveData[] (背包物品)
```

### 17.2 Chunk 持久化

```csharp
ChunkPersistence.SaveAllChunks(saveId, chunks)
ChunkPersistence.LoadChunk(saveId, coord)
```

## 18. 技能盘系统

### 18.1 SkillNodeData

```
NodeType: Small(属性加成) / Big(主动技能) / Keystone(强力+代价) / Start(起点)
Region: Str / Dex / Con / Int / Wis / Cha / Transition
```

解锁条件：等级 + 前置节点 + 相邻已激活节点

### 18.2 技能执行

```
SkillEffectExecutor.ExecuteActiveSkill(caster, skillEffect, target, grid, ...)
CareerSkillExecutor.ExecuteCareerSkill(caster, target, grid, ...)
```

技能处理器分类：MeleeSkillHandlers, RangedSkillHandlers, MagicSkillHandlers, AssassinSkillHandlers, StealthSkillHandlers, SupportSkillHandlers

## 19. 触发引擎 (TriggerEngine)

```
TriggerType: Spatial / Time / Environment / Interaction / Chain
TriggerCondition: Id, Type, Priority, Chance, RequiredTerrains, MinPlayerLevel, CooldownDays
TriggerHandler: SpatialTriggerHandler, TimeTriggerHandler, EnvironmentTriggerHandler, InteractionTriggerHandler, ChainTriggerHandler
```

默认触发条件：
- spatial_wild_monsters (25%, 森林/沼泽/丘陵)
- spatial_hostile_patrol (15%, 3级以上)
- spatial_resource_node (20%, 森林/丘陵/草原)
- spatial_mystery (5%, 密林/沼泽/针叶林)
- env_weather (30%)
- time_raid_spawn (每7天)
- time_poi_recovery (每天)


## 20. 音频系统

### AudioManager (Autoload)

```
音频总线: Master → Music / SFX / Ambient
BGM: 双播放器交叉淡入淡出，场景+变体选择
SFX: 12 个池化播放器，按名称查找
Ambient: 4 个循环播放器
```

场景枚举：MainMenu, Overworld, Combat, Town, Dungeon, Event, Tavern, Victory, Defeat

```csharp
AudioManager.PlayScenarioBgm(Scenario.Combat, "boss", fadeTime)
AudioManager.PlaySfxName("combat_sword_crit", volumeDb)
AudioManager.PlayAmbient("forest_birds")
AudioManager.GetFootstepSfx(terrainKey)
```

### AudioEventReactor (Autoload)

自动订阅 EventBus 信号并映射到音效：
- CombatStarted → "ow_combat_trigger"
- UnitDamaged → "combat_armor_hit" / "combat_sword_crit"
- UnitDied → "combat_death"
- SkillUsed → "skill_{effect}" 或回退 "skill_melee_combo"
- GoldChanged → "ui_gold_gain" / "ui_gold_spend"
- QuestCompleted → "quest_complete"

## 21. 天气系统

### WeatherManager (Autoload)

```
WeatherType: Clear(-1), Rain(0), Snow(1), Sandstorm(2)
WeatherIntensity: Light(0.4), Moderate(0.7), Heavy(1.0)
```

季节权重表（Clear/Rain/Snow/Sand）：
- Spring: 75/22/0/3
- Summer: 82/15/0/3
- Fall: 70/27/0/3
- Winter: 65/15/17/3

地形过滤：雪只在冬季+雪地，沙尘暴只在沙漠。天气惯性：当前天气有额外 25-35% 维持概率。

```csharp
WeatherManager.SetWeatherImmediate(WeatherType.Rain, WeatherIntensity.Heavy)
WeatherManager.TransitionTo(WeatherType.Clear)
WeatherManager.TickWeatherCycle(season, elapsedHours)
WeatherManager.GetEffectiveIntensity() → float [0,1]
```

战斗集成：天气通过 `BattleContext.WeatherOverride` 传入战斗场景，影响 BGM 变体选择和地面特效。

## 22. 声望系统

### ReputationTracker

```
范围: -100 ~ +100（每国独立）
等级: Hated(-100~-75) / Hostile(-74~-50) / Unfriendly(-49~-25) / Neutral(-24~+24)
      / Friendly(+25~+49) / Honored(+50~+74) / Exalted(+75~+100)
```

影响：
| 等级 | 城镇准入 | 招募倍率 | 价格倍率 | 特殊 |
|------|----------|----------|----------|------|
| Hated | 拒绝 | 0x | - | 守军攻击 |
| Hostile | 拒绝 | 0x | - | - |
| Unfriendly | 允许 | 0.5x | 1.2x | - |
| Neutral | 允许 | 1.0x | 1.0x | - |
| Friendly | 允许 | 1.0x | 0.9x | - |
| Honored | 允许 | 1.2x | 0.85x | 高级招募 |
| Exalted | 允许 | 1.5x | 0.8x | 领主任务 |

封地条件：声望 >= 60

## 23. 封地系统

### FiefManager

```csharp
FiefManager.RequestFief(factionId, name, centerHex, worldPos) → FiefData?
FiefManager.Build(fief, buildingType, hexIndex, edgeDir, gold) → int cost
FiefManager.ProcessAllFiefs() → FiefDailyReport (每日结算)
```

建筑类型：LordManor, Farm, Mine, Barracks, Wall, Tower, Market, Workshop

## 24. 士气系统

### MoraleSystem (静态工具类)

```
范围: -60 ~ +40
阈值: High(+20) / Low(-20) / Broken(-40) / Rout(-60)
```

事件触发：
- 友方阵亡: -6~-10（距离衰减）
- 敌方击杀: +8~+10（距离衰减）
- 被暴击: -5
- 被包夹: -3
- 单方损失过半: -15
- 英雄光环: +3（CHA 修正范围内）

效果：
| 等级 | 暴击加成 | 失误率 | AC修正 | 命中修正 |
|------|----------|--------|--------|----------|
| 高昂 | +20% | 0% | 0 | +2 |
| 正常 | 0% | 0% | 0 | 0 |
| 低落 | 0% | 20% | 0 | -1 |
| 崩溃 | 0% | 40% | -2 | -2 |
| 溃逃 | 0% | 100% | -2 | -4 |

## 25. 武器精通系统

### WeaponMastery

9 条精通轨道 = 3 伤害类型(Slash/Pierce/Crush) × 3 重量(Light/Medium/Heavy)

```
等级 1-10，每级 +10% 伤害加成
XP 需求: Lv2=100, Lv3=300, Lv4=600, ..., Lv10=4500
同类型同重量的所有武器变体共享精通进度
```

## 26. 坐骑系统

### MountData

| 坐骑 | 速度 | HP | 冲锋加成 | 特殊 |
|------|------|-----|----------|------|
| 驮马 | +1 | 15 | 0% | 高负重 |
| 军马 | +2 | 20 | +25% | 可骑射 |
| 战马 | +3 | 25 | +50% | 免疫恐惧 |
| 精灵角鹿 | +2 | 18 | +25% | 森林穿越/潜行不中断 |
| 矮人战熊 | +1 | 30 | +25% | 额外1d4伤害/免疫恐惧 |
| 狼 | +3 | 12 | +25% | 包夹加成 |

## 27. Buff/状态效果系统

### BuffSystem (Core, 静态)

```csharp
BuffSystem.Apply(target, buffId, duration, sourceUnitId)
BuffSystem.Remove(target, buffId)
BuffSystem.TickAll(target) → int totalDamage
BuffSystem.ResolveStatModifiers(target, stat) → StatResolveResult
BuffSystem.HasBuff(target, buffId) → bool
```

多乘区属性计算：`result = (base + flatBonus) * (1 + increased%) * moreMultiplier * finalMultiplier`

### StatusEffectData

13 负面: Poison, Burning, Freeze, Fear, Silence, Blind, Stun, Bleed, Slow, Root, Charmed, Confused, Wet
7 正面: Bless, Shield, Haste, Regen, Invisibility, Phantom, TempHp

元素交互：Burning+Freeze 互消，Wet+Lightning 增伤

## 28. RPGRuleEngine (Core, 静态)

```
120 级体系
属性修正: StatMod = floor(sqrt(score / 2))
专精加值: Prof = floor(sqrt(level)) + 1
HP 公式: MaxHP = 10 + floor(sqrt(CON/4)) * Level
AP 公式: MaxAP = BaseAP + DEX_Mod + CON_Mod/2
经验公式: 每级需 300 + (level-1)*200 XP
CR 映射: CR = floor(level / 6)
```

```csharp
RPGRuleEngine.RollDice(count, sides)
RPGRuleEngine.RollD20()
RPGRuleEngine.GetStatModifier(score)
RPGRuleEngine.GetProficiencyBonus(level)
RPGRuleEngine.CalculateMaxHp(baseHp, con, level)
RPGRuleEngine.CalculateHitChance(attackBonus, targetAc, advantage, disadvantage)
RPGRuleEngine.MakeSave(abilityScore, prof, advantage, dc)
```

## 29. 大地图寻路

### HexOverworldAStar (单层)

- PriorityQueue 优化 A*
- 地形消耗权重 + 道路偏好
- 可配置穿越不可通行地形（生成阶段用）

### ChunkAStar (双层)

- Layer 1: Chunk 级粗粒度引导
- Layer 2: Tile 级精确寻路
- 路径缓存（64 条，5 秒 TTL）
- Land/Sea 双导航模式
- 跨 chunk 边界目标自动寻路到边界

### MovementSpeedComponent

8 个速度修正因子：地形 × 季节 × 昼夜 × 负重 × 坐骑 × 技能盘 × 天气 × ZoC

## 30. 战争迷雾

### ChunkFogOfWar

```
三级状态: Unexplored / Revealed / InVision
粒度: Chunk 级（16x16 tiles）
种族初始揭示: 按种族出生区域预揭示
```

```csharp
ChunkFogOfWar.UpdateVision(activeChunkCoords)
ChunkFogOfWar.GetState(chunkCoord) → ChunkFogState
ChunkFogOfWar.RevealArea(center, radius)
ChunkFogOfWar.GetExplorationProgress(totalChunks) → float
```

## 31. 大地图渲染

### HexOverworldRenderer3D

- MultiMeshInstance3D 按地形类型分桶
- 世界坐标 UV shader（消除接缝）+ stochastic tiling
- 增量加载（chunk 加载时调用 LoadTiles）
- 像素→3D 缩放: 1.0/156.0

### OverworldPropRenderer

- MultiMesh billboard QuadMesh 按 prop 类型分桶
- LOD: 相机 ortho size > 20 时隐藏
- 增量加载 + OverworldPropScatter 生成规则
- 性能: 10 万 prop 仅 ~30 个 MultiMesh 节点

## 32. 角色起源系统

### origin_questions.json

每种族 4 个问题，每问题 4 个选择：
- 属性修正: +2/-1/-1 分配
- 物品奖励: 武器/护甲/消耗品
- 插画 ID: 用于 UI 展示

种族: Human, Elf, Dwarf, HalfOrc, HalfElf

## 33. 对话系统（设计阶段）

节点类型：DialogueNode / ChoiceNode / ResultNode

条件系统：属性检定、声望阈值、势力关系、物品检查、种族检查、技能盘检查、任务状态、对话标记

效果系统：属性检定(DC)、设置标记、给予/移除物品、调整声望、触发任务/战斗、招募英雄、解锁商品

## 34. OverworldParty

```csharp
OverworldParty : Node2D, IOverworldMapEntity
├── Roster: PartyRoster        队伍名册
├── Inventory: PartyInventory  背包
├── SpeedComponent: MovementSpeedComponent  速度计算
├── CurrentShip: ShipData?     船只（海上航行）
├── ChunkAStar: ChunkAStar     寻路
└── CharacterView2D            队长视觉
```

移动：Catmull-Rom 样条插值 + 路径简化（二分法视线检测）+ 跨 chunk 续航

## 35. UIFactory

```csharp
UIFactory.CreatePanel(minSize, bg, border, margin)
UIFactory.CreateCard(minSize, hoverable)
UIFactory.CreateButton(text, minSize, actionName)  // 自动挂载点击/悬停音效
UIFactory.CreateIconButton(iconText, tooltip, size)
UIFactory.CreateActionButton(label, shortcut, icon, color)
UIFactory.CreateTitleLabel(text) / CreateBodyLabel(text) / CreateMutedLabel(text)
UIFactory.CreateProgressBar(type, minSize)  // HP/Mana/XP
UIFactory.CreateRichText(minSize)
UIFactory.CreateSeparatorH() / CreateSeparatorV()
```

所有按钮自动附加 `ui_click` / `ui_hover` 音效。面板自动附加 `ui_panel_open` / `ui_panel_close` 音效。


## 36. 战斗 UI (CombatUI)

```
CombatUI : CanvasLayer
├── TurnOrderBar          回合顺序条
├── EnemyInfoPanel        敌方信息面板
├── HitPreviewTooltip     命中预览浮窗
├── TerrainTooltip        地形信息浮窗
├── BattleLogPanel        战斗日志
├── SpellSelectionPanel   法术选择
├── RadialMenu            径向菜单
├── UnitInspectPanel      单位检视
├── BottomPanel           底部信息面板
│   ├── CharacterAvatar   角色头像
│   ├── HP/MP/AP 条
│   ├── 武器槽 (主/副)
│   └── 快捷技能槽 ×10
└── ESC 菜单 / 速度按钮
```

信号：ActionSelected, SpellSelected, ActionHovered, EnemyHoveredInPanel, UnitSelectedInList

## 37. 投射物系统

三层架构：

| 层 | 类 | 职责 |
|----|----|----|
| 数据 | `ProjectileData` (Core) | 纯数据：origin/target/type/speed/arcHeight |
| 逻辑 | `ProjectileSystem` (Frontend) | 计算飞行时间，发布 EventBus 事件，延时触发命中 |
| 表现 | `ProjectileView` (Frontend) | Sprite3D billboard，抛物线轨迹，旋转动画，对象池复用 |

投射物类型：arrow, crossbow_bolt, throwing_knife, throwing_axe, fireball, magic_bolt, ice_shard, lightning

## 38. 装备能力系统

### EquipmentAbility (抽象基类)

钩子方法：
```csharp
OnDealDamage(DealDamageContext)   // 攻击方装备触发
OnTakeDamage(TakeDamageContext)   // 防御方装备触发
GetMaxHpMultiplierBonus()         // HP 百分比加成
GetFlatDamageReduction()          // 固定伤害减免
GetSpellDcBonus()                 // 法术 DC 加成
GetShopDiscountMultiplier()       // 商店折扣
GetFlankingHitBonus()             // 包夹命中加成
```

已注册能力：life_steal, thorns, extra_hp_percent, damage_reduction, spell_dc_bonus, shop_discount, recruit_discount, flanking_bonus

```csharp
EquipmentAbilityRegistry.Create("life_steal", 0.15f) → LifestealAbility
```

## 39. 控制区系统 (ZoneOfControl)

```
POI 类型 → ZoC 半径: Castle=4, Town=3, Village/Settlement=2, Lair=1
移速惩罚: 0.7x（技能减轻后 0.85x）
寻路代价乘数: 1.43x
```

```csharp
ZoneOfControlManager.Initialize(pois)
ZoneOfControlManager.IsInHostileZoc(q, r, playerFaction) → bool  // O(1)
ZoneOfControlManager.GetZocTiles(poiId) → HashSet<Vector2I>
```

## 40. 围攻系统 (SiegeProcessor)

```csharp
SiegeProcessor.ProcessSieges(entities, signals)
  → 围攻 2 天后结算（OverworldAIResolver.ResolveSiege）
  → 胜利: 占领 POI，改变阵营
  → 失败: 实体逃跑或被消灭

SiegeProcessor.ProcessReinforcementChecks(entities, pois, signals)
  → 最近领主 800px 内前往回援

SiegeProcessor.ProcessRecruitment(entities)
  → 领主在己方城堡每 tick +2 兵力（上限 80）
```

## 41. 特殊角色生成器

```csharp
SpecialCharacterGenerator.GenerateAll(nations, territories, pois, worldTileCount)
```

- 领主：主要国家 3-5 个，小势力 1-2 个，等级 30-80，绑定 POI
- 冒险者：12+ 个，4 类型（新手 40%/老手 30%/精英 15%/亡命 15%）
- 种族名池：人类/精灵/矮人/兽人各 8 个姓 + 8 个名

## 42. 国家系统

### NationConfig

```
字段: Id, DisplayName, Race, IsMajorNation, PreferredBiomes,
      PopulationScale, PoiDensityPer1000Tiles, RecruitPool,
      TradeGoods, BuildingStyle, BaseMilitaryPower, EncounterPool,
      BaseDangerLevel, InitialDiplomacy
```

默认 9 国：2 人类王国 + 精灵 + 矮人 + 兽人 + 4 小势力(哥布林/狗头人/牛头人/暗影教团)

### NationAllocator

Voronoi 式同步生长：每国从偏好生态区中心开始，同时向外扩展，先到先得。两阶段（配额制 + 无限制填充）。

## 43. 地形代价表 (TerrainCostTable)

| 地形 | 移动代价 | 可通行 | 速度因子 |
|------|----------|--------|----------|
| Road | 0.2 | 是 | 1.5x |
| Plains/Grassland | 1.0 | 是 | 1.0x |
| Forest/Taiga/Sand | 1.5 | 是 | 0.7x |
| Hills/Snow/Ice | 2.0 | 是 | 0.5x |
| DenseForest/Jungle/Swamp | 2.5 | 是 | 0.4x |
| ShallowWater/Bog | 3.0 | 是 | 0.3x |
| Mountain/DeepWater/River | 99.0 | 否 | - |

海上模式：DeepWater=0.8, ShallowWater=1.2, Sand=2.0(登陆), 其他陆地=99

## 44. 船只/海航系统

### ShipData

| 船只 | 速度 | 容量 | 耐久 | 购买 | 租赁/天 |
|------|------|------|------|------|---------|
| 木筏 | 0.8x | 10 | 60 | 500g | 30g |
| 单桅帆船 | 1.2x | 30 | 120 | 2000g | 80g |
| 大帆船 | 1.0x | 80 | 200 | 8000g | 200g |

支持：购买/租赁/维修/耐久损耗

## 45. 调试控制台 (DebugConsole)

热键：` 切换，F5 刷新，/ 聚焦命令

内置命令：help, clear, copy, close, refresh, auto, interval

快捷按钮：
- 天气: clear/rain/snow/sand
- 时间: +1天/+7天/设置小时
- 速度: x1/x10
- 资源: 加金/加食物/治疗/升级
- 地图: 全揭示/切换迷雾/杀死全部/生成实体

分区数据提供者模式：场景注册 `RegisterSection(name, provider)`

## 46. GlobalState (Autoload)

```csharp
GlobalState : Node
├── Save: SaveContext           存档加载状态
├── WorldGen: WorldGenContext   世界生成参数（seed, size, race）
├── QuickCombat: QuickCombatContext  快速战斗配置
└── OriginContext: PlayerOriginContext  出身选择数据
```

访问：`Globals.State.WorldGen.Seed`

## 47. 加载屏阶段

| 类型 | 阶段 |
|------|------|
| NewWorld | 起源→山脉→河流→城镇→英雄→降临 |
| LoadSave | 溯源→重构→复苏→归来 |
| Combat | 对峙→推演→死斗 |
| QuickGame | 瞬息→塑形→跃迁 |
| QuickCombat | 集结→演武→开战 |

最小展示时长：NewWorld/QuickGame = 6 秒

## 48. 相机系统 (CameraBoundsController)

```csharp
CameraBoundsController.SetWorldBounds(aabb, pitchAngle, viewportAspect)
CameraBoundsController.ClampOrthoSize(currentSize) → float
CameraBoundsController.ClampPosition(pos, orthoSize, aspect) → Vector3
```

通用正交 3D 相机边界：自动计算最大缩小（刚好看到全地图），位置限制（视野不超出边界）。大地图和战斗场景共用。

## 49. 大地图 HUD (OverworldUI)

```
顶部栏: 日期 | 金币 | 食物 | 速度状态 | 士气 | 声望 | 季节 | 时间 | 地形 | 天气
底部栏: [军队I] [技能盘K] [任务J] [营地T] [领地F]
子面板: PartyPanel, TownUI, SkillTreeUI, QuestLog
```

信号：MenuOpened, PartyClicked, InventoryClicked, PanelDismissed

## 50. 物品数据加载 (ItemDataLoader)

```
数据源: res://BladeHexCore/src/Data/items/
├── weapons_melee_slash.json
├── weapons_melee_pierce.json
├── weapons_melee_crush.json
├── weapons_ranged_bow.json
├── weapons_ranged_crossbow.json
├── weapons_ranged_thrown.json
├── weapons_catalyst.json
├── armors.json
├── consumables.json
├── quivers.json
└── accessories.json
```

Mod 支持：`user://mods/items/` 下的 JSON 自动合并加载

加载结果：171 武器 + 19 护甲 + 10 消耗品 + 3 箭筒 + 5 饰品

## 51. 种族系统 (RaceData)

| 种族 | 属性修正 | 种族特性 |
|------|----------|----------|
| Human | 无 | 适应（每10级额外获取一个技能点） |
| Elf | 无 | 技艺（剑类和弓类命中+1，伤害修正x1.1） |
| Dwarf | 无 | 韧性（AC+1，生命值+20%） |
| HalfOrc | 无 | 狂暴（血量低于50%时伤害加20%），先攻+2 |
| HalfElf | 无 | 双重血统，社交天赋 |

JSON 驱动：`res://BladeHexCore/src/Data/character/races.json` + mod 支持

## 52. 法术数据 (SpellData)

```
8 学派: 塑能/防护/幻术/死灵/变化/附魔/预言/咒唤
8 环阶: 戏法(0环) ~ 7环
8 形状: 单体/射线/锥形/球形/线形/十字/自身/触碰
3 解析: 攻击检定/豁免/自动命中
6 豁免: STR/DEX/CON/INT/WIS/CHA
```

魔力消耗按环阶缩放：戏法=0, 1环=5, 2环=10, ..., 7环=35

## 53. 伤害类型体系

```
物理: Slash(斩), Pierce(刺), Crush(钝)
魔法: Magic(力场), Fire(火), Frost(冰), Lightning(雷)
```

影响：音效选择、护甲穿透加成（Crush 在 DR=0 时有额外加成）、投射物贴图、法术伤害类型


## 54. Lua 脚本系统

```
BladeHexCore/src/Scripting/
├── LuaScriptEngine.cs    Lua 运行时引擎（NLua 绑定）
├── LuaCombatAPI.cs       暴露给 Lua 的战斗 API
└── LuaUnitProxy.cs       单位数据代理（Lua 可读写）
```

用途：技能效果脚本化（`scripts/skills/*.lua`），允许策划不改 C# 即可添加新技能逻辑。

## 55. 单位渲染管线 (View/Unit)

```
View/Unit/
├── Unit.cs                    战斗单位节点（Node3D，持有 Model + View）
├── CharacterPresenter.cs      角色表现层（动画、装备渲染）
├── CharacterRenderNode.cs     渲染节点（Sprite3D 多层合成）
├── CharacterRenderBus.cs      渲染事件总线（装备变化→重绘）
├── CharacterView2D.cs         2D 角色视图（大地图/UI 头像用）
├── CharacterAvatarControl.cs  头像控件（UI 面板用）
├── EquipmentPlaceholderRenderer.cs  装备占位渲染
├── UnitPlaceholderRenderer.cs       单位占位渲染
├── PixelDraw.cs               像素绘制工具
├── Components/                组件（HP条、状态图标等）
└── Slots/                     装备分部位渲染配置
```

## 56. 环境特效层

```
View/Environment/
├── WeatherParticles2D.cs      2D 天气粒子（雨/雪/沙）
├── WeatherParticles3D.cs      3D 天气粒子
├── CloudLayer3D.cs            3D 云层
├── FogOverlay3D.cs            3D 迷雾覆盖
├── FogIllustrationLayer.cs    迷雾插画层
├── EnvironmentEffectsLayer.cs 环境特效聚合层
├── WindSystem.cs              风力系统（影响粒子方向）
└── CombatWeatherSetup.cs      战斗场景天气初始化
```

## 57. 战斗地图渲染

```
View/Map/ (战斗相关)
├── HexGrid.cs                 战斗六边形网格（Node3D）
├── HexCell.cs                 单个格子（占用、地形、高程、掩体）
├── HexCellMultiMeshBatcher.cs 格子 MultiMesh 合批渲染
├── CombatMaterialManager.cs   地形材质工厂与缓存
├── BattlePropRegistry.cs      战斗场景物体注册表
├── BattlePropRenderer.cs      战斗场景物体渲染
├── CoordConverter.cs          坐标转换（屏幕↔世界↔格子）
├── TerrainAtlas.cs            地形纹理图集
├── TerritoryOverlay.cs        领土覆盖层
├── RiverRenderer.cs           河流渲染
├── RoadRenderer.cs            道路渲染
└── MapLabelLayer.cs           地图标签层
```

## 58. 战斗辅助系统

| 类 | 职责 |
|----|------|
| `CombatResolver` | 攻击结算委托（收集 Node 数据→调用 CombatRuleEngine） |
| `CombatResultBuilder` | 构建战斗结果（存活者/阵亡/金币/XP） |
| `CombatSpeed` | 战斗动画速度控制（1x/2x/4x） |
| `CombatTextureLoader` | 战斗纹理预加载 |
| `DamageNumberPopup` | 伤害数字弹出动画 |
| `EnemyGenerator` | 快速战斗敌人生成 |
| `EnvironmentEventSystem` | 战斗环境事件（地震/毒雾/风暴） |
| `FacingSystem` | 单位朝向系统（影响反击/包夹） |
| `LineOfSight` | 视线检查（Frontend 包装 LosCore） |
| `LuaSkillBridge` | Lua 技能脚本桥接 |
| `MoraleSystem` | 士气系统 |
| `PassiveSkillResolver` | 被动技能解析 |
| `SceneDecorationPlacer` | 场景装饰放置 |
| `SkillEffectExecutor` | 主动技能执行 |
| `SkillRegistry` | 技能注册表 |
| `UnitViewPool` | 单位视图对象池 |
| `VFXManager` | 视觉特效管理 |

## 59. 技能盘详细结构

```
BladeHexCore/src/SkillTree/
├── SkillNodeData.cs           节点数据（类型/区域/效果/前置）
├── SkillTreeData.cs           技能盘完整数据（节点图）
├── SkillTreeCoord.cs          六边形坐标系
├── CharacterSkillTree.cs      角色技能盘实例（已激活节点）
├── SkillTreeAllocator.cs      AI 自动分配技能点
├── ConstellationBuilder.cs    星座图构建器
├── NodeFiller.cs              节点效果填充
├── ClassTitleResolver.cs      职业称号解析（63 种组合）
├── CareerSkillData.cs         职业专属技能数据
└── CareerSkillRegistry.cs     职业技能注册表
```

## 60. 交互系统 (Interaction)

```
BladeHexCore/src/Interaction/
├── InteractionType.cs         交互类型枚举（15种）
├── InteractionOption.cs       交互选项数据
├── NPCProfile.cs              NPC 档案（种族/态度/对话树）
└── TownFacility.cs            城镇设施

InteractionType.Type:
  Attack, Talk, Trade, Leave, Rest, Train, Repair, Heal,
  Quest, Arena, Recruit, Intimidate, Bribe, Steal, Observe
```

## 61. 命名生成器家族

```
BladeHexCore/src/Character/
├── NameGenerator.cs           角色命名（种族×等级→称号）
├── FactionNameGenerator.cs    势力命名
├── GeographicNameGenerator.cs 地理命名（河流/山脉/平原）
└── POINameGenerator.cs        POI 命名（城镇/村庄/据点）
```

## 62. Headless 战斗模拟

```
BladeHexCore/src/Combat/Headless/
├── HeadlessCombatLoop.cs      无 GUI 战斗循环
└── HeadlessAi.cs              简化 AI（用于批量模拟）
```

用途：`TEST_MODE=sim` 批量跑战斗平衡性测试，无需 Godot 渲染。

## 63. 大地图 POI 详细结构

### OverworldPOI (Resource)

```
类型: Town, Village, Castle, Port, Outpost, Tavern, Mine, Shrine,
      Settlement, Lair, Ruin, Landmark
属性: PoiName, Position, OwningFaction, Prosperity, Garrison,
      HasTavern, HasShop, HasBlacksmith, HasQuestBoard, HasBarracks
聚落种族: Goblin, Kobold, Minotaur, ShadowCult, Bandit, Robber, Pirate
巢穴类型: DragonLair, GolemForge, UndeadCrypt, BeastDen
领主性格: Aggressive, Defensive, Balanced, Expansionist
```

### POI 比例尺系统 (Strategic/Scale/)

```
POIBattlePresetRegistry.Resolve(poi) → POIBattlePreset
POIScaleTable.Get(scale) → ScaleEntry { BattleSize, GarrisonRange }
```

## 64. 大地图过渡动画

```
View/Transitions/
├── CombatTransition.cs          战斗切换过渡（淡出→加载→淡入）
└── CombatEntranceTransition.cs  战斗入场动画（底部面板/回合条滑入）
```

## 65. UI 子目录结构

```
View/UI/
├── Character/     角色面板（属性/装备/技能）
├── Combat/        战斗 UI（CombatUI, TurnOrderBar, BattleLog, RadialMenu...）
├── Common/        通用组件
├── Global/        全局菜单（GameMenuManager, SettingsPanel）
├── Inventory/     背包/装备 UI（ItemGridWidget, EquipmentSlotView, ItemPopup）
├── Loading/       加载屏（LoadingScreen, LoadingPhaseData, TipsDisplay）
├── MainMenu/      主菜单（MainMenu, NewGamePanel, QuickGamePanel）
├── Minimap/       小地图（MinimapPanel, CombatMinimapPanel）
├── Overworld/     大地图面板（TownPanel, SmithyPanel, ArenaPanel, ...全部 POI 面板）
├── Quest/         任务 UI（QuestBoard, QuestLog）
└── Shaders/       UI shader（模糊/发光/渐变）
```

## 66. 海上遭遇表

```csharp
SeaEncounterTable.Roll(playerLevel, distanceTraveled) → EncounterData?
```

海上遭遇类型：海盗袭击、海怪、暴风雨、漂流物、商船

## 67. 战斗数据桥梁

```
EntityCombatBridge.GetDeployment(entity, isAttacker) → BattleUnitDeployment[]
EntityCombatBridge.ApplyBattleOutcome(entity, outcome)
EntityCombatBridge.GetEncounterCR(entity) → float

POICombatBridge.GenerateDefenseDeployment(poi) → BattleUnitDeployment[]
```

## 68. 关键接口

| 接口 | 位置 | 用途 |
|------|------|------|
| `IOverworldContext` | Frontend | 大地图场景对外暴露的数据接口 |
| `IOverworldMapEntity` | Frontend | 大地图上可显示的实体 |
| `ICombatSceneAdapter` | Frontend | 战斗场景适配器 |
| `IWorldStage` | Core | 世界生成阶段接口 |
| `ITimeProvider` | Core | 时间源接口（EconomyManager 实现） |
| `IProjectileSystem` | Core | 投射物系统接口 |
| `ITickScheduler` | Frontend | 延时调度接口 |
| `IBattleField` | Core | 战场抽象（Headless 用） |
| `IFightable` | Core | 可战斗单位抽象 |
| `IRandomSource` | Core | 随机数源（可注入确定性种子） |
| `ISiegeSignals` | Core | 围攻事件信号接口 |


## 69. 营地系统 (CampSystem)

```csharp
CampSystem.Rest(roster, ref food, partySize) → CampResult { Success, HoursElapsed, Message }
```

野外扎营：消耗食物恢复 HP，推进时间。快捷键 R 触发。

## 70. 食物系统 (FoodSystem)

食物消耗与补给逻辑，与 EconomyManager.Food 联动。每日自动消耗，食物耗尽时队伍受惩罚。

## 71. 工资系统 (WageSystem)

```csharp
WageSystem.CalculateDailyWage(roster) → int
```

每日自动从金币扣除队伍工资（DailyWage），金币不足时士气下降。

## 72. 装备词缀系统 (EquipmentAffix)

装备随机词缀生成：前缀/后缀影响属性加成。与 EquipmentGenerator 配合，按稀有度决定词缀数量。

## 73. 伤害穿透表 (DamagePenetrationTable)

```csharp
DamagePenetrationTable.GetPenetration(damageType, armorType) → float
```

不同伤害类型对不同护甲类型的穿透率。Crush 对重甲有额外穿透。

## 74. 战斗统计 (CombatStats)

```csharp
CombatStats.GetMaxHp(unit) → int
CombatStats.GetMaxMana(unit) → int
CombatStats.GetAc(unit) → int
CombatStats.GetAttackBonus(unit) → int
CombatStats.CanCastSpells(unit) → bool
```

汇总 UnitData + 装备 + Buff + 技能盘的最终战斗属性。

## 75. 世界事件引擎 (WorldEventEngine)

```
Strategic/WorldEvents/
```

大地图随机事件系统：商队到达、掠夺队生成、POI 繁荣度变化、季节性事件。

## 76. 每日决策处理器 (DailyDecisionProcessor)

处理所有 AI 实体的每日决策：领主巡逻/围攻/招募、掠夺队目标选择、商队路线。

## 77. 移动处理器 (MovementProcessor)

处理大地图实体的逐帧移动：路径跟随、碰撞检测、到达目标回调。

## 78. 背包/物品栏 UI

```
View/UI/Inventory/
├── ItemGridWidget.cs        网格背包控件
├── GridInventoryView.cs     网格背包视图
├── EquipmentSlotView.cs     装备槽视图
├── ItemPopup.cs             物品详情弹窗
├── ShopGridView.cs          商店网格视图
├── DragController.cs        拖拽控制器
└── DragGhost.cs             拖拽幽灵
```

## 79. 小地图系统

```
View/UI/Minimap/
├── MinimapPanel.cs          大地图小地图
├── MinimapPanelBase.cs      小地图基类
├── MinimapController.cs     小地图控制器
└── CombatMinimapPanel.cs    战斗小地图
```

## 80. 主菜单系统

```
View/UI/MainMenu/
├── MainMenu.cs              主菜单（新游戏/快速游戏/快速战斗/设置/退出）
├── OriginSelect.cs          出身选择界面
└── QuickCombatSetup.cs      快速战斗配置
```

## 81. 消耗品系统

```csharp
ConsumableData : ItemData     消耗品数据（治疗/增益/投掷）
ConsumableManager             消耗品使用管理（战斗中使用消耗品）
```

类型：HealingPotion, ManaPotion, Antidote, ThrowingItem, Buff, Food

## 82. 特质系统 (TraitData)

角色特质：在 CharacterGenerator 中随机分配，影响属性和行为。

## 83. 完整类清单（按命名空间）

共 **340+** 个 C# 类文件，分布在 9 个 Core 目录 + 13 个 Frontend 目录中。

所有系统均已在上述章节中覆盖。
