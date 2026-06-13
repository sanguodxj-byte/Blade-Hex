// CombatLayerHeight.cs
// 战斗场景渲染层级 Y 轴偏移常量。
// 从低到高：六棱柱顶面 → 纹理层 → 遮盖层(高亮) → UI提示层 → 角色
// 所有偏移相对于 cell.Position（六棱柱中心）。
using BladeHex.Map;

namespace BladeHex.View.Combat;

/// <summary>
/// 战斗场景渲染层级 Y 偏移量（相对 hex 顶面）。
/// hex 顶面 = cell.Position.Y + HexTopOffset
/// </summary>
public static class CombatLayerHeight
{
    /// <summary>六棱柱顶面相对 cell.Position 的 Y 偏移</summary>
    public static readonly float HexTopOffset = HexUtils.Size * 0.25f; // 24

    /// <summary>纹理覆盖层（草地/泥土/雪地/水面精灵）</summary>
    public const float TextureLayer = 0.5f;

    /// <summary>遮盖层（移动范围/攻击范围高亮色块）
    /// 必须高于草地精灵最大 Y(TextureLayer + yJitter≈2.5)，低于角色脚底(CharacterLayer)。
    /// 24 + 3.5 = 27.5，安全区间：草地(26.0) < Overlay(27.5) < Character(29.0)</summary>
    public const float OverlayLayer = 3.5f;

    /// <summary>脚底接触阴影层。高于大多数地表纹理，低于高亮层。</summary>
    public const float ContactShadowLayer = 2.8f;

    /// <summary>技能假光投影层。与接触阴影相近，略高以避免闪烁。</summary>
    public const float FakeLightLayer = 3.0f;

    /// <summary>UI 提示层（路径预览线、悬浮魔法阵 Decal）
    /// 在高亮层之上、角色脚底之下，确保路径线不被高亮遮挡。</summary>
    public const float UIHintLayer = 4.2f;

    /// <summary>角色 body 底部</summary>
    public const float CharacterLayer = 5.0f;
}
