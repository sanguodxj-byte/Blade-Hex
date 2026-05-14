// BattlePropPlacement.cs
// 战斗地图立牌（prop）摆放数据 — 纯 Core 层模型
//
// 背景：战斗地图是 3D 六棱柱 + 贴 2D 纹理，树/石/建筑/角色都是 billboard 立牌。
// 每个 HexCell 可挂载 0..N 个 prop；prop 的资源 ID 在 BattlePropPack 里列出，
// 由 View 层的 BattlePropRenderer + BattlePropRegistry 解析为 Sprite3D。
//
// 本类只持 string ID 和位置参数，不碰 Texture2D / Sprite3D 等渲染类型。
using Godot;

namespace BladeHex.Map;

/// <summary>
/// 单个 prop 的布置信息（挂在 HexCell 上）
/// </summary>
public sealed class BattlePropPlacement
{
    /// <summary>prop 资源 id（对应 BattlePropRegistry 里的贴图）</summary>
    public string PropId = "";

    /// <summary>格内偏移（本地坐标，单位同 hex size）。默认 0 = 格中心</summary>
    public Vector3 LocalOffset = Vector3.Zero;

    /// <summary>绕 Y 轴的朝向偏移（度），仅对 non-billboard 的 prop 生效</summary>
    public float YawDegrees = 0.0f;

    /// <summary>缩放系数</summary>
    public float Scale = 1.0f;

    /// <summary>是否作为半掩体（给战斗规则读）</summary>
    public bool ProvidesHalfCover = false;

    /// <summary>是否作为全掩体</summary>
    public bool ProvidesFullCover = false;

    /// <summary>是否阻挡视线</summary>
    public bool BlocksLineOfSight = false;
}
