// SceneDecorationPlacer.cs
// 战斗场景装饰精灵放置器
// 职责：根据地图数据在 3D 场景中放置 2D 装饰精灵（树木、岩石、旗帜等）
// 所有装饰使用 Sprite3D + Billboard Enabled + Nearest 过滤
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 战斗场景装饰精灵放置器
/// 根据地形类型和掩体等级在对应格子上放置 2D 装饰精灵。
/// </summary>
[GlobalClass]
public partial class SceneDecorationPlacer : Node3D
{
    // ========================================
    // 地形类型 → 装饰精灵 ID 映射
    // ========================================

    private static readonly Dictionary<BattleCellData.TerrainType, string[]> TerrainDecorations = new()
    {
        { BattleCellData.TerrainType.Forest, new[] { "tree_oak", "tree_pine", "bush_green" } },
        { BattleCellData.TerrainType.DenseForest, new[] { "tree_oak", "tree_pine", "tree_pine", "bush_green" } },
        { BattleCellData.TerrainType.Ruins, new[] { "rock_large", "rock_moss", "barrel", "crate" } },
        { BattleCellData.TerrainType.Swamp, new[] { "tree_dead", "bush_dry", "skull" } },
        { BattleCellData.TerrainType.Snow, new[] { "tree_pine", "rock_small" } },
        { BattleCellData.TerrainType.Sand, new[] { "rock_small", "rock_large" } },
        { BattleCellData.TerrainType.Savanna, new[] { "bush_dry", "rock_small" } },
        { BattleCellData.TerrainType.PoisonMushroom, new[] { "poison_mushroom_prop" } },
    };

    /// <summary>掩体等级 → 装饰精灵 ID</summary>
    private static readonly Dictionary<int, string[]> CoverDecorations = new()
    {
        { 1, new[] { "bush_green", "fence_wood", "barrel" } },   // 半掩体
        { 2, new[] { "rock_large", "fence_stone", "crate" } },   // 全掩体
    };

    // ========================================
    // 配置
    // ========================================

    /// <summary>每个格子放置装饰的概率（0.0~1.0）</summary>
    private const float DecorationChance = 0.3f;

    /// <summary>装饰精灵的像素大小(基础值;角色 sprite 用 PixelSize=2,装饰物显著小一档避免遮挡)</summary>
    private const float SpritePixelSize = 0.5f;

    /// <summary>装饰精灵 Y 轴基础偏移(放在格子顶面上方。格子顶面为 cell.Position.Y + HexHeight * 0.5f = 24.0f)</summary>
    private const float BaseYOffset = 24.0f;

    // ========================================
    // 状态
    // ========================================

    private readonly List<Sprite3D> _placedDecorations = new();

    public int DecorationCount => _placedDecorations.Count;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 根据地图数据放置装饰精灵。
    /// 在 GenerateBattlefield 之后、SpawnUnits 之前调用。
    /// </summary>
    public void PlaceDecorations(HexGrid hexGrid, BattleMapGenerator.BattleMapData? mapData)
    {
        if (hexGrid == null) return;

        foreach (var kvp in hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;
            if (cell.Occupant != null) continue; // 有单位的格子不放装饰

            var terrainType = cell.Data?.terrainType ?? BattleCellData.TerrainType.Plains;
            int coverType = cell.CoverType;

            // 障碍物特殊效果优先放置
            if (cell.Data != null && !string.IsNullOrEmpty(cell.Data.specialEffect))
            {
                if (cell.Data.specialEffect.Contains("obstacle_tree"))
                {
                    PlaceRandomDecoration(cell, new[] { "tree_oak", "tree_pine" }, 1.0f);
                    continue;
                }
                else if (cell.Data.specialEffect.Contains("obstacle_rock"))
                {
                    PlaceRandomDecoration(cell, new[] { "rock_large", "rock_moss" }, 1.0f);
                    continue;
                }
                else if (cell.Data.specialEffect.Contains("obstacle_crate"))
                {
                    PlaceRandomDecoration(cell, new[] { "crate", "barrel" }, 1.0f);
                    continue;
                }
                else if (cell.Data.specialEffect.Contains("obstacle_wagon"))
                {
                    PlaceRandomDecoration(cell, new[] { "fence_wood", "barrel" }, 1.0f);
                    continue;
                }
            }

            // 掩体格子优先放置掩体装饰
            if (coverType > 0 && CoverDecorations.TryGetValue(coverType, out var coverSprites))
            {
                PlaceRandomDecoration(cell, coverSprites, 0.7f);
                continue;
            }

            // 地形装饰
            if (TerrainDecorations.TryGetValue(terrainType, out var terrainSprites))
            {
                PlaceRandomDecoration(cell, terrainSprites, DecorationChance);
            }
        }
    }

    /// <summary>清除所有已放置的装饰</summary>
    public void ClearDecorations()
    {
        foreach (var sprite in _placedDecorations)
        {
            if (GodotObject.IsInstanceValid(sprite))
                sprite.QueueFree();
        }
        _placedDecorations.Clear();
    }

    // ========================================
    // 内部方法
    // ========================================

    private void PlaceRandomDecoration(HexCell cell, string[] spriteIds, float chance)
    {
        if (GD.Randf() > chance) return;
        if (spriteIds.Length == 0) return;

        // 随机选择一个装饰基础 ID
        string baseId = spriteIds[(int)(GD.Randf() * spriteIds.Length)];
        // 随机生成 4 个变体后缀（由 AI 批量生成 _0 ~ _3）
        int variantIndex = (int)(GD.Randf() * 4);
        string spriteId = $"{baseId}_{variantIndex}";

        // 优先级 1:从 CombatTextureLoader 获取真实纹理
        var texture = CombatTextureLoader.Instance.GetSceneSprite(spriteId);
        // 优先级 2:占位 — BattlePropRegistry 在贴图缺失时返回紫色占位
        // 这样美术资产没准备好时战场仍有可见装饰物
        if (texture == null)
        {
            texture = BladeHex.View.Map.BattlePropRegistry.GetTexture(spriteId);
        }
        if (texture == null) return;

        // 创建 Sprite3D
        var sprite = new Sprite3D();
        sprite.Texture = texture;
        sprite.PixelSize = SpritePixelSize;
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        sprite.AlphaScissorThreshold = 0.5f;
        sprite.AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass;
        // 不接光只投影（与单位/BattlePropRenderer 口径一致）：保持 2D 原画亮度，向地面投出剪影
        sprite.Shaded = false;
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        // 底部对齐（精灵脚踩地面）
        sprite.Offset = new Vector2(0, texture.GetHeight() / 2.0f);

        // 位置：格子中心 + 随机偏移 + Y 偏移
        float randX = (GD.Randf() - 0.5f) * HexUtils.Size * 0.4f;
        float randZ = (GD.Randf() - 0.5f) * HexUtils.Size * 0.4f;
        sprite.Position = cell.Position + new Vector3(randX, BaseYOffset, randZ);

        // 随机缩放变化（0.8~1.2）
        float scale = 0.8f + GD.Randf() * 0.4f;
        sprite.Scale = new Vector3(scale, scale, scale);

        AddChild(sprite);
        _placedDecorations.Add(sprite);
    }
}
