# BladeHexFrontend — View 层目录结构与边界规则

## 目录分区图

```
BladeHexFrontend/src/View/
├── Combat/              ← 战斗运行时
│   ├── AI/             ← AI 策略与决策（Controller、Strategy、Evaluator）
│   ├── commands/       ← 命令模式（ICommand、CommandHistory）
│   ├── Equipment/      ← 装备/战利品/消耗品管理
│   ├── Projectile/     ← 投射物系统（Pool、System、View）
│   ├── Skills/         ← 技能处理器（Melee、Ranged、Magic、Support）
│   ├── StatusEffect/   ← 状态效果管理
│   └── Turn/           ← 回合管理（TurnManager、UnitRegistry）
├── Data/               ← 全局状态、存档、资源注册表
├── Events/             ← EventBus 类型化事件
├── Map/                ← 六边形网格渲染（HexGrid、HexCell、Batcher）
├── Quest/              ← 任务运行时管理器
├── Strategic/          ← 战略层实体、渲染器
└── Unit/               ← 单位节点与渲染流水线
    └── Slots/          ← 装备分部位渲染配置
```

## Core / View 边界硬规则

| 规则 | 说明 |
|------|------|
| **Core 禁渲染类型** | `BladeHexCore/**` 不得出现 `Texture2D`、`SpriteFrames`、`Material`、`Mesh`、`StandardMaterial3D` 等 Godot 渲染类型。违例须立即搬迁。 |
| **Core 禁节点类型** | `BladeHexCore/**` 不得继承 `Node`、`Node3D`、`Control` 或其子类。违例须迁移至 `BladeHexFrontend`。 |
| **Core 只持 string ID** | 所有渲染资源引用（icon、sprite、material）在 Core 层以 `string IconId` / `string SpriteFramesId` 持有。实际的 `Texture2D` / `SpriteFrames` 由 View 层的 `ResourceRegistry` 在运行时解析。 |
| **View 不持规则** | 伤害解算、属性计算、等级公式等纯逻辑不得出现在 View 层文件（`.TakeDamage` 是唯一例外，作为 View→Core 的委托入口）。 |
| **View 不直接 new 材质** | 六棱柱材质统一由 `CombatMaterialManager` 按 `(TerrainType, Elevation)` 缓存并提供。禁止散落在各 `_Ready()` 中的 `new StandardMaterial3D()`。 |
| **数据流单向** | 数据从 Core 向上流向 View。View 不修改 Core 数据模型的结构。View 通过 `BattleUnitModel.ApplyDamage()` 等委托方法改变运行时状态。 |

## 新加 View 模块的 Checklist

向 `src/View/` 添加新模块/文件时，逐项核对：

- [ ] **位置正确** — 文件放在 `View/` 下细分目录中（`Combat/`、`Map/`、`Unit/` 等）。Core 逻辑放在 `BladeHexCore/src/` 对应目录。
- [ ] **无渲染类型泄漏** — 如果是 Core 层文件，确保不含 `Texture2D`、`SpriteFrames`、`Material` 等类型。若是 View 层文件，确保不引用 Core 不应知道的渲染类型。
- [ ] **无 `new StandardMaterial3D()`** — 六棱柱材质通过 `CombatMaterialManager` 获取。单位材质通过 `ResourceRegistry` 按 ID 查找。
- [ ] **Resource ID 优先** — 新的数据字段优先用 `string IconId` 而不是 `[Export] Texture2D`。
- [ ] **只做委托不做规则** — View 层的伤害/属性相关方法应委托到 `Model.ApplyDamage()` / `CombatStats`，不自己实现公式。
- [ ] **`[GlobalClass]` 正确标注** — 需要暴露给 Godot 编辑器的类加 `[GlobalClass]`。纯工具类不加。
- [ ] **使用 `Godot.Collections.Dictionary` 与 GDScript 交互** — Core 内部用 `System.Collections.Generic`，跨语言边界用 `Godot.Collections`。

## 关键类型快速参考

| 类型 | 层 | 作用 |
|------|----|------|
| `BattleUnitModel` | Core | 单位数学模型，持有 `ApplyDamage()` |
| `UnitData` | Core | 单位静态数据，持 `string SpriteFramesId` |
| `ItemData` | Core | 物品数据，持 `string IconId` |
| `ResourceRegistry` | View | id → 资源的查表与懒加载 |
| `CombatMaterialManager` | View | 地形材质工厂与缓存 |
| `HexCellMultiMeshBatcher` | View | 战斗网格 MultiMesh 合批 |
| `CombatResolver` | View | 攻击结算委托（调用 `Model.ApplyDamage`） |
| `RenderBus` / `CharacterRenderBus` | View | 渲染事件总线，注入到 Unit |
