using Godot;
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图上的敌军队伍实体 — 扩展支持 NPCProfile，区分人形/非人形交互
/// </summary>
public partial class OverworldEnemy : Node2D
{
    [Export] public Texture2D? OverworldSprite;
    [Export] public SpriteFrames? OverworldFrames;

    private Polygon2D? _visualPoly;
    private Sprite2D? _visualSprite;
    private AnimatedSprite2D? _visualAnim;

    // ========================================
    // 交互系统扩展
    // ========================================

    // NPC档案（为null则视为非人形生物）
    public NPCProfile? NpcProfile = null;
    public bool IsHostile = true;
    public string DisplayName = "";
    public string DescriptionText = "";
    public int EnemyType = 1; // 默认 BEAST=1

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
        float radius = 15.0f;
        _visualPoly.Polygon = new Vector2[]
        {
            new(0, radius),
            new(radius * 0.7f, -radius * 0.5f),
            new(-radius * 0.7f, -radius * 0.5f)
        };

        if (NpcProfile != null) _visualPoly.Color = new Color(0.9f, 0.7f, 0.2f); // 黄色=人形NPC
        else _visualPoly.Color = new Color(0.9f, 0.2f, 0.2f); // 红色=非人形
        AddChild(_visualPoly);

        // 名称标签
        var label = new Label();
        label.Text = GetDisplayName();
        label.Position = new Vector2(-40, -25);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(80, 20);
        label.AddThemeFontSizeOverride("font_size", 12);
        AddChild(label);

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_visualSprite == null || _visualAnim == null || _visualPoly == null) return;

        _visualSprite.Visible = false;
        _visualAnim.Visible = false;
        _visualPoly.Visible = false;

        if (OverworldFrames != null)
        {
            _visualAnim.SpriteFrames = OverworldFrames;
            float h = 60.0f;
            if (OverworldFrames.GetFrameCount("default") > 0)
            {
                var tex = OverworldFrames.GetFrameTexture("default", 0);
                if (tex != null) h = tex.GetHeight();
            }
            _visualAnim.Position = new Vector2(0, -h / 2.0f);
            _visualAnim.Visible = true;
            _visualAnim.Play("default");
        }
        else if (OverworldSprite != null)
        {
            _visualSprite.Texture = OverworldSprite;
            _visualSprite.Position = new Vector2(0, -OverworldSprite.GetHeight() / 2.0f);
            _visualSprite.Visible = true;
        }
        else
        {
            _visualPoly.Visible = true;
        }
    }

    public void PlayAnim(string animName)
    {
        if (_visualAnim == null || !_visualAnim.Visible) return;
        if (_visualAnim.SpriteFrames != null && _visualAnim.SpriteFrames.HasAnimation(animName))
            _visualAnim.Play(animName);
        else
            _visualAnim.Play("default");
    }

    public void PlaceAt(float px, float py) => Position = new Vector2(px, py);

    // ========================================
    // 交互辅助
    // ========================================

    public string GetEntityType()
    {
        if (NpcProfile != null) return "humanoid";
        if (EnemyType == 0) return "humanoid"; // HUMANOID=0
        return "nonhumanoid";
    }

    public string GetDisplayName()
    {
        if (NpcProfile != null) return NpcProfile.npcName;
        return string.IsNullOrEmpty(DisplayName) ? "未知敌人" : DisplayName;
    }

    public string GetDescription()
    {
        if (NpcProfile != null) return NpcProfile.GetDescription();
        return string.IsNullOrEmpty(DescriptionText) ? "一个危险的生物，看起来不太友好。" : DescriptionText;
    }
}
