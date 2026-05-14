// ProjectileData.cs
// 投射物数据模型 — 纯数据，不依赖任何 Node 类型
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 投射物数据 — 描述一次投射物飞行的全部参数
/// 逻辑层和表现层通过此数据通信，互不直接引用
/// </summary>
[GlobalClass]
public partial class ProjectileData : RefCounted
{
    /// <summary>出发格坐标</summary>
    public Vector2I Origin = Vector2I.Zero;

    /// <summary>目标格坐标</summary>
    public Vector2I Target = Vector2I.Zero;

    /// <summary>投射物类型: "throwing_knife", "arrow", "fireball", "magic_bolt"</summary>
    public string ProjectileType = "arrow";

    /// <summary>飞行速度（格/秒）</summary>
    public float Speed = 8.0f;

    /// <summary>抛物线弧高（仅弓箭等弹道武器使用）</summary>
    public float ArcHeight = 1.5f;

    /// <summary>命中伤害 — 由 DamageSystem 结算，ProjectileSystem 不处理</summary>
    public int Damage = 0;

    /// <summary>发射者 UnitData 引用</summary>
    public int AttackerUnitId = -1;

    /// <summary>目标 UnitData 引用</summary>
    public int TargetUnitId = -1;

    /// <summary>自定义贴图路径（空则用默认）</summary>
    public string TexturePath = "";

    // ===== 序列化 =====

    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            ["origin"] = Origin,
            ["target"] = Target,
            ["type"] = ProjectileType,
            ["speed"] = Speed,
            ["arc_height"] = ArcHeight,
            ["damage"] = Damage,
            ["attacker_id"] = AttackerUnitId,
            ["target_id"] = TargetUnitId,
            ["texture_path"] = TexturePath,
        };
    }

    public static ProjectileData Deserialize(Godot.Collections.Dictionary data)
    {
        var p = new ProjectileData();
        if (data.ContainsKey("origin")) p.Origin = (Vector2I)data["origin"];
        if (data.ContainsKey("target")) p.Target = (Vector2I)data["target"];
        if (data.ContainsKey("type")) p.ProjectileType = data["type"].AsString();
        if (data.ContainsKey("speed")) p.Speed = data["speed"].AsSingle();
        if (data.ContainsKey("arc_height")) p.ArcHeight = data["arc_height"].AsSingle();
        if (data.ContainsKey("damage")) p.Damage = data["damage"].AsInt32();
        if (data.ContainsKey("attacker_id")) p.AttackerUnitId = data["attacker_id"].AsInt32();
        if (data.ContainsKey("target_id")) p.TargetUnitId = data["target_id"].AsInt32();
        if (data.ContainsKey("texture_path")) p.TexturePath = data["texture_path"].AsString();
        return p;
    }
}
