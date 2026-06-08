// ArenaPanel.cs
// 竞技场面板 — 练习赛 + 锦标赛八强
// 使用统一布局基类，只填充数据
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Tournament;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class ArenaPanel : POIPanelBase
{
    [Signal]
    public delegate void ArenaFinishedEventHandler();

    /// <summary>请求进入竞技场战斗（BattleContext + 奖金）</summary>
    [Signal]
    public delegate void ArenaCombatRequestedEventHandler(BattleContext context, int prize);

    /// <summary>请求进入锦标赛战斗（BattleContext + 锦标赛上下文）</summary>
    [Signal]
    public delegate void TournamentCombatRequestedEventHandler(BattleContext context, Godot.Collections.Dictionary tournamentState);

    private EconomyManager? _economy;
    private TournamentService? _tournamentService;
    private TournamentBracket? _currentTournament;
    private string _currentTab = "practice"; // "practice" or "tournament"
    private VBoxContainer? _contentContainer;
    private int _playerLevel = 1;
    private string _hostTownName = "竞技场";
    private string _hostNationId = "";

    // ── 数据填充 ──

    protected override Color GetIllustrationColor() => new(0.14f, 0.08f, 0.06f, 1.0f);
    protected override string GetIllustrationText() => "[ 竞技场 ]";
    protected override string? GetIllustrationPath()
        => POIIllustrationResolver.GetPanelIllustration("arena");
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        if (_currentTournament != null && !_currentTournament.IsComplete)
            return $"竞技场 | 锦标赛进行中 | 金币: {_economy?.Gold ?? 0}";
        return _economy != null ? $"竞技场 | 金币: {_economy.Gold}" : "竞技场";
    }
    protected override string GetDescriptionText()
    {
        if (_currentTab == "tournament")
            return "八强淘汰赛 — 三场连续战斗，冠军获得5000金币、5影响力和竞技场冠军头衔！";
        return "在这里展示你的实力，赢取金币和声望。选择对手难度，报名参赛。";
    }
    protected override string GetLeaveButtonText() => "离开竞技场";

    protected override void PopulateActions(VBoxContainer container)
    {
        _contentContainer = container;

        // Tab 栏
        var tabHbox = new HBoxContainer();
        tabHbox.AddThemeConstantOverride("separation", 10);
        container.AddChild(tabHbox);

        var btnPractice = new Button { Text = "练习赛", CustomMinimumSize = new Vector2(120, 36) };
        btnPractice.Pressed += () => { _currentTab = "practice"; RefreshContent(); };
        tabHbox.AddChild(btnPractice);

        var btnTournament = new Button { Text = "锦标赛", CustomMinimumSize = new Vector2(120, 36) };
        btnTournament.Pressed += () => { _currentTab = "tournament"; RefreshContent(); };
        tabHbox.AddChild(btnTournament);

        container.AddChild(new HSeparator());

        RefreshContent();
    }

    private void RefreshContent()
    {
        if (_contentContainer == null) return;

        // 清除旧内容（保留 Tab 栏和分隔线）
        while (_contentContainer.GetChildCount() > 3)
        {
            var child = _contentContainer.GetChild(_contentContainer.GetChildCount() - 1);
            child.QueueFree();
        }

        if (_currentTab == "practice")
            PopulatePracticeTab(_contentContainer);
        else
            PopulateTournamentTab(_contentContainer);
    }

    private void PopulatePracticeTab(VBoxContainer container)
    {
        int easyFee = FacilityPricingService.GetArenaEntryFee(1);
        int easyPrize = FacilityPricingService.GetArenaPrize(1);
        bool canEasy = _economy != null && _economy.Gold >= easyFee;
        var btnEasy = CreateActionButton($"新手挑战 (报名费{easyFee}金 | 奖金{easyPrize}金) -- 低等级对手", canEasy, "金币不足");
        btnEasy.Pressed += () => StartFight(easyFee, easyPrize, 1);
        container.AddChild(btnEasy);

        int medFee = FacilityPricingService.GetArenaEntryFee(3);
        int medPrize = FacilityPricingService.GetArenaPrize(3);
        bool canMed = _economy != null && _economy.Gold >= medFee;
        var btnMed = CreateActionButton($"精英挑战 (报名费{medFee}金 | 奖金{medPrize}金) -- 中等对手", canMed, "金币不足");
        btnMed.Pressed += () => StartFight(medFee, medPrize, 3);
        container.AddChild(btnMed);

        int hardFee = FacilityPricingService.GetArenaEntryFee(5);
        int hardPrize = FacilityPricingService.GetArenaPrize(5);
        bool canHard = _economy != null && _economy.Gold >= hardFee;
        var btnHard = CreateActionButton($"冠军挑战 (报名费{hardFee}金 | 奖金{hardPrize}金) -- 强力对手", canHard, "金币不足");
        btnHard.Pressed += () => StartFight(hardFee, hardPrize, 5);
        container.AddChild(btnHard);
    }

    private void PopulateTournamentTab(VBoxContainer container)
    {
        // 如果已有锦标赛进行中
        if (_currentTournament != null && !_currentTournament.IsComplete)
        {
            DisplayBracket(container, _currentTournament);
            return;
        }

        // 如果锦标赛已完成，显示结果
        if (_currentTournament != null && _currentTournament.IsComplete)
        {
            bool isPlayerChampion = _currentTournament.WinnerParticipantId == "player";
            if (isPlayerChampion)
            {
                var winLabel = new Label { Text = "🏆 恭喜！你获得了锦标赛冠军！" };
                winLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.0f));
                winLabel.AddThemeFontSizeOverride("font_size", 20);
                container.AddChild(winLabel);
            }
            else
            {
                var winner = _currentTournament.Participants.FirstOrDefault(p => p.ParticipantId == _currentTournament.WinnerParticipantId);
                var loseLabel = new Label { Text = $"锦标赛结束。冠军: {winner?.DisplayName ?? "未知"}" };
                loseLabel.AddThemeFontSizeOverride("font_size", 16);
                container.AddChild(loseLabel);
            }

            _currentTournament = null;
        }

        // 报名按钮
        int entryFee = 1000;
        bool canEnter = _economy != null && _economy.Gold >= entryFee;
        var btn = CreateActionButton($"报名参赛 (报名费 {entryFee} 金币) — 八强淘汰赛", canEnter, "金币不足");
        btn.Pressed += () => RegisterForTournament();
        container.AddChild(btn);

        // 说明
        var infoLabel = new Label { Text = "\n📋 赛制说明:\n• 八强淘汰赛，共3轮\n• 1/4决赛: 胜利+200金\n• 半决赛: 胜利+1000金\n• 决赛: 冠军+5000金+5影响力+头衔" };
        infoLabel.AddThemeFontSizeOverride("font_size", 14);
        infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        container.AddChild(infoLabel);
    }

    private void DisplayBracket(VBoxContainer container, TournamentBracket bracket)
    {
        // 显示赛程
        var titleLabel = new Label { Text = $"⚔ {TournamentService.GetRoundName(bracket.CurrentRound)}" };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        container.AddChild(titleLabel);

        container.AddChild(new HSeparator());

        // 显示当前轮次比赛
        var matches = bracket.GetCurrentRoundMatches();
        foreach (var match in matches)
        {
            var matchHbox = new HBoxContainer();
            matchHbox.AddThemeConstantOverride("separation", 10);
            container.AddChild(matchHbox);

            string aName = match.ParticipantA?.DisplayName ?? "???";
            string bName = match.ParticipantB?.DisplayName ?? "???";

            if (match.IsResolved)
            {
                string winnerName = match.Winner?.DisplayName ?? "???";
                var resultLabel = new Label { Text = $"✅ {aName} vs {bName} — 胜者: {winnerName}" };
                resultLabel.AddThemeFontSizeOverride("font_size", 14);
                matchHbox.AddChild(resultLabel);
            }
            else if (match.IsPlayerMatch)
            {
                var playerLabel = new Label { Text = $"⚔ {aName} vs {bName} — 你的比赛！" };
                playerLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.3f));
                playerLabel.AddThemeFontSizeOverride("font_size", 14);
                matchHbox.AddChild(playerLabel);
            }
            else
            {
                var pendingLabel = new Label { Text = $"⏳ {aName} vs {bName} — 等待中..." };
                pendingLabel.AddThemeFontSizeOverride("font_size", 14);
                matchHbox.AddChild(pendingLabel);
            }
        }

        container.AddChild(new HSeparator());

        // 如果有玩家比赛，显示战斗按钮
        var playerMatch = bracket.GetPlayerMatch();
        if (playerMatch != null)
        {
            var opponent = playerMatch.ParticipantA?.IsPlayer == true ? playerMatch.ParticipantB : playerMatch.ParticipantA;
            var btn = CreateActionButton($"开始战斗 — 对阵 {opponent?.DisplayName ?? "???"}", true, "");
            btn.Pressed += () => StartTournamentFight(bracket, playerMatch);
            container.AddChild(btn);
        }
    }

    // ── 公开接口 ──

    public void ShowArena(EconomyManager economy, TournamentService? tournamentService = null, int playerLevel = 1, string hostTownName = "竞技场", string hostNationId = "", bool instantOverlay = false)
    {
        _economy = economy;
        _tournamentService = tournamentService;
        _playerLevel = playerLevel;
        _hostTownName = hostTownName;
        _hostNationId = hostNationId;
        _currentTab = "practice";
        ShowPanel(instantOverlay);
    }

    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.ArenaFinished);
    }

    // ── 逻辑 ──

    private void StartFight(int entryFee, int prize, int difficulty)
    {
        if (_economy == null || !_economy.SpendGold(entryFee))
        {
            SetResult("[color=red]金币不足，无法报名![/color]");
            return;
        }

        // 构建竞技场战斗上下文
        var ctx = BattleContext.Create(
            BladeHex.Map.HexOverworldTile.TerrainType.Plains,
            difficulty <= 2 ? BattleContext.BattleSize.Mercenary : BattleContext.BattleSize.Knight,
            BattleContext.EngagementType.Normal,
            (int)GD.Randi()
        );
        ctx.EnvironmentOverride = "arena";

        // 发射信号，由 OverworldScene3D 处理场景切换
        EmitSignal(SignalName.ArenaCombatRequested, ctx, prize);
    }

    private void RegisterForTournament()
    {
        if (_economy == null || _tournamentService == null)
        {
            SetResult("[color=red]系统错误![/color]");
            return;
        }

        if (!_economy.SpendGold(1000))
        {
            SetResult("[color=red]金币不足，无法报名![/color]");
            return;
        }

        // 创建锦标赛
        _currentTournament = _tournamentService.CreateTournament(_hostTownName, _hostNationId, _playerLevel);

        // 推进到第一轮
        _tournamentService.AdvanceAndResolve(_currentTournament);

        RefreshContent();
        SetResult("[color=green]成功报名锦标赛！准备战斗！[/color]");
    }

    private void StartTournamentFight(TournamentBracket bracket, TournamentMatch match)
    {
        if (_economy == null) return;

        var opponent = match.ParticipantA?.IsPlayer == true ? match.ParticipantB : match.ParticipantA;

        // 构建战斗上下文
        var ctx = BattleContext.Create(
            BladeHex.Map.HexOverworldTile.TerrainType.Plains,
            BattleContext.BattleSize.Knight,
            BattleContext.EngagementType.Normal,
            (int)GD.Randi()
        );
        ctx.EnvironmentOverride = "arena_tournament";

        // 保存锦标赛状态
        var state = new Godot.Collections.Dictionary
        {
            { "is_tournament", true },
            { "round", bracket.CurrentRound },
            { "opponent_name", opponent?.DisplayName ?? "???" }
        };

        // 发射信号
        EmitSignal(SignalName.TournamentCombatRequested, ctx, state);
    }

    /// <summary>
    /// 由外部调用，报告锦标赛战斗结果
    /// </summary>
    public void ReportTournamentFightResult(bool playerWon)
    {
        if (_currentTournament == null || _tournamentService == null) return;

        var playerMatch = _currentTournament.GetPlayerMatch();
        if (playerMatch == null) return;

        // 解算玩家比赛
        _tournamentService.ResolvePlayerMatch(playerMatch, playerWon);

        if (!playerWon)
        {
            // 玩家失败
            _currentTournament.IsPlayerEliminated = true;
            SetResult("[color=red]你被淘汰了！[/color]");
            RefreshContent();
            return;
        }

        // 玩家胜利，推进轮次
        _tournamentService.AdvanceAndResolve(_currentTournament);

        if (_currentTournament.IsComplete)
        {
            // 颁发奖励
            _tournamentService.AwardChampion(_currentTournament);

            if (_currentTournament.WinnerParticipantId == "player")
            {
                _economy?.AddGold(_currentTournament.GetPrizeForRound(2));
                SetResult("[color=gold]🏆 恭喜夺冠！获得5000金币和竞技场冠军头衔！[/color]");
            }
        }
        else
        {
            int prize = _currentTournament.GetPrizeForRound(_currentTournament.CurrentRound - 1);
            _economy?.AddGold(prize);
            SetResult($"[color=green]胜利！获得{prize}金币！进入下一轮！[/color]");
        }

        RefreshContent();
    }
}
