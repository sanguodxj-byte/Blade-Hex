# 比例尺统一 — Design

## 设计目标

让 POI 在大地图上以**可变形 footprint** 占据 1~多 个 hex，把以下几件事统一到同一份数据：

1. **视觉占地**：城堡、城镇等大型 POI 在地图上视觉跨多 hex
2. **互动范围**：玩家走到 footprint 任一 hex 上即触发，鼠标悬浮显示 tooltip
3. **战斗采样**：进入 POI 战斗时，footprint 的所有 hex（含外围 1 圈过渡）参与战斗地图生成
4. **变形 POI**：港口城市的"外海一格"、山城的"山坡一格" 通过 footprint cell 的地形约束明确表达

**关键洞察**：永远规则形状不对——港口必须含海岸、山城必须含山坡。footprint 不只是大小，**还携带 cell 地形约束**。

## 尺度锚定（重要）

为了让"hex 数 ↔ 现实尺度"有明确语境，本 spec 钉死如下锚点：

| 锚点 | 值 | 依据 |
|------|----|----|
| **1 大地图 hex** | ≈ 250 m 直径 | 中世纪小村庄典型直径 = 1 hex；大型城市直径 1.5 km = 7 hex |
| **1 战斗 cell** | ≈ 20 m 直径 | 一个步兵班活动空间；中世纪会战阵线 300-800 m = 战斗地图 15-40 cell |
| **大地图典型尺寸** | 200×150 hex | 50 km × 37 km，约一个郡/公国领土范围 |
| **大地图 ↔ 战斗** | **不是** 物理放大 | 12.5:1 的尺寸差表示战斗是"该 hex 周边战术接触区"的抽象，不是 1:1 mapping |

中世纪城市直径参考，对应 footprint hex 数：

| POI 类型 | 现实直径 | hex 数 | Scale |
|---------|---------|-------|-------|
| 哨塔 / 农庄 / 矿场 / 路边设施 | 30-300 m | 1 | Tiny |
| 自然村 / 山贼营地 / 古墓 | 200-700 m | 3 | Small |
| 普通市镇 / 山城 / 哥布林大营 | 500 m - 1 km | 5 | Medium |
| 大型城市 / 都城 / 牛头人石堡 | 1-2 km | 7 | Large |

> ⚠️ "Hex 对应世界哪片大陆/地区"已暂存（用户决定）。本 spec 只解决"游戏内三层尺度内部一致"。

## 概念模型

### POI 不再是单点，而是 hex 集合

每个 POI 由以下数据描述：

- `Position`：中心 hex 的像素坐标（用于 sprite 锚点 / 名字标签）
- `Footprint`：一个 `FootprintCell[]` 数组，描述每个占用 hex 的 axial 偏移与地形约束

```csharp
/// <summary>
/// Footprint cell 的地形约束 —— 仅用于世界生成选址。
/// 视觉差异 / 战斗采样 由其他系统处理，本枚举不掺杂。
/// </summary>
public enum FootprintCellRole
{
    Any,            // 无约束 — 任何可建造陆地（普通 cell）
    CoastalDock,    // 必须 ShallowWater 或邻接陆地的 DeepWater
    RiverDock,      // 必须含 River
    MountainSlope,  // 必须 Hills 或 Mountain
    ForestEdge,     // 必须 Forest 或 DenseForest
}

public readonly struct FootprintCell
{
    public Vector2I Offset;               // axial 偏移（中心是 (0,0)）
    public FootprintCellRole Role;        // 选址地形约束
    public string? VisualSpriteId;        // 可选：该 hex 渲染哪个建筑 sprite（"city_wall"/"dock"），不影响地形或选址
}
```

> **注意**：Role 是大地图 hex 层的「地形约束」，不是战斗 cell 概念。
> 战斗地图采样直接读 `tile.Terrain`，不读 Role。
> 视觉差异（城墙/码头建筑）走 `VisualSpriteId`，与 Role 解耦。

中心 hex 用 `POI.Position` 作为 sprite 锚点，footprint 内其他 hex 在逻辑上平等，不再单独标 Center / Annex。

### Footprint 模板：声明式表达"这个 POI 长什么样"

```csharp
public sealed class FootprintTemplate
{
    public string Name;              // "port_city_3", "mountain_castle"
    public FootprintCell[] Cells;    // 占用偏移 + 角色
    public int RequiredCellCount;    // 至少需要几个 cell 满足约束才能落点（< Cells.Length 表示部分可选）
}
```

**例子**：

```csharp
// 港口城市：3 陆 1 海，海格在任一方向（旋转匹配）
var portCity = new FootprintTemplate("port_city_3", [
    new(( 0,  0), Any),                                  // 主体（陆地）
    new(( 1,  0), Any),                                  // 附属陆地
    new(( 0,  1), Any),                                  // 附属陆地
    new((-1,  1), CoastalDock, "dock"),                  // 海岸格 + 码头建筑
], 4);

// 山城：1 山脚 + 2 山坡
var mountainCastle = new FootprintTemplate("mountain_castle", [
    new(( 0,  0), Any),
    new(( 1, -1), MountainSlope),
    new(( 0, -1), MountainSlope),
], 3);

// 普通村庄：1 中心 + 2 附属（无地形约束）
var simpleVillage = new FootprintTemplate("village_3", [
    new(( 0,  0), Any),
    new(( 1,  0), Any),
    new(( 0,  1), Any),
], 3);

// 哨塔：单格
var solo = new FootprintTemplate("solo", [
    new(( 0,  0), Any),
], 1);
```

### 旋转匹配

Footprint 在世界生成时尝试 6 个 hex 旋转方向，找到第一个满足所有约束的方向后落点。这样港口城市能朝任意方向延伸出海格，不需要每个方向单独写一个模板。

### Size Hint：仍保留 4 档作为参数派生源

Footprint 形状由模板决定，但视觉/互动/战斗参数仍按 4 档 `POIScale` 派生（避免数据爆炸）：

| Scale | 典型 cell 数 | 现实直径 | Marker size | 灯光 Range / Energy | 战斗 hex 半径 N | 战斗 cell 数 | 战斗实际宽度 (1cell=20m) | Min Spawn Dist (hex) |
|-------|-------------|---------|-------------|----------------------|---------------|------------|-------|--------|
| **Tiny** | 1 | ~250 m | 0.35 | 1.5 / 0.4 | 7 | 169 | 300 m | 1 |
| **Small** | 3 | ~500 m | 0.45 | 2.0 / 0.6 | 8 | 217 | 340 m | 3 |
| **Medium** | 5 | ~1 km | 0.6 | 3.0 / 0.9 | 11 | 397 | 460 m | 5 |
| **Large** | 7 | ~1.5 km | 0.8 | 4.0 / 1.2 | 14 | 631 | 580 m | 7 |

> 战斗 cell 数公式 = `1 + 3·N·(N+1)`，每档约对应中世纪战场常见尺度（小遭遇 300m / 攻城正面 460m / 大型会战 580m）。
> Min Spawn Dist 改为 hex 数（任意两个 POI 的 footprint hex 之间最小距离）。

**互动判定**：纯 hex 命中 — 玩家进入 footprint 内任意 hex → 触发交互；鼠标悬浮 footprint hex → 显示 POI tooltip。**不再用 pixel buffer 距离 fallback**。

**战斗规模映射**：

| Scale | BattleSize | 用途 |
|-------|-----------|------|
| Tiny | Mercenary | 小遭遇 |
| Small | Mercenary | 普通遭遇（默认）；preset 可 override 到 Knight |
| Medium | Knight | 普通城镇 / 营地战 |
| Large | Lord (默认) / Stronghold | 由 preset OverrideBattleSize 控制（boss / 都城用 Stronghold） |

**战斗采样**：footprint 所有 hex + 1 圈外围邻居（地形过渡），sample 集合的几何中心映射到战斗地图原点 (0,0)。

### POIScaleProfile 数据结构

每档 Scale 对应的参数集中在一个不可变 struct：

```csharp
public readonly struct POIScaleProfile
{
    public float MarkerSize;                       // POI sprite / mesh 尺寸
    public float LightRange;                       // 灯光辐射范围
    public float LightEnergy;                      // 灯光强度
    public int   BattleHexRadius;                  // 战斗六边形 grid 半径 N
    public BattleContext.BattleSize BattleSize;    // 默认战斗规模（preset 可 override）
    public int   MinSpawnDistanceHex;              // 与其他 POI 的最小 footprint hex 距离
}

public static class POIScaleTable
{
    public static POIScaleProfile Get(POIScale s) => s switch {
        Tiny   => new(0.35f, 1.5f, 0.4f,  7, BattleSize.Mercenary, 1),
        Small  => new(0.45f, 2.0f, 0.6f,  8, BattleSize.Mercenary, 3),
        Medium => new(0.6f,  3.0f, 0.9f, 11, BattleSize.Knight,    5),
        Large  => new(0.8f,  4.0f, 1.2f, 14, BattleSize.Lord,      7),
        _ => Get(POIScale.Tiny),
    };
}
```

> 注：互动判定纯 hex 命中，不需要 EnterDist / LeaveDist 字段。

## 数据结构改动

### 1. `OverworldPOI` 增加 `Footprint`

```csharp
[Export] public POIScale Scale = POIScale.Tiny;

/// <summary>POI 占用的所有 hex 偏移 + 角色（相对于中心 hex）</summary>
public FootprintCell[] FootprintCells { get; private set; } = [new(Vector2I.Zero, Center)];

/// <summary>footprint 模板名（用于 preset 查表 + 存档恢复）</summary>
[Export] public string FootprintTemplateName = "solo";

/// <summary>占用的所有 hex 在大地图上的实际 axial 坐标（运行时计算缓存）</summary>
public Vector2I[] OccupiedHexes;

/// <summary>玩家是否站在 footprint 内（hex 级判定）</summary>
public bool ContainsHex(Vector2I hex) => OccupiedHexes.Contains(hex);
```

`OccupiedHexes` 在世界生成 / 存档加载时计算一次：`OccupiedHexes[i] = CenterHex + Rotated(FootprintCells[i].Offset)`。

### 2. `HexOverworldTile` 增 `OccupyingPoiName`

```csharp
public string? OccupyingPoiName;     // null 表示未被 POI 占用
public bool IsPoiCenter;             // 是否是 POI 的中心 hex（sprite 锚点 / 名字标签那一格）
```

互动判定：玩家进入某 hex 时，`if (tile.OccupyingPoiName != null) → 触发 POI 交互`。距离判定的 fallback（边缘 buffer）保留，但优先级低。

寻路：footprint 内 hex 仍然可通行，但被视为"目标可达点"——寻路 destination 是 footprint 任一 hex 都解析为同一 POI。

### 3. `POIBattlePresetRegistry`

```csharp
public readonly record struct POIBattlePreset(
    string TemplateName,                 // 战斗模板 key（BattleMapGenerator）
    POIScale Scale,                      // 决定 marker / 光照 / battle size
    string FootprintTemplate,            // 决定 hex 占用形状
    BattleContext.BattleSize? OverrideBattleSize,
    string DisplayName);

public static class POIBattlePresetRegistry
{
    static readonly Dictionary<(POIType, int subType), POIBattlePreset> _table = new()
    {
        // === Tiny: 单格 POI ===
        [(POIType.Outpost,  0)] = new("plain_field",        Tiny,   "solo",            null, "前哨遭遇"),
        [(POIType.Mine,     0)] = new("plain_field",        Tiny,   "solo",            null, "矿场冲突"),
        [(POIType.Farm,     0)] = new("plain_field",        Tiny,   "solo",            null, "农庄突袭"),
        [(POIType.Shrine,   0)] = new("plain_field",        Tiny,   "solo",            null, "祭坛守卫"),
        [(POIType.Tavern,   0)] = new("plain_field",        Tiny,   "solo",            null, "旅店纠纷"),

        // === Small: 多格小聚落 ===
        [(POIType.Village, 0)]                  = new("village_defense",    Small, "village_3",       null, "村庄防御"),
        [(POIType.Settlement, (int)Bandit)]     = new("bandit_stronghold",  Small, "forest_camp_3",   null, "山贼营地"),
        [(POIType.Settlement, (int)Goblin)]     = new("goblin_camp",        Small, "swamp_camp_3",    null, "哥布林营地"),
        [(POIType.Settlement, (int)Kobold)]     = new("kobold_mine",        Small, "mountain_dig_3",  null, "狗头人矿坑"),
        [(POIType.Lair, (int)AncientTomb)]      = new("ancient_tomb",       Small, "ruins_3",         null, "远古墓穴"),
        [(POIType.Lair, (int)Ruins)]            = new("ruins_exploration",  Small, "ruins_3",         null, "遗迹探索"),
        [(POIType.Lair, (int)PirateCove)]       = new("pirate_cove",        Small, "coastal_3",       null, "海寇巢穴"),

        // === Medium: 中型据点（含变形） ===
        [(POIType.Town,   0)]                  = new("town_defense",        Medium, "town_5",          null, "城镇防御战"),
        [(POIType.Port,   0)]                  = new("pirate_cove",         Medium, "port_city_4",     null, "港口袭扰"),
        [(POIType.Lair, (int)GolemForge)]      = new("golem_forge",         Medium, "ruins_5",         null, "魔像工坊"),
        [(POIType.Lair, (int)RaiderOutpost)]   = new("raider_outpost",      Medium, "plains_5",        null, "劫掠据点"),

        // === Large: 大型据点 / boss（含地形依赖型） ===
        [(POIType.Castle, 0)]                  = new("castle_siege",        Large, "mountain_castle_5", BattleSize.Stronghold, "城堡攻防"),
        [(POIType.Lair, (int)DragonLair)]      = new("dragon_lair",         Large, "mountain_lair_5",   null, "巨龙巢穴"),
        [(POIType.Settlement, (int)Minotaur)]  = new("minotaur_stronghold", Large, "fortress_7",        BattleSize.Stronghold, "牛头人石堡"),
        [(POIType.Settlement, (int)ShadowCult)]= new("shadow_cult_temple",  Large, "swamp_temple_5",    null, "暗影教团祭坛"),
        [(POIType.Settlement, (int)Pirate)]    = new("pirate_cove",         Large, "port_city_7",       null, "海寇大寨"),
    };
}
```

每个 footprint 模板（`solo`、`village_3`、`port_city_4`、`mountain_castle_5` 等）在 `FootprintTemplateRegistry` 单独注册一次，preset 表只引用名字。

### 4. `FootprintTemplateRegistry`

```csharp
public static class FootprintTemplateRegistry
{
    static readonly Dictionary<string, FootprintTemplate> _templates = new();

    public static FootprintTemplate Get(string name);

    /// <summary>
    /// 在指定中心 hex 尝试落点：枚举 6 个旋转方向，返回第一个满足约束的旋转 + 实际占用 hex 列表。
    /// 若无解返回 null。
    /// </summary>
    public static (int rotation, Vector2I[] cells)? TryFit(
        FootprintTemplate tpl,
        Vector2I center,
        HexOverworldGrid grid,
        Func<Vector2I, bool>? extraConstraint = null);
}
```

`TryFit` 检查每个 cell 的角色约束（CoastalDock 必须是水/海岸；MountainSlope 必须是山或丘陵；普通 Center/Annex 必须是可建造陆地），并要求该 hex 当前未被其他 POI 占用。

## 调用点改动

### 世界生成（`WorldRegionRegistry` / POI factory）
- 选址流程：先选 candidate 中心 → 调 `FootprintTemplateRegistry.TryFit(preset.FootprintTemplate, center)` 
- `TryFit` 失败：fallback 到更小 footprint 模板，或换中心
- 落点成功后：写 POI.OccupiedHexes + 给 footprint 内每个 tile 写 `OccupyingPoiName`
- 最小距离：现状 `IsValidPoiPosition(... minDistance=120)` 改为「任意 footprint hex 之间的距离 ≥ Scale.MinSpawnDist」

### `POIController.cs`
- 删 `POI_ENTER_DIST=450 / LEAVE_DIST=600` 常量
- `RenderAll()`：marker size 从 `POIScaleTable.Get(poi.Scale).MarkerSize` 取，sprite 视觉跨多 hex 但锚点在中心 hex
- `CheckEnter()`：**纯 hex 命中** — `var tile = grid.GetTileAtPixel(playerPixel); if (tile?.OccupyingPoiName != null) → 触发`；删掉所有距离判定
- 离开判定：玩家移动到 footprint 之外的 hex 即视为离开
- 鼠标悬浮：`OnHexHover(hex)` → 若 `tile.OccupyingPoiName != null` → 显示 POI tooltip（名字、类型、Scale 描述）

### `OverworldLightSystem.cs`
- 删 `_poiLightConfigs` 字典，改 `Profile = POIScaleTable.Get(poi.Scale); Range = Profile.LightRange`
- 颜色（暖白/冷白）保留按 POIType 区分

### `BattleMapGenerator.GenerateFromOverworld()`

**改动重点 1：战斗地图改成六边形**（不再是 axial 平行四边形）
**改动重点 2：野外/POI 统一采样规则**

```csharp
// 1. 决定战斗 hex 半径 N
int N;
List<HexOverworldTile> sampledTiles;
if (context.DefendingPOI is { } poi && poi.OccupiedHexes.Length > 0)
{
    // POI 战斗：footprint cells + 1 圈外围邻居
    N = POIScaleTable.Get(poi.Scale).BattleHexRadius;
    var coreSet = new HashSet<Vector2I>(poi.OccupiedHexes);
    sampledTiles = poi.OccupiedHexes
        .Select(h => grid.GetTileAtCoord(h))
        .Where(t => t != null).ToList()!;
    foreach (var h in poi.OccupiedHexes)
        sampledTiles.AddRange(grid.GetNeighbors(h.X, h.Y).Where(t => !coreSet.Contains(t.Coord)));
}
else
{
    // 野外遭遇：等同 Tiny footprint（玩家所在 hex + 6 邻居）
    N = POIScaleTable.Get(POIScale.Tiny).BattleHexRadius;
    var center = context.EncounterCoord;
    sampledTiles = [grid.GetTileAtCoord(center)!];
    sampledTiles.AddRange(grid.GetNeighbors(center.X, center.Y));
}

// 2. 生成六边形战斗 grid：所有满足 hexDistance(coord, (0,0)) <= N 的 axial coord
var battleCells = HexUtils.GetHexagonCoords(N);  // 新增 helper

// 3. 战斗 cell 地形 = Voronoi 取最近的 sampled tile
foreach (var bc in battleCells) {
    var nearest = FindNearestSampleByAxial(bc, sampledTiles);
    var battleTerrain = MapOverworldToBattle(nearest.Terrain);
    // ... apply detail noise
}
```

**改动 3：W×H 字段废弃**
`BattleMapData.Width / Height` 改为 `BattleHexRadius int N`，`SMap` 替换为：

```csharp
static readonly Dictionary<BattleSize, int> RadiusMap = new() {
    { BattleSize.Mercenary, 7 },
    { BattleSize.Knight,    8 },
    { BattleSize.Lord,      11 },
    { BattleSize.Stronghold, 14 },
};
```

**改动 4：`DeploymentZone.GenerateZones` 重写**
六边形地图的部署区改为「上半弧 / 下半弧」（沿 axial 某一轴对称切半），而非"左列/右列"。

**改动 5：`EnsureConnectivity` 简化**
六边形 grid 没有"角落孤岛"问题，连通性检查更直观。

**改动 6：渲染层调整**
- `BattleHexGridRenderer`（如有）的 iteration 改为遍历 `GetHexagonCoords(N)` 而非 `for q,r`
- `CombatScene` 摄像机边界从 `W×H` bbox 改为以 (0,0) 为中心、半径 `N * HexUtils.Size * 1.5f` 的方形 bbox
- 战斗地图边缘 visual fade（如雾化）天然适配六边形

> ⚠️ **视觉提醒**：现状代码用 axial+offset 公式让平行四边形 grid 摆成"视觉矩形"。改六边形后玩家会看到真正的六边形战场边界。这是有意为之，与大地图视觉一致。

### `BattleContext.CreateFromEncounter()`
```csharp
if (poi != null) {
    var preset = POIBattlePresetRegistry.Resolve(poi);
    context.Size = preset.OverrideBattleSize ?? POIScaleTable.Get(preset.Scale).BattleSize;
}
```

### `OverworldPOI.GetBattleTemplateName()`
```csharp
public string GetBattleTemplateName() => POIBattlePresetRegistry.Resolve(this).TemplateName;
```

### `DailyDecisionProcessor.cs:122` 与 `OverworldEntityManager.GetVisiblePois()`
- DailyDecisionProcessor 那个 `< 150f` 是 entity 视野，与 POI scale 无关，不动
- GetVisiblePois 的 visionRange 是玩家视野，不动

## 存档兼容

旧存档的 POI 没有 `FootprintTemplateName` / `OccupiedHexes`：

```csharp
if (!data.ContainsKey("footprint_template"))
{
    var preset = POIBattlePresetRegistry.Default(PoiTypeEnum);
    FootprintTemplateName = preset.FootprintTemplate;
    Scale = preset.Scale;
}
// OccupiedHexes 从 FootprintTemplate + Position 重新计算
```

如果旧存档的 POI 中心格周围地形不满足新 footprint 约束（比如港口的海格那一面是陆地）：fallback 到 `solo` template（单格）+ warn log，不阻塞加载。

## 失败模式与回退

| 风险 | 触发 | 回退 |
|------|------|------|
| 港口选址找不到合适方向（中心周围全是陆地） | 世界生成 | 退回 `solo`，warn log |
| Stronghold 跨 chunk 边界 | 世界生成 | POI 中心远离 chunk 边 ≥ 3 hex；fallback 到 Medium 模板 |
| 旧存档 POI 没法重建 footprint | 反序列化 | fallback 到 `solo` |
| 战斗地图把 footprint 海格映射到错位置 | 港口战斗生成 | Voronoi 取最近 sample，映射失败的 cell 不影响主体生成 |
| 互动判定遗漏 footprint 内某 hex | hex 标记没刷上 | 世界生成时按 OccupiedHexes 全量刷 OccupyingPoiName，序列化也保存这份索引 |
| 战斗地图改六边形后摄像机/UI 边缘错位 | UI 层 | 把摄像机 bbox 改成圆形/方形外切边界；战斗 UI 在 hex 外缘做 fade |
| BattleSize Stronghold 的战斗 hex 半径 14（631 cell）渲染负担过重 | 性能 | 已在现状 30×20=600 cell 数量级，量级相同；渲染层若仍受限可降到 N=12（469 cell） |

## 不做的事

- ❌ Hex ↔ 真实公里换算（暂存「世界对应地球哪片」议题；本 spec 内部 1 hex = 250m / 1 cell = 20m 是设计假设，非游戏事实）
- ❌ Footprint 跨多个 chunk（强制中心远离 chunk 边）
- ❌ POI footprint 在游戏过程中扩展（村→镇升级）— 后续 spec
- ❌ 战斗地图尺寸跟 footprint 大小连续变化（保持 4 档 BattleHexRadius）
- ❌ 玩家修建 / 拆除 footprint cell — 后续 spec

## 阶段拆分

1. **P1**：建立 `POIScale` / `FootprintCell` / `FootprintTemplate` / `FootprintTemplateRegistry` / `POIBattlePresetRegistry`，仅做查表测试
2. **P2**：`OverworldPOI` 加 `Footprint` 相关字段，世界生成走 `TryFit` 选址；最小距离改为 hex 数
3. **P3**：`HexOverworldTile.OccupyingPoiName` + `IsPoiCenter` 标记，互动判定改为纯 hex 命中（`POIController`）+ 鼠标 tooltip
4. **P4**：渲染 / 灯光参数从 `POIScaleTable` 派生（marker / light）
5. **P5**：`BattleMapGenerator` 改六边形 + `RadiusMap` + Voronoi sample 映射；`DeploymentZone` / `EnsureConnectivity` 重写
6. **P6**：战斗 UI / 摄像机适配六边形边界
7. **P7**：sim 验证（world_gen / battle_scale）
8. **P8**：补 footprint 模板，覆盖每个 preset 行（港口、山城、林边等变体）

P5/P6 是本 spec 中风险最高的两步，改动量最大。建议在专门分支开发并保留旧路径作为 feature flag 直到 sim 验证通过。

## 验证方式

### Sim 测试
- `world_gen` scenario 输出每档 Scale 的 POI 数 / 实际 footprint cell 总数 / fallback 到 solo 的比例
- 港口 POI 的 CoastalDock cell 地形分布（应 100% 是 ShallowWater 或邻接陆地的 DeepWater）
- 山城 POI 的 MountainSlope cell 地形分布

### 战斗 sim
- `battle_scale` scenario：每档 Scale × 每个 preset 跑 N 次，验证：
  - 战斗 hex 总数 = `1 + 3·N·(N+1)`
  - 港口战斗：水域 cell ≥ 战斗总 cell 的 5%
  - 山城战斗：hills/mountain cell ≥ 战斗总 cell 的 15%
  - 普通村庄：地形多样性指数（Shannon entropy）≥ 阈值
  - 玩家/敌方部署区互通

### 手动验证
- 港口 POI 在地图上向海延伸一格，sprite 跨 2-3 hex；战斗地图明显有"陆海交界"
- 城堡 POI 在山脚 + 山坡，sprite 大；进战斗有崖壁
- 鼠标悬浮 footprint hex → tooltip 显示 POI 名 + Scale 描述
- 玩家走到 footprint 任一 hex → 自动触发交互；走出 footprint → 重置交互锁
- 战斗地图视觉是六边形边界（不是矩形）
