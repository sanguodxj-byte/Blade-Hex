// DialoguePanel.cs
// 对话面板 — 与大地图 NPC 交谈时显示对话内容与选项
// 统一继承自 POIPanelBase，保证在大地图遭遇切换时大小比例（960x760）完全合一。
using Godot;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class DialoguePanel : POIPanelBase
{
    [Signal] public delegate void DialogueFinishedEventHandler();
    [Signal] public delegate void TradeRequestedFromDialogueEventHandler(string npcName);
    [Signal] public delegate void CombatRequestedFromDialogueEventHandler();
    [Signal] public delegate void RecruitSuccessFromDialogueEventHandler();
    [Signal] public delegate void SurrenderFromDialogueEventHandler();

    private NPCProfile? _currentProfile;
    private DialogueRunner? _runner;

    // ============================================================================
    // 数据填充 (重写 POIPanelBase 规范)
    // ============================================================================

    protected override Color GetIllustrationColor() => new(0.08f, 0.08f, 0.12f, 1.0f);

    protected override string GetIllustrationText()
    {
        if (_runner?.Profile == null) return "[ 交谈 ]";
        return $"[ {_runner.Profile.npcName} ]";
    }

    protected override string? GetIllustrationPath()
    {
        if (_runner?.Profile == null) return null;
        return POIIllustrationResolver.GetNpcIllustration((int)_runner.Profile.npcType);
    }

    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        if (_runner?.Profile == null) return "";
        string typeName = _runner.Profile.GetNpcTypeNameForType((int)_runner.Profile.npcType);
        string attitudeText = _runner.Profile.GetAttitudeText();
        return $"{_runner.Profile.npcName} | {typeName} | 态度: {attitudeText} (与玩家关系: {_runner.Profile.relation})";
    }

    protected override string GetDescriptionText()
    {
        if (_runner == null) return "";
        return _runner.GetCurrentText();
    }

    protected override string GetLeaveButtonText() => "结束交谈";

    protected override void PopulateActions(VBoxContainer actionsContainer)
    {
        if (_runner?.CurrentNode == null) return;

        var grid = new GridContainer();
        grid.Columns = 1; // 对话选项垂直铺开，一行一个
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddThemeConstantOverride("v_separation", 6);
        actionsContainer.AddChild(grid);

        var options = _runner.CurrentNode.Options;
        foreach (var opt in options)
        {
            // 检查显示条件是否满足
            bool meet = _runner.CheckCondition(opt);
            string formattedText = _runner.FormatText(opt.Text);

            Button btn;
            if (!meet)
            {
                // 不满足条件，把按钮置灰并增加后缀提示
                string suffix = "";
                if (opt.Condition.Contains("gold") || opt.Condition.Contains("has_gold")) suffix = " (金币不足)";
                else if (opt.Condition.Contains("relation")) suffix = " (关系不足)";
                else if (opt.Condition.Contains("rep") || opt.Condition.Contains("faction_rep")) suffix = " (声望不足)";
                else if (opt.Condition.Contains("army") || opt.Condition.Contains("player_army")) suffix = " (兵力不足)";
                
                btn = CreateActionButton(formattedText + suffix, enabled: false, disabledReason: suffix);
            }
            else
            {
                btn = CreateActionButton(formattedText);
                var captured = opt;
                btn.Pressed += () => OnResponseSelected(captured);
            }

            grid.AddChild(btn);
        }
    }

    // ============================================================================
    // 公开接口与业务逻辑
    // ============================================================================

    public void ShowDialogue(NPCProfile profile, BladeHex.Scenes.Overworld2d.OverworldScene2D overworldScene, bool instantOverlay = false)
    {
        _currentProfile = profile;

        // 初始化对话树状态机
        _runner = new DialogueRunner(profile, overworldScene.ReputationTracker, overworldScene.EconomyMgr);
        
        // 注入当前大地图的玩家真实兵力大小与领袖种族
        if (overworldScene.PlayerParty?.Roster != null)
        {
            _runner.Context.PlayerArmySize = overworldScene.PlayerParty.Roster.Count;
            if (overworldScene.PlayerParty.Roster.Leader?.Race != null)
            {
                _runner.Context.PlayerRace = overworldScene.PlayerParty.Roster.Leader.Race.raceId.ToString();
            }
        }
        
        // 绑定动作触发事件
        _runner.TradeRequested += OnTradeRequested;
        _runner.CombatRequested += OnCombatRequested;
        _runner.RecruitSuccessRequested += OnRecruitSuccess;
        _runner.SurrenderRequested += OnSurrender;
        _runner.DialogueEnded += OnDialogueEnded;

        ShowPanel(instantOverlay);
    }

    public override void HidePanel()
    {
        base.HidePanel();
        _currentProfile = null;
        if (_runner != null)
        {
            _runner.TradeRequested -= OnTradeRequested;
            _runner.CombatRequested -= OnCombatRequested;
            _runner.RecruitSuccessRequested -= OnRecruitSuccess;
            _runner.SurrenderRequested -= OnSurrender;
            _runner.DialogueEnded -= OnDialogueEnded;
            _runner = null;
        }
    }

    private void OnResponseSelected(DialogueOption option)
    {
        if (_runner == null) return;
        _runner.SelectOption(option);
        
        // 只有在 runner 未被隐藏销毁时（有些 action 比如 trade/combat 会立即 HidePanel 并置空 runner），才更新下一个节点的渲染
        if (_runner != null)
        {
            RefreshLayout();
        }
    }

    // ============================================================================
    // 动作回调与关闭
    // ============================================================================

    private void OnTradeRequested(string npcName)
    {
    	HidePanel();
    	EmitSignal(SignalName.TradeRequestedFromDialogue, npcName);
    }
   
    private void OnCombatRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.CombatRequestedFromDialogue);
    }
   
    private void OnRecruitSuccess()
    {
    	HidePanel();
    	EmitSignal(SignalName.RecruitSuccessFromDialogue);
    }
   
    private void OnSurrender()
    {
    	HidePanel();
    	EmitSignal(SignalName.SurrenderFromDialogue);
    }

    private void OnDialogueEnded()
    {
    	HidePanel();
    	EmitSignal(SignalName.DialogueFinished);
    }
   
    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.DialogueFinished);
    }
}
