using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图上的城镇/据点实体 — 扩展支持城镇设施列表、繁荣度等交互数据
/// </summary>
[GlobalClass]
public partial class OverworldTown : Node2D
{
    [Export] public Texture2D? TownSprite { get; set; }
    [Export] public SpriteFrames? TownFrames { get; set; }

    [Export] public string TownName { get; set; } = "中立城镇";
    private Polygon2D? _visualPoly;
    private Sprite2D? _visualSprite;
    private AnimatedSprite2D? _visualAnim;

    /// <summary>访问视觉多边形用（用于改色/缩放）</summary>
    public Polygon2D? VisualPoly => _visualPoly;

    // ========================================
    // 交互系统扩展
    // ========================================

    public List<TownFacility> Facilities = new();
    [Export] public string TownType { get; set; } = "town"; // "town" 或 "village"
    [Export] public int Prosperity { get; set; } = 50;
    [Export] public string TownDescription { get; set; } = "";
    [Export] public string Faction { get; set; } = "";
    [Export] public int Garrison { get; set; } = 50;

    public override void _Ready()
    {
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        _visualSprite = new Sprite2D();
        AddChild(_visualSprite);

        _visualAnim = new AnimatedSprite2D();
        AddChild(_visualAnim);

        _visualPoly = new Polygon2D();
        float size = 25.0f;
        _visualPoly.Polygon = new Vector2[]
        {
            new(-size, -size),
            new(size, -size),
            new(size, size),
            new(-size, size)
        };

        if (TownType == "village") _visualPoly.Color = new Color(0.3f, 0.5f, 0.3f); // 绿色=村庄
        else _visualPoly.Color = new Color(0.2f, 0.4f, 0.8f); // 蓝色=城镇
        AddChild(_visualPoly);

        var label = new Label();
        label.Text = TownName;
        label.Position = new Vector2(-40, size + 5);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(80, 20);
        label.AddThemeFontSizeOverride("font_size", 14);
        AddChild(label);

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_visualSprite == null || _visualAnim == null || _visualPoly == null) return;

        _visualSprite.Visible = false;
        _visualAnim.Visible = false;
        _visualPoly.Visible = false;

        if (TownFrames != null)
        {
            _visualAnim.SpriteFrames = TownFrames;
            float h = 80.0f;
            if (TownFrames.GetFrameCount("default") > 0)
            {
                var tex = TownFrames.GetFrameTexture("default", 0);
                if (tex != null) h = tex.GetHeight();
            }
            _visualAnim.Position = new Vector2(0, -h / 2.0f);
            _visualAnim.Visible = true;
            _visualAnim.Play("default");
        }
        else if (TownSprite != null)
        {
            _visualSprite.Texture = TownSprite;
            _visualSprite.Position = new Vector2(0, -TownSprite.GetHeight() / 2.0f);
            _visualSprite.Visible = true;
        }
        else
        {
            _visualPoly.Visible = true;
        }
    }

    public void SetupDefaultFacilities() => Facilities = TownFacility.CreateDefaultFacilities();
    public void SetupVillageFacilities()
    {
        Facilities = TownFacility.CreateVillageFacilities();
        TownType = "village";
    }
    public void SetupPortFacilities()
    {
        Facilities = TownFacility.CreatePortFacilities();
        TownType = "port";
    }
    public void SetupCastleFacilities()
    {
        Facilities = TownFacility.CreateCastleFacilities();
        TownType = "castle";
    }
    public void SetupOutpostFacilities()
    {
        Facilities = TownFacility.CreateOutpostFacilities();
        TownType = "outpost";
    }
    public void SetupTavernFacilities()
    {
        Facilities = TownFacility.CreateTavernFacilities();
        TownType = "tavern";
    }
    public void SetupMineFacilities()
    {
        Facilities = TownFacility.CreateMineFacilities();
        TownType = "mine";
    }
    public void SetupShrineFacilities()
    {
        Facilities = TownFacility.CreateShrineFacilities();
        TownType = "shrine";
    }

    /// <summary>根据POI类型自动设置设施</summary>
    public void SetupFacilitiesByType(string poiType)
    {
        switch (poiType)
        {
            case "village": SetupVillageFacilities(); break;
            case "port": SetupPortFacilities(); break;
            case "castle": SetupCastleFacilities(); break;
            case "outpost": SetupOutpostFacilities(); break;
            case "tavern": SetupTavernFacilities(); break;
            case "mine": SetupMineFacilities(); break;
            case "shrine": SetupShrineFacilities(); break;
            default: SetupDefaultFacilities(); break;
        }
    }

    /// <summary>放置城镇到大地图坐标（调用）</summary>
    public void PlaceAt(float px, float py) => Position = new Vector2(px, py);

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(TownDescription)) return TownDescription;
        return InteractionDescriptions.GetTownDescription(TownName, TownType, Prosperity, Garrison);
    }
}
