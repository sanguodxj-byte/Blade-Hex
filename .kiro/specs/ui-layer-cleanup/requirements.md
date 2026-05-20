# UI 层清理 — Requirements（最终版）

## 背景

架构优化 spec 完成后，View/UI 占 19,791 行 / 62 文件，是 Frontend 中最大的单一模块（54,002 行的 37%）。Top 10 文件普遍 > 700 行，最大 1,424 行。

**初版假设**："大文件就是该拆"。盘点流程**强制要求 grep 实证 + 实际读代码**后，结论被反复修正：

- ArmyManagementUI（1,424 行）→ 全工程 0 实例化，**死代码**
- CharacterDetailPanel（675 行）→ 唯一调用 case 不可达 + new 之后从不显示，**死代码**
- PartyPanel（809 行）→ 已复用 Inventory 子模块，自身仅做三栏布局协调，**已对**
- MainMenu（711 行）→ 存档管理 270 行 + 雨效 150 行均稳定子模块，抽出去净行数增加，**不动**
- OverworldUI（862 行）→ 顶栏 110 行是 8 个 Label 顺序构建无业务逻辑，底栏 25 行调工厂方法，**没东西可抽**
- OriginSelect（717 行）→ View/Controller 拆分会净增 ~200 行 + 1 个接口面；面板单次使用（仅新游戏入口）；未来 mod 接入修改的是 JSON 数据不是代码，**不动**
- SkillTreeUI（734 行）→ 渲染 250 行紧耦合 `_drawContainer.QueueRedraw()` 调度链，拆出会破回调，**合理复杂度**
- CombatUI（769 行）→ 已 facade 给 6 个子组件，每个公开 API 都 forward，**已对**

最终本 spec 仅做**死代码清理**。其余文件 ≥ 500 行但每个都是合理复杂度或抽出去净增行数，列在"评估后保留"。

## 范围与非范围

### 范围内（已完成）

- 删除 `ArmyManagementUI.cs` + `.uid`（S1）
- 删除 `CharacterDetailPanel.cs` + `.uid`，并清除 OverworldUI / CombatUI 中的所有死代码引用（S2）
- 同步更新 `CLAUDE.md` UI 列表

### 范围外（评估后保留）

- ❌ PartyPanel — 已对（复用 Inventory 子模块）
- ❌ MainMenu — 抽出去净增行数，且子模块稳定不会动
- ❌ OverworldUI — 顶/底栏均是无逻辑顺序构建，没真正可抽点
- ❌ OriginSelect — 单次使用面板，未来 mod 修改的是 JSON 而非代码
- ❌ SkillTreeUI — 渲染紧耦合调度链，拆分破坏回调
- ❌ CombatUI — 已 facade
- ❌ 不引入新 UI 框架，不改主题/配色/布局
- ❌ 不动 UI 与场景层的信号 / 事件协议
- ❌ 不动 UI 公共 API 签名

## 需求清单

### R1 — 删除 ArmyManagementUI 死代码 ✅ 已完成（S1）

**当前情况：** `ArmyManagementUI.cs` 1,424 行，但 grep 全工程**无任何 `new ArmyManagementUI`**。实际跑的是 `PartyPanel`（OverworldUI 中 `_partyPanel = new PartyPanel()`）；`OverworldUI._OpenArmyManagement()` 内部调的是 `_OpenPartyPanel()`。这是早期"军队管理"面板被 `PartyPanel` 替代后**没删的死代码**。

**已执行：**
- 删除 `ArmyManagementUI.cs` 及其 `.uid`
- 修正 `POIPanelBase.cs` 注释
- 更新 `CLAUDE.md` UI 列表

**验收：** ✅ 编译 0 错误；UI 行为完全等价。净减 **1,424 行**。

### R2 — 删除 CharacterDetailPanel 死代码 ✅ 已完成（S2）

**当前情况：** `CharacterDetailPanel.cs` 675 行，但 grep 实证全工程**无任何实际显示路径**：
- 底栏 5 个按钮注释明确写"角色面板已合并到军队面板"，按钮列表里没有 `character`
- `case "character":` / `_OpenCharacterDetail()` / `CharacterClicked` 信号 / `_characterDetail` 字段全是孤儿
- CombatUI 的 `_characterDetail` 仅 `new + AddChild + Visible=false`，从无显示路径
- 公开 API `ShowDetail()` / `HideDetail()` / `UpdateDisplay()` 全部零外部调用

**已执行：**
- 删除 `CharacterDetailPanel.cs` + `.uid`
- OverworldUI 清理：信号 / 字段 / 方法 / case / 4 处状态判断引用
- CombatUI 清理：字段 + 3 行 new/Visible/AddChild
- 更新 `CLAUDE.md` UI 列表

**验收：** ✅ 编译 0 错误；UI 行为完全等价。净减 **705 行**（675 文件 + 30 引用）。

### R3 — PartyPanel 评估保留 ✅ 已完成

复用 Inventory 子模块（GridInventoryView / EquipmentSlotView / ShopGridView / DragController / ItemPopup），自身仅做三栏布局协调，无重复实现。**不动**。

### R4 — MainMenu 评估保留 ✅ 已完成

存档管理 270 行 + 雨效 150 行均是稳定子模块，抽出去净行数增加，且**两块都基本不会修改**。设置已 forward 给 GameMenuManager。**不动**。

### R5 — OverworldUI 评估保留 ✅ 已完成

顶栏 110 行是 8 个 Label 顺序构建（无业务逻辑），底栏 25 行调 `_CreateBarButton` 工厂。`_OnButtonPressed` switch 里 save 分支 30 行存档构建本质是 SaveManager 的事而不是 UI 的事，应当后续在架构层处理。**不动**。

### R6 — OriginSelect 评估保留 ✅ 已完成

- 单次使用面板（仅新游戏入口）
- View/Controller 拆分会净增 ~200 行 + 1 个接口面
- 未来 mod 接入修改的是 JSON 数据（origin_questions.json）而非代码

**不动**。

### R7 — SkillTreeUI 评估保留 ✅ 已完成

734 行 = 输入 ~120 + 几何 ~120 + 渲染 ~250 + 信息面板 ~120 + 构建 ~100。渲染紧耦合到 `_drawContainer.QueueRedraw()` 调度链，拆出去会破坏 OnDraw 回调；几何已部分外移到 `SkillTreeCoord`。**合理复杂度，不动**。

### R8 — CombatUI 评估保留 ✅ 已完成

已 facade 给 6 个子组件（TurnOrderBar / EnemyInfoPanel / BattleLogPanel / RadialMenu / UnitInspectPanel / HitPreviewTooltip）。24 个公开 API 全部 forward。**已对**。

## 累计行数变化

| Sprint | 范围 | 净变化 |
|--------|------|--------|
| S1 | 删 ArmyManagementUI | **-1,424 行** |
| S2 | 删 CharacterDetailPanel + 清理引用 | **-705 行** |
| **总计** | | **-2,129 行** |

UI 层从 19,791 行降到约 17,662 行（-10.8%）。单文件最大值从 1,424 → 862（OverworldUI），降 39%。

## 关键判断框架（沉淀到 notes.md）

**"大文件该不该动"的实证流程：**
1. **grep 实际调用**（不是看文件名/行数猜测）
2. **追溯按钮/热键到 case 分支**（确认 case 是否可达）
3. **看是否复用了已有模块**（复用了就不是重复实现）
4. **看抽出去净行数变化**（增加就别抽）
5. **看面板使用频次**（单次使用面板拆分价值低）
6. **看渲染回调链**（紧耦合 OnDraw 的不能轻易拆）

**死代码识别**：
- grep `new XxxUI` 全工程 0 → 直接删
- 唯一调用 case 不可达（按钮列表注释"已合并"）→ 死代码链，连引用一起清

## 不动的相关文档

- `Blade&Hex/docs/09-UI设计.md` — GDScript 时代历史
- `Blade&Hex/docs/29-类骑砍玩法实现路线.md` — 路线快照
- `docs/code_review.md` — 评审快照
- `.kiro/specs/gdscript-to-csharp-migration/` — 已完成 spec
- 历史快照不修改是为了保留时间线证据

## 与上一个 spec 的关系

- 架构优化 spec 完成时遗留 "OriginSelect View/Controller 拆分留作未来工作"
- 本 spec 评估后判定"未来工作 = 不做"是合理选项（理由记录在 R6）
- 本 spec 不动 SaveManager / GlobalState / EventBus / WeatherManager — 都已由架构优化收尾
