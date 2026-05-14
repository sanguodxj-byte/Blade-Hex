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

### 遭遇触发条件

玩家进入一个新 chunk（或 chunk 从休眠恢复为活跃）时，检查该 chunk 的遭遇槽位：

```
遭遇判定 = f(
    Region.DangerLevel,     // 区域危险等级
    DistanceFromOrigin,     // 距出生点距离
    DaysElapsed,            // 已过天数
    PlayerLevel,            // 玩家等级
    ChunkSeed               // chunk 确定性种子
)
```

### 遭遇类型

| 类型 | 触发条件 | 效果 |
|---|---|---|
| 野生怪物 | 随机（森林/沼泽/山地高概率）| 战斗 → 战利品 |
| 敌对巡逻队 | 靠近敌对聚落的 chunk | 战斗或避开 |
| 商队事件 | 道路附近的 chunk | 交易/护送/抢劫 |
| 环境事件 | 特定地形（暴风雪/山崩/洪水）| 属性影响或路线改变 |
| 资源点 | 随机（森林→草药，山地→矿石）| 采集 |
| 悬念事件 | 低概率 | 特殊剧情/隐藏任务 |
| 无遭遇 | 安全区域（城镇附近）| 无事发生 |

### 遭遇强度缩放

```
EncounterLevel = clamp(
    PlayerAvgLevel + random(-1, +1) + Region.DangerModifier,
    1, 120
)

PartySize = baseByType + floor(EncounterLevel / 10)
```

### 遭遇生成流程

```
1. 玩家进入 chunk
2. 检查 chunk.EncounterSlot（0=无遭遇, 1=已触发, 2=可触发）
3. 如果可触发：
   a. 根据 chunk seed 确定性选择遭遇类型
   b. 根据 Region + PlayerLevel 计算遭遇强度
   c. 生成遭遇实体（仅视觉标记，不创建 OverworldEntity）
   d. 玩家接触标记 → 触发战斗/事件
4. 标记 chunk.EncounterSlot = 1（已触发，不重复）
```

## POI 系统（不变）

POI（城镇/村庄/城堡/巢穴/外族聚落）保持现有数据模型不变。

调整点：
- POI 位置存储为**全局坐标**，不依赖特定 chunk
- POI 附近的 chunk 在生成时自动标记为"安全区域"
- 玩家靠近 POI 时加载对应 chunk（确保 POI 始终可达）

## 实体系统（大幅简化）

### 删除

- `OverworldEntityManager` 的全局 AI tick 逻辑
- `OverworldEntity` 的 `AIState` 状态机（巡逻/移动/追击等）
- 商队/掠夺队/领主军队的持续移动模拟

### 保留

- `OverworldEntity` 作为遭遇数据模板（不作为运行时实体）
- `OverworldPOI` 数据模型完全保留
- `GetEncounterConfig()` 方法保留（用于遭遇生成）

### 新增

- `EncounterSpawner` — 根据 chunk 条件生成遭遇
- 遭遇实体为**静态标记**（地图上的感叹号/骷髅图标），不是移动的 AI 实体

## 迷雾系统

### 简化

当前 `FogOfWar` 使用像素网格 `[y,x]` 存储。改为：

- **Chunk 级别迷雾** — 每个 chunk 记录 `Unexplored / Revealed / Active`
- 不再需要像素级迷雾（chunk 未加载 = 自然不可见）
- 已探索的 chunk 序列化存储（只存 chunk 坐标列表）

## 模块变更清单

### Core (BladeHexCore)

| 文件 | 操作 | 说明 |
|---|---|---|
| `Map/HexOverworldGrid.cs` | **重构** | 拆分为 ChunkManager + ChunkData |
| `Map/HexOverworldGenerator.cs` | **重构** | 改为 ChunkGenerator（单 chunk 生成） |
| `Map/ChunkData.cs` | **新增** | 单个 chunk 的瓦片数据 + 遭遇槽位 |
| `Map/ChunkManager.cs` | **新增** | Chunk 加载/卸载/序列化管理 |
| `Map/ChunkGenerator.cs` | **新增** | 确定性单 chunk 地形生成 |
| `Map/RiverSkeleton.cs` | **新增** | 全局河流路径骨架 |
| `Map/RoadSkeleton.cs` | **新增** | 全局道路路径骨架 |
| `Strategic/WorldGenerator.cs` | **重构** | 改为增量式世界初始化 |
| `Strategic/EncounterSpawner.cs` | **新增** | 遭遇条件判定与生成 |
| `Strategic/FogOfWar.cs` | **简化** | 改为 chunk 级别迷雾 |
| `Strategic/OverworldEntity.cs` | **简化** | 移除 AI 状态机，保留数据模板 |
| `Strategic/OverworldAIResolver.cs` | **删除** | 不再需要全局 AI 解析 |

### Frontend (BladeHexFrontend)

| 文件 | 操作 | 说明 |
|---|---|---|
| `View/Strategic/OverworldEntityManager.cs` | **重构** | 简化为 EncounterRenderer |
| `View/Map/HexOverworldRenderer.cs` | **重构** | 改为 chunk 渲染（加载/卸载 chunk visual） |
| `View/Strategic/OverworldEnemy.cs` | **简化** | 静态遭遇标记，不移动 |
| `View/Strategic/OverworldTown.cs` | **保留** | POI 交互逻辑不变 |

## 数据流

```
游戏启动:
  WorldSeed → 全局骨架生成(河流/道路/区域/POI位置)
  
玩家移动:
  玩家位置 → ChunkManager.UpdateChunks(playerPos)
    → 计算需要加载的 chunk 列表
    → 卸载超出范围的 chunk
    → 对每个需要生成的新 chunk:
      → ChunkGenerator.Generate(chunkCoord, worldSeed)
      → 遭遇槽位计算
    → 对每个恢复的休眠 chunk:
      → 从存档读取

玩家进入新 chunk:
  → EncounterSpawner.CheckEncounters(activeChunks)
  → 在 chunk 内放置遭遇标记

玩家接触遭遇标记:
  → 读取 OverworldEntity.GetEncounterConfig()
  → 进入战斗场景

天数推进:
  → POI.OnDayPassed()（城镇恢复繁荣度等）
  → 不再 tick 全局实体
```

## 存档格式

```json
{
  "world_seed": 12345,
  "day": 15,
  "player_pos": { "q": 32, "r": 24 },
  "generated_chunks": {
    "0,0": { "encounter_state": [0,0,2,1,...], "modified_tiles": [...] },
    "1,0": { ... }
  },
  "pois": [ ... ],
  "player_party": { ... }
}
```

- 只存储已生成 chunk 的变更（差异），不存完整瓦片
- 未生成 chunk = 每次用种子确定性重建

## 向后兼容

- `HexOverworldTile` 数据模型完全不变
- `OverworldPOI` 数据模型完全不变
- `HexOverworldAStar` 在活跃 chunk 集合上工作（限制寻路范围）
- 区域定义（霜冠山脉/银叶森林等）不变
