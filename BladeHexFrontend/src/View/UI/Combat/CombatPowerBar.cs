// CombatPowerBar.cs
// 战力对比面板 — 顶部居中小面板，显示双方人数和战力条
// 战力条从中间向两侧延伸：左侧蓝色=我方，右侧红色=敌方
// 角色死亡或脱离战场时实时扣减对应方战力
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战力对比面板 — 显示双方人数 + 战力比例条。
/// 战力基于角色等级(CR)计算，死亡/脱离时实时扣减。
/// </summary>
[GlobalClass]
public partial class CombatPowerBar : PanelContainer
{
    // ============================================================================
    // 内部控件
    // ============================================================================
    private Label _allyCountLabel = null!;
    private Label _enemyCountLabel = null!;
    private Control _powerBarContainer = null!;
    private TextureRect _allyBar = null!;
    private TextureRect _enemyBar = null!;

    // ============================================================================
    // 战力数据
    // ============================================================================
    private float _totalAllyPower;
    private float _totalEnemyPower;
    private float _currentAllyPower;
    private float _currentEnemyPower;
    private float _maxAllyPower;
    private float _maxEnemyPower;
    private int _allyAliveCount;
    private int _enemyAliveCount;
    private int _allyTotalCount;
    private int _enemyTotalCount;

    // 动画
    private float _displayAllyRatio;
    private float _displayEnemyRatio;
    private const float LerpSpeed = 4.0f;

    // ============================================================================
    // 颜色
    // ============================================================================
    private static readonly Color AllyColor = new(0.36f, 0.56f, 0.68f, 0.95f);
    private static readonly Color EnemyColor = new(0.70f, 0.26f, 0.22f, 0.95f);
    private static readonly Color BgColor = new(0.070f, 0.060f, 0.052f, 0.88f);
    private static readonly Color BarBgColor = new(0.035f, 0.032f, 0.030f, 0.82f);
    private static readonly Color BorderColor = new(0.42f, 0.34f, 0.23f, 0.78f);

    // ============================================================================
    // 常量
    // ============================================================================
    private const float BarWidth = 360f;
    private const float BarHeight = 14f;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        BuildUI();
    }

    public override void _Process(double delta)
    {
        // 平滑动画插值
        float targetAllyRatio = GetAllyRatio();
        float targetEnemyRatio = GetEnemyRatio();

        _displayAllyRatio = Mathf.Lerp(_displayAllyRatio, targetAllyRatio, (float)delta * LerpSpeed);
        _displayEnemyRatio = Mathf.Lerp(_displayEnemyRatio, targetEnemyRatio, (float)delta * LerpSpeed);

        UpdateBarVisuals();
    }

    // ============================================================================
    // UI 构建
    // ============================================================================

    private void BuildUI()
    {
        var style = new StyleBoxFlat();
        style.BgColor = BgColor;
        style.SetBorderWidthAll(1);
        style.BorderColor = BorderColor;
        style.SetCornerRadiusAll(5);
        style.SetContentMarginAll(10);
        AddThemeStyleboxOverride("panel", style);

        CustomMinimumSize = new Vector2(BarWidth + 80, 0);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        AddChild(vbox);

        // 第一行：人数显示
        var countRow = new HBoxContainer();
        countRow.AddThemeConstantOverride("separation", 0);
        vbox.AddChild(countRow);

        _allyCountLabel = new Label();
        _allyCountLabel.Text = "友方 0/0";
        _allyCountLabel.AddThemeFontSizeOverride("font_size", 16);
        _allyCountLabel.AddThemeColorOverride("font_color", AllyColor);
        _allyCountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _allyCountLabel.HorizontalAlignment = HorizontalAlignment.Left;
        countRow.AddChild(_allyCountLabel);

        _enemyCountLabel = new Label();
        _enemyCountLabel.Text = "敌方 0/0";
        _enemyCountLabel.AddThemeFontSizeOverride("font_size", 16);
        _enemyCountLabel.AddThemeColorOverride("font_color", EnemyColor);
        _enemyCountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _enemyCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
        countRow.AddChild(_enemyCountLabel);

        // 第二行：战力条
        _powerBarContainer = new Control();
        _powerBarContainer.CustomMinimumSize = new Vector2(BarWidth, BarHeight);
        vbox.AddChild(_powerBarContainer);

        // 条背景
        var barBg = new ColorRect();
        barBg.Color = BarBgColor;
        barBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _powerBarContainer.AddChild(barBg);

        // 我方战力条（从左向右）
        _allyBar = new TextureRect();
        var allyFallback = new ColorRect();
        allyFallback.Color = AllyColor;
        _allyBar.AddChild(allyFallback);
        allyFallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _allyBar.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _allyBar.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _allyBar.Position = Vector2.Zero;
        _allyBar.Size = new Vector2(0, BarHeight);
        _powerBarContainer.AddChild(_allyBar);

        // 敌方战力条（从右向左）
        _enemyBar = new TextureRect();
        var enemyFallback = new ColorRect();
        enemyFallback.Color = EnemyColor;
        _enemyBar.AddChild(enemyFallback);
        enemyFallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _enemyBar.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _enemyBar.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _enemyBar.Position = new Vector2(BarWidth, 0);
        _enemyBar.Size = new Vector2(0, BarHeight);
        _powerBarContainer.AddChild(_enemyBar);
    }

    // ============================================================================
    // 公开 API
    // ============================================================================

    /// <summary>初始化战力数据（战斗开始时调用）</summary>
    public void Initialize(IEnumerable<Unit> playerUnits, IEnumerable<Unit> enemyUnits)
    {
        _totalAllyPower = 0;
        _totalEnemyPower = 0;

        _allyTotalCount = 0;
        _enemyTotalCount = 0;

        foreach (var unit in playerUnits)
        {
            if (unit?.Data == null) continue;
            _totalAllyPower += GetUnitPower(unit);
            _allyTotalCount++;
        }

        foreach (var unit in enemyUnits)
        {
            if (unit?.Data == null) continue;
            _totalEnemyPower += GetUnitPower(unit);
            _enemyTotalCount++;
        }

        _currentAllyPower = _totalAllyPower;
        _currentEnemyPower = _totalEnemyPower;
        _maxAllyPower = _currentAllyPower;
        _maxEnemyPower = _currentEnemyPower;
        _allyAliveCount = _allyTotalCount;
        _enemyAliveCount = _enemyTotalCount;

        // 初始化显示比例
        _displayAllyRatio = GetAllyRatio();
        _displayEnemyRatio = GetEnemyRatio();

        UpdateLabels();
        UpdateBarVisuals();
    }

    /// <summary>刷新战力（每次有单位死亡/脱离时调用）</summary>
    public void Refresh(IEnumerable<Unit> playerUnits, IEnumerable<Unit> enemyUnits)
    {
        _currentAllyPower = 0;
        _allyAliveCount = 0;
        foreach (var unit in playerUnits)
        {
            if (unit == null || !GodotObject.IsInstanceValid(unit)) continue;
            if (unit.Data == null || unit.CurrentHp <= 0) continue;
            _currentAllyPower += GetUnitPower(unit);
            _allyAliveCount++;
        }

        _currentEnemyPower = 0;
        _enemyAliveCount = 0;
        foreach (var unit in enemyUnits)
        {
            if (unit == null || !GodotObject.IsInstanceValid(unit)) continue;
            if (unit.Data == null || unit.CurrentHp <= 0) continue;
            _currentEnemyPower += GetUnitPower(unit);
            _enemyAliveCount++;
        }

        // 动态校准历史最大战力，防止因加载顺序导致初始总战力偏小
        _maxAllyPower = Mathf.Max(_maxAllyPower, _currentAllyPower);
        _maxEnemyPower = Mathf.Max(_maxEnemyPower, _currentEnemyPower);

        UpdateLabels();
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    /// <summary>计算单位战力值 = CR（基于等级）</summary>
    private static float GetUnitPower(Unit unit)
    {
        if (unit?.Data == null) return 0;

        // 敌方直接用 ThreatLevel（已经是 CR）
        if (unit.Data.IsEnemy && unit.Data.ThreatLevel > 0)
            return unit.Data.ThreatLevel;

        // 玩家方用等级换算 CR
        return RPGRuleEngine.GetCrFromLevel(unit.Data.Level);
    }

    private float GetAllyRatio()
    {
        float maxTotal = _maxAllyPower + _maxEnemyPower;
        if (maxTotal <= 0) return 0.5f;
        return _currentAllyPower / maxTotal;
    }

    private float GetEnemyRatio()
    {
        float maxTotal = _maxAllyPower + _maxEnemyPower;
        if (maxTotal <= 0) return 0.5f;
        return _currentEnemyPower / maxTotal;
    }

    private void UpdateLabels()
    {
        _allyCountLabel.Text = $"友方 {_allyAliveCount}/{_allyTotalCount}";
        _enemyCountLabel.Text = $"敌方 {_enemyAliveCount}/{_enemyTotalCount}";
    }

    private void UpdateBarVisuals()
    {
        // 我方从左向右
        float allyWidth = _displayAllyRatio * BarWidth;
        _allyBar.Position = new Vector2(0, 0);
        _allyBar.Size = new Vector2(allyWidth, BarHeight);

        // 敌方从右向左
        float enemyWidth = _displayEnemyRatio * BarWidth;
        _enemyBar.Position = new Vector2(BarWidth - enemyWidth, 0);
        _enemyBar.Size = new Vector2(enemyWidth, BarHeight);
    }
}
