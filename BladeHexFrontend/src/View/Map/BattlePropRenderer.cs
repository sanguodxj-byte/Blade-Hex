// BattlePropRenderer.cs
// 战斗地图立牌渲染器 — 把 BattlePropPlacement 实例化为 Sprite3D billboard
//
// HD-2D 风格约定：
// - Sprite3D + BillboardMode.FixedY（绕 Y 轴面向相机，不躺倒）
// - AlphaCut = OpaquePrepass 避免 z-fight
// - 底部贴一个椭圆阴影 decal（可选，未实现时省略）
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 战斗地图立牌渲染器
/// 挂在 HexGrid 子节点；调用 AddProp / ClearPropsFor 管理格子上的立牌
/// </summary>
[GlobalClass]
public partial class BattlePropRenderer : Node3D
{
    /// <summary>每个 HexCell 坐标下挂载的所有 prop 节点</summary>
    private readonly Dictionary<Vector2I, List<Sprite3D>> _propsByCell = new();

    /// <summary>立牌像素缩放（越小立牌越小）</summary>
    [Export] public float BasePixelSize { get; set; } = 0.08f;

    public override void _Ready()
    {
        Name = "BattlePropRenderer";
    }

    /// <summary>为一个 HexCell 实例化所有 prop</summary>
    public void AddPropsForCell(Vector2I cellCoord, Vector3 cellWorldPos, List<BattlePropPlacement> props)
    {
        if (props == null || props.Count == 0) return;

        ClearPropsFor(cellCoord);

        var list = new List<Sprite3D>();
        foreach (var placement in props)
        {
            var sprite = CreatePropSprite(placement);
            sprite.Position = cellWorldPos + placement.LocalOffset;
            AddChild(sprite);
            list.Add(sprite);
        }
        _propsByCell[cellCoord] = list;
    }

    /// <summary>清除某格子上的所有 prop</summary>
    public void ClearPropsFor(Vector2I cellCoord)
    {
        if (!_propsByCell.TryGetValue(cellCoord, out var list)) return;
        foreach (var sprite in list)
        {
            if (GodotObject.IsInstanceValid(sprite))
                sprite.QueueFree();
        }
        _propsByCell.Remove(cellCoord);
    }

    /// <summary>清空所有 prop</summary>
    public void ClearAll()
    {
        foreach (var kvp in _propsByCell)
        {
            foreach (var sprite in kvp.Value)
            {
                if (GodotObject.IsInstanceValid(sprite))
                    sprite.QueueFree();
            }
        }
        _propsByCell.Clear();
    }

    /// <summary>为一个 placement 创建 Sprite3D 节点</summary>
    private Sprite3D CreatePropSprite(BattlePropPlacement placement)
    {
        var sprite = new Sprite3D();
        sprite.Name = $"Prop_{placement.PropId}";
        sprite.Texture = BattlePropRegistry.GetTexture(placement.PropId);
        sprite.PixelSize = BasePixelSize * placement.Scale;

        // HD-2D 立牌参数
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        sprite.Shaded = false;
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        sprite.AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass;
        sprite.AlphaScissorThreshold = 0.5f;
        sprite.DoubleSided = false;

        // 纸片底边对齐格子地面
        sprite.Offset = new Vector2(0, sprite.Texture.GetHeight() / 2.0f);

        // 朝向：billboard 时 Y 旋转无视觉效果（shader 会覆盖），但保留以防切换到 non-billboard 模式
        sprite.RotationDegrees = new Vector3(0, placement.YawDegrees, 0);

        return sprite;
    }

    /// <summary>当前渲染的立牌总数（调试用）</summary>
    public int TotalPropCount
    {
        get
        {
            int n = 0;
            foreach (var kv in _propsByCell) n += kv.Value.Count;
            return n;
        }
    }
}
