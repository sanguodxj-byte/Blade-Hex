// BattleLogPanel.cs
// 战斗日志面板 — 滚动文本显示攻击结果、伤害数字、状态变化
// 对应策划案 09-UI设计.md → 战斗日志 / 伤害数字弹出 / 状态变化通知
// 设计原则：不暴露骰子术语，显示概率和直观结果
using Godot;
using System.Collections.Generic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗日志面板 — 滚动文本显示攻击结果、伤害数字、状态变化
/// </summary>
[GlobalClass]
public partial class BattleLogPanel : PanelContainer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal]
    public delegate void LogHoveredEventHandler(int entryIndex);

    // ============================================================================
    // 常量
    // ============================================================================
    private const int MaxEntries = 200;

    // ============================================================================
    // 日志详细度（与 GameSettings.combat_log_detail 对齐）
    // 0=简洁, 1=标准, 2=详细（含骰子）
    // ============================================================================
    public int LogDetail { get; set; } = 1;

    // ============================================================================
    // 内部
    // ============================================================================
    private RichTextLabel _log = null!;
    private ScrollContainer _scroll = null!;
    private readonly List<string> _entries = new();
    private UIFactory _factory = null!;

    private Timer _fadeTimer = null!;
    private Tween? _fadeTween;
    private bool _isHovered;

    public override void _Ready()
    {
        _factory = new UIFactory();
        Setup();

        _fadeTimer = new Timer();
        _fadeTimer.WaitTime = 4.0f;
        _fadeTimer.OneShot = true;
        _fadeTimer.Timeout += OnFadeTimerTimeout;
        AddChild(_fadeTimer);

        MouseEntered += () =>
        {
            _isHovered = true;
            WakeUp();
        };
        MouseExited += () =>
        {
            _isHovered = false;
            _fadeTimer.Start();
        };

        // 初始化可见（战斗日志始终显示）
        Modulate = new Color(Modulate, 0.85f);
    }

    private void WakeUp()
    {
        if (_fadeTween != null && _fadeTween.IsValid())
            _fadeTween.Kill();
        Modulate = new Color(Modulate, 0.85f);
        // 不再自动淡出 — 战斗日志始终可见
    }

    private void OnFadeTimerTimeout()
    {
        if (_isHovered)
            return;
        if (_fadeTween != null && _fadeTween.IsValid())
            _fadeTween.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 0.0f, 1.0f);
    }

    private void Setup()
    {
        CustomMinimumSize = new Vector2(0, 100);
        AddThemeStyleboxOverride("panel", UITheme.Instance!.MakePanelStyle(
            UITheme.Instance.BgTertiary,
            UITheme.Instance.BorderDefault,
            cornerRadius: UITheme.Instance.RadiusMd));

        _scroll = _factory.CreateScrollContainer(false);
        _scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        AddChild(_scroll);

        _log = _factory.CreateRichText();
        _log.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _log.ScrollActive = true;
        _log.ScrollFollowing = true;
        _log.BbcodeEnabled = true;
        _log.CustomMinimumSize = new Vector2(0, 80);
        _scroll.AddChild(_log);
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>
    /// 添加日志条目
    /// </summary>
    public void AddEntry(string text, string category = "info")
    {
        WakeUp();

        // 日志详细度过滤
        switch (LogDetail)
        {
            case 0: // 简洁：只显示回合、死亡、暴击
                if (category is not ("turn" or "death_ally" or "death_enemy" or "critical"))
                    return;
                break;
            case 1: // 标准：隐藏移动日志
                if (category == "move")
                    return;
                break;
            // case 2: 详细：显示所有
        }

        string bbcode = FormatEntry(text, category);
        _entries.Add(bbcode);

        // 超出上限时移除最旧的
        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(0);

        RefreshDisplay();
    }

    /// <summary>
    /// 记录攻击结果
    /// </summary>
    public void LogAttack(string attackerName, string targetName,
        bool hit, int damage = 0, bool isCritical = false, bool isMiss = false)
    {
        if (isMiss)
        {
            AddEntry($"{attackerName} 攻击 {targetName} → 失误!", "miss");
        }
        else if (hit)
        {
            if (isCritical)
                AddEntry($"★ {attackerName} 命中 {targetName}！暴击！造成 {damage} 伤害", "critical");
            else
                AddEntry($"{attackerName} 命中 {targetName}，造成 {damage} 伤害", "hit");
        }
        else
        {
            AddEntry($"{attackerName} 攻击 {targetName} → 未命中", "miss");
        }
    }

    /// <summary>
    /// 记录法术施放
    /// </summary>
    public void LogSpell(string casterName, string spellName,
        string targetName = "", int damage = 0, bool hit = true)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            AddEntry($"{casterName} 施放了 {spellName}", "spell");
        }
        else if (hit)
        {
            AddEntry($"{casterName} 对 {targetName} 施放 {spellName}，造成 {damage} 伤害", "spell");
        }
        else
        {
            AddEntry($"{casterName} 对 {targetName} 施放 {spellName} → 抵抗", "miss");
        }
    }

    /// <summary>
    /// 记录状态变化
    /// </summary>
    public void LogStatus(string unitName, string status, bool gained = true)
    {
        if (gained)
            AddEntry($"{unitName} 获得 {status}", "status_gain");
        else
            AddEntry($"{unitName} 解除 {status}", "status_loss");
    }

    /// <summary>
    /// 记录士气变化
    /// </summary>
    public void LogMorale(string unitName, string moraleText, bool isPositive = true)
    {
        if (isPositive)
            AddEntry($"{unitName} 士气{moraleText}", "morale_up");
        else
            AddEntry($"{unitName} 士气{moraleText}", "morale_down");
    }

    /// <summary>
    /// 记录单位死亡
    /// </summary>
    public void LogDeath(string unitName, bool isPlayer = true)
    {
        if (isPlayer)
            AddEntry($"✘ {unitName} 倒下！", "death_ally");
        else
            AddEntry($"✘ {unitName} 被击败！", "death_enemy");
    }

    /// <summary>
    /// 记录回合信息
    /// </summary>
    public void LogTurn(string text)
    {
        AddEntry(text, "turn");
    }

    /// <summary>
    /// 记录移动
    /// </summary>
    public void LogMove(string unitName, string fromPos, string toPos)
    {
        AddEntry($"{unitName} 移动 {fromPos} → {toPos}", "move");
    }

    /// <summary>
    /// 清空日志
    /// </summary>
    public void ClearLog()
    {
        _entries.Clear();
        RefreshDisplay();
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    private string FormatEntry(string text, string category)
    {
        var color = UITheme.Instance!.TextPrimary;
        switch (category)
        {
            case "hit":           color = UITheme.Instance.TextPositive; break;
            case "miss":          color = UITheme.Instance.TextNegative; break;
            case "critical":      color = UITheme.Instance.TextAccent; break;
            case "spell":         color = UITheme.Instance.TextMagic; break;
            case "status_gain":   color = new Color(0.3f, 0.8f, 0.3f); break;
            case "status_loss":   color = new Color(0.8f, 0.5f, 0.3f); break;
            case "morale_up":     color = new Color(0.3f, 0.8f, 0.9f); break;
            case "morale_down":   color = new Color(0.9f, 0.5f, 0.2f); break;
            case "death_ally":    color = new Color(0.9f, 0.3f, 0.3f); break;
            case "death_enemy":   color = new Color(0.9f, 0.7f, 0.2f); break;
            case "turn":          color = UITheme.Instance.TextAccent; break;
            case "move":          color = UITheme.Instance.TextMuted; break;
            case "info":          color = UITheme.Instance.TextSecondary; break;
        }
        return $"[color={color.ToHtml(false)}]{text}[/color]";
    }

    private async void RefreshDisplay()
    {
        var fullText = string.Empty;
        foreach (string entry in _entries)
        {
            fullText += entry + "\n";
        }
        _log.Text = fullText;

        // 自动滚到底部
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;
    }
}
