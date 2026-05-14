// BattleUnitDeployment.cs
// 战斗部署数据 — 描述一个单位在战斗中的部署配置
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗部署数据 — 描述一个战斗单位的部署配置
/// 用于从战略层实体生成战斗场景中的具体单位
/// </summary>
[GlobalClass]
public partial class BattleUnitDeployment : RefCounted
{
    /// <summary>单位模板 ID（对应 UnitTemplateDB 中的条目）</summary>
    public string UnitTemplateId = "";

    /// <summary>部署位置（战斗网格坐标）</summary>
    public Vector2I DeployPosition = Vector2I.Zero;

    /// <summary>数量（同种单位复制几份）</summary>
    public int Count = 1;

    /// <summary>等级覆盖（0 = 使用模板默认）</summary>
    public int LevelOverride = 0;

    /// <summary>是否由玩家控制</summary>
    public bool IsPlayerControlled = false;

    /// <summary>装备方案 ID（对应装备配置表）</summary>
    public string EquipmentSchemeId = "";

    /// <summary>指定部署区域（"front_line", "back_line", "flank_left", "flank_right"）</summary>
    public string DeployZone = "front_line";

    // ===== 序列化 =====
    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            ["template_id"] = UnitTemplateId,
            ["position"] = DeployPosition,
            ["count"] = Count,
            ["level"] = LevelOverride,
            ["player_controlled"] = IsPlayerControlled,
            ["equipment_scheme"] = EquipmentSchemeId,
            ["zone"] = DeployZone,
        };
    }

    public static BattleUnitDeployment Deserialize(Godot.Collections.Dictionary data)
    {
        var deploy = new BattleUnitDeployment();
        if (data.ContainsKey("template_id")) deploy.UnitTemplateId = data["template_id"].AsString();
        if (data.ContainsKey("position")) deploy.DeployPosition = (Vector2I)data["position"];
        if (data.ContainsKey("count")) deploy.Count = data["count"].AsInt32();
        if (data.ContainsKey("level")) deploy.LevelOverride = data["level"].AsInt32();
        if (data.ContainsKey("player_controlled")) deploy.IsPlayerControlled = data["player_controlled"].AsBool();
        if (data.ContainsKey("equipment_scheme")) deploy.EquipmentSchemeId = data["equipment_scheme"].AsString();
        if (data.ContainsKey("zone")) deploy.DeployZone = data["zone"].AsString();
        return deploy;
    }
}