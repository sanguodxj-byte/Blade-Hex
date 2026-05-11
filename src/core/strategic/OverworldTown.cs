using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图上的城镇/据点实体 — 扩展支持城镇设施列表、繁荣度等交互数据
/// </summary>
public partial class OverworldTown : Node2D
{
    [Export] public Texture2D? TownSprite;
    [Export] public SpriteFrames? TownFrames;

    public string TownName = "中立城镇";
    private Polygon2D? _visualPoly;
    private Sprite2D? _visualSprite;
    private AnimatedSprite2D? _visualAnim;

    // ========================================
    // 交互系统扩展
    // ========================================

    public List<TownFacility> Facilities = new();
    public string TownType = "town"; // "town" 或 "village"
    public int Prosperity = 50;
    public string TownDescription = "";
    public string Faction = "";
    public int Garrison = 50;

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

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(TownDescription)) return TownDescription;
        string typeText = TownType == "village" ? "村庄" : "城镇";
        string prosperText = Prosperity >= 60 ? "繁荣" : (Prosperity >= 30 ? "一般" : "萧条");
        return $"一座{prosperText}的{typeText}，守军约{Garrison}人。";
    }
}
