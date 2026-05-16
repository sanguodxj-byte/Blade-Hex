---
inclusion: always
---

# 全局对象分类规范

## 三大类别

项目中所有"可被多处访问"的对象必须归入下列三类之一。新增类时按决策树定位，不允许出现"野单例"。

### 1. Autoload Singleton（全局生命周期）

**定义：** 跨场景持久存在，从应用启动到退出都活着。
**注册方式：** `project.godot` 的 `[autoload]` 段。
**访问方式：** `XxxName.Instance`，可空检查后使用。

**判定标准（必须同时满足）：**
- 状态需要跨场景保持（玩家进入战斗、退出战斗、回大地图，状态不丢）
- 全应用只有一份意义
- 不属于任何具体场景的子领域

**当前归类：**

| 类 | 注册路径 | 用途 |
|----|----------|------|
| `GlobalState` | `BladeHexFrontend/src/View/Data/GlobalState.cs` | 跨场景状态聚合根（拆分后承载 SaveContext / WorldGenContext / QuickCombatContext / WeatherContext / PlayerOriginContext） |
| `EventBus` | `BladeHexFrontend/src/View/Events/EventBus.cs` | 全局事件总线（强类型 + 弱类型兼容） |
| `AudioManager` | `BladeHexFrontend/src/View/Audio/AudioManager.cs` | BGM / SFX 跨场景管理 |
| `AudioEventReactor` | `BladeHexFrontend/src/View/Events/AudioEventReactor.cs` | 监听事件触发音效 |
| `UITheme` | `BladeHexFrontend/src/View/UI/UITheme.cs` | UI 主题常量与 stylebox 工厂 |
| `DebugConsole` | `BladeHexFrontend/src/View/Debug/DebugConsole.cs` | 全局调试控制台 |
| `GameMenuManager` | `BladeHexFrontend/src/View/UI/Global/GameMenuManager.cs` | ESC 系统菜单（设置 / 存读档） |
| `SkillTreeManager` | `BladeHexFrontend/src/View/SkillTreeManager.cs` | 角色技能盘进度（跨场景持久） |

**编码约定：**
- 类必须有头部注释，标明 `[Autoload Singleton]` + 注册路径 + 测试替换方式
- `static Instance` 字段必须 `private set;`
- `_EnterTree` 写入 `Instance`，`_ExitTree` 清空（防止重复加载）
- 测试替换：通过 `#if DEBUG` 包裹的 `OverrideForTest` 静态方法

### 2. Scene Service（场景生命周期）

**定义：** 在某个场景（OverworldScene3D / CombatScene 等）内单实例存在，场景结束即销毁。
**注册方式：** 由所属场景节点持有，`[Export]` 暴露引用或 `_Ready` 中创建/查找。
**访问方式：** 通过场景节点引用，**禁止 `static Instance`**。

**判定标准：**
- 状态只在当前场景有意义
- 离开场景后状态可丢弃
- 场景内有多个组件需要协作（适合作为公共服务而不是散落在各组件里）

**当前归类（含目标状态）：**

| 类 | 当前 | 目标 |
|----|------|------|
| `QuestManager` | static Instance（错位） | OverworldScene3D 持有 |
| `VFXManager` | static Instance（错位） | CombatSceneBase 持有 |
| `HexCellMultiMeshBatcher` | static Instance（错位） | HexGrid 直接持有 |

**编码约定：**
- 不暴露 `static Instance`
- 父场景通过 `[Export] public XxxService Service { get; set; }` 装配
- 子组件需要时通过父场景或 `IXxxContext` 接口拿
- 头部注释标明 `[Scene Service]` + 所属场景

### 3. Plain Helper（无状态工具）

**定义：** 静态方法集合，不持有可变状态。
**注册方式：** 普通 C# `static class`。
**访问方式：** 类名直接调用。

**判定标准：**
- 完全无状态（输入决定输出）
- 不依赖场景树、不依赖 Godot 节点

**当前示例：**

| 类 | 用途 |
|----|------|
| `HexUtils` | 六边形坐标转换 |
| `CoordConverter` | 屏幕 / 世界坐标转换 |
| `CombatStats` | 属性 modifier 计算（纯函数） |
| `HexOverworldAStar` | 寻路算法（纯函数式 API） |

**编码约定：**
- `static class`
- 方法都是纯函数：相同输入产生相同输出，无副作用
- 如果发现需要持有状态，立刻重新归类

---

## 决策树（新增对象时使用）

```
新增一个会被多处访问的对象
        │
        ▼
┌────────────────────────────────────┐
│ 它需要跨场景保持状态吗？            │
└────────────────────────────────────┘
   │ 是                       │ 否
   ▼                          ▼
┌──────────────┐    ┌────────────────────────────────────┐
│ Autoload     │    │ 它需要持有可变状态吗？              │
│ Singleton    │    └────────────────────────────────────┘
└──────────────┘       │ 是                       │ 否
                       ▼                          ▼
                  ┌──────────────┐         ┌──────────────┐
                  │ Scene        │         │ Plain        │
                  │ Service      │         │ Helper       │
                  └──────────────┘         └──────────────┘
```

## 反模式

❌ **不要出现：**

```csharp
// ❌ 普通业务类做静态单例
public class MyManager
{
    public static MyManager Instance { get; set; }
}

// ❌ Scene Service 暴露静态访问
public partial class QuestManager : Node
{
    public static QuestManager Instance { get; set; }  // 改为场景持有
}

// ❌ Helper 类持有可变状态
public static class HexUtils
{
    private static int _cachedCount;  // ← 改成实例
}

// ❌ 字符串路径硬编码访问 Autoload
GetNode<GlobalState>("/root/GlobalState")  // ← 改用 GlobalState.Get()
```

✅ **应该这样：**

```csharp
// ✅ Autoload 用 Instance + 静态访问器
public partial class GlobalState : Node
{
    public static GlobalState Instance { get; private set; } = null!;
    public static GlobalState Get() => Instance;
}

// ✅ Scene Service 由场景持有
public partial class OverworldScene3D : Node3D
{
    [Export] public QuestManager QuestManager { get; set; } = null!;
}

// ✅ Helper 全静态无状态
public static class HexUtils
{
    public static Vector3 AxialToWorld(int q, int r) => ...;  // 纯函数
}
```

---

## 测试可替换性

**Autoload 类**必须支持测试替换：

```csharp
public partial class EventBus : Node
{
    public static EventBus? Instance { get; private set; }

#if DEBUG
    /// <summary>仅测试用：替换 Instance 为 mock 或重置为 null</summary>
    public static void OverrideForTest(EventBus? mock) => Instance = mock;
#endif
}
```

**Scene Service** 在测试时直接 `new` 实例传给被测对象，不依赖场景树。

**Plain Helper** 无需特殊处理，纯函数天然可测。


---

## EventBus 事件命名规范

EventBus 同时支持**强类型**和**弱类型**两种 API，新代码必须用强类型：

### 强类型 API（推荐）

```csharp
// 事件类型放在 BladeHex.Events.Payloads 命名空间
public sealed record UnitDamagedEvent(Node3D Unit, int Damage, int RemainingHp);

// 订阅
EventBus.Instance?.Subscribe<UnitDamagedEvent>(OnDamaged);

// 发布
EventBus.Instance?.Publish(new UnitDamagedEvent(unit, 10, 80));

// 取消订阅（_ExitTree 中）
EventBus.Instance?.Unsubscribe<UnitDamagedEvent>(OnDamaged);
```

**编码约定：**
- 事件类型用 `record` 或 `record class`（不可变快照）
- 命名以 `Event` 结尾：`UnitDamagedEvent`、`DayPassedEvent`
- 命名空间统一在 `BladeHex.Events.Payloads`
- 字段优先用值类型 + 不可变引用（`int`、`string`、`Node3D` 等）

### 弱类型 API（已过时，仅兼容期）

```csharp
// ❌ 新代码不要用
EventBus.Instance?.Subscribe(EventBus.Signals.UnitDamaged, dict => {
    int dmg = dict["damage"].AsInt32();  // 字符串键 + Variant 解码，易错
});

EventBus.Instance?.Publish(EventBus.Signals.UnitDamaged, new Godot.Collections.Dictionary {
    { "damage", 10 }
});
```

弱类型路径将在所有调用方迁移完成后删除。

### 何时新增事件类型

需要新增事件时：
1. 在 `Payloads/CoreEvents.cs`（或主题相关的新文件）添加 `record`
2. 在 `EventBus` 内增加 `PublishXxx` 双发便捷方法（强类型 + 旧 Signals 字符串）
3. 订阅方 `Subscribe<XxxEvent>(...)`

**禁止只发字符串 + Dictionary**：会留下永远迁不走的弱类型债务。
