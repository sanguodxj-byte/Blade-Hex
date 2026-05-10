# Godot GDScript 项目守则

**本文件在每个会话开始时必须阅读。所有涉及 .gd 文件的操作必须遵守以下规则。**

---

## 工具

- **Godot CLI**: `C:\Users\Administrator\Desktop\Godot_v4.6.2-stable_win64.exe`
- **项目路径**: `D:\123\新建游戏项目`
- **快捷验证**: `tools\validate.bat`

**修改任何 .gd 文件后，必须跑一次 Godot 验证再报告完成。**

```bash
& "C:\Users\Administrator\Desktop\Godot_v4.6.2-stable_win64.exe" --headless --editor --quit --path "D:\123\新建游戏项目" 2>&1 | Select-String -Pattern "error|Error|SCRIPT" -CaseSensitive:$false
```

---

## 血泪教训（强制遵守）

### 1. 绝不用 PowerShell 处理含中文的文件内容
**背景**: PowerShell 的 `Get-Content -Raw` + `-replace` + `WriteAllText` 会把 UTF-8 中文变成乱码，且不可逆。CharacterGenerator.gd 的全部中文注释和字符串因此被永久损坏，不得不手动重建。

**规则**:
- 读取/修改含中文的文件 → 用 Python 脚本（`open(path, 'r', encoding='utf-8')`）
- 或用 Read/Edit/Write 工具（它们正确处理 UTF-8）
- **永远不要**用 PowerShell 的 `-replace`、`Get-Content`、`Set-Content`、`WriteAllText` 处理 .gd 文件

### 2. create_tween() 必须检查 null
**背景**: `create_tween()` 在节点不在场景树中时返回 null，链式调用 `.set_trans()` / `.set_loops()` / `.set_parallel()` 会崩溃。

**规则**:
```gdscript
# ❌ 错误
var tween = create_tween().set_parallel(false)
_tween = create_tween().set_loops()

# ✅ 正确
var tween = create_tween()
if tween == null:
    return
tween.set_parallel(false)
```

### 3. Object.get() 只接受 1 个参数
**背景**: Godot 4 中 `Dictionary.get(key, default)` 支持 2 参数，但 `Object.get(key)` 只接受 1 参数。`Resource`/`UnitData` 等 extend `Object`，不是 Dictionary。

**规则**:
```gdscript
# ❌ 错误（UnitData 是 Resource，不是 Dictionary）
unit_data.get("_flag", false)

# ✅ 正确
unit_data.get("_flag") == true          # 判断布尔
int(unit_data.get("_stacks") or 0)      # 带默认值
```

### 4. 静态方法 vs 实例方法
**背景**: 对实例方法用 `ClassName.method()` 调用会报 "Cannot call non-static function"。

**规则**:
- 如果方法不依赖实例状态 → 加 `static` 关键字
- 如果方法依赖实例状态 → 必须通过实例调用

### 5. HBoxContainer/VBoxContainer 没有 vertical_alignment
**背景**: `vertical_alignment` 是 `Label` 的属性，不是 `BoxContainer` 的。

**规则**:
- 容器的子节点垂直对齐 → 在每个子 Label 上设 `vertical_alignment`
- 容器本身的排列 → 用 `alignment` 属性

### 6. CanvasLayer 没有 modulate 属性
**背景**: LoadingScreen extends CanvasLayer，`tween_property(self, "modulate:a", ...)` 会报错 "The tweened property does not exist"。`modulate` 是 `CanvasItem` 的属性，`CanvasLayer` 不继承它。

**规则**:
- `CanvasLayer` 上的淡入淡出 → 作用于子 Control 节点（如 `_content_container`）
- 不要对 `self`（CanvasLayer）做 modulate 动画

### 7. 大型字典/数组字面量不要跨模板粘贴属性
**背景**: BattleMapGenerator.gd 中 8 个地图模板的属性代码被错插到别的模板的字典字面量中间，导致逻辑错误。

**规则**:
- 每个模板定义必须是完整的、自包含的代码块
- 修改前看清当前花括号/方括号的嵌套层级

---

## 验证检查清单

每次修改 .gd 文件后，确认以下全部通过：

- [ ] `Godot --headless --editor --quit` 无 SCRIPT ERROR
- [ ] 无 `create_tween().xxx` 链式调用（必须拆开加 null 检查）
- [ ] 无 `.get(key, default)` 在 Object/Resource 类型上（只在 Dictionary 上用）
- [ ] 无 `ClassName.non_static_method()` 调用
- [ ] 文件编码为 UTF-8 无 BOM（如需批量修改用 Python，不用 PowerShell）

---

## 项目结构速览

```
src/
├── core/
│   ├── ai/          # AI 控制器 + 策略
│   ├── character/   # 角色生成器
│   ├── combat/      # 战斗系统（解析器、技能、士气、状态效果）
│   ├── data/        # 数据层（UnitData、RPGRuleEngine、存档等）
│   ├── interaction/ # NPC 交互
│   ├── map/         # 六角格地图生成
│   ├── quest/       # 任务系统
│   ├── skill_tree/  # 技能树
│   ├── strategic/   # 战略层（大地图、世界生成）
│   └── unit/        # 单位基类
├── resources/       # 资源数据
├── scenes/          # 场景（战斗、大地图、测试）
└── ui/              # UI 组件
    ├── combat/      # 战斗 UI
    ├── loading/     # 加载界面
    ├── main_menu/   # 主菜单
    ├── overworld/   # 大地图 UI 面板
    └── quest/       # 任务 UI
```
