using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 对话树状态机执行器 — 处理对话节点推进、条件判断与动作触发 (无 UI 依赖)
///
/// 改进版本：
/// - 注入 DialogueContext 携带兵力/种族/好感/声望动态上下文
/// - 使用 DialogueConditionEvaluator 解析 JSON 中的表达式条件
/// - GetCurrentText() 从节点的 Texts 条件列表中按优先级匹配文本
/// - FormatText() 支持全套 {占位符} 替换
/// </summary>
public class DialogueRunner
{
    private readonly NPCProfile _profile;
    private readonly ReputationTracker? _reputationTracker;
    private readonly IEconomyProvider? _economy;
    private readonly Dictionary<string, DialogueNode> _tree;
    private DialogueNode _currentNode;

    // 可在 ShowDialogue 之前或之后动态设置上下文
    public DialogueContext Context { get; set; }

    // ========================================
    // 动作回调事件
    // ========================================
    public event Action<string>? TradeRequested;
    public event Action? CombatRequested;
    public event Action? RecruitSuccessRequested;
    public event Action? SurrenderRequested;
    public event Action? DialogueEnded;

    public DialogueNode CurrentNode => _currentNode;
    public NPCProfile Profile => _profile;

    public DialogueRunner(
        NPCProfile profile,
        ReputationTracker? reputationTracker,
        IEconomyProvider? economy,
        DialogueContext? context = null)
    {
        _profile            = profile;
        _reputationTracker  = reputationTracker;
        _economy            = economy;

        // 若调用方不传 context，则根据 profile 构建基础 context
        Context = context ?? BuildDefaultContext(profile, economy, reputationTracker);

        _tree = profile.LoadDialogueTree();
        _currentNode = _tree.ContainsKey("start") ? _tree["start"] : new DialogueNode();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 静态工厂：从 NPCProfile + 系统接口构建基础 DialogueContext
    // ─────────────────────────────────────────────────────────────────────────────

    public static DialogueContext BuildDefaultContext(
        NPCProfile profile,
        IEconomyProvider? economy,
        ReputationTracker? repTracker)
    {
        int factionRep = 0;
        if (repTracker != null && !string.IsNullOrEmpty(profile.faction))
            factionRep = repTracker.GetReputation(profile.faction);

        return new DialogueContext
        {
            PlayerArmySize          = 1,   // 前端在调用 ShowDialogue 时会根据实际兵力覆盖
            NpcArmySize             = profile.armySize,
            NpcRace                 = profile.race,
            NpcFaction              = profile.faction ?? "",
            PlayerNpcRelation       = profile.relation,
            PlayerFactionReputation = factionRep,
            PlayerGold              = economy?.Gold ?? 0,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 当前节点文本解析（条件列表匹配）
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 从当前节点解析应显示的文本：
    /// 1. 若节点有 Texts 条件列表，则按顺序找到第一个满足 Context 条件的文本。
    /// 2. 否则降级使用 Node.Text 兜底（旧内联格式）。
    /// 最终通过 FormatText() 替换 {占位符}。
    /// </summary>
    public string GetCurrentText()
    {
        if (_currentNode == null) return "";

        // 条件文本列表优先
        if (_currentNode.Texts != null && _currentNode.Texts.Count > 0)
        {
            foreach (var ct in _currentNode.Texts)
            {
                if (DialogueConditionEvaluator.Evaluate(ct.Condition, Context))
                    return FormatText(ct.Text);
            }
        }

        // 兜底: 旧格式单一文本
        return FormatText(_currentNode.Text);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 占位符格式化
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 格式化含有占位符的文本，将 {token} 替换为对应的动态值。
    /// </summary>
    public string FormatText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        int dynamicBribe = CalculateDynamicBribe();

        return text
            // NPC 相关
            .Replace("{name}",          _profile.npcName)
            .Replace("{npc_name}",      _profile.npcName)
            .Replace("{faction}",       string.IsNullOrEmpty(_profile.faction) ? "无阵营" : _profile.faction)
            .Replace("{npc_race}",      _profile.race)
            .Replace("{npc_army_size}", Context.NpcArmySize.ToString())
            // 玩家相关
            .Replace("{player_army_size}", Context.PlayerArmySize.ToString())
            .Replace("{player_race}",      Context.PlayerRace)
            .Replace("{player_gold}",      Context.PlayerGold.ToString())
            // 关系与声望
            .Replace("{relation}",      Context.PlayerNpcRelation.ToString())
            .Replace("{faction_rep}",   Context.PlayerFactionReputation.ToString())
            // 经济
            .Replace("{cost}",          _profile.recruitCost.ToString())
            .Replace("{recruit_cost}",  _profile.recruitCost.ToString())
            .Replace("{bribe_cost}",    dynamicBribe.ToString());
    }

    private int CalculateDynamicBribe()
    {
        // 过路费根据双方兵力差距动态调整，最低 50 金币
        return Math.Max(50, Context.NpcArmySize * 15 - Context.PlayerArmySize * 5);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 选项条件检查
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 检查某选项的条件是否满足（同时支持旧 key:param 格式与新表达式格式）。
    /// </summary>
    public bool CheckCondition(DialogueOption option)
    {
        if (string.IsNullOrEmpty(option.Condition)) return true;

        // 新格式：包含运算符，交给表达式评估器
        if (option.Condition.Contains(">=") || option.Condition.Contains("<=") ||
            option.Condition.Contains("==") || option.Condition.Contains("!=") ||
            option.Condition.Contains(">")  || option.Condition.Contains("<"))
        {
            return DialogueConditionEvaluator.Evaluate(option.Condition, Context);
        }

        // 旧格式：逗号分隔的 condition + condition_param 列表
        string[] conditions = option.Condition.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        string[] paramsList = (option.ConditionParam ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < conditions.Length; i++)
        {
            string cond  = conditions[i].Trim().ToLower();
            string param = i < paramsList.Length ? paramsList[i].Trim() : "";
            if (!CheckLegacyCondition(cond, param))
                return false;
        }
        return true;
    }

    private bool CheckLegacyCondition(string cond, string param)
    {
        switch (cond)
        {
            case "has_gold":
                return int.TryParse(param, out int goldNeeded) && _economy != null && _economy.Gold >= goldNeeded;

            case "relation_ge":
                return int.TryParse(param, out int relGe) && _profile.relation >= relGe;

            case "relation_le":
                return int.TryParse(param, out int relLe) && _profile.relation <= relLe;

            case "faction_rep_ge":
                return int.TryParse(param, out int repGe)
                       && _reputationTracker != null
                       && !string.IsNullOrEmpty(_profile.faction)
                       && _reputationTracker.GetReputation(_profile.faction) >= repGe;
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 动作执行
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 执行该选项对应的各种游戏动作（支持多动作，用分号/逗号分隔）
    /// </summary>
    public void ExecuteAction(DialogueOption option)
    {
        if (string.IsNullOrEmpty(option.Action)) return;

        string[] actions   = option.Action.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        string[] paramsList = (option.ActionParam ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < actions.Length; i++)
        {
            string act   = actions[i].Trim().ToLower();
            string param = i < paramsList.Length ? paramsList[i].Trim() : "";
            ExecuteSingleAction(act, param);
        }
    }

    private void ExecuteSingleAction(string action, string param)
    {
        param = FormatText(param);
        switch (action)
        {
            case "lose_gold":
                if (int.TryParse(param, out int goldCost) && _economy != null)
                    _economy.SpendGold(goldCost);
                break;
 
            case "gain_gold":
                if (int.TryParse(param, out int goldGain) && _economy != null)
                    _economy.AddGold(goldGain);
                break;

            case "add_relation":
                if (int.TryParse(param, out int relDelta))
                {
                    _profile.AddRelation(relDelta);
                    Context.PlayerNpcRelation = _profile.relation; // 同步回 context
                }
                break;

            case "add_faction_rep":
                if (int.TryParse(param, out int repDelta)
                    && _reputationTracker != null
                    && !string.IsNullOrEmpty(_profile.faction))
                {
                    _reputationTracker.AddReputation(_profile.faction, repDelta);
                }
                break;

            case "trade":
                TradeRequested?.Invoke(_profile.npcName);
                break;

            case "combat":
                CombatRequested?.Invoke();
                break;

            case "recruit":
                RecruitSuccessRequested?.Invoke();
                break;

            case "surrender":
                SurrenderRequested?.Invoke();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 状态机推进
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 选择一个分支并推进状态机
    /// </summary>
    public void SelectOption(DialogueOption option)
    {
        ExecuteAction(option);

        string next = (option.NextNodeId ?? "").Trim().ToLower();
        if (next == "end" || string.IsNullOrEmpty(next))
        {
            DialogueEnded?.Invoke();
        }
        else if (_tree.TryGetValue(option.NextNodeId, out var nextNode))
        {
            _currentNode = nextNode;
        }
        else
        {
            GD.PrintErr($"[DialogueRunner] 找不到节点: '{option.NextNodeId}'，对话结束。");
            DialogueEnded?.Invoke();
        }
    }

    /// <summary>
    /// 结束对话，触发 DialogueEnded 事件
    /// </summary>
    public void EndDialogue()
    {
        DialogueEnded?.Invoke();
    }
}
