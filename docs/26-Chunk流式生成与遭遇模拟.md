# 26-Chunk 流式生成与遭遇模拟系统

## 概述

大地图采用 **Chunk 流式生成** — 以玩家为中心，只生成周围 chunk 的瓦片数据，远离的 chunk 卸载。不模拟全局大地图。

遭遇采用 **条件触发式模拟** — 玩家进入区域时按条件判定遭遇（敌人/事件/资源），不需要 AI 实体常驻地图。

## 设计动机

| 问题 | 原架构 | 新架构 |
|---|---|---|
| 内存占用 | 64×48 = 3072 瓦片常驻 | 只加载玩家周围 3~5 个 chunk |
| AI 开销 | 所有实体每帧 tick | 按需触发，无全局 tick |
| 体验 | 开图即见全貌 | 探索感 + 未知感 |
| 存档 | 序列化整张地图 | 只序列化已生成 chunk + 世界种子 |

## Chunk 系统

### Chunk 定义

```
一个 Chunk = 16×16 六边形瓦片 (256 tiles)
全局坐标 = ChunkCoord × 16 + TileOffset
```

- **Chunk 坐标**：`(chunkQ, chunkR)` — 每个 chunk 在世界中的位置
- **世界坐标**：`(worldQ, worldR)` — 全局唯一的轴向坐标
- **转换**：`worldQ = chunkQ * 16 + tileQ`, `worldR = chunkR * 16 + tileR`

### Chunk 生命周期

```
[不存在] → 玩家接近 → [生成中] → [活跃] → 玩家远离 → [休眠] → 存档写入 → [卸载]
                         ↑                                         ↓
                         └────────── 玩家返回时从存档恢复 ──────────┘
```

### 加载范围

```
以玩家所在 chunk 为中心，加载半径 2（共 19 个 chunk）:

  × × ×
 × × × ×
× × P × ×   P = 玩家 chunk
 × × × ×
  × × ×

生成范围 = 半径 2（正六边形区域）
活跃范围 = 半径 1（9 个 chunk）— 每帧 tick
```

### Chunk 生成流程

```
1. 判定 chunk 是否已生成（查 ChunkIndex）
2. 未生成 → 调用 ChunkGenerator.Generate(chunkQ, chunkR, worldSeed)
3. 用 FastNoiseLite 基于全局坐标生成地形
4. 区域判定 → 根据坐标分配 RegionName
5. 河流/道路 → 跨 chunk 的线性特征，用全局种子确定性生成
6. 遭遇槽位计算 → 根据 Region.DangerLevel 分配遭遇概率
7. 存入 ChunkManager 的活跃池
```

## 地形生成（确定性）

### 噪声参数（全局一致）

- 使用与当前 `HexOverworldGenerator` 相同的噪声参数
- 每个 tile 的 `(worldQ, worldR)` 直接喂给 FastNoiseLite
- **不依赖周围 tile 的结果** — 生成完全独立，无跨 chunk 依赖
- 河流/道路通过全局路径计算（确定性种子）

### 河流生成策略

河流在当前全图生成中是从高处到水边的路径。Chunk 模式下改为：

1. **全局河流规划**：世界初始化时，用种子确定性生成 N 条河流的起点和终点坐标
2. **Chunk 内河流片段**：当 chunk 被生成时，检查全局河流路径是否穿过该 chunk
3. **河流路径用 Bresenham 六角线算法**（`HexOverworldTile.CubeLine`）— 确定性，不依赖邻居 chunk

### 道路生成策略

同理河流：
1. **全局道路骨架**：POI 之间的连接路线（确定性计算）
2. **Chunk 内道路片段**：chunk 生成时，检查全局道路是否经过

## 遭遇模拟

### 遭遇触发与流式生成

遭遇机制已由静态标记完全升级为**动态实体遭遇**。遭遇槽（Encounter Slot）被填充后，并不会直接触发暗雷战斗，而是流式生成为活跃在地图上的游荡 `OverworldEntity`。

*   **遭遇判定因子**：
    *   `Region.DangerLevel`（区域危险等级）
    *   `PlayerLevel`（玩家等级，作为属性和等级缩放的基准）
    *   `ChunkSeed`（chunk 确定性种子，用于生成遭遇怪的生态类型）
*   **防刷脸生成机制（Proximity Spawn Filtering）**：
    *   在加载 Chunk 遭遇槽并驱动生成时，检测槽位坐标与玩家实体的距离。
    *   如果距离玩家过近（**小于 3 格 / 400 像素**），为避免实体在玩家视野内凭空刷脸，该生成将被拦截并保持 `Available` 状态，直到玩家远离该区域后才会自然生成。
*   **生态野怪实体化**：
    *   流式生成将直接向场景管理器注册真正的游荡实体（非静态标记）。
    *   生成的野怪将被赋予特定的性格（狂暴 `Rampage`、本能 `Instinct`、领地 `Territorial`），这决定了它们的 AI 决策逻辑。
    *   实体拥有大地图战力，并在 `GetEncounterConfig()` 中携带专属的生态遭遇怪物数组配置。

### 统一的战力折算算法

为了支持对称式 AI 决策（大地图 AI 评估玩家以及其他实体的威胁性），系统提取了统一的战力算法：
1.  **玩家队伍战力**：
    *   由 `PartyRoster.CalculateCombatPower()` 算定。
    *   依据队伍内出战且未受伤（HP > 0 且未处于 Wounded 状态）的成员等级之和乘以 `1.5f` 折算，底限为 `10.0f`。
2.  **普通 AI 实体战力**：
    *   由 `OverworldEntity.CalculateBaseCombatPower()` 算定。
    *   根据实体类型加权系数（如巨兽、领主、普通小队）结合基础战力乘以相应系数折合。

---

## 实体与 AI 决策系统（对称式通用决策）

### 玩家投影机制（Player Entity Projection）

为了在统一的 AI 决策网络中进行无差别的对称计算，系统实现了**玩家大地图投影**：
*   在 `OverworldEntityManager` 的 `LoadWorld()` 中率先注入一个内存级常驻的 `PlayerEntity` 投影。
*   玩家实时移动时，通过 `UpdatePlayerPosition()` 同步位置并更新 `EntitySpatialIndex` 空间索引。
*   每帧实时计算玩家队伍战力并同步至 `PlayerCombatPower`，这使得玩家在大地图上被野怪和领主完全等同于一个普通的 `OverworldEntity` 处理。
*   **防存档污染**：该投影实体属于内存常驻，由 `SaveManager` 在序列化实体列表时显式进行过滤（`if (entity == entityMgr.PlayerEntity) continue;`），不写入物理存档。

### 对称 AI 决策 network

所有大地图上的实体（包括生成的野怪、领主军队、商队等）均通过统一的 `EntityBehaviorEvaluator` 进行决策，不需要针对玩家编写特例逻辑。

*   **性格修正决策**：
    *   AI 定期（每 Tick）对周围感知范围内的实体执行 `EvaluateAll()`。
    *   结合自身性格修正系数（`Rampage` 极易发起攻击，`Instinct` 关注力量差，`Territorial` 对领地侵入敏感）和战力对比。
    *   决策出 `AIState.Chasing`（追击）、`AIState.Fleeing`（逃跑）或 `AIState.Patrolling`（巡逻）。
*   **物理寻路与避障追逃**：
    *   **避障追击**：若决策为追击，AI 共享 `BuildChasePath` 直线步进避障寻路追随目标位置。
    *   **避障逃跑**：若决策为逃跑，AI 计算反向避障向量逃离威胁。
*   **统一交战闭环**：
    *   所有大地图碰撞，无论是玩家遭遇野怪，还是玩家主动拦截领主，均**严禁暗雷或直接拉入战斗**。
    *   当玩家与 AI 实体碰撞时，必须统一唤起“实体交互面板”（Interaction Panel）。玩家在面板中选择“交互/战斗”后，由面板内指令触发战斗切入，完成交战闭环。

---

## 模块变更清单

### Core (BladeHexCore)

| 文件 | 操作 | 说明 |
|---|---|---|
| `Strategic/OverworldEntity.cs` | **扩展** | 引入 `TempEncounterEnemies` 生态定制，以及静态 `CalculateBaseCombatPower` 战力计算 |
| `Strategic/Party/PartyRoster.cs` | **扩展** | 实现 `CalculateCombatPower()`，提供玩家当前出战队伍等级加权战力 |
| `Strategic/Overworld/EntityBehaviorEvaluator.cs` | **重构** | 公开性格修正及阈值判定，供 Spawner 执行对称 AI 状态评估 |
| `Strategic/Encounter/EncounterEntitySpawner.cs` | **重构** | 实现遭遇槽的流式生物生成与 400px 防刷脸 Proximity 拦截，并驱动统一的决策状态更新 |

### Frontend (BladeHexFrontend)

| 文件 | 操作 | 说明 |
|---|---|---|
| `View/Strategic/OverworldEntityManager.cs` | **扩展** | 内存常驻注入 `PlayerEntity` 投影，在位置更新时同步空间索引，并在保存序列化时实施过滤 |
| `Scenes/overworld2d/OverworldScene2D.Entities.cs` | **扩展** | 每帧帧更新处实时计算玩家队伍战力并同步至 `PlayerEntity`，保证决策的一致性 |
| `Scenes/overworld2d/OverworldScene2D.World.cs` | **重构** | 当 chunk 加载时触发遭遇槽，在 400px 范围外生成游荡的生态野怪，随机分配性格并注册渲染 |
| `View/Data/SaveManager.cs` | **修改** | 序列化 Entities 时增加过滤，防止内存中的 `PlayerEntity` 投影被写入物理存档 |

---

## 遭遇实体生命周期数据流

```
地图加载/生成:
  玩家移动到新 Chunk -> Chunk 加载
    -> 加载遭遇槽位（Encounter Slots）
    
每日/Tick 驱动:
  EncounterEntitySpawner 检测可用（Available）遭遇槽
    -> 检查该槽位与玩家的距离
    -> 距离 < 400px ? -> 拦截，保持 Available 状态（防刷脸）
    -> 距离 >= 400px ? -> 触发生成，槽位标记为 Triggered
         -> 依据 Chunk 生态模板提取野怪数据
         -> 创建真实的 OverworldEntity，设定战力与随机性格
         -> 注册到 EntityManager 与空间索引中
         
对称决策更新 (Tick):
  1. 计算玩家队伍战力 -> 同步给 PlayerEntity 投影 -> 同步空间坐标
  2. 对所有实体（包含玩家投影）执行 EntityBehaviorEvaluator.EvaluateAll()
  3. 各实体根据性格、距对方距离与战力对比，决定 Chasing/Fleeing/Patrolling
  4. 对于 Chasing，向目标实体位置执行 BuildChasePath 直线避障移动
  
碰撞与切入战斗:
  玩家实体与游荡野怪/领主在大地图碰撞
    -> 停止移动，唤起统一的实体交互面板
    -> 玩家在面板中点击“战斗”
    -> 卸载大地图，载入战斗场景进行战役
    -> 战斗结束，由 OverworldEntityManager 消费战果，移除被击败/消灭的实体
```

## POI 系统（不变）

POI（城镇/村庄/城堡/巢穴/外族聚落）保持现有数据模型不变。

调整点：
- POI 位置存储为**全局坐标**，不依赖特定 chunk
- POI 附近的 chunk 在生成时自动标记为"安全区域"
- 玩家靠近 POI 时加载对应 chunk（确保 POI 始终可达）

## 迷雾系统（不变）

- **Chunk 级别迷雾** — 每个 chunk 记录 `Unexplored / Revealed / Active`
- 已探索的 chunk 序列化存储（只存 chunk 坐标列表）

## 存档格式

```json
{
  "world_seed": 12345,
  "day": 15,
  "player_pos": { "x": 120.0, "y": -350.0 },
  "generated_chunks": {
    "0,0": { "encounter_state": [0,0,2,1,...], "modified_tiles": [...] },
    "1,0": { ... }
  },
  "pois": [ ... ],
  "player_party": { ... }
}
```

- 只存储已生成 chunk 的变更（差异），不存完整瓦片。
- 未生成 chunk = 每次用种子确定性重建。

## 向后兼容

- `HexOverworldTile` 数据模型完全不变。
- `OverworldPOI` 数据模型完全不变。
- `HexOverworldAStar` 在活跃 chunk 集合上工作（限制寻路范围）。
- 区域定义（霜冠山脉/银叶森林等）不变。
