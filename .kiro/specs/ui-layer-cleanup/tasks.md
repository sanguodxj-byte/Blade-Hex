# UI 层清理 — Tasks

## Sprint 1: 删除 ArmyManagementUI 死代码 ✅

- [x] 1.1 grep 验证 `new ArmyManagementUI` 全工程 0 引用
- [x] 1.2 删除 `BladeHexFrontend/src/View/UI/Overworld/ArmyManagementUI.cs`（1,424 行）
- [x] 1.3 删除 `ArmyManagementUI.cs.uid`
- [x] 1.4 修正 `POIPanelBase.cs` 注释（`物品相关面板使用 ArmyManagementUI` → `... PartyPanel`）
- [x] 1.5 更新 `CLAUDE.md` UI 列表移除 `ArmyManagementUI`
- [x] 1.6 编译 BladeHexFrontend 验证 0 错误
- [x] 1.7 grep 兜底确认代码层 0 残留（仅历史文档不动）

**验收**：编译 0 错误；行为等价（本来就没在用）。

## Sprint 2: 删除 CharacterDetailPanel 死代码 ✅

- [x] 2.1 grep 验证 `_OpenCharacterDetail` 唯一调用是 `case "character"`
- [x] 2.2 验证底栏 5 个按钮无 `character`（注释"角色面板已合并到军队面板"）
- [x] 2.3 验证 CombatUI `_characterDetail` 仅 new+AddChild，无显示路径
- [x] 2.4 验证公开 API `ShowDetail` 零外部调用
- [x] 2.5 删除 `BladeHexFrontend/src/View/UI/Character/CharacterDetailPanel.cs`（675 行）+ `.uid`
- [x] 2.6 OverworldUI 清理：
  - [x] 删 `CharacterClicked` 信号
  - [x] 删 `_characterDetail` 字段
  - [x] 删 `_OpenCharacterDetail()` 方法
  - [x] 删 `case "character":` 分支
  - [x] 删状态查询 `"character"` 分支
  - [x] 删 `_CloseAllPanels` 中的 `_characterDetail` 行
  - [x] 删 `anyPanelOpen` 中的 `_characterDetail` 行
- [x] 2.7 CombatUI 清理：
  - [x] 删 `_characterDetail` 字段
  - [x] 删 3 行 `new + Visible + AddChild`
- [x] 2.8 更新 `CLAUDE.md` UI 列表
- [x] 2.9 编译 BladeHexFrontend 验证 0 错误
- [x] 2.10 grep 兜底 `CharacterDetailPanel|_characterDetail|CharacterClicked` 在 `*.cs` 中 0 残留

**验收**：编译 0 错误；行为等价（本来就无显示路径）。

## Sprint 3-7: 评估保留（不执行）✅

每个文件经实际读代码评估后判定为合理复杂度或抽出会净增行数：

- [x] 3 PartyPanel — 已复用 Inventory 子模块
- [x] 4 MainMenu — 子模块稳定，抽出净增行数
- [x] 5 OverworldUI — 顶/底栏无业务逻辑
- [x] 6 OriginSelect — 单次使用面板，mod 改 JSON 不改代码
- [x] 7 SkillTreeUI — 渲染紧耦合调度链
- [x] 8 CombatUI — 已 facade

评估结论详见 `notes.md`。

## 收尾

- [x] 9.1 更新 `requirements.md` 反映最终范围（仅 R1+R2 执行，R3-R8 评估保留）
- [x] 9.2 完整 `notes.md` 决策日志
- [x] 9.3 编写 `tasks.md`（本文件）

## 累计行数变化

| 阶段 | 净变化 |
|------|--------|
| S1 | -1,424 |
| S2 | -705 |
| **合计** | **-2,129** |

UI 层 19,791 → ~17,662 行（-10.8%）。单文件最大 1,424 → 862（-39%）。
