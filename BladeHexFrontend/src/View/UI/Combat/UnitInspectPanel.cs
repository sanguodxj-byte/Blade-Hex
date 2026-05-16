// UnitInspectPanel.cs
// 战斗中右键单位弹出的详细信息面板 — 悬浮在鼠标位置
// 布局参考部队UI左侧：属性、装备、战斗数值
using Godot;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 单位检视面板 — 右键任何单位时在鼠标位置弹出
/// 显示：名称、等级、属性六维、HP/AP、AC/DR、武器信息、护甲信息
/// </summary>
[GlobalClass]
public partial class UnitInspectPanel : PanelContainer
{
    private VBoxContainer _content = null!;
    private Label _nameLabel = null!;
    private Label _levelLabel = null!;
    private GridContainer _attrGrid = null!;
    private Label _hpLabel = null!;
    private Label _apLabel = null!;
    private Label _acLabel = null!;
    private Label _drLabel = null!;
    private Label _weaponLabel = null!;
    private Label _armorLabel = null!;
    private Label _moveLabel = null!;
    private Label _strategyLabel = null!;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = Control.MouseFilterEnum.Ignore;
        ZIndex = 200;
        BuildLayout();
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(220, 0);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        style.SetBorderWidthAll(1);
        style.BorderColor = new Color(0.4f, 0.35f, 0.25f, 0.8f);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(10);
        AddThemeStyleboxOverride("panel", style);

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 4);
        AddChild(_content);

        // 名称
        _nameLabel = MakeLabel("", 16, new Color(0.95f, 0.85f, 0.5f));
        _content.AddChild(_nameLabel);

        // 等级
        _levelLabel = MakeLabel("", 12, new Color(0.7f, 0.7f, 0.7f));
        _content.AddChild(_levelLabel);

        _content.AddChild(MakeSeparator());

        // 六维属性
        _attrGrid = new GridContainer { Columns = 6 };
        _attrGrid.AddThemeConstantOverride("h_separation", 6);
        _attrGrid.AddThemeConstantOverride("v_separation", 2);
        _content.AddChild(_attrGrid);

        _content.AddChild(MakeSeparator());

        // 战斗数值
        _hpLabel = MakeLabel("", 13, new Color(0.3f, 0.9f, 0.3f));
        _content.AddChild(_hpLabel);
        _apLabel = MakeLabel("", 13, new Color(0.3f, 0.7f, 1.0f));
        _content.AddChild(_apLabel);
        _acLabel = MakeLabel("", 13, Colors.White);
        _content.AddChild(_acLabel);
        _drLabel = MakeLabel("", 13, new Color(0.5f, 0.6f, 0.9f));
        _content.AddChild(_drLabel);
        _moveLabel = MakeLabel("", 13, Colors.White);
        _content.AddChild(_moveLabel);

        _content.AddChild(MakeSeparator());

        // 装备
        _weaponLabel = MakeLabel("", 12, new Color(0.9f, 0.8f, 0.6f));
        _content.AddChild(_weaponLabel);
        _armorLabel = MakeLabel("", 12, new Color(0.6f, 0.7f, 0.9f));
        _content.AddChild(_armorLabel);

        // AI 策略（仅敌方显示）
        _strategyLabel = MakeLabel("", 11, new Color(0.6f, 0.6f, 0.6f));
        _content.AddChild(_strategyLabel);
    }

    /// <summary>显示指定单位的详细信息</summary>
    public void ShowForUnit(Unit unit, Vector2 screenPos)
    {
        if (unit?.Data == null) return;
        var data = unit.Data;

        _nameLabel.Text = data.UnitName;
        _levelLabel.Text = $"Lv.{data.Level}  {(data.IsEnemy ? "[敌方]" : "[友方]")}";

        // 六维属性
        foreach (var child in _attrGrid.GetChildren()) child.QueueFree();
        AddAttr("力", data.Str);
        AddAttr("敏", data.Dex);
        AddAttr("体", data.Con);
        AddAttr("智", data.Intel);
        AddAttr("感", data.Wis);
        AddAttr("魅", data.Cha);

        // 战斗数值
        _hpLabel.Text = $"HP: {unit.CurrentHp}/{unit.GetMaxHp()}";
        _apLabel.Text = $"AP: {(int)unit.CurrentAp}/{unit.GetMaxAp()}";
        _acLabel.Text = $"AC: {unit.GetEffectiveAc()}";
        int dr = unit.GetDr();
        int maxDr = unit.GetMaxDr();
        _drLabel.Text = maxDr > 0 ? $"DR: {dr}/{maxDr}" : "DR: 无";
        _moveLabel.Text = $"移动: {unit.GetMoveRange()} 格";

        // 装备
        var weapon = unit.GetMainHand() as WeaponData;
        _weaponLabel.Text = weapon != null
            ? $"武器: {weapon.ItemName} ({weapon.DamageDiceCount}d{weapon.DamageDiceSides})"
            : "武器: 徒手";

        var armor = data.Armor;
        _armorLabel.Text = armor != null
            ? $"护甲: {armor.ItemName} (DR{armor.DrThreshold})"
            : "护甲: 无";

        // AI 策略
        if (data.IsEnemy)
        {
            _strategyLabel.Text = $"行为: {GetStrategyName(data.aiStrategy)}";
            _strategyLabel.Visible = true;
        }
        else
        {
            _strategyLabel.Visible = false;
        }

        // 定位到鼠标位置
        Visible = true;
        Position = screenPos + new Vector2(16, 16);

        // 边界修正（下一帧生效）
        CallDeferred(nameof(ClampToViewport));
    }

    public void HidePanel()
    {
        Visible = false;
    }

    private void ClampToViewport()
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        var pos = Position;
        if (pos.X + Size.X > vpSize.X) pos.X = vpSize.X - Size.X - 8;
        if (pos.Y + Size.Y > vpSize.Y) pos.Y = vpSize.Y - Size.Y - 8;
        if (pos.X < 0) pos.X = 8;
        if (pos.Y < 0) pos.Y = 8;
        Position = pos;
    }

    private void AddAttr(string name, int value)
    {
        var lbl = MakeLabel($"{name}{value}", 12, new Color(0.85f, 0.82f, 0.75f));
        _attrGrid.AddChild(lbl);
    }

    private static string GetStrategyName(UnitData.AIStrategy s) => s switch
    {
        UnitData.AIStrategy.Reckless => "鲁莽",
        UnitData.AIStrategy.Cautious => "谨慎",
        UnitData.AIStrategy.Tactical => "战术",
        UnitData.AIStrategy.Instinct => "本能",
        _ => "未知",
    };

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        sep.Modulate = new Color(1, 1, 1, 0.3f);
        return sep;
    }
}
