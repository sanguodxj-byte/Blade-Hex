using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// NPC档案数据类 — 描述大地图上人形NPC的完整信息
/// </summary>
[GlobalClass]
public partial class NPCProfile : Resource
{
    public enum NpcType
    {
        Adventurer,        // 冒险者
        Merchant,          // 商队
        Traveler,          // 旅行者
        WanderingKnight,  // 流浪骑士
        BountyTarget,     // 通缉犯
        HostileHumanoid,  // 敌对人形生物
        LordArmy,         // 领主军队
        BanditParty,      // 山贼
        RobberParty,      // 劫匪
        PirateCrew,       // 海寇
        RaidingParty,     // 掠夺队
    }

    public enum Attitude
    {
        Friendly,   // 友好
        Neutral,    // 中立
        Cold,       // 冷漠
        Hostile,    // 敌意
    }

    [Export] public string npcName { get; set; } = "无名旅人";
    [Export] public NpcType npcType = NpcType.Traveler;
    [Export] public Attitude attitude = Attitude.Neutral;
    [Export] public string faction { get; set; } = "";
    [Export] public string race { get; set; } = "Human"; // NPC 所属种族（Human/Orc/Elf/Undead/…）
    [Export] public int armySize { get; set; } = 10;      // NPC 麾下兵力
    [Export] public int relation { get; set; } = 0;        // 与玩家的关系值 -100~100
    [Export] public string dialogueIntro { get; set; } = "";
    [Export] public Godot.Collections.Dictionary dialogueLines = new(); // 对话节点 key->DialogueEntry
    [Export] public int gold { get; set; } = 50;
    [Export] public int recruitCost { get; set; } = 100; // 招募费用

    public void AddRelation(int amount)
    {
        relation = Math.Clamp(relation + amount, -100, 100);
        if (relation >= 25) attitude = Attitude.Friendly;
        else if (relation <= -50) attitude = Attitude.Hostile;
        else if (relation < 0) attitude = Attitude.Cold;
        else attitude = Attitude.Neutral;
    }

    public static string GetNpcTypeName(NpcType type) => type switch
    {
        NpcType.Adventurer => "冒险者",
        NpcType.Merchant => "商队",
        NpcType.Traveler => "旅行者",
        NpcType.WanderingKnight => "流浪骑士",
        NpcType.BountyTarget => "通缉犯",
        NpcType.HostileHumanoid => "敌对者",
        NpcType.LordArmy => "领主军队",
        NpcType.BanditParty => "山贼",
        NpcType.RobberParty => "劫匪",
        NpcType.PirateCrew => "海寇",
        NpcType.RaidingParty => "掠夺队",
        _ => "未知"
    };

    public string GetNpcTypeNameForType(int type) => GetNpcTypeName((NpcType)type);

    public string GetAttitudeText() => attitude switch
    {
        Attitude.Friendly => "友好",
        Attitude.Neutral => "中立",
        Attitude.Cold => "冷漠",
        Attitude.Hostile => "敌意",
        _ => "未知"
    };

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(dialogueIntro)) return dialogueIntro;

        string typeName = GetNpcTypeName(npcType);
        string attitudeText = GetAttitudeText();

        return npcType switch
        {
            NpcType.Adventurer => $"一支{typeName}的冒险者队伍，态度{attitudeText}。",
            NpcType.Merchant => $"一支{typeName}，看起来有不少货物。态度{attitudeText}。",
            NpcType.Traveler => $"一位{typeName}，似乎在赶路。态度{attitudeText}。",
            NpcType.WanderingKnight => $"一位{typeName}，装备精良，神情傲然。态度{attitudeText}。",
            NpcType.BountyTarget => $"一名{typeName}，悬赏金额不菲。",
            NpcType.HostileHumanoid => $"一群{typeName}，看起来不太友善。",
            _ => $"一个{typeName}。"
        };
    }

    public Godot.Collections.Dictionary GetDefaultDialogue()
    {
        return npcType switch
        {
            NpcType.Adventurer => new Godot.Collections.Dictionary
            {
                ["greeting"] = "你好，旅人。我们是冒险者，正在寻找任务委托。",
                ["options"] = new Godot.Collections.Array { "你们愿意加入我的队伍吗？", "有什么消息吗？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "加入队伍？嗯，报酬合适的话可以考虑。", "听说北边的矿坑里有怪物出没，可能有好东西。", "后会有期。" }
            },
            NpcType.Merchant => new Godot.Collections.Dictionary
            {
                ["greeting"] = "欢迎！看看我们的商品吧，价格公道。",
                ["options"] = new Godot.Collections.Array { "让我看看你的货物。", "需要护送吗？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "当然，请随意挑选。", "如果你能护送我们到下一个城镇，我们会付你报酬。", "慢走，欢迎下次光临。" }
            },
            NpcType.Traveler => new Godot.Collections.Dictionary
            {
                ["greeting"] = "哦，你好。我只是在赶路而已。",
                ["options"] = new Godot.Collections.Array { "附近有什么有趣的地方吗？", "你从哪里来？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "东边的森林里据说有古老的遗迹，不过很危险。", "我从南方的城镇来，那里最近不太太平。", "再见，祝旅途平安。" }
            },
            _ => new Godot.Collections.Dictionary
            {
                ["greeting"] = "……",
                ["options"] = new Godot.Collections.Array { "告辞。" },
                ["responses"] = new Godot.Collections.Array { "……" }
            }
        };
    }

    /// <summary>
    /// 加载/生成该 NPC 的完整对话树。
    /// 支持新格式：节点内 "texts" 为条件文本列表（ConditionalText），也向下兼容旧 "text" 单一字段。
    /// </summary>
    public Dictionary<string, DialogueNode> LoadDialogueTree()
    {
        var tree = new Dictionary<string, DialogueNode>();
        bool parsedCustom = false;

        if (dialogueLines != null && dialogueLines.Count > 0 && dialogueLines.ContainsKey("start"))
        {
            try
            {
                foreach (var key in dialogueLines.Keys)
                {
                    string nodeId = key.AsString();
                    var nodeVal = dialogueLines[key];
                    if (nodeVal.Obj is Godot.Collections.Dictionary nodeDict)
                    {
                        var node = new DialogueNode { NodeId = nodeId };

                        // ── 新格式：texts 条件文本列表 ─────────────────────────
                        if (nodeDict.ContainsKey("texts") && nodeDict["texts"].Obj is Godot.Collections.Array textsArray)
                        {
                            foreach (var tv in textsArray)
                            {
                                if (tv.Obj is Godot.Collections.Dictionary td)
                                {
                                    var ct = new ConditionalText();
                                    if (td.ContainsKey("condition")) ct.Condition = td["condition"].AsString();
                                    if (td.ContainsKey("text"))      ct.Text      = td["text"].AsString();
                                    node.Texts.Add(ct);
                                }
                            }
                        }
                        // ── 旧格式：单一 text 字段（向下兼容）────────────────
                        else if (nodeDict.ContainsKey("text"))
                        {
                            node.Text = nodeDict["text"].AsString();
                        }

                        // ── 选项列表（两种格式共用）───────────────────────────
                        if (nodeDict.ContainsKey("options") && nodeDict["options"].Obj is Godot.Collections.Array optArray)
                        {
                            foreach (var optVal in optArray)
                            {
                                if (optVal.Obj is Godot.Collections.Dictionary optDict)
                                {
                                    var opt = new DialogueOption();
                                    if (optDict.ContainsKey("text"))            opt.Text           = optDict["text"].AsString();
                                    if (optDict.ContainsKey("next"))            opt.NextNodeId     = optDict["next"].AsString();
                                    if (optDict.ContainsKey("condition"))       opt.Condition      = optDict["condition"].AsString();
                                    if (optDict.ContainsKey("condition_param")) opt.ConditionParam = optDict["condition_param"].AsString();
                                    if (optDict.ContainsKey("action"))          opt.Action         = optDict["action"].AsString();
                                    if (optDict.ContainsKey("action_param"))    opt.ActionParam    = optDict["action_param"].AsString();
                                    node.Options.Add(opt);
                                }
                            }
                        }
                        tree[nodeId] = node;
                    }
                }
                if (tree.Count > 0) parsedCustom = true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[NPCProfile] 解析自定义对话树失败，退回到默认对话树: {ex.Message}");
                tree.Clear();
            }
        }

        if (!parsedCustom)
        {
            parsedCustom = TryLoadDialogueTreeFromJson(tree);
        }

        if (!parsedCustom)
            GenerateDefaultDialogueTree(tree);

        return tree;
    }

    private bool TryLoadDialogueTreeFromJson(Dictionary<string, DialogueNode> tree)
    {
        string filename = npcType switch
        {
            NpcType.Adventurer => "dialogue_adventurer.json",
            NpcType.Merchant => "dialogue_merchant.json",
            NpcType.WanderingKnight => "dialogue_wandering_knight.json",
            NpcType.BountyTarget => "dialogue_bounty_target.json",
            NpcType.HostileHumanoid => "dialogue_hostile_humanoid.json",
            NpcType.LordArmy => "dialogue_lord_army.json",
            NpcType.BanditParty => "dialogue_bandit_party.json",
            NpcType.RobberParty => "dialogue_robber_party.json",
            NpcType.PirateCrew => "dialogue_pirate_crew.json",
            NpcType.RaidingParty => "dialogue_raiding_party.json",
            _ => "dialogue_traveler.json"
        };

        string path = $"res://BladeHexCore/src/Interaction/dialogues/{filename}";
        if (!FileAccess.FileExists(path))
        {
            return false;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return false;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PrintErr($"[NPCProfile] 解析 JSON 对话文件失败 {path}: {json.GetErrorMessage()}");
            return false;
        }

        if (json.Data.VariantType != Variant.Type.Dictionary) return false;
        var rootDict = json.Data.AsGodotDictionary();

        if (!rootDict.ContainsKey("nodes") || rootDict["nodes"].VariantType != Variant.Type.Dictionary) return false;
        var nodesDict = rootDict["nodes"].AsGodotDictionary();

        try
        {
            foreach (var key in nodesDict.Keys)
            {
                string nodeId = key.AsString();
                var nodeVal = nodesDict[key];
                
                Godot.Collections.Dictionary? nodeDict = null;
                if (nodeVal.VariantType == Variant.Type.Dictionary)
                {
                    nodeDict = nodeVal.AsGodotDictionary();
                }
                else if (nodeVal.Obj is Godot.Collections.Dictionary gd)
                {
                    nodeDict = gd;
                }

                if (nodeDict != null)
                {
                    var node = new DialogueNode { NodeId = nodeId };

                    // ── texts 条件文本列表 ─────────────────────────
                    if (nodeDict.ContainsKey("texts"))
                    {
                        var textsVal = nodeDict["texts"];
                        Godot.Collections.Array? textsArray = null;
                        if (textsVal.VariantType == Variant.Type.Array)
                            textsArray = textsVal.AsGodotArray();
                        else if (textsVal.Obj is Godot.Collections.Array ga)
                            textsArray = ga;

                        if (textsArray != null)
                        {
                            foreach (var tv in textsArray)
                            {
                                Godot.Collections.Dictionary? td = null;
                                if (tv.VariantType == Variant.Type.Dictionary)
                                    td = tv.AsGodotDictionary();
                                else if (tv.Obj is Godot.Collections.Dictionary gd)
                                    td = gd;

                                if (td != null)
                                {
                                    var ct = new ConditionalText();
                                    if (td.ContainsKey("condition")) ct.Condition = td["condition"].AsString();
                                    if (td.ContainsKey("text"))      ct.Text      = td["text"].AsString();
                                    node.Texts.Add(ct);
                                }
                            }
                        }
                    }
                    // ── 旧/单一 text 字段（向下兼容）────────────────
                    else if (nodeDict.ContainsKey("text"))
                    {
                        node.Text = nodeDict["text"].AsString();
                    }

                    // ── 选项列表 ───────────────────────────
                    if (nodeDict.ContainsKey("options"))
                    {
                        var optVal = nodeDict["options"];
                        Godot.Collections.Array? optArray = null;
                        if (optVal.VariantType == Variant.Type.Array)
                            optArray = optVal.AsGodotArray();
                        else if (optVal.Obj is Godot.Collections.Array ga)
                            optArray = ga;

                        if (optArray != null)
                        {
                            foreach (var ov in optArray)
                            {
                                Godot.Collections.Dictionary? optDict = null;
                                if (ov.VariantType == Variant.Type.Dictionary)
                                    optDict = ov.AsGodotDictionary();
                                else if (ov.Obj is Godot.Collections.Dictionary gd)
                                    optDict = gd;

                                if (optDict != null)
                                {
                                    var opt = new DialogueOption();
                                    if (optDict.ContainsKey("text"))            opt.Text           = optDict["text"].AsString();
                                    if (optDict.ContainsKey("next"))            opt.NextNodeId     = optDict["next"].AsString();
                                    if (optDict.ContainsKey("condition"))       opt.Condition      = optDict["condition"].AsString();
                                    if (optDict.ContainsKey("condition_param")) opt.ConditionParam = optDict["condition_param"].AsString();
                                    if (optDict.ContainsKey("action"))          opt.Action         = optDict["action"].AsString();
                                    if (optDict.ContainsKey("action_param"))    opt.ActionParam    = optDict["action_param"].AsString();
                                    node.Options.Add(opt);
                                }
                            }
                        }
                    }
                    tree[nodeId] = node;
                }
            }
            return tree.Count > 0;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NPCProfile] 解析 JSON 对话数据失败 {path}: {ex.Message}");
            tree.Clear();
            return false;
        }
    }


    private void GenerateDefaultDialogueTree(Dictionary<string, DialogueNode> tree)
    {
        switch (npcType)
        {
            case NpcType.HostileHumanoid:
                // 敌对者/强盗 (被袭击)
                tree["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "站住！把钱留下，或者把命留下！不想死的话就快点交过路费！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "想拿我的钱？做梦！(正面迎击来犯之敌)", NextNodeId = "end", Action = "combat" },
                        new() { Text = "这是过路费。(交纳 150 金币)", NextNodeId = "bribe_success", Condition = "has_gold", ConditionParam = "150", Action = "lose_gold;add_relation;add_faction_rep", ActionParam = "150;10;5" },
                        new() { Text = "我投降！(缴械认输)", NextNodeId = "surrender_end", Action = "surrender" }
                    }
                };
                tree["bribe_success"] = new DialogueNode
                {
                    NodeId = "bribe_success",
                    Text = "很好，算你识相。拿了你的钱，今天就放你一马。滚吧，别让我再看见你！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "告辞。(离开)", NextNodeId = "end" }
                    }
                };
                tree["surrender_end"] = new DialogueNode
                {
                    NodeId = "surrender_end",
                    Text = "哈哈！算你聪明，全部丢下武器，老老实实当我们的战利品吧！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "唉……(顺从命运)", NextNodeId = "end" }
                    }
                };
                break;

            case NpcType.Merchant:
                // 商队 (交易与闲聊)
                tree["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "你好，旅人！我们是行商队伍，带了不少好东西。你想看看吗？",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "让我看看你的货物。(打开交易面板)", NextNodeId = "end", Action = "trade" },
                        new() { Text = "有什么新闻吗？(闲聊)", NextNodeId = "chat_news", Action = "add_relation", ActionParam = "2" },
                        new() { Text = "你们需要护送吗？", NextNodeId = "escort_decline" },
                        new() { Text = "告辞。(离开)", NextNodeId = "end" }
                    }
                };
                tree["chat_news"] = new DialogueNode
                {
                    NodeId = "chat_news",
                    Text = "听说这附近有些土匪在游荡，行商和旅人都得小心。另外，北方的城镇正在举行锦标赛，据说奖励很丰厚！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "多谢提醒，我们再谈谈别的。", NextNodeId = "start" },
                        new() { Text = "告辞。(离开)", NextNodeId = "end" }
                    }
                };
                tree["escort_decline"] = new DialogueNode
                {
                    NodeId = "escort_decline",
                    Text = "多谢好意。不过我们已经雇佣了足够强大的护卫。如果你有兴趣，可以看看我们的商品。",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "好吧，让我看看你的商品。(交易)", NextNodeId = "end", Action = "trade" },
                        new() { Text = "那告辞了。", NextNodeId = "end" }
                    }
                };
                break;

            case NpcType.Adventurer:
                // 冒险者 (招募与切磋)
                tree["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "你好！我们是四处接受委托的冒险者。有什么我们可以帮你的吗？",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "你们愿意加入我的队伍吗？(招募)", NextNodeId = "recruit_ask" },
                        new() { Text = "附近有什么值得注意的事情吗？(打听消息)", NextNodeId = "chat_info" },
                        new() { Text = "告辞。(离开)", NextNodeId = "end" }
                    }
                };
                tree["recruit_ask"] = new DialogueNode
                {
                    NodeId = "recruit_ask",
                    Text = $"我们接受招募！不过，我们需要收取一点定金作为装备整顿的费用。一共需要 {recruitCost} 金币，你愿意支付吗？",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = $"没问题，这是给你们的费用。(支付 {recruitCost} 金币)", NextNodeId = "recruit_success", Condition = "has_gold", ConditionParam = recruitCost.ToString(), Action = "lose_gold;recruit", ActionParam = $"{recruitCost};0" },
                        new() { Text = "这太贵了，我付不起。", NextNodeId = "recruit_fail" }
                    }
                };
                tree["recruit_success"] = new DialogueNode
                {
                    NodeId = "recruit_success",
                    Text = "太好了，队长！兄弟们，收拾行李，我们现在就编入主队！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "欢迎加入！(结束)", NextNodeId = "end" }
                    }
                };
                tree["recruit_fail"] = new DialogueNode
                {
                    NodeId = "recruit_fail",
                    Text = "好吧，要是之后您改主意了或者资金充足了，随时都可以来这里找我们。",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "好的，我们换个话题。", NextNodeId = "start" },
                        new() { Text = "告辞。(离开)", NextNodeId = "end" }
                    }
                };
                tree["chat_info"] = new DialogueNode
                {
                    NodeId = "chat_info",
                    Text = "听行商说，东边的遗迹深处好像出现了罕见的古代遗骸，也许隐藏着未知的宝藏，不过里面非常凶险。",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "多谢分享，我知道了。", NextNodeId = "start", Action = "add_relation", ActionParam = "1" },
                        new() { Text = "告辞。(离开)", NextNodeId = "end" }
                    }
                };
                break;

            case NpcType.WanderingKnight:
                // 流浪骑士 (切磋与招募)
                tree["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "我是四处流浪的骑士。我只尊崇力量、或者名扬天下的领主。你找我有何贵干？",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "我想和你切磋一番，证明我的实力！(切磋)", NextNodeId = "duel_confirm" },
                        new() { Text = "你愿意加入我的麾下，共同开创霸业吗？(尝试招募)", NextNodeId = "knight_recruit_check" },
                        new() { Text = "打扰了，告辞。", NextNodeId = "end" }
                    }
                };
                tree["duel_confirm"] = new DialogueNode
                {
                    NodeId = "duel_confirm",
                    Text = "切磋？有意思。你看起来是个有勇气的战士。但与我交手需有赌注。若你赢了，我自会钦佩你的实力；若你输了，需留下 50 金币作为赔礼，你敢接受挑战吗？",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "来吧！手底下见真章。(开始战斗)", NextNodeId = "end", Action = "combat" },
                        new() { Text = "我还是算了。(退缩)", NextNodeId = "start", Action = "add_relation", ActionParam = "-2" }
                    }
                };
                tree["knight_recruit_check"] = new DialogueNode
                {
                    NodeId = "knight_recruit_check",
                    Text = "你想招募我？(评估实力中...)",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "(询问评估结果)", NextNodeId = "knight_recruit_ok", Condition = "relation_ge", ConditionParam = "10" },
                        new() { Text = "(询问评估结果)", NextNodeId = "knight_recruit_reject", Condition = "relation_le", ConditionParam = "9" }
                    }
                };
                tree["knight_recruit_ok"] = new DialogueNode
                {
                    NodeId = "knight_recruit_ok",
                    Text = $"你我意气相投，且你证明了你的勇气。我愿意宣誓效忠于你，成为你的佩剑！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "很好！欢迎效忠。(招募)", NextNodeId = "end", Action = "recruit" }
                    }
                };
                tree["knight_recruit_reject"] = new DialogueNode
                {
                    NodeId = "knight_recruit_reject",
                    Text = "哼，你目前的实力和名声，还不足以让我为你效力。等你的剑磨得更利、或者你我交情更深时再提此事吧！",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "好吧，我会证明给你看的。", NextNodeId = "start" }
                    }
                };
                break;

            default:
                // 默认旅人/普通NPC (问路和闲聊)
                tree["start"] = new DialogueNode
                {
                    NodeId = "start",
                    Text = "哦，你好。我只是一位过路的旅人。有什么我可以帮你的吗？",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "附近有什么有趣的地方吗？", NextNodeId = "places" },
                        new() { Text = "你从哪里来？", NextNodeId = "origin" },
                        new() { Text = "再见，祝旅途平安。", NextNodeId = "end", Action = "add_relation", ActionParam = "1" }
                    }
                };
                tree["places"] = new DialogueNode
                {
                    NodeId = "places",
                    Text = "我听说东边的森林里有一些古老的遗迹废墟，里面经常会有怪兽出没。如果您是冒险者，倒可以去那里碰碰运气。",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "多谢告知，我们聊聊别的。", NextNodeId = "start" },
                        new() { Text = "多谢，告辞了。", NextNodeId = "end" }
                    }
                };
                tree["origin"] = new DialogueNode
                {
                    NodeId = "origin",
                    Text = "我从南方的商贸城镇来，那里最近在大规模收购矿石和谷物，很多行商都在往那边赶。",
                    Options = new List<DialogueOption>
                    {
                        new() { Text = "原来如此，我们聊点别的。", NextNodeId = "start" },
                        new() { Text = "多谢你的消息，再见。", NextNodeId = "end" }
                    }
                };
                break;
        }
    }
}
