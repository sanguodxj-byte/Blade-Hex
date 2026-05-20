# UI 层清理 — Decision Log

## S1 — 删除 ArmyManagementUI 死代码（2026-05-17 完成）

### 触发与判定
- 起因：UI 层代码盘点发现 `ArmyManagementUI.cs` 1,424 行，是单文件最大
- grep 实证：全工程**无任何 `new ArmyManagementUI`**（仅 class 定义自身和注释提及）
- OverworldUI 实际持有的是 `PartyPanel`（`_partyPanel = new PartyPanel()`），且 `_OpenArmyManagement()` 内部 `_OpenPartyPanel()`
- 判定：早期军队管理面板被 `PartyPanel` 替代后**未删的死代码**

### 执行
1. 删除 `BladeHexFrontend/src/View/UI/Overworld/ArmyManagementUI.cs`（1,424 行）
2. 删除 `BladeHexFrontend/src/View/UI/Overworld/ArmyManagementUI.cs.uid`（Godot uid 元数据）
3. 修正 `POIPanelBase.cs` 注释：`物品相关面板使用 ArmyManagementUI` → `物品相关面板使用 PartyPanel`
4. 更新 `d:\123\CLAUDE.md` UI 列表，移除 `ArmyManagementUI`

### 验证
- `dotnet build BladeHexFrontend.csproj` → **0 错误**
- 10 个警告（WeatherContext [Obsolete] × 9 + AIController CS8604 + OverworldScene3D CS8600）全是既有项，与本改动无关
- grep 兜底确认：代码层 0 残留；剩余引用全在历史文档 / 已完成 spec（不动）

### 不动的引用
- `Blade&Hex/docs/09-UI设计.md` — 通篇 `.gd` 后缀，GDScript 时期的历史设计文档，保留
- `.kiro/specs/gdscript-to-csharp-migration/` — 已完成的历史 spec，保留任务清单原貌

### 行数变化
- 净减 **1,424 行**（仅源码；不计 .uid）
- 一文件最大 1,424 → 862（OverworldUI），下降 39%

## PartyPanel 评估结论（2026-05-17）

不在本 spec 范围内修改。grep 加读代码确认：
- 文件 809 行，已复用 Inventory 子模块
- `GridInventoryView` / `EquipmentSlotView` / `ShopGridView` / `DragController` / `ItemPopup` 均不是 PartyPanel 内联实现
- PartyPanel 自身仅做"三栏布局协调器"职责（左角色+装备 / 中详情或商店+背包 / 右队伍列表）
- 主要消耗：`_BuildLayout` (~120 行) + `_RefreshLeft/_RefreshCenter/_RefreshRight` 各 50-80 行 + `_BuildStatGrid` / `_BuildShopArea` 等小方法
- 结论：结构合理，无重复实现需要合并，**不动**

## MainMenu 评估结论（继承自上一次盘点）

不在本 spec 范围内修改。
- 711 行 = UI 构建 + 存档管理（~230 行） + 快速战斗入口 forward + 雨天氛围（~150 行）
- 设置已 forward 给 `GameMenuManager.OpenSettings()`（11 行）
- 内容稳定，单一文件可读性可接受，**保留**

## 判断框架（凝练）

**该不该动一个大文件？**
1. grep 实际调用（不是看文件名/行数猜测）
2. 看内容稳定性 — 量大但稳定且分工清晰的可保留（MainMenu）
3. 看是否复用了已有模块 — 复用了就不是重复实现（PartyPanel）
4. 死代码：grep 无 `new XxxUI` → 直接删

**风险预设**
- 极低：删死代码（S1 完成）
- 低：纯文档评估（S2）
- 中：拆 facade 子组件（S3 OverworldUI / S4 OriginSelect）
- 高：本 spec 不涉及（参考架构优化 Sprint 6 的 Weather 教训：多依赖组件抽取需 step-by-step + 每步手测，已在范围外）


## S2 — 删除 CharacterDetailPanel 死代码（2026-05-17 完成）

### 触发与判定
- 起因：原 R5 计划只是"评估"，但用户敏锐指出"目前无调用"
- grep 实证（追溯按钮路径）：
  1. `case "character":` 跳到 `_OpenCharacterDetail()`，但底栏 5 个按钮注释明确说"角色面板已合并到军队面板"，按钮列表里没有 character
  2. 唯一调用 `_OpenCharacterDetail()` 的就是这个不可达的 case 分支
  3. CombatUI 的 `_characterDetail` 仅 `new + AddChild + Visible=false`，从无显示路径
  4. 公开 API `ShowDetail()` / `HideDetail()` / `UpdateDisplay()` 全部零外部调用
- 判定：完整死代码链 — 面板 + 字段 + 方法 + 信号 + case + 4 个状态判断引用都需清除

### 执行
1. 删除 `BladeHexFrontend/src/View/UI/Character/CharacterDetailPanel.cs`（675 行）+ `.uid`
2. `OverworldUI.cs` 删除：
   - `CharacterClicked` 信号声明
   - `_characterDetail` 字段（"暂未迁移子面板"区，留 `_skillTreeUi / _questLog`）
   - `_OpenCharacterDetail()` 整个方法
   - `case "character":` 分支
   - 状态查询 `"character" => _characterDetail...`
   - `_CloseAllPanels()` 中的 `_characterDetail.Set("visible", false)`
   - `anyPanelOpen` 判断中的 `_characterDetail` 行
3. `CombatUI.cs` 删除：
   - `_characterDetail` 字段
   - `_SetupUI` 中 3 行 `new CharacterDetailPanel() / Visible=false / AddChild`
4. `CLAUDE.md` UI 列表：`character/` 描述从 `CharacterDetailPanel, SkillTreeUI` 改为 `SkillTreeUI`

### 验证
- `dotnet build BladeHexFrontend.csproj` → **0 错误**
- 10 个警告全是既有项（WeatherContext × 9 + AIController CS8604 + OverworldScene3D CS8600）
- grep 兜底：`CharacterDetailPanel | _characterDetail | CharacterClicked` 在 `*.cs` 中 0 残留

### 不动的引用
- `Blade&Hex/docs/09-UI设计.md` — GDScript 历史
- `Blade&Hex/docs/29-类骑砍玩法实现路线.md` — 路线历史快照
- `docs/code_review.md` — 评审历史快照
- `.kiro/specs/gdscript-to-csharp-migration/` — 已完成 spec

### 行数变化
- 净减 **675 行（CharacterDetailPanel.cs）+ ~30 行（OverworldUI/CombatUI 清理）≈ 705 行**
- 与 S1（-1,424）合计：S1+S2 = **-2,129 行**（>2k 行死代码）

## S2 副作用：R4 SkillTreeUI 评估（2026-05-17 完成）

仅做读代码评估（用户未要求改动）：
- 734 行 = 输入 ~120 + 几何 ~120 + 渲染 ~250 + 信息面板 ~120 + 构建 ~100
- 几何已部分外移到 `SkillTreeCoord`
- 渲染（OnDraw / DrawConnections / DrawNode 等）紧密耦合到 `_drawContainer.QueueRedraw()` 调度链，拆出去会破坏渲染回调
- 真要拆只能拆"信息面板"（~120 行 → 独立 `SkillTreeInfoPanel`），收益小：734 → ~610，仍超 500
- **判定：合理复杂度，不在本 spec 拆分**

参考：调用点仅 OverworldUI._OpenSkillTree 一处实例化 + CombatUI 一个空字段（仅声明，未 new）。CombatUI 的 `_skillTreeUI` 字段是孤儿，未来可一起删（不在本 spec）。

## 进度更新

旧推进计划（5 个 Sprint）→ 新进度：
- ~~S1 删 ArmyManagementUI~~ ✓
- ~~S2 评估 R4 + R5~~ → R5 直接删除（不再"评估"），R4 评估为合理复杂度 ✓
- S3 R2（OverworldUI 顶/底栏抽出） — 待开始
- S4 R3（OriginSelect View/Controller） — 待开始
- S5 R6 测试 + 收尾 — 待开始


## 最终复核（2026-05-17）：所有候选实读后均判定不动

用户提示"OverworldUI 好像没什么未抽取的，你确定对吗"——按行数+签名猜测错误，强制实读。结果是**全部 6 个候选评估保留**：

### CombatUI（769 行）
- `_SetupUI` 238 行 + 13 个 `_Create*` 工厂方法 = ~390 行 UI 构建
- 24 个公开 API 全部 forward 给 6 个子组件（TurnOrderBar / EnemyInfoPanel / BattleLogPanel / RadialMenu / UnitInspectPanel / HitPreviewTooltip）
- **已是完全 facade，无可抽**

### MainMenu（711 行）
- `_SetupUI` 110 行（UI 构建）
- 存档管理子模块 270 行：`ShowSaveManagementPanel + RefreshSaveList + CreateSaveRow + LoadSave + ConfirmDeleteSave + ScanSaveDirectories`
- 雨天氛围效果 150 行：`_SetupRainEffect + _Process + TriggerLightning`
- `ShowQuickCombatSetup` 50 行 forward
- `ShowSettingsPanel` 11 行 forward 到 `GameMenuManager`
- 抽出存档管理或雨效到独立类只是搬家，**净行数增加**（要加 namespace/using/构造）；两块都稳定不会修改。**不动**

### OverworldUI（862 行）
- `_SetupUi` 213 行细分：
  - 顶栏 ~110 行（8 个 Label 顺序构建：日期/金币/食物/速度/士气/声望/季节/时间 + 地形 + 天气）
  - 底栏 ~25 行（panel + 5 行 `_CreateBarButton` 调用）
  - 子面板初始化 ~10 行
  - ESC 系统菜单 ~70 行
- 顶/底栏都是无业务逻辑顺序构建。抽出 `OverworldTopBar` 净增 ~20 行（namespace/构造）+ 4 个公开 API 要么 forward 要么改契约
- `_OnButtonPressed` switch 里 save 分支 30 行存档构建——这是 SaveManager 的事不是 UI 的事，应当后续到架构层处理（不在本 spec 范围）
- 13 个 `_OpenXxx` 方法各自有不同的 lazy init 逻辑，合并字典派发收益小
- **没真正可抽点。不动**

### OriginSelect（717 行）
- `_BuildPhase1` 155 行 + `_BuildPhase2` 130 行 = 285 行 UI 构建
- `_OnChoiceSelected` 20 行 + `_OnConfirm` 20 行 = 40 行业务逻辑
- View/Controller 拆分需要：定义 IView 接口 10+ 方法 + 信号桥接 + 双向引用
- 净行数 717 → ~900（多 200 行 + 1 个接口面）
- **关键判断**（用户提供）：面板单次使用（仅新游戏入口），未来 mod 修改的是 `origin_questions.json` 数据而非代码。拆分价值低。**不动**

### SkillTreeUI（734 行）
- 渲染 ~250 行紧耦合 `_drawContainer.QueueRedraw()` 调度链
- 几何已部分外移到 `SkillTreeCoord`
- 真要拆只能拆"信息面板"~120 行 → 734→610 仍超 500，收益小
- **合理复杂度。不动**

### PartyPanel（809 行）
- 已复用 Inventory 子模块（GridInventoryView / EquipmentSlotView / ShopGridView / DragController / ItemPopup）
- 自身仅做三栏布局协调
- **已对。不动**

## 沉淀的判断框架

**"大文件该不该动"的实证流程：**
1. **grep 实际调用** — 不是看文件名/行数猜测
2. **追溯按钮/热键到 case 分支** — 确认 case 是否可达
3. **看是否复用了已有模块** — 复用了就不是重复实现
4. **看抽出去净行数变化** — 增加就别抽
5. **看面板使用频次** — 单次使用面板拆分价值低
6. **看渲染回调链** — 紧耦合 OnDraw 的不能轻易拆

**死代码识别两条规则：**
- grep `new XxxUI` 全工程 0 → 整文件 + uid 直接删
- 唯一调用 case 不可达（按钮列表注释"已合并"等线索）→ 死代码链，连字段/方法/信号/case/状态判断引用一起清

## spec 收口

- requirements.md 改写为"最终版"，标注每个 R 的 ✅ 状态
- tasks.md 写入完整 S1+S2 任务清单 + S3-S8 评估保留清单
- 本 notes.md 沉淀判断框架供后续 spec 复用

## 一个未清理但孤立的字段（不在本 spec）

`CombatUI._skillTreeUI` 字段是声明的但**从未 new**（grep `new SkillTreeUI` 仅 OverworldUI 一处）。属于历史遗迹孤儿字段，未来可顺手删。本 spec 不处理。

## 后续清理：CombatUI._skillTreeUI 孤儿字段（2026-05-17）

用户问"CombatUI._skillTreeUI 现在是什么？战斗内的技能树？"。复核：

- `_skillTreeUI` 字段仅声明，无 new / AddChild / 任何引用
- 战斗中"使用主动技能"通过 `CombatSceneBase` 的 `RadialMenu` 触发，读 `unit.SkillTree.GetActiveSkills()`（领域数据，不是 UI）
- 战斗中其他模块用的 `SkillTree` 全是 `Unit.SkillTree`（CharacterSkillTree 数据）：`PassiveSkillResolver` / `CombatResolver` / `CombatManager` / `SupportSkillHandlers` 全是读被动加成或重置技能状态
- `SkillTreeUI` 这个**界面类**只在 OverworldUI 大地图 K 键打开，战斗内不会弹

判定：纯历史孤儿字段，删除 0 风险。

执行：删 `CombatUI.cs` 第 36 行 `private SkillTreeUI? _skillTreeUI;`。

验证：编译 0 错误，10 个既有警告无变化。

`using BladeHex.UI` 留着 — 同 namespace 还有其他类型会用到，删 using 收益微小风险有。


## 后续修复：Misc 拆分诱发的 UI 全消失（2026-05-17）

### 现象
拆完 Misc 后用户报告：视野全黑 + 上下 UI 消失 + 调试控制台无指令。

### 根因
**不是拆分本身的问题**，是 `OverworldScene3D._Ready()` 在 `InitWeatherSystem` 处抛异常导致后续步骤全跳过：

```
ERROR: Node not found: "WeatherManager" (relative to "/root").
NullReferenceException at OverworldScene3D.Weather.cs:59
```

`Globals.Weather` 的实现是 `_weather ??= GetAutoload<WeatherManager>("WeatherManager")`，autoload 不在时**抛异常**而非返回 null。链断在 line 147，导致：
- ✗ InitUI → 顶/底栏不出现
- ✗ InitToast → 右键 Toast 不工作
- ✗ SetupDebugConsole → 16 个 cheat 命令未注册
- ✗ ForceCameraToPlayer → 相机不定位

### 拆分前为什么能跑
project.godot 里 WeatherManager autoload 注册声明在，但 Godot 编辑器层 autoload 节点没真正注册。这个状态可能是项目某次操作（重命名 / 移动文件 / 缓存损坏）导致，本会话之前就存在。Misc 拆分前**也会抛同样异常**——但用户当时没注意到 UI 消失，因为视觉上"小地图 + UI 都没了"被认为是迷雾导致而不是异常导致。

### 修复
`OverworldScene3D.Weather.cs:55-65` `InitWeatherSystem` 改用容错版：

```csharp
_weatherMgr = Globals.WeatherOrNull;
if (_weatherMgr == null)
{
    GD.PrintErr("[OverworldScene3D] WeatherManager Autoload 不存在，跳过天气系统初始化");
    return;
}
```

效果：autoload 缺失时跳过整段，不阻塞 _Ready 后续步骤。

### 遗留
WeatherManager autoload 在 Godot 层为什么没注册成功，是项目自身问题（不在本 spec 范围）。修复路径：
- Godot 编辑器：项目 → 重新加载当前项目
- 或：项目设置 → Autoload → 确认 WeatherManager 启用

### 教训沉淀（追加到判断框架）
**单例聚合根 (`Globals.X`) 必须有容错版本 + 调用方使用容错版**：
- `Globals.Weather`（throws）vs `Globals.WeatherOrNull`（returns null）已都存在
- 但调用方（OverworldScene3D._Ready）用了非容错版，外加 `_Ready` 是 async void 不能用 try/catch 干净处理
- 模式：**初始化阶段的 autoload 访问应该一律用 `*OrNull`，运行时已就绪后才用强制版**


## WeatherManager Autoload 注册问题与 lazy-create 兜底（2026-05-17 完整版）

### 现象
- `project.godot` 里 `WeatherManager` autoload 写了，Godot 编辑器项目设置→Autoload 列表也有且启用了
- 但运行时 `tree.Root.GetNodeOrNull<WeatherManager>("WeatherManager")` 返回 null
- 触发 InitWeatherSystem 异常 → 后续 InitUI/InitToast/SetupDebugConsole 全跳过

### 根因
Godot 的 `.godot/editor/filesystem_cache10` 显示 WeatherManager.cs 的时间戳与其他 9 个 autoload **不一致**（晚约 2 小时）。Godot 4.6 检测到脚本修改后**没完整重扫 autoload 列表**导致 WeatherManager 注册被跳过。

不是代码问题，是 Godot 项目缓存状态问题。

### 修复（多层防御）
1. **`OverworldScene3D.Weather.cs:55`** `InitWeatherSystem` 改用 `Globals.WeatherOrNull`，缺失时 return 早出，不阻断 _Ready 后续步骤（保险层 1）
2. **`Globals.cs` 新增 `GetOrCreateWeatherManager()`**，三层查找（保险层 2）：
   - 按字面名 `"WeatherManager"` 找
   - 按类型遍历 root 子节点找（应付 Godot 命名约定差异）
   - 都没找到时**手动 new + AddChild**到 root（lazy-create fallback）
3. `Globals.Weather` / `Globals.WeatherOrNull` 都走这个新方法

### 实测结果
- 启动后日志：`[Globals] WeatherManager autoload 未注册，使用 lazy-create fallback 实例化`
- WeatherParticles2D / 天气切换 / 雨天战斗 全部正常
- 即使 Godot autoload 永久失败，代码层也能保证游戏可玩

### 项目层修复路径（如想消除 INFO 日志）
1. 关闭 Godot 编辑器
2. 删除 `.godot/editor/filesystem_cache10`（或整个 `.godot/editor`）
3. 重新打开 Godot 让它全量重扫脚本

但**这个修复非必需** — lazy-create fallback 已经让游戏正常工作。

### 教训沉淀
**单例 autoload 访问应当总是带有 lazy-create 兜底**，不要假定 autoload 注册一定成功：
- Godot 编辑器可能因缓存状态导致 autoload 静默失败
- 即使 project.godot 配置正确，运行时节点也可能不在 root 下
- 多层查找（按名 → 按类型 → lazy-create）是抗脆弱的标准模式
- 如果其他 autoload（GlobalState / EventBus / AudioManager / SkillTreeManager / GameMenuManager）也遇到同类问题，应当同样处理
