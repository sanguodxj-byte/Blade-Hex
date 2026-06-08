## 大地图 AI 实体策略组件完备性检测报告

检测日期: 2026-06-06 | 项目: Blade&Hex | 范围: BladeHexCore/Strategic

---

### 一、已实现组件清单

| 组件 | 文件 | 职责 | 状态 |
|------|------|------|------|
| OverworldEntity | OverworldEntity.cs | 实体数据模型，8种类型、11种AI状态、LOD、序列化 | 完备 |
| EntityBehaviorEvaluator | Overworld/EntityBehaviorEvaluator.cs | 感知→敌对判定→战力评估→追/逃意图设定 | 完备 |
| DailyDecisionProcessor | Overworld/DailyDecisionProcessor.cs | 每日决策分派，按实体类型执行行为 | **有缺口** |
| BattleResolver | Overworld/BattleResolver.cs | 实体交互检测、视野感知、AI战斗结算 | 完备 |
| SiegeProcessor | Overworld/SiegeProcessor.cs | 围攻结算、回援检查、招募恢复 | 完备 |
| MovementProcessor | Overworld/MovementProcessor.cs | 帧级移动更新、追击加速 | 完备 |
| OverworldAIResolver | OverworldAIResolver.cs | AI间战斗/围攻/掠夺自动结算(静态类) | 完备 |
| EntitySpatialIndex | Spatial/EntitySpatialIndex.cs | 网格空间索引，O(k)邻域查询 | 完备 |
| EntityLodController | Spatial/EntityLodController.cs | LOD休眠/激活阈值控制(含迟滞) | 完备 |
| EncounterEntitySpawner | Encounter/EncounterEntitySpawner.cs | 玩家附近遭遇实体生成与追击AI | 完备 |
| OverworldEntityAITests | tests/Strategic/OverworldEntityAITests.cs | 52项测试用例，覆盖全子系统 | 完备 |

---

### 二、发现的缺口与问题

#### 严重 — BanditParty / RobberParty / PirateCrew 决策缺失

`OverworldEntity.EntityType` 枚举定义了 8 种实体类型，其中 `BanditParty`(山贼)、`RobberParty`(劫匪)、`PirateCrew`(海寇) 三种类型已在 `EncounterEntitySpawner` 中被生成（人类系占比约35%），且 `EntityBehaviorEvaluator` 已为它们做了策略分派（复用掠夺队阈值）。

**但** `DailyDecisionProcessor.DecideDailyAction` 的 switch 语句仅处理了 5 种类型：

```
Adventurer / RaidingParty / Caravan / EpicMonster / LordArmy
```

这意味着 BanditParty、RobberParty、PirateCrew 被 BehaviorEvaluator 设定了 Chasing/Fleeing 意图后，在 DecideDailyAction 中**没有任何分支执行实际移动**。它们会停留在原地，不会追击也不会逃跑，成为"活靶子"。

**影响范围**: 约35%的人类系遭遇实体行为异常。

**建议**: 在 `DecideDailyAction` 中为这三种类型添加 case，可复用 `DecideRaidingParty` 逻辑或新建一个通用的 `DecideBanditLike` 方法。

#### 中等 — AIStrategyEnum 定义后未被战略层引用

`AIStrategy.cs` 定义了 8 种战斗策略枚举：

```
Reckless / Cautious / Tactical / Instinct / Territorial / Cunning / Intimidate / Berserk
```

该枚举仅在 `Frontend/View/Combat/AI` (战术层战斗AI) 中有部分引用。在战略层（大地图）中完全未使用——`OverworldEntity` 没有策略字段，`DailyDecisionProcessor` 不做策略分派。

实体在大地图层面的"性格差异"完全依赖 `EntityBehaviorEvaluator` 中硬编码的阈值常量和领主的 `LordPersonality`。若需要让不同实体在大地图展现不同策略风格（例如 Cunning 型会伏击、Territorial 型不追出领地），需要打通此枚举与战略层的关联。

#### 低 — 优化文档建议未采纳项

对照 `docs/overworld-entity-ai-optimization.md` 的 8 条建议，当前状态如下：

| 建议 | 状态 | 说明 |
|------|------|------|
| 一、状态机层级化重构 | 未采纳 | AIState 仍为 11 值扁平枚举，无 IStateHandler |
| 二、DailyDecisionProcessor 职责拆分 | 未采纳 | ProcessDailyDecisions 仍同时承担 OnDayPassed + 决策 + 清理 |
| 三、随机数引擎注入 | 未采纳 | 各处理器仍各自持有 `static readonly Random` |
| 四、视野检测与决策解耦 | 部分采纳 | EntityBehaviorEvaluator 前置了感知管线，但 BattleResolver.CheckVisionDetection 仍独立运行 |
| 五、追击路径优化(有限前瞻A*) | 未采纳 | 仍为 Chunk A* 全路径寻路 + 休眠直线插值 |
| 六、LOD 阈值配置化 | 未采纳 | 5000/5500px 仍为硬编码常量 |
| 七、OverworldAIResolver 返回类型改进 | 未采纳 | 仍返回 `Godot.Collections.Dictionary`，字符串键访问 |
| 八、测试覆盖率缺口 | 已采纳 | 52 项测试用例覆盖了所有关键子系统 |

---

### 三、架构关系图

```
每日Tick流程:

EntityLodController.Update()        ← LOD 休眠/激活
        ↓
EntityBehaviorEvaluator.EvaluateAll() ← 感知→评估→追/逃意图
        ↓
BattleResolver.ProcessEntityInteractions()
  ├─ CheckEngagement()              → 接触(<100px) → 双方进入 Engaged 状态
  ├─ ResolveEngagedCombats()        → 交战满 2 天后结算 → OverworldAIResolver.ResolveBattle()
  └─ CheckVisionDetection()         ← 远距离(VisionRange)感知 → Chasing/Fleeing
        ↓
DailyDecisionProcessor.ProcessDailyDecisions()  ← 交战(Engaged)实体跳过
  ├─ DecideAdventurer()
  ├─ DecideRaidingParty()
  ├─ DecideCaravan()
  ├─ DecideEpicMonster()           ← 领地外优先返回
  ├─ DecideBanditLike()            ← BanditParty/RobberParty/PirateCrew
  └─ DecideLordArmy()
       ├─ DecideMarshal()
       └─ DecideFollower()
        ↓
SiegeProcessor.ProcessSieges()
SiegeProcessor.ProcessReinforcementChecks()
SiegeProcessor.ProcessRecruitment()
        ↓
MovementProcessor.TickMovement()    ← 帧级移动(交战实体强制停止, 追击1.1x加速)
```

---

### 四、结论

当前大地图 AI 实体策略组件的**核心管线完备**：感知、评估、决策、战斗结算、围攻、回援、招募、移动、LOD、空间索引、遭遇生成均已实现且有测试覆盖。

**最紧迫的缺口**是 `DailyDecisionProcessor` 遗漏了 BanditParty / RobberParty / PirateCrew 三种实体类型的决策分支，导致这些被 EncounterEntitySpawner 正常生成的实体在大地图上无法执行任何有意义的行为。这是一个低成本修复项，只需在 DecideDailyAction 的 switch 中补充对应 case 即可。
