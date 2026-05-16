using Godot;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 玩家选择的出身数据 — 由 OriginSelect 写入，OverworldScene3D 读取一次后即不再使用。
///
/// 字段说明：
/// - <c>race</c>：<see cref="BladeHex.Data.RaceData"/> 引用
/// - <c>unit_data</c>：玩家初始化好的 <see cref="BladeHex.Data.UnitData"/>
/// - <c>gender</c>："male" / "female"
/// - <c>companion</c>：伙伴选项 summary（"忠犬相随"等）
/// - <c>items</c>：起始物品名称数组
///
/// 仍以 Dictionary 形式存放是因为下游消费方较多且包含强类型 Resource 引用，
/// 后续可改造成强类型字段；当前优先消除 GlobalState 顶层字段膨胀。
/// </summary>
[GlobalClass]
public partial class PlayerOriginContext : Resource
{
    /// <summary>出身数据字典（详见类注释）。</summary>
    [Export] public Godot.Collections.Dictionary Data { get; set; } = new();

    /// <summary>是否已设置出身数据（通常等价于"主菜单走过出身选择"）。</summary>
    public bool HasData => Data.Count > 0;
}
