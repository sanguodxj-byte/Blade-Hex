// TerrainAtlas.cs
// 地形图集工具 — 将分散的地形纹理合并为一张 atlas 纹理
// 提供 UV 坐标查询，替代逐像素扫描 (_crop_transparent) 方式
// 图集布局: 4列×6行, 每格 128×128, 总大小 512×768
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 地形图集静态工具类
/// 管理地形纹理的 加载/烘焙/缓存，通过 UV rect 提供图块索引
///
/// 加载优先级:
///   1. res://assets/baked/terrain_atlas.png  (预烘焙)
///   2. user://baked_terrain_atlas.png        (之前运行时烘焙)
///   3. 运行时从单个纹理烘焙并保存到 user://
/// </summary>
public static class TerrainAtlas
{
    // ========================================
    // 图集布局常量
    // ========================================

    /// <summary>图集列数</summary>
    private const int AtlasColumns = 4;

    /// <summary>图集行数</summary>
    private const int AtlasRows = 6;

    /// <summary>每格纹理大小 (像素)</summary>
    private const int TileSize = 128;

    /// <summary>图集总宽度 (像素)</summary>
    private const int AtlasWidth = AtlasColumns * TileSize; // 512

    /// <summary>图集总高度 (像素)</summary>
    private const int AtlasHeight = AtlasRows * TileSize;   // 768

    /// <summary>预烘焙图集路径 (res://)</summary>
    private const string BakedAtlasResPath = "res://assets/baked/terrain_atlas.png";

    /// <summary>运行时烘焙图集路径 (user://)</summary>
    private const string BakedAtlasUserPath = "user://baked_terrain_atlas.png";

    // ========================================
    // 运行时缓存
    // ========================================

    private static Texture2D? _atlasTexture;
    private static Dictionary<int, Rect2> _uvRects = new();
    private static bool _initialized = false;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 获取地形图集纹理 (懒加载)
    /// 首次调用触发加载/烘焙，后续返回缓存
    /// </summary>
    public static Texture2D GetAtlasTexture()
    {
        EnsureInitialized();
        return _atlasTexture!;
    }

    /// <summary>
    /// 获取指定地形类型在图集中的 UV 坐标 (归一化 Rect2)
    /// </summary>
    public static Rect2 GetUvRect(HexOverworldTile.TerrainType type)
    {
        EnsureInitialized();
        int idx = (int)type;
        if (_uvRects.TryGetValue(idx, out var rect))
            return rect;
        return new Rect2();
    }

    /// <summary>
    /// 强制重新加载/烘焙图集 (清除缓存)
    /// </summary>
    public static void Reload()
    {
        _atlasTexture = null;
        _uvRects.Clear();
        _initialized = false;
    }

    // ========================================
    // 内部初始化
    // ========================================

    private static void EnsureInitialized()
    {
        if (_initialized && _atlasTexture != null)
            return;

        _atlasTexture = LoadAtlas();
        if (_atlasTexture != null)
        {
            BuildUvRects();
            _initialized = true;
        }
    }

    /// <summary>
    /// 按优先级加载图集:
    /// res://assets/baked/terrain_atlas.png
    ///   → user://baked_terrain_atlas.png
    ///   → 运行时烘焙
    /// </summary>
    private static Texture2D? LoadAtlas()
    {
        // 1. 尝试加载预烘焙图集 (美术资源)
        if (ResourceLoader.Exists(BakedAtlasResPath))
        {
            var tex = ResourceLoader.Load<Texture2D>(BakedAtlasResPath);
            if (tex != null)
                return tex;
        }

        // 2. 尝试加载之前运行时缓存
        if (ResourceLoader.Exists(BakedAtlasUserPath))
        {
            var tex = ResourceLoader.Load<Texture2D>(BakedAtlasUserPath);
            if (tex != null)
                return tex;
        }

        // 3. 运行时从单个纹理烘焙
        return BakeAtlas();
    }

    // ========================================
    // UV Rect 构建 (根据类型枚举索引映射到 4×6 网格)
    // ========================================

    private static void BuildUvRects()
    {
        _uvRects.Clear();

        var types = (HexOverworldTile.TerrainType[])Enum.GetValues(typeof(HexOverworldTile.TerrainType));
        foreach (var type in types)
        {
            int idx = (int)type;
            int col = idx % AtlasColumns;
            int row = idx / AtlasColumns;

            _uvRects[idx] = new Rect2(
                col * TileSize / (float)AtlasWidth,
                row * TileSize / (float)AtlasHeight,
                TileSize / (float)AtlasWidth,
                TileSize / (float)AtlasHeight
            );
        }
    }

    // ========================================
    // 运行时烘焙
    // ========================================

    /// <summary>
    /// 运行时从单个地形纹理烘焙 atlas 并保存到 user://
    /// 回退: 用 HexOverworldTile.TerrainTextureName() 获取纹理路径
    /// </summary>
    private static Texture2D? BakeAtlas()
    {
        // 创建空图集 (RGBA8, 透明底色)
        Image atlasImage;
        try
        {
            atlasImage = Image.CreateEmpty(AtlasWidth, AtlasHeight, false, Image.Format.Rgba8);
        }
        catch
        {
            return null;
        }

        if (atlasImage.IsEmpty())
            return null;

        // 填充透明黑底 (Image.Create 默认已填充零, 明确调用以确保)
        atlasImage.Fill(new Color(0, 0, 0, 0));

        // 逐一加载地形纹理, 缩放到 TileSize, blit 到对应网格位置
        var types = (HexOverworldTile.TerrainType[])Enum.GetValues(typeof(HexOverworldTile.TerrainType));
        foreach (var type in types)
        {
            int idx = (int)type;
            string texPath = HexOverworldTile.GetTerrainTexturePath(type, 0);

            if (!ResourceLoader.Exists(texPath))
                continue;

            var tex = ResourceLoader.Load<Texture2D>(texPath);
            if (tex == null)
                continue;

            var img = tex.GetImage();
            if (img == null || img.IsEmpty())
                continue;

            // 缩放到图集统一尺寸
            if (img.GetWidth() != TileSize || img.GetHeight() != TileSize)
            {
                img.Resize(TileSize, TileSize);
            }

            // 计算图集网格坐标并 blit
            int col = idx % AtlasColumns;
            int row = idx / AtlasColumns;
            int destX = col * TileSize;
            int destY = row * TileSize;

            atlasImage.BlitRect(img, new Rect2I(0, 0, TileSize, TileSize), new Vector2I(destX, destY));
        }

        // 保存到 user:// 以供后续会话复用
        Error err = atlasImage.SavePng(BakedAtlasUserPath);
        if (err != Error.Ok)
            return null;

        // 重新加载保存的图集
        return ResourceLoader.Load<Texture2D>(BakedAtlasUserPath);
    }
}
