---
inclusion: always
---

# C# ↔ GDScript 互操作规则

## 核心问题

Godot 4 的 C# source generator 对 `[Export]` 属性暴露给 GDScript **不可靠**，尤其是：
- 跨程序集（BladeHexCore 的类被 GDScript 访问）
- `Resource` 子类的属性
- 编辑器缓存未刷新时

## 硬规则

### 规则 1：任何被 GDScript 读写字段的 C# 类，必须手动实现 `_Get` / `_Set` / `_GetPropertyList`

不要依赖 `[Export]` 自动暴露。用 `_Get` 覆写是唯一 100% 可靠的方式。

### 规则 2：GDScript 访问 C# 属性时一律用 snake_case

C# `OptionLabel` → GDScript `option_label`
C# `IsLeaderAlive` → GDScript `is_leader_alive`

### 规则 3：C# `List<T>` 不能直接被 GDScript 访问

必须在 `_Get` 中转为 `Godot.Collections.Array` 返回。

### 规则 4：C# 类继承选择

| 场景 | 继承 |
|---|---|
| 被 GDScript 访问字段 | `Resource`（不是 `RefCounted`） |
| 只被 C# 使用 | `RefCounted` 或普通类 |
| 场景节点 | `Node` / `Node2D` / `Node3D` |

### 规则 5：新建被 GDScript 访问的 C# 数据类时，使用此模板

```csharp
[GlobalClass]
public partial class MyDataClass : Resource
{
    // C# 侧正常使用的属性
    public string MyField { get; set; } = "";
    public int MyNumber { get; set; } = 0;
    public List<SomeType> MyList { get; set; } = new();

    // GDScript 暴露（必须实现）
    public override Variant _Get(StringName property)
    {
        return property.ToString() switch
        {
            "my_field" => MyField,
            "my_number" => MyNumber,
            "my_list" => ToGdArray(MyList),
            _ => default,
        };
    }

    public override bool _Set(StringName property, Variant value)
    {
        switch (property.ToString())
        {
            case "my_field": MyField = value.AsString(); return true;
            case "my_number": MyNumber = value.AsInt32(); return true;
            default: return false;
        }
    }

    private static Godot.Collections.Array ToGdArray<T>(List<T> list) where T : GodotObject
    {
        var arr = new Godot.Collections.Array();
        foreach (var item in list) arr.Add(item);
        return arr;
    }
}
```

### 规则 6：避免 C# 属性名与 Godot 内置类名冲突

禁止：`Label`, `Button`, `Panel`, `Timer`, `Node`, `Control`, `Texture`, `Material`
改用：`OptionLabel`, `ActionButton`, `InfoPanel` 等带前缀的名字

## 已知需要 `_Get` 的类

- `InteractionOption` ✅ 已实现
- `PartyRoster` ✅ 已实现
- `TownFacility` — 如果 GDScript 直接访问字段需要加
- `OverworldTown` — 已用 `[Export]` + Node2D（Node 子类的 Export 可靠）
- `OverworldEnemy` — 同上
- `OverworldPOI` — Resource 子类，可能需要加
- `NPCProfile` — Resource 子类，目前工作（可能因为字段名简单）

## 检查清单（新增 C# 类时）

- [ ] 这个类会被 GDScript 访问吗？
- [ ] 如果是 → 继承 `Resource` + 实现 `_Get`/`_Set`
- [ ] 如果否 → 随意继承
- [ ] GDScript 侧用 snake_case 访问
- [ ] `List<T>` 字段在 `_Get` 中转 `Godot.Collections.Array`
