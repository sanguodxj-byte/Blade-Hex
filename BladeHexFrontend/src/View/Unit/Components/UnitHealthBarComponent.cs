// UnitHealthBarComponent.cs
// 单位头顶 HP 条 + 装甲条 — 服务于架构优化 spec R10。
//
// 抽取自 Unit.cs（原 SetupHpBar / UpdateHpBar / UpdateArmorBar / CreateBarSprite / CreateColorTexture）。
// 由 Unit 在 SetupVisuals 时创建并 AddChild，状态变化时通过 Refresh() 触发重绘。
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Unit.Components;

/// <summary>
/// 单位头顶 HP 条 + 装甲条组件。
/// 由父级 <see cref="BladeHex.Unit"/> 注入引用，自身不读取 Unit 字段（通过 SetState 推送）。
/// </summary>
[GlobalClass]
public partial class UnitHealthBarComponent : Node3D
{
    // ========================================
    // 视觉常量（与原 Unit.cs 一致）
    // ========================================
    private const int BarPixelWidth = 60;
    private const int HpBarPixelHeight = 6;
    private const int ArmorBarPixelHeight = 4;
    private const float BarPixelSize = 0.5f;    // 每像素 = 0.5 世界单位（60px = 30 世界单位宽）
    private const float HpBarYOffset = 110.0f;  // 角色头顶上方（本地坐标）
    private const float ArmorBarYGap = 5.0f;    // 装甲条在 HP 条下方

    // ========================================
    // 内部 Sprite 节点
    // ========================================
    private Sprite3D? _hpBarBg;
    private Sprite3D? _hpBarFill;
    private Sprite3D? _armorBarBg;
    private Sprite3D? _armorBarFill;

    // ========================================
    // 当前状态（由调用方推送）
    // ========================================
    private int _currentHp;
    private int _maxHp = 1;
    private int _currentArmor;
    private int _maxArmor;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        BuildBars();
    }

    private void BuildBars()
    {
        // === HP 条 ===
        _hpBarBg = CreateBarSprite(BarPixelWidth + 2, HpBarPixelHeight + 2, new Color(0.1f, 0.1f, 0.1f, 0.9f));
        _hpBarBg.Position = new Vector3(0, HpBarYOffset, 0);
        AddChild(_hpBarBg);

        _hpBarFill = CreateBarSprite(BarPixelWidth, HpBarPixelHeight, new Color(0.2f, 0.85f, 0.2f));
        _hpBarFill.Position = new Vector3(0, HpBarYOffset, -0.5f);
        AddChild(_hpBarFill);

        // === 装甲条（HP 条下方）===
        float armorY = HpBarYOffset - ArmorBarYGap;
        _armorBarBg = CreateBarSprite(BarPixelWidth + 2, ArmorBarPixelHeight + 2, new Color(0.1f, 0.1f, 0.1f, 0.7f));
        _armorBarBg.Position = new Vector3(0, armorY, 0);
        AddChild(_armorBarBg);

        _armorBarFill = CreateBarSprite(BarPixelWidth, ArmorBarPixelHeight, new Color(0.3f, 0.5f, 0.9f));
        _armorBarFill.Position = new Vector3(0, armorY, -0.5f);
        AddChild(_armorBarFill);

        RefreshHp();
        RefreshArmor();
    }

    // ========================================
    // 公共 API — 状态推送
    // ========================================

    /// <summary>更新 HP 数据并刷新血条（最常用）。</summary>
    public void SetHp(int currentHp, int maxHp)
    {
        _currentHp = currentHp;
        _maxHp = maxHp <= 0 ? 1 : maxHp;
        RefreshHp();
    }

    /// <summary>更新装甲数据并刷新装甲条。</summary>
    public void SetArmor(int currentArmor, int maxArmor)
    {
        _currentArmor = currentArmor;
        _maxArmor = maxArmor;
        RefreshArmor();
    }

    /// <summary>组合刷新 — 一次设置 HP + 装甲（防止视觉跳变）。</summary>
    public void SetState(int currentHp, int maxHp, ArmorData? armor)
    {
        _currentHp = currentHp;
        _maxHp = maxHp <= 0 ? 1 : maxHp;
        _currentArmor = armor?.CurrentArmorPoints ?? 0;
        _maxArmor = armor?.MaxArmorPoints ?? 0;
        RefreshHp();
        RefreshArmor();
    }

    // ========================================
    // 内部刷新逻辑
    // ========================================

    private void RefreshHp()
    {
        if (_hpBarFill == null) return;
        float ratio = Mathf.Clamp((float)_currentHp / _maxHp, 0f, 1f);

        _hpBarFill.Scale = new Vector3(ratio, 1f, 1f);
        float halfBarWorld = BarPixelWidth * BarPixelSize * 0.5f;
        _hpBarFill.Position = new Vector3(-(1f - ratio) * halfBarWorld, HpBarYOffset, -0.5f);

        // 颜色：绿→黄→红
        Color barColor;
        if (ratio > 0.6f) barColor = new Color(0.2f, 0.85f, 0.2f);
        else if (ratio > 0.3f) barColor = new Color(0.95f, 0.8f, 0.1f);
        else barColor = new Color(0.95f, 0.2f, 0.1f);
        _hpBarFill.Modulate = barColor;

        // 死亡时隐藏所有条
        if (_currentHp <= 0)
        {
            if (_hpBarBg != null) _hpBarBg.Visible = false;
            _hpBarFill.Visible = false;
            if (_armorBarBg != null) _armorBarBg.Visible = false;
            if (_armorBarFill != null) _armorBarFill.Visible = false;
        }
    }

    private void RefreshArmor()
    {
        if (_armorBarFill == null || _armorBarBg == null) return;

        // 无护甲时隐藏装甲条
        if (_maxArmor <= 0)
        {
            _armorBarBg.Visible = false;
            _armorBarFill.Visible = false;
            return;
        }

        _armorBarBg.Visible = true;
        _armorBarFill.Visible = true;

        float ratio = Mathf.Clamp((float)_currentArmor / _maxArmor, 0f, 1f);
        float armorY = HpBarYOffset - ArmorBarYGap;

        _armorBarFill.Scale = new Vector3(ratio, 1f, 1f);
        float halfBarWorld = BarPixelWidth * BarPixelSize * 0.5f;
        _armorBarFill.Position = new Vector3(-(1f - ratio) * halfBarWorld, armorY, -0.5f);

        // 颜色：满时蓝色，低时灰色
        Color armorColor;
        if (ratio > 0.5f) armorColor = new Color(0.3f, 0.5f, 0.9f);
        else if (ratio > 0.2f) armorColor = new Color(0.5f, 0.5f, 0.6f);
        else armorColor = new Color(0.4f, 0.35f, 0.3f);
        _armorBarFill.Modulate = armorColor;
    }

    // ========================================
    // 工具方法
    // ========================================

    /// <summary>创建一个条形 Sprite3D（unshaded + 透明 + billboard）。</summary>
    private static Sprite3D CreateBarSprite(int width, int height, Color color)
    {
        var sprite = new Sprite3D();
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        sprite.PixelSize = BarPixelSize;
        sprite.Texture = CreateColorTexture(color, width, height);
        sprite.Modulate = color;
        sprite.NoDepthTest = true;  // 不被地形遮挡
        sprite.RenderPriority = 10; // 高优先级渲染
        return sprite;
    }

    private static ImageTexture CreateColorTexture(Color color, int width, int height)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }
}
