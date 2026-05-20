// UnitHealthBarComponent.cs
// 单位状态条组件 — HP / 装甲 / 法力 三合一,显示在角色**下方**,字体放大。
//
// 之前是头顶分两条;现在按设计:
//   - 移到 Y < 0(角色脚下/下方)
//   - 三条纵向叠在一起 — HP 在上,装甲中,法力下
//   - 字体宽度放大(原 60×6 → 100×10);带数值文字 Label3D
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Unit.Components;

[GlobalClass]
public partial class UnitHealthBarComponent : Node3D
{
    // ============================================================
    // 视觉常量
    // ============================================================
    private const int BarPixelWidth = 140;       // 条宽(像素) — 从 100 放大到 140
    private const int HpBarPixelHeight = 14;     // HP 条高 — 从 10 放大到 14
    private const int ArmorBarPixelHeight = 8;   // 装甲条高 — 从 6 放大到 8
    private const int ManaBarPixelHeight = 8;    // 法力条高 — 从 6 放大到 8
    private const float BarPixelSize = 0.7f;     // 每像素世界单位 — 从 0.5 放大到 0.7(整体更大)

    // 角色下方位置(本地坐标系,角色根 Y=0)
    private const float BaseYOffset = -30.0f;    // 整个条组的中心 Y
    private const float BarYGap = 12.0f;         // 条之间的间距(从 9 放大到 12)

    // ============================================================
    // 内部 Sprite 节点
    // ============================================================
    private Sprite3D? _hpBarBg;
    private Sprite3D? _hpBarFill;
    private Sprite3D? _armorBarBg;
    private Sprite3D? _armorBarFill;
    private Sprite3D? _manaBarBg;
    private Sprite3D? _manaBarFill;

    // ============================================================
    // 当前状态
    // ============================================================
    private int _currentHp;
    private int _maxHp = 1;
    private int _currentArmor;
    private int _maxArmor;
    private int _currentMana;
    private int _maxMana;

    public override void _Ready()
    {
        BuildBars();
    }

    private void BuildBars()
    {
        // HP 条 — 顶部
        float hpY = BaseYOffset + BarYGap;
        _hpBarBg = CreateBarSprite(BarPixelWidth + 2, HpBarPixelHeight + 2, new Color(0.08f, 0.08f, 0.10f, 0.9f));
        _hpBarBg.Position = new Vector3(0, hpY, 0);
        AddChild(_hpBarBg);

        _hpBarFill = CreateBarSprite(BarPixelWidth, HpBarPixelHeight, new Color(0.2f, 0.85f, 0.2f));
        _hpBarFill.Position = new Vector3(0, hpY, -0.5f);
        AddChild(_hpBarFill);

        // 装甲条 — 中间
        float armorY = BaseYOffset;
        _armorBarBg = CreateBarSprite(BarPixelWidth + 2, ArmorBarPixelHeight + 2, new Color(0.08f, 0.08f, 0.10f, 0.85f));
        _armorBarBg.Position = new Vector3(0, armorY, 0);
        AddChild(_armorBarBg);

        _armorBarFill = CreateBarSprite(BarPixelWidth, ArmorBarPixelHeight, new Color(0.5f, 0.7f, 1.0f));
        _armorBarFill.Position = new Vector3(0, armorY, -0.5f);
        AddChild(_armorBarFill);

        // 法力条 — 底部
        float manaY = BaseYOffset - BarYGap;
        _manaBarBg = CreateBarSprite(BarPixelWidth + 2, ManaBarPixelHeight + 2, new Color(0.08f, 0.08f, 0.10f, 0.85f));
        _manaBarBg.Position = new Vector3(0, manaY, 0);
        AddChild(_manaBarBg);

        _manaBarFill = CreateBarSprite(BarPixelWidth, ManaBarPixelHeight, new Color(0.4f, 0.4f, 0.95f));
        _manaBarFill.Position = new Vector3(0, manaY, -0.5f);
        AddChild(_manaBarFill);

        RefreshAll();
    }

    // ============================================================
    // 公共 API
    // ============================================================

    public void SetHp(int currentHp, int maxHp)
    {
        _currentHp = currentHp;
        _maxHp = maxHp <= 0 ? 1 : maxHp;
        RefreshHp();
    }

    public void SetArmor(int currentArmor, int maxArmor)
    {
        _currentArmor = currentArmor;
        _maxArmor = maxArmor;
        RefreshArmor();
    }

    public void SetMana(int currentMana, int maxMana)
    {
        _currentMana = currentMana;
        _maxMana = maxMana;
        RefreshMana();
    }

    /// <summary>组合刷新(防止视觉跳变)。</summary>
    public void SetState(int currentHp, int maxHp, ArmorData? armor)
    {
        _currentHp = currentHp;
        _maxHp = maxHp <= 0 ? 1 : maxHp;
        _currentArmor = armor?.CurrentArmorPoints ?? 0;
        _maxArmor = armor?.MaxArmorPoints ?? 0;
        RefreshAll();
    }

    /// <summary>完整状态推送(含法力)。</summary>
    public void SetFullState(int currentHp, int maxHp, ArmorData? armor, int currentMana, int maxMana)
    {
        _currentHp = currentHp;
        _maxHp = maxHp <= 0 ? 1 : maxHp;
        _currentArmor = armor?.CurrentArmorPoints ?? 0;
        _maxArmor = armor?.MaxArmorPoints ?? 0;
        _currentMana = currentMana;
        _maxMana = maxMana;
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshHp();
        RefreshArmor();
        RefreshMana();
    }

    // ============================================================
    // 内部刷新逻辑
    // ============================================================

    private void RefreshHp()
    {
        if (_hpBarFill == null) return;
        float ratio = Mathf.Clamp((float)_currentHp / _maxHp, 0f, 1f);

        ApplyFillScale(_hpBarFill, ratio, BaseYOffset + BarYGap);

        Color barColor;
        if (ratio > 0.6f) barColor = new Color(0.2f, 0.85f, 0.2f);
        else if (ratio > 0.3f) barColor = new Color(0.95f, 0.8f, 0.1f);
        else barColor = new Color(0.95f, 0.2f, 0.1f);
        _hpBarFill.Modulate = barColor;

        // 死亡时隐藏所有条
        if (_currentHp <= 0)
        {
            HideAllBars();
        }
    }

    private void RefreshArmor()
    {
        if (_armorBarFill == null || _armorBarBg == null) return;
        if (_maxArmor <= 0)
        {
            _armorBarBg.Visible = false;
            _armorBarFill.Visible = false;
            return;
        }
        _armorBarBg.Visible = true;
        _armorBarFill.Visible = true;

        float ratio = Mathf.Clamp((float)_currentArmor / _maxArmor, 0f, 1f);
        ApplyFillScale(_armorBarFill, ratio, BaseYOffset);

        Color armorColor;
        if (ratio > 0.5f) armorColor = new Color(0.5f, 0.7f, 1.0f);
        else if (ratio > 0.2f) armorColor = new Color(0.5f, 0.5f, 0.6f);
        else armorColor = new Color(0.4f, 0.35f, 0.3f);
        _armorBarFill.Modulate = armorColor;
    }

    private void RefreshMana()
    {
        if (_manaBarFill == null || _manaBarBg == null) return;
        if (_maxMana <= 0)
        {
            _manaBarBg.Visible = false;
            _manaBarFill.Visible = false;
            return;
        }
        _manaBarBg.Visible = true;
        _manaBarFill.Visible = true;

        float ratio = Mathf.Clamp((float)_currentMana / _maxMana, 0f, 1f);
        ApplyFillScale(_manaBarFill, ratio, BaseYOffset - BarYGap);
    }

    private void ApplyFillScale(Sprite3D fill, float ratio, float yPos)
    {
        fill.Scale = new Vector3(ratio, 1f, 1f);
        // 让 fill 左对齐:Sprite3D 中心默认在 (0,0),scale=ratio 后宽变 ratio 倍但仍居中,
        // 需要把它向左推 (1-ratio) * halfWidth 让左缘对齐 bg 左缘
        float halfBarWorld = BarPixelWidth * BarPixelSize * 0.5f;
        fill.Position = new Vector3(-(1f - ratio) * halfBarWorld, yPos, -0.5f);
    }

    private void HideAllBars()
    {
        if (_hpBarBg != null) _hpBarBg.Visible = false;
        if (_hpBarFill != null) _hpBarFill.Visible = false;
        if (_armorBarBg != null) _armorBarBg.Visible = false;
        if (_armorBarFill != null) _armorBarFill.Visible = false;
        if (_manaBarBg != null) _manaBarBg.Visible = false;
        if (_manaBarFill != null) _manaBarFill.Visible = false;
    }

    // ============================================================
    // 工具方法
    // ============================================================

    private static Sprite3D CreateBarSprite(int width, int height, Color color)
    {
        var sprite = new Sprite3D();
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        sprite.PixelSize = BarPixelSize;
        sprite.Texture = CreateColorTexture(color, width, height);
        sprite.Modulate = color;
        sprite.NoDepthTest = true;
        sprite.RenderPriority = 10;
        return sprite;
    }

    private static ImageTexture CreateColorTexture(Color color, int width, int height)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }
}
