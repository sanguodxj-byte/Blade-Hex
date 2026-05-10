# 大地图生成方案 — 分步实施计划

> 基于 `overworld_generation_supplement.md` 的 9 项补充方案，按依赖关系排列的实施步骤。
> 每步完成后需要 **Review 检查点** 确认质量，再进入下一步。

---

## 实施流程总览

```mermaid
flowchart LR
    Step1[Step 1: S1 温度降温] --> Step2[Step 2: S2 贝塞尔河流]
    Step2 --> Step3[Step 3: S5 坐标统一]
    Step3 --> Step4[Step 4: S4 流水线+POI]
    Step4 --> Step5[Step 5: S6 地形偏好]
    Step5 --> Step6[Step 6: S3 桥梁逻辑]
    Step6 --> Step7[Step 7: S7 迷雾渲染]
    Step7 --> Step8[Step 8: S9 坐标转换]
    Step8 --> Step9[Step 9: S8 Chunk渲染]
```

---

## Step 1: S1 — 温度高海拔降温

### 目标
在温度计算中叠加高程降温因子，使高山地区温度合理降低。

### 修改文件
- `src/core/map/HexOverworldGenerator.gd`

### 具体改动

**位置**: `_generate_base_layers()` 第 188-191 行

**当前代码**:
```gdscript
var latitude_factor := float(r) / float(height)
var temp_noise := _noise_temp.get_noise_2d(float(q), float(r)) * 0.2
t.temperature = clampf(latitude_factor + temp_noise, 0.0, 1.0)
```

**修改为**:
```gdscript
var latitude_factor := float(r) / float(height)
var temp_noise := _noise_temp.get_noise_2d(float(q), float(r)) * 0.2
var base_temp := latitude_factor + temp_noise
# 高海拔降温: elevation > 0.5 开始降温，最大降幅 0.3
var altitude_penalty := clampf(t.elevation - 0.5, 0.0, 0.5) * 0.6
t.temperature = clampf(base_temp - altitude_penalty, 0.0, 1.0)
```

### Review 检查点
- [ ] 运行地图生成，检查雪山区域是否扩大（高山温度降低后更多区域进入"极寒"带）
- [ ] 检查低海拔热带区域是否不受影响（elevation < 0.5 时 penalty = 0）
- [ ] 地图北部高海拔区域应出现更多 SNOW / ICE 地形
- [ ] 地图南部低海拔区域仍保持 PLAINS / SAVANNA / JUNGLE 等炎热地形

---

## Step 2: S2 — 贝塞尔河流重写

### 目标
将河流生成从简易 A* 寻路改为贝塞尔地形雕刻算法，实现优美弧线 + 河谷降级。

### 修改文件
- `src/core/map/HexOverworldGenerator.gd`（主要修改）

### 具体改动

#### 2.1 新增常量

在现有河流常量区域（第 44-48 行附近）追加：

```gdscript
## 贝塞尔河流参数
const BEZIER_SAMPLE_DENSITY: float = 2.0
const BEZIER_LATERAL_OFFSET_MAX: float = 8.0
const VALLEY_CARVE_PRIMARY: float = 0.25
const VALLEY_CARVE_NEIGHBOR_HIGH: float = 0.15
const VALLEY_CARVE_NEIGHBOR_MID: float = 0.08
const VALLEY_SMOOTH_PROBABILITY: float = 0.7
const MIN_SOURCE_DISTANCE: int = 15
```

#### 2.2 新增方法: `_cubic_bezier()`

```gdscript
func _cubic_bezier(p0: Vector2, p1: Vector2, p2: Vector2, p3: Vector2, t: float) -> Vector2:
    var u := 1.0 - t
    return u * u * u * p0 + 3.0 * u * u * t * p1 + 3.0 * u * t * t * p2 + t * t * t * p3
```

#### 2.3 新增方法: `_generate_bezier_river_path()`

输入源头和入海口的 Axial 坐标，返回贝塞尔采样路径。

核心逻辑：
1. 在源头和入海口之间生成 1-2 个带横向偏移的控制点
2. 沿三次贝塞尔曲线等距采样
3. 将浮点坐标转为 Axial 整数坐标
4. 去重

#### 2.4 新增方法: `_carve_river_valley()`

输入贝塞尔路径，执行河谷雕刻：

1. 路径上每个瓦片 → terrain = RIVER, elevation 降低 0.25
2. 6邻居中 elevation > 0.60 的瓦片 → 降低 0.15
3. 6邻居中 elevation > 0.45 的瓦片 → 降低 0.08
4. 河谷平滑概率 70%

#### 2.5 替换 `_generate_rivers()` 实现

- 替换 `find_lowest_elevation_path()` 调用为 `_generate_bezier_river_path()`
- 追加 `_carve_river_valley()` 调用
- 保持现有的 `_mark_river_path()` 用于方向标记

#### 2.6 修正 `generate()` 流水线

将第 119 行的 `_generate_rivers()` 改名为 `_carve_bezier_rivers()`。

### Review 检查点
- [ ] 河流路径呈现优美的 S/U 型弧线（不是锯齿状）
- [ ] 河流两岸出现降级的河谷地形（山脉旁的河流两侧应为丘陵）
- [ ] 河流数量仍在 3-6 条范围内
- [ ] 河流不穿过雪山（贝塞尔路径应避开极高海拔，可通过控制点约束）
- [ ] 河流入海口正确连接到浅水区
- [ ] 河流源头之间距离 > 15 格
- [ ] 控制台输出河流数量日志

---

## Step 3: S5 — WorldGenerator 坐标空间统一

### 目标
让 WorldGenerator 从 HexOverworldGrid 查询地形信息，而非使用独立的噪声+像素坐标系统。

### 修改文件
- `src/core/strategic/WorldGenerator.gd`
- `src/core/strategic/OverworldPOI.gd`（新增 hex_coord 字段）

### 具体改动

#### 3.1 OverworldPOI 新增字段

```gdscript
## 六边形轴向坐标 (新增，与 position 并存)
var hex_coord: Vector2i = Vector2i.MIN
```

#### 3.2 WorldGenerator 新增方法

```gdscript
## 新接口: 基于六边形网格生成世界
func generate_from_grid(hex_grid: HexOverworldGrid, hex_gen: HexOverworldGenerator) -> Dictionary:
    _hex_grid = hex_grid
    _hex_gen = hex_gen
    pois.clear()
    entities.clear()
    
    _generate_towns_on_grid()
    _generate_villages_on_grid()
    _generate_castles_on_grid()
    _generate_settlements_on_grid()
    _generate_lairs_on_grid()
    _generate_elf_settlements_on_grid()
    _generate_dwarf_cities_on_grid()
    _generate_initial_entities()
    
    return {"pois": pois, "entities": entities}
```

#### 3.3 区域查询改为读取 tile.region_name

旧: `get_region_at(px, py, noise_val)` → 新: `hex_grid.get_tile(q, r).region_name`

#### 3.4 POI 定位改为 HexOverworldGenerator.find_settlement_position()

利用已有的 `find_settlement_position(region_name, min_distance)` 方法。

#### 3.5 保留旧接口

旧 `generate(mapnoise)` 方法保留不动（地图编辑器 JSON 导入仍需）。

### Review 检查点
- [ ] `generate_from_grid()` 能成功生成所有类型的 POI
- [ ] POI 的 `hex_coord` 正确设置为 Axial 坐标
- [ ] POI 的 `position` 正确计算为像素坐标（兼容旧渲染代码）
- [ ] 城镇出现在平原/草地地形上（不再随机落在水域或山脉）
- [ ] 龙巢出现在雪山/山脉地形上
- [ ] 旧 `generate()` 接口仍可用（向后兼容）

---

## Step 4: S4 — 生成流水线补充 POI + 巢穴

### 目标
在 HexOverworldGenerator.generate() 中集成 POI 放置和巢穴放置，修正执行顺序。

### 修改文件
- `src/core/map/HexOverworldGenerator.gd`

### 前置依赖
- Step 3 完成（WorldGenerator 已有 `generate_from_grid()` 接口）

### 具体改动

#### 4.1 修正 `generate()` 流水线顺序

```gdscript
func generate(width, height, world_seed) -> HexOverworldGrid:
    # ... 步骤 0-7 不变 ...
    
    # 第6步: 贝塞尔河流雕刻 (Step 2 已改名)
    _carve_bezier_rivers()
    
    # 第7步: 地理区域定义
    _define_regions(width, height)
    _assign_region_names()
    
    # ★ 第8步: 聚落放置 (新增)
    _place_settlements()
    
    # ★ 第9步: 道路生成 (修正: 现在可以连接真正的城镇)
    _generate_roads()
    
    # ★ 第10步: 隐秘巢穴放置 (新增)
    _place_dungeons_and_lairs()
    
    # 第11步: 后处理
    _finalize_terrain()
    
    return grid
```

#### 4.2 新增 `_place_settlements()` 方法

利用 WorldGenerator 的 POI 生成逻辑，但在 HexOverworldGrid 上操作：

```gdscript
func _place_settlements() -> void:
    var world_gen := WorldGenerator.new()
    var result := world_gen.generate_from_grid(grid, self)
    _placed_pois = result.get("pois", [])
    _placed_entities = result.get("entities", [])
```

#### 4.3 新增 `_place_dungeons_and_lairs()` 方法

按补充方案 S4.4 的规则放置巢穴（龙巢→雪山、哥布林营地→森林边缘等）。

#### 4.4 修正道路生成

`_generate_roads()` 的路点来源改为已放置的定居点：

```gdscript
# 旧: 使用区域中心作为路点
# 新: 使用 grid.get_settlement_tiles() 获取路点
var road_nodes: Array[Vector2i] = []
for tile in grid.get_settlement_tiles():
    if tile.is_passable:
        road_nodes.append(tile.coord)
```

### Review 检查点
- [ ] 流水线顺序正确：区域 → 聚落 → 道路 → 巢穴
- [ ] 道路连接的是真正的城镇/村庄（不是虚拟区域中心）
- [ ] 巢穴不出现在道路上
- [ ] 巢穴与最近城镇距离 > 8 格
- [ ] 龙巢只在雪山地形上
- [ ] 控制台输出 POI 和巢穴数量日志

---

## Step 5: S6 — POI 地形偏好评分

### 目标
POI 放置时根据地形类型评分，优先放置在地形匹配的位置。

### 修改文件
- `src/core/map/HexOverworldGenerator.gd`（增强 `find_settlement_position()`）

### 前置依赖
- Step 4 完成

### 具体改动

#### 5.1 新增地形偏好常量

```gdscript
const POI_TERRAIN_SCORES := {
    "town": { TerrainType.PLAINS: 1.0, TerrainType.GRASSLAND: 0.9, TerrainType.SAVANNA: 0.5 },
    "village": { TerrainType.PLAINS: 1.0, TerrainType.GRASSLAND: 0.9, TerrainType.FOREST: 0.3 },
    "castle": { TerrainType.HILLS: 1.0, TerrainType.PLAINS: 0.3 },
    "goblin_camp": { TerrainType.FOREST: 0.8, TerrainType.SWAMP: 0.7 },
    "dragon_lair": { TerrainType.MOUNTAIN_SNOW: 1.0, TerrainType.MOUNTAIN: 0.4 },
}
```

#### 5.2 增强 `find_settlement_position()`

为每个候选位置计算综合评分 = 地形评分 + 河流邻近加分 + 道路邻近加分，选择最高分位置。

### Review 检查点
- [ ] 城镇出现在平原/草地上（不再出现在沼泽或沙漠）
- [ ] 城堡出现在丘陵上
- [ ] 优先选择临近河流的位置
- [ ] 龙巢只出现在雪山

---

## Step 6: S3 — 道路桥梁逻辑

### 目标
道路穿越河流时产生桥梁瓦片，不再断裂。

### 修改文件
- `src/core/map/HexOverworldTile.gd`（新增字段）
- `src/core/map/HexOverworldGenerator.gd`（道路生成+代价修改）

### 前置依赖
- Step 2 完成（河流已用贝塞尔生成）

### 具体改动

#### 6.1 HexOverworldTile 新增字段

```gdscript
var is_bridge: bool = false
var bridge_directions: int = 0
```

#### 6.2 道路代价修改

`_apply_road_cost_modifier()` 中为河流瓦片设置高代价：

```gdscript
if t.is_river:
    cost = 12.0  # 高昂但可通过
```

#### 6.3 桥梁标记

`_mark_road_path()` 中增加桥梁处理逻辑。

#### 6.4 序列化补充

`serialize()` / `deserialize()` 新增 `is_bridge` 和 `bridge_dirs` 字段。

### Review 检查点
- [ ] 道路穿过河流时产生桥梁瓦片
- [ ] 桥梁瓦片 is_passable = true, move_cost = 0.5
- [ ] 桥梁方向位正确标记
- [ ] 存档/读档包含桥梁数据
- [ ] 道路不再因为河流而断裂

---

## Step 7: S7 — 迷雾渲染

### 目标
实现三层迷雾渲染（未探索/已探索/当前可见）。

### 修改文件
- `src/core/map/HexOverworldRenderer.gd`
- `src/core/strategic/FogOfWar.gd`（如有）

### 具体改动

#### 7.1 在 `_render_tile_into()` 中追加迷雾层

根据 `tile.visibility` 值渲染不同的遮罩。

#### 7.2 新增迷雾更新接口

```gdscript
func update_visibility(changed_tiles: Array[HexOverworldTile]) -> void:
    # 重新渲染受影响瓦片的迷雾层
```

### Review 检查点
- [ ] 未探索区域显示深色遮罩
- [ ] 已探索区域显示半透明灰色
- [ ] 当前可见区域完全清晰
- [ ] 瓦片 visibility 变更后迷雾正确更新
- [ ] 初始状态全部为未探索

---

## Step 8: S9 — 大地图↔战斗地图坐标转换

### 目标
定义大地图瓦片到战斗地图的坐标转换接口。

### 修改文件
- 新增 `src/core/map/OverworldMapBridge.gd`

### 具体改动

创建 `OverworldMapBridge` 静态工具类，提供：
- `overworld_tile_to_battle_origin()` — 坐标转换
- `overworld_terrain_to_battle()` — 地形主题映射

### Review 检查点
- [ ] 接口可被 CombatScene 调用
- [ ] 大地图地形正确映射到战斗地图主题
- [ ] 无运行时错误

---

## Step 9: S8 — Chunk 渲染启用

### 目标
启用被禁用的 Chunk 分块渲染系统，为地图扩展做准备。

### 修改文件
- `src/core/map/HexOverworldRenderer.gd`

### 具体改动

1. 恢复 `_process()` 中的 chunk 更新逻辑
2. 将 `_render_all_tiles()` 改为可选（小地图用全量，大地图用 chunk）
3. 增加地图大小阈值判断

### Review 检查点
- [ ] 小地图（64×48）仍使用全量渲染
- [ ] Chunk 加载/卸载正确工作
- [ ] 视口移动时无闪烁或遗漏
- [ ] 内存占用合理（视口外 chunk 被释放）

---

## 通用 Review 规则

每步完成后，除了特定的检查点外，还需要通过以下通用检查：

1. **编译检查**: 项目在 Godot 4 中无报错
2. **生成测试**: 运行地图生成，控制台无报错
3. **序列化测试**: 生成地图 → 存档 → 读档 → 无数据丢失
4. **视觉检查**: 地图渲染无异常（无洋红色未知地形）
5. **Git 提交**: 每步完成后独立提交，commit message 标注步骤编号
