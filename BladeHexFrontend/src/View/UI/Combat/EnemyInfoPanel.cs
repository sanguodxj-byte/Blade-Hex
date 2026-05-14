// EnemyInfoPanel.cs
// 右侧敌方信息面板 - 显示所有可见敌方单位的列表
// 布局：垂直滚动列表，每个条目包含名称/HP条/AC/威胁等级/AI策略标签
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 敌方信息面板 — 右侧敌方列表，显示所有可见敌方单位的HP/士气/AC等信息
/// </summary>
[GlobalClass]
public partial class EnemyInfoPanel : PanelContainer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal] public delegate void EnemyHoveredEventHandler(Unit unit);
    [Signal] public delegate void EnemyUnhoveredEventHandler();

    // ============================================================================
    // 内部控件
    // ============================================================================
    private VBoxContainer _enemyList = null!;
    private ScrollContainer _scrollContainer = null!;
    private readonly Dictionary<string, Control> _enemyEntries = new();

    // ============================================================================
    // 样式常量
    // ============================================================================
    private static readonly Color BG_COLOR = new(0.08f, 0.06f, 0.1f, 0.92f);
    private static readonly Color BORDER_COLOR = new(0.4f, 0.15f, 0.15f, 0.8f);
    private static readonly Color HP_BAR_BG = new(0.2f, 0.1f, 0.1f, 0.6f);
    private static readonly Color HP_HIGH = new(0.2f, 0.75f, 0.2f);
    private static readonly Color HP_MID = new(0.85f, 0.75f, 0.1f);
    private static readonly Color HP_LOW = new(0.9f, 0.15f, 0.1f);
    private static readonly Color MORALE_HIGH = new(0.2f, 0.8f, 0.9f);
    private static readonly Color MORALE_NORMAL = new(0.6f, 0.6f, 0.6f);
    private static readonly Color MORALE_LOW = new(0.9f, 0.7f, 0.1f);
    private static readonly Color MORALE_BROKEN = new(0.9f, 0.2f, 0.1f);

    private static readonly Dictionary<UnitData.EnemyType, Color> ENEMY_TYPE_COLORS = new()
    {
        { UnitData.EnemyType.Humanoid, new Color(0.7f, 0.65f, 0.55f) },
        { UnitData.EnemyType.Beast, new Color(0.6f, 0.5f, 0.3f) },
        { UnitData.EnemyType.Undead, new Color(0.5f, 0.55f, 0.7f) },
        { UnitData.EnemyType.Demon, new Color(0.7f, 0.3f, 0.5f) },
        { UnitData.EnemyType.Giant, new Color(0.8f, 0.5f, 0.2f) },
    };

    // ============================================================================
    // _Ready
    // ============================================================================
    public override void _Ready()
    {
        _SetupPanel();
    }

    // ============================================================================
    // 面板初始化
    // ============================================================================
    private void _SetupPanel()
    {
        // 面板样式
        var style = new StyleBoxFlat();
        style.BgColor = BG_COLOR;
        style.SetBorderWidthAll(2);
        style.BorderColor = BORDER_COLOR;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(6);
        AddThemeStyleboxOverride("panel", style);

        // 固定宽度
        CustomMinimumSize = new Vector2(220, 0);

        // 标题
        var title = new Label();
        title.Text = "— 敌 方 —";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.4f));
        AddChild(title);

        // 分隔线
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", _MakeLineStyle(new Color(0.4f, 0.15f, 0.15f, 0.5f)));
        AddChild(sep);

        // 滚动容器
        _scrollContainer = new ScrollContainer();
        _scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        _scrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        AddChild(_scrollContainer);

        _enemyList = new VBoxContainer();
        _enemyList.AddThemeConstantOverride("separation", 4);
        _scrollContainer.AddChild(_enemyList);
    }

    private static StyleBoxFlat _MakeLineStyle(Color color)
    {
        var s = new StyleBoxFlat();
        s.BgColor = color;
        s.SetContentMarginAll(1);
        return s;
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>添加一个敌方单位到列表</summary>
    public void AddEnemy(Unit unit)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit))
            return;
        if (unit.Data == null)
            return;

        string key = unit.Name.ToString();
        if (_enemyEntries.ContainsKey(key))
            return;

        var entry = _CreateEnemyEntry(unit);
        _enemyList.AddChild(entry);
        _enemyEntries[key] = entry;
    }

    /// <summary>移除一个敌方单位（死亡时）</summary>
    public void RemoveEnemy(Unit unit)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit))
            return;
        if (unit.Data == null)
            return;

        string key = unit.Name.ToString();
        if (!_enemyEntries.TryGetValue(key, out var entry))
            return;

        _enemyList.RemoveChild(entry);
        entry.QueueFree();
        _enemyEntries.Remove(key);
    }

    /// <summary>更新指定敌方单位的信息</summary>
    public void UpdateEnemy(Unit unit)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit))
            return;
        if (unit.Data == null)
            return;

        string key = unit.Name.ToString();
        if (!_enemyEntries.TryGetValue(key, out var entry))
            return;

        var data = unit.Data;
        int maxHp = unit.GetMaxHp();
        float hpRatio = (float)unit.CurrentHp / Mathf.Max(maxHp, 1);

        // 更新 HP 条
        var hpBar = entry.GetNodeOrNull<ProgressBar>("HPBar");
        if (hpBar != null)
        {
            hpBar.Value = unit.CurrentHp;
            hpBar.MaxValue = maxHp;

            if (hpBar.GetThemeStylebox("fill") is StyleBoxFlat hpFill)
            {
                hpFill.BgColor = _GetHpColor(hpRatio);
            }
        }

        // 更新 HP 文本
        var hpLabel = entry.GetNodeOrNull<Label>("HPLabel");
        if (hpLabel != null)
        {
            hpLabel.Text = $"HP {unit.CurrentHp}/{maxHp}";
        }

        // 更新士气条
        var moraleBar = entry.GetNodeOrNull<ProgressBar>("MoraleBar");
        if (moraleBar != null && data.IsEnemy)
        {
            // 士气范围 -60 ~ +40，映射为进度条 0~100
            float moraleNormalized = (float)(data.Morale + 60) / 100.0f;
            moraleBar.Value = moraleNormalized * 100;

            if (moraleBar.GetThemeStylebox("fill") is StyleBoxFlat moraleFill)
            {
                moraleFill.BgColor = _GetMoraleColor(data.GetMoraleLevel());
            }
        }

        // 更新士气标签
        var moraleLabel = entry.GetNodeOrNull<Label>("MoraleLabel");
        if (moraleLabel != null && data.IsEnemy)
        {
            moraleLabel.Text = _GetMoraleText(data.GetMoraleLevel());
        }
    }

    /// <summary>高亮指定敌方（被悬停/选中时）</summary>
    public void HighlightEnemy(Unit unit, bool highlighted)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit))
            return;
        if (unit.Data == null)
            return;

        string key = unit.Name.ToString();
        if (!_enemyEntries.TryGetValue(key, out var entry))
            return;

        if (entry is not PanelContainer panel)
            return;

        if (panel.GetThemeStylebox("panel") is StyleBoxFlat style)
        {
            if (highlighted)
            {
                style.BgColor = new Color(0.5f, 0.15f, 0.15f, 0.7f);
                style.BorderColor = new Color(0.9f, 0.4f, 0.4f);
            }
            else
            {
                style.BgColor = new Color(0.15f, 0.08f, 0.1f, 0.6f);
                style.BorderColor = new Color(0.3f, 0.15f, 0.15f, 0.4f);
            }
        }
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    /// <summary>创建单个敌方条目</summary>
    private PanelContainer _CreateEnemyEntry(Unit unit)
    {
        var data = unit.Data!;
        int maxHp = unit.GetMaxHp();
        float hpRatio = (float)unit.CurrentHp / Mathf.Max(maxHp, 1);

        var entry = new PanelContainer();
        var entryStyle = new StyleBoxFlat();
        entryStyle.BgColor = new Color(0.15f, 0.08f, 0.1f, 0.6f);
        entryStyle.SetBorderWidthAll(1);
        entryStyle.BorderColor = new Color(0.3f, 0.15f, 0.15f, 0.4f);
        entryStyle.SetCornerRadiusAll(3);
        entryStyle.SetContentMarginAll(5);
        entry.AddThemeStyleboxOverride("panel", entryStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        entry.AddChild(vbox);

        // 第一行：名称 + 威胁等级标签
        var row1 = new HBoxContainer();
        vbox.AddChild(row1);

        var nameLabel = new Label();
        nameLabel.Text = data.UnitName;
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.8f));
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row1.AddChild(nameLabel);

        // CR标签
        if (data.IsEnemy)
        {
            var crLabel = new Label();
            crLabel.Text = data.GetCrText();
            crLabel.AddThemeFontSizeOverride("font_size", 11);
            crLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.3f));
            row1.AddChild(crLabel);
        }

        // 第二行：敌人类型标签 + AI策略标签
        if (data.IsEnemy)
        {
            var row1b = new HBoxContainer();
            row1b.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(row1b);

            var typeLabel = new Label();
            typeLabel.Text = $"[ {data.GetEnemyTypeName()} ]";
            typeLabel.AddThemeFontSizeOverride("font_size", 10);

            Color typeColor = Colors.Gray;
            if (ENEMY_TYPE_COLORS.TryGetValue(data.enemyType, out var tc))
                typeColor = tc;
            typeLabel.AddThemeColorOverride("font_color", typeColor);
            row1b.AddChild(typeLabel);

            var stratLabel = new Label();
            stratLabel.Text = $"AI: {data.GetAiStrategyName()}";
            stratLabel.AddThemeFontSizeOverride("font_size", 10);
            stratLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            row1b.AddChild(stratLabel);
        }

        // 第三行：HP 条
        var hpHbox = new HBoxContainer();
        vbox.AddChild(hpHbox);

        var hpBar = new ProgressBar();
        hpBar.Name = "HPBar";
        hpBar.MinValue = 0;
        hpBar.MaxValue = maxHp;
        hpBar.Value = unit.CurrentHp;
        hpBar.CustomMinimumSize = new Vector2(120, 12);
        hpBar.ShowPercentage = false;

        var hpBg = new StyleBoxFlat();
        hpBg.BgColor = HP_BAR_BG;
        hpBg.SetCornerRadiusAll(2);
        hpBar.AddThemeStyleboxOverride("background", hpBg);

        var hpFill = new StyleBoxFlat();
        hpFill.BgColor = _GetHpColor(hpRatio);
        hpFill.SetCornerRadiusAll(2);
        hpBar.AddThemeStyleboxOverride("fill", hpFill);

        hpHbox.AddChild(hpBar);

        var hpLabel = new Label();
        hpLabel.Name = "HPLabel";
        hpLabel.Text = $"HP {unit.CurrentHp}/{maxHp}";
        hpLabel.AddThemeFontSizeOverride("font_size", 10);
        hpLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        hpHbox.AddChild(hpLabel);

        // 第四行：士气条（仅敌方显示）
        if (data.IsEnemy)
        {
            var moraleHbox = new HBoxContainer();
            vbox.AddChild(moraleHbox);

            var moraleIcon = new Label();
            moraleIcon.Text = "士气";
            moraleIcon.AddThemeFontSizeOverride("font_size", 9);
            moraleIcon.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            moraleHbox.AddChild(moraleIcon);

            var moraleBar = new ProgressBar();
            moraleBar.Name = "MoraleBar";
            moraleBar.MinValue = 0;
            moraleBar.MaxValue = 100;
            float moraleNormalized = (float)(data.Morale + 60) / 100.0f;
            moraleBar.Value = moraleNormalized * 100;
            moraleBar.CustomMinimumSize = new Vector2(80, 8);
            moraleBar.ShowPercentage = false;

            var moraleBg = new StyleBoxFlat();
            moraleBg.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            moraleBg.SetCornerRadiusAll(1);
            moraleBar.AddThemeStyleboxOverride("background", moraleBg);

            var moraleFill2 = new StyleBoxFlat();
            moraleFill2.BgColor = _GetMoraleColor(data.GetMoraleLevel());
            moraleFill2.SetCornerRadiusAll(1);
            moraleBar.AddThemeStyleboxOverride("fill", moraleFill2);

            moraleHbox.AddChild(moraleBar);

            var moraleLabel = new Label();
            moraleLabel.Name = "MoraleLabel";
            moraleLabel.Text = _GetMoraleText(data.GetMoraleLevel());
            moraleLabel.AddThemeFontSizeOverride("font_size", 9);
            moraleLabel.AddThemeColorOverride("font_color", _GetMoraleColor(data.GetMoraleLevel()));
            moraleHbox.AddChild(moraleLabel);
        }

        // 第五行：AC/速度信息
        var rowBottom = new HBoxContainer();
        rowBottom.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(rowBottom);

        var acLabel = new Label();
        acLabel.Text = $"闪避 {unit.GetAc()}";
        acLabel.AddThemeFontSizeOverride("font_size", 10);
        acLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
        rowBottom.AddChild(acLabel);

        var speedLabel = new Label();
        speedLabel.Text = $"速度 {data.BaseMoveRange}格";
        speedLabel.AddThemeFontSizeOverride("font_size", 10);
        speedLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        rowBottom.AddChild(speedLabel);

        // 免疫/抗性标签
        if (data.IsEnemy && (data.Immunities.Length > 0 || data.Resistances.Length > 0))
        {
            var resistRow = new HBoxContainer();
            resistRow.AddThemeConstantOverride("separation", 3);
            vbox.AddChild(resistRow);

            foreach (string imm in data.Immunities)
            {
                var immLabel = new Label();
                immLabel.Text = $"[免疫:{imm}]";
                immLabel.AddThemeFontSizeOverride("font_size", 9);
                immLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 0.9f));
                resistRow.AddChild(immLabel);
            }

            foreach (string res in data.Resistances)
            {
                var resLabel = new Label();
                resLabel.Text = $"[抗性:{res}]";
                resLabel.AddThemeFontSizeOverride("font_size", 9);
                resLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.4f));
                resistRow.AddChild(resLabel);
            }
        }

        // 悬停信号
        entry.GuiInput += (ev) => _OnEntryInput(ev, unit);

        return entry;
    }

    private void _OnEntryInput(InputEvent @event, Unit unit)
    {
        if (@event is InputEventMouseMotion motion)
        {
            if (motion.Relative == Vector2.Zero)
            {
                EmitSignal(SignalName.EnemyHovered, unit);
            }
        }
        else if (@event is InputEventMouseButton btn && btn.Pressed)
        {
            // 可扩展：点击选中敌方
        }
    }

    // ============================================================================
    // 颜色/文本辅助方法
    // ============================================================================

    private static Color _GetHpColor(float ratio)
    {
        if (ratio > 0.6f) return HP_HIGH;
        if (ratio > 0.3f) return HP_MID;
        return HP_LOW;
    }

    private static Color _GetMoraleColor(UnitData.MoraleLevel level)
    {
        return level switch
        {
            UnitData.MoraleLevel.High => MORALE_HIGH,
            UnitData.MoraleLevel.Normal => MORALE_NORMAL,
            UnitData.MoraleLevel.Low => MORALE_LOW,
            UnitData.MoraleLevel.Broken => MORALE_BROKEN,
            UnitData.MoraleLevel.Routing => MORALE_BROKEN,
            _ => MORALE_NORMAL,
        };
    }

    private static string _GetMoraleText(UnitData.MoraleLevel level)
    {
        return level switch
        {
            UnitData.MoraleLevel.High => "高昂",
            UnitData.MoraleLevel.Normal => "正常",
            UnitData.MoraleLevel.Low => "低落",
            UnitData.MoraleLevel.Broken => "崩溃!",
            UnitData.MoraleLevel.Routing => "溃逃!!",
            _ => "正常",
        };
    }
}
