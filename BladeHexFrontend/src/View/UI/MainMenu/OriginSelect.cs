// OriginSelect.cs
// 玩家出身选择 — 两阶段：
// 阶段1：左插图+右侧"你是谁？"（种族+名字+性别）
// 阶段2：左插图+右侧问答
//
// 数据外置：问答内容、属性修正、物品奖励、插图 ID 全部来自
// res://BladeHexCore/src/Data/origin/origin_questions.json
// 加载器：BladeHex.Data.Origin.OriginQuestionLoader
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Data.Origin;
using BladeHex.Strategic;
using BladeHex.UI.Loading;

namespace BladeHex.UI;

[GlobalClass]
public partial class OriginSelect : CanvasLayer
{
    private UIFactory _factory = null!;

    // ── 数据 ──
    private List<RaceData> _allRaces = new();
    private RaceData? _selectedRace;
    private readonly Dictionary<string, int> _attrs = new()
    { ["str"] = 5, ["dex"] = 5, ["con"] = 5, ["intel"] = 5, ["wis"] = 5, ["cha"] = 5 };
    private int _currentQuestion = 0;
    private readonly List<AnswerRecord> _answers = new();
    private LineEdit _nameInput = null!;
    private ButtonGroup _genderGroup = null!;

    // ── UI ──
    private Control _phase1 = null!;
    private Control _phase2 = null!;
    private TextureRect _phase1Illust = null!;
    private TextureRect _phase2Illust = null!;
    private Label _questionText = null!;
    private VBoxContainer _choicesArea = null!;
    private Label _attrLabel = null!;
    private Label _itemLabel = null!;
    private readonly List<string> _items = new();

    private record AnswerRecord(string Question, string Choice, Dictionary<string, int> Mods);

    /// <summary>加载自 origin_questions.json 的题目列表，按种族切换时刷新。</summary>
    private List<OriginQuestion> _questions = new();

    // ── 常量 ──
    private const string IllustBase = "res://assets/generated_origin_illust/";
    private static readonly Dictionary<string, string> RaceIllust = new()
    {
        ["人类"] = "race_human",
        ["精灵"] = "race_elf",
        ["矮人"] = "race_dwarf",
        ["半兽人"] = "race_halforc",
        ["半精灵"] = "race_halfelf",
    };

    public override void _Ready()
    {
        _factory = new UIFactory();
        _allRaces = new List<RaceData>(RaceData.GetAllRaces());
        _selectedRace = _allRaces.Count > 0 ? _allRaces[0] : null;
        _RegisterRaceBgm();
        _SetupUI();
        _ShowPhase1();
        _PreloadWorldTextures();
    }

    /// <summary>注册各种族的BGM到AudioManager（Event场景的不同variant）</summary>
    private static void _RegisterRaceBgm()
    {
        var audio = BladeHex.Audio.AudioManager.Instance;
        if (audio == null) return;

        // 路径约定: res://src/assets/audio/bgm/race_{race}.ogg
        var bgms = new (RaceData.Race race, string variant, string path)[]
        {
            (RaceData.Race.Human,   "race_human",   "res://src/assets/audio/bgm/race_human.ogg"),
            (RaceData.Race.Elf,     "race_elf",     "res://src/assets/audio/bgm/race_elf.ogg"),
            (RaceData.Race.Dwarf,   "race_dwarf",   "res://src/assets/audio/bgm/race_dwarf.ogg"),
            (RaceData.Race.HalfOrc, "race_halforc", "res://src/assets/audio/bgm/race_halforc.ogg"),
            (RaceData.Race.HalfElf, "race_halfelf", "res://src/assets/audio/bgm/race_halfelf.ogg"),
        };
        foreach (var (_, variant, path) in bgms)
        {
            if (ResourceLoader.Exists(path))
                audio.AddBgmVariant(BladeHex.Audio.AudioManager.Scenario.Event, variant, path);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // UI 构建
    // ════════════════════════════════════════════════════════════════
    private void _SetupUI()
    {
        // 背景
        var bgTex = GD.Load<Texture2D>("res://assets/generated_ui_main/selected/MainMenu_Background.png");
        if (bgTex != null)
        {
            var bg = new TextureRect { Texture = bgTex, Modulate = new Color(0.45f, 0.45f, 0.45f) };
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            AddChild(bg);
        }
        else
        {
            var bg = new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f) };
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);
        }

        _phase1 = new Control();
        _phase1.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_phase1);
        _BuildPhase1();

        _phase2 = new Control();
        _phase2.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _phase2.Visible = false;
        AddChild(_phase2);
        _BuildPhase2();
    }

    private void _ShowPhase1()
    {
        _phase1.Visible = true;
        _phase2.Visible = false;
        _UpdateRaceIllust();
        _UpdateRaceBgm();
    }

    private void _ShowPhase2()
    {
        _phase1.Visible = false;
        _phase2.Visible = true;
        _currentQuestion = 0;
        _answers.Clear();
        _items.Clear();
        _attrs["str"] = 5; _attrs["dex"] = 5; _attrs["con"] = 5;
        _attrs["intel"] = 5; _attrs["wis"] = 5; _attrs["cha"] = 5;
        _BuildQuestions();
        _ShowIllust(_phase2Illust, "chaos_default");
        _RefreshQuestion();
    }

    // ════════════════════════════════════════════════════════════════
    // 阶段1：你想成为谁？（左插图 + 右种族/名字/性别）
    // ════════════════════════════════════════════════════════════════
    private void _BuildPhase1()
    {
        var margin = _FullMargin();
        _phase1.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        // 标题
        var title = _Lbl("你是谁？", 36, new Color(0.9f, 0.8f, 0.5f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 主体：左插图 + 右内容
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 24);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(body);

        // 左：插图（3:4比例，完整显示，高度撑满后宽度需 高×0.75）
        _phase1Illust = _MakeIllustRect();
        var illustPanel = _WrapIllust(_phase1Illust, 720);
        body.AddChild(illustPanel);

        // 右：选择内容
        var rightPanel = new PanelContainer();
        rightPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var rps = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.88f) };
        rps.SetBorderWidthAll(1);
        rps.BorderColor = new Color(0.3f, 0.28f, 0.22f, 0.5f);
        rps.SetCornerRadiusAll(6);
        rps.SetContentMarginAll(30);
        rightPanel.AddThemeStyleboxOverride("panel", rps);
        body.AddChild(rightPanel);

        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", 18);
        rightPanel.AddChild(rightVbox);

        // 顶部弹性空间，让表单居中偏上
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // 种族
        rightVbox.AddChild(_Lbl("选择血脉", 22, new Color(0.85f, 0.78f, 0.55f)));
        var raceRow = new HBoxContainer();
        raceRow.AddThemeConstantOverride("separation", 10);
        rightVbox.AddChild(raceRow);

        var raceGroup = new ButtonGroup();
        foreach (var race in _allRaces)
        {
            var btn = new Button { Text = race.RaceName, CustomMinimumSize = new Vector2(76, 42), ToggleMode = true };
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.ButtonGroup = raceGroup;
            if (race == _selectedRace) btn.ButtonPressed = true;
            var captured = race;
            btn.Pressed += () =>
            {
                _selectedRace = captured;
                _UpdateRaceIllust();
                // 播放种族专属 BGM
                var audio = BladeHex.Data.Globals.AudioOrNull;
                audio?.PlayRaceBgm(_GetRaceId(captured), 1.5f);
            };
            _AttachSfx(btn);
            raceRow.AddChild(btn);
        }

        // 名字
        rightVbox.AddChild(_Lbl("你的名字", 22, new Color(0.85f, 0.78f, 0.55f)));
        _nameInput = new LineEdit { PlaceholderText = "输入名字...", CustomMinimumSize = new Vector2(0, 42) };
        _nameInput.AddThemeFontSizeOverride("font_size", 20);
        rightVbox.AddChild(_nameInput);

        // 性别
        rightVbox.AddChild(_Lbl("性别", 22, new Color(0.85f, 0.78f, 0.55f)));
        var genderRow = new HBoxContainer();
        genderRow.AddThemeConstantOverride("separation", 12);
        rightVbox.AddChild(genderRow);
        _genderGroup = new ButtonGroup();
        var maleBtn = new Button { Text = "男", CustomMinimumSize = new Vector2(80, 42), ToggleMode = true, ButtonPressed = true };
        maleBtn.AddThemeFontSizeOverride("font_size", 18);
        maleBtn.ButtonGroup = _genderGroup;
        maleBtn.Pressed += _UpdateRaceIllust;
        _AttachSfx(maleBtn);
        var femaleBtn = new Button { Text = "女", CustomMinimumSize = new Vector2(80, 42), ToggleMode = true };
        femaleBtn.AddThemeFontSizeOverride("font_size", 18);
        femaleBtn.ButtonGroup = _genderGroup;
        femaleBtn.Pressed += _UpdateRaceIllust;
        _AttachSfx(femaleBtn);
        genderRow.AddChild(maleBtn);
        genderRow.AddChild(femaleBtn);

        // 中间弹性空间，把按钮栏推到底部
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // 按钮栏（左下返回 + 右下前进）
        var btnRow = new HBoxContainer();
        rightVbox.AddChild(btnRow);

        var backBtn = new Button { Text = "← 返回", CustomMinimumSize = new Vector2(140, 44) };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        backBtn.Pressed += () => BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/main_menu.tscn");
        _AttachSfx(backBtn);
        btnRow.AddChild(backBtn);

        btnRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var nextBtn = new Button { Text = "继续 →", CustomMinimumSize = new Vector2(160, 44) };
        nextBtn.AddThemeFontSizeOverride("font_size", 20);
        nextBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        nextBtn.Pressed += _ShowPhase2;
        _AttachSfx(nextBtn);
        btnRow.AddChild(nextBtn);
    }

    private void _UpdateRaceIllust()
    {
        if (_selectedRace == null || _phase1Illust == null) return;
        string key = RaceIllust.TryGetValue(_selectedRace.RaceName, out var id) ? id : "chaos_default";
        // 根据性别切换：race_human / race_human_f
        bool isFemale = (_genderGroup.GetPressedButton() as Button)?.Text == "女";
        if (isFemale) key += "_f";
        _ShowIllust(_phase1Illust, key);

        // 切换种族BGM
        _UpdateRaceBgm();
    }

    /// <summary>根据当前选中种族切换BGM</summary>
    private void _UpdateRaceBgm()
    {
        if (_selectedRace == null) return;
        var audio = BladeHex.Audio.AudioManager.Instance;
        if (audio == null) return;

        string variant = _selectedRace.raceId switch
        {
            RaceData.Race.Human => "race_human",
            RaceData.Race.Elf => "race_elf",
            RaceData.Race.Dwarf => "race_dwarf",
            RaceData.Race.HalfOrc => "race_halforc",
            RaceData.Race.HalfElf => "race_halfelf",
            _ => "default",
        };
        // 优先按变体播放Event场景BGM，找不到则保持当前BGM
        // 直接切换不交叉淡入（crossfadeTime=0）
        try
        {
            audio.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Event, variant, 0.0f);
        }
        catch { /* 没注册该变体则忽略 */ }
    }

    // ════════════════════════════════════════════════════════════════
    // 阶段2：问答（左插图 + 右对话）
    // ════════════════════════════════════════════════════════════════
    private void _BuildPhase2()
    {
        var margin = _FullMargin();
        _phase2.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        // 标题（与阶段1对齐高度）
        var title = _Lbl("书写你的过往", 36, new Color(0.9f, 0.8f, 0.5f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 主体：左插图 + 右对话
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 24);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(body);

        _phase2Illust = _MakeIllustRect();
        body.AddChild(_WrapIllust(_phase2Illust, 720));

        // 右：问题+选项
        var rightPanel = new PanelContainer();
        rightPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var rps = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.88f) };
        rps.SetBorderWidthAll(1);
        rps.BorderColor = new Color(0.3f, 0.28f, 0.22f, 0.5f);
        rps.SetCornerRadiusAll(6);
        rps.SetContentMarginAll(30);
        rightPanel.AddThemeStyleboxOverride("panel", rps);
        body.AddChild(rightPanel);

        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", 16);
        rightPanel.AddChild(rightVbox);

        // 顶部：左属性栏 + 右物品栏
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 16);
        rightVbox.AddChild(topRow);

        // 左：属性栏
        var attrPanel = new PanelContainer();
        attrPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var aps = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.09f, 0.7f) };
        aps.SetBorderWidthAll(1);
        aps.BorderColor = new Color(0.25f, 0.22f, 0.18f, 0.5f);
        aps.SetCornerRadiusAll(4);
        aps.SetContentMarginAll(10);
        attrPanel.AddThemeStyleboxOverride("panel", aps);
        topRow.AddChild(attrPanel);

        var attrVbox = new VBoxContainer();
        attrVbox.AddThemeConstantOverride("separation", 4);
        attrPanel.AddChild(attrVbox);

        var attrTitle = _Lbl("属性", 14, new Color(0.6f, 0.55f, 0.45f));
        attrTitle.AutowrapMode = TextServer.AutowrapMode.Off;
        attrVbox.AddChild(attrTitle);

        _attrLabel = _Lbl("", 16, new Color(0.85f, 0.82f, 0.7f));
        _attrLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        attrVbox.AddChild(_attrLabel);

        // 右：物品栏
        var itemPanel = new PanelContainer();
        itemPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var ips = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.09f, 0.7f) };
        ips.SetBorderWidthAll(1);
        ips.BorderColor = new Color(0.25f, 0.22f, 0.18f, 0.5f);
        ips.SetCornerRadiusAll(4);
        ips.SetContentMarginAll(10);
        itemPanel.AddThemeStyleboxOverride("panel", ips);
        topRow.AddChild(itemPanel);

        var itemVbox = new VBoxContainer();
        itemVbox.AddThemeConstantOverride("separation", 4);
        itemPanel.AddChild(itemVbox);

        var itemTitle = _Lbl("行囊", 14, new Color(0.6f, 0.55f, 0.45f));
        itemTitle.AutowrapMode = TextServer.AutowrapMode.Off;
        itemVbox.AddChild(itemTitle);

        _itemLabel = _Lbl("（空）", 14, new Color(0.65f, 0.62f, 0.55f));
        _itemLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        itemVbox.AddChild(_itemLabel);

        // 弹性间隔，把问答区往下推
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.3f });

        _questionText = new Label();
        _questionText.AddThemeFontSizeOverride("font_size", 22);
        _questionText.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.85f));
        _questionText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rightVbox.AddChild(_questionText);

        _choicesArea = new VBoxContainer();
        _choicesArea.AddThemeConstantOverride("separation", 10);
        rightVbox.AddChild(_choicesArea);

        // 弹性间隔，把按钮栏推到底部
        rightVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // 按钮栏（左下返回 + 右下踏上旅途）
        var btnRow = new HBoxContainer();
        rightVbox.AddChild(btnRow);

        var backBtn = new Button { Text = "← 重新选择", CustomMinimumSize = new Vector2(140, 44) };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        backBtn.Pressed += _ShowPhase1;
        _AttachSfx(backBtn);
        btnRow.AddChild(backBtn);

        btnRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var confirmBtn = new Button { Text = "踏上旅途 →", CustomMinimumSize = new Vector2(180, 44) };
        confirmBtn.AddThemeFontSizeOverride("font_size", 20);
        confirmBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        confirmBtn.Pressed += _OnConfirm;
        _AttachSfx(confirmBtn);
        btnRow.AddChild(confirmBtn);
    }

    // ════════════════════════════════════════════════════════════════
    // 刷新 & 交互
    // ════════════════════════════════════════════════════════════════
    private void _RefreshQuestion()
    {
        foreach (Node c in _choicesArea.GetChildren()) c.QueueFree();
        _RefreshAttr();

        if (_currentQuestion >= _questions.Count)
        {
            _questionText.Text = "你的过往已经书写完毕。\n准备好踏上旅途了吗？";
            return;
        }

        var q = _questions[_currentQuestion];
        _questionText.Text = q.Text;

        foreach (var choice in q.Choices)
        {
            var btn = new Button();
            btn.Text = choice.Text;
            btn.Alignment = HorizontalAlignment.Left;
            btn.CustomMinimumSize = new Vector2(0, 44);
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var cap = choice;
            btn.Pressed += () => _OnChoiceSelected(cap);
            _AttachSfx(btn);
            _choicesArea.AddChild(btn);
        }
    }

    private void _RefreshAttr()
    {
        if (_attrLabel != null)
            _attrLabel.Text = $"力{_attrs["str"]} 敏{_attrs["dex"]} 体{_attrs["con"]} 智{_attrs["intel"]} 感{_attrs["wis"]} 魅{_attrs["cha"]}";
        _RefreshItems();
    }

    private void _RefreshItems()
    {
        if (_itemLabel == null) return;
        _itemLabel.Text = _items.Count == 0 ? "（空）" : string.Join("\n", _items);
    }

    private void _OnChoiceSelected(OriginChoice choice)
    {
        foreach (var kv in choice.AttrMods)
            if (_attrs.ContainsKey(kv.Key))
                _attrs[kv.Key] += kv.Value;
        _answers.Add(new AnswerRecord(_questions[_currentQuestion].Text, choice.Summary, choice.AttrMods));

        // 添加选项对应的物品（从 JSON 配置直接读，不再依赖 Summary 查表）
        if (!string.IsNullOrEmpty(choice.ItemReward))
            _items.Add(choice.ItemReward);

        // 记录伙伴选择（companion 题目的选项标记 IsCompanion=true）
        if (choice.IsCompanion)
            _companionChoice = choice.Summary;

        _UpdateChoiceIllust(choice);
        _currentQuestion++;
        _RefreshQuestion();
    }

    // 选项数据（题目、属性修正、物品奖励、插图 ID）来自
    // res://BladeHexCore/src/Data/origin/origin_questions.json
    // 加载器：BladeHex.Data.Origin.OriginQuestionLoader

    private void _UpdateChoiceIllust(OriginChoice choice)
    {
        string id = choice.IllustId;
        if (string.IsNullOrEmpty(id)) return;

        // 少女伙伴根据种族切换插图
        if (id == "companion_girl" && _selectedRace != null)
        {
            string raceKey = _GetRaceId(_selectedRace);
            string raceGirlId = $"companion_girl_{raceKey}";
            string raceGirlPath = $"{IllustBase}{raceGirlId}.png";
            if (ResourceLoader.Exists(raceGirlPath))
                id = raceGirlId;
        }

        bool isFemale = (_genderGroup.GetPressedButton() as Button)?.Text == "女";
        string finalId = isFemale ? id + "_f" : id;
        // 如果女性版本不存在，回退到通用版本
        string path = $"{IllustBase}{finalId}.png";
        if (!ResourceLoader.Exists(path)) finalId = id;
        _ShowIllust(_phase2Illust, finalId);
    }

    private void _ShowIllust(TextureRect rect, string id)
    {
        if (rect == null) return;
        string path = $"{IllustBase}{id}.png";
        rect.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    // ════════════════════════════════════════════════════════════════
    // 确认
    // ════════════════════════════════════════════════════════════════
    private void _OnConfirm()
    {
        var unit = new UnitData();
        unit.UnitName = string.IsNullOrEmpty(_nameInput.Text) ? "无名旅人" : _nameInput.Text;
        unit.Race = _selectedRace;
        unit.Level = 1;
        unit.Str = _attrs["str"]; unit.Dex = _attrs["dex"]; unit.Con = _attrs["con"];
        unit.Intel = _attrs["intel"]; unit.Wis = _attrs["wis"]; unit.Cha = _attrs["cha"];

        var gs = BladeHex.Data.Globals.State;
        gs.Save.IsLoadingSave = false;
        gs.WorldGen.Size = 1;
        gs.OriginContext.Data = new Godot.Collections.Dictionary
        {
            { "race", _selectedRace! },
            { "unit_data", unit },
            { "gender", (_genderGroup.GetPressedButton() as Button)?.Text == "女" ? "female" : "male" },
            { "companion", _companionChoice },
            { "items", _items.ToArray() },
        };
        LoadingScreen.LoadScene("res://src/scenes/overworld/overworld_scene_3d.tscn", LoadingScreen.PhaseType.NewWorld);
    }

    // ════════════════════════════════════════════════════════════════
    // 问题生成（从 JSON 加载）
    // ════════════════════════════════════════════════════════════════
    private string _companionChoice = "";

    private void _BuildQuestions()
    {
        _questions.Clear();
        if (_selectedRace == null) return;

        var data = OriginQuestionLoader.Load();
        string raceKey = _selectedRace.raceId.ToString();

        if (data.Races.TryGetValue(raceKey, out var raceQuestions))
            _questions.AddRange(raceQuestions);
        else
            GD.PushWarning($"[OriginSelect] origin_questions.json 缺少种族 '{raceKey}' 的题目");

        // 所有种族共用的最终问题：忠实伙伴
        if (data.CompanionQuestion?.Choices?.Count > 0)
            _questions.Add(data.CompanionQuestion);
    }

    // ════════════════════════════════════════════════════════════════
    // 工具方法
    // ════════════════════════════════════════════════════════════════
    private static string _GetRaceId(RaceData? race)
    {
        if (race == null) return "human";
        return race.raceId switch
        {
            RaceData.Race.Human => "human",
            RaceData.Race.Elf => "elf",
            RaceData.Race.Dwarf => "dwarf",
            RaceData.Race.HalfOrc => "halforc",
            RaceData.Race.HalfElf => "halfelf",
            _ => "human",
        };
    }

    private static Label _Lbl(string text, int size, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    /// <summary>给按钮挂上UI点击/悬停音效</summary>
    private static void _AttachSfx(BaseButton btn)
    {
        btn.Pressed += () => BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_click");
        btn.MouseEntered += () => BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_hover", -6.0f);
    }

    private static MarginContainer _FullMargin()
    {
        var m = new MarginContainer();
        m.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        m.AddThemeConstantOverride("margin_left", 50);
        m.AddThemeConstantOverride("margin_right", 50);
        m.AddThemeConstantOverride("margin_top", 30);
        m.AddThemeConstantOverride("margin_bottom", 30);
        return m;
    }

    private static TextureRect _MakeIllustRect()
    {
        var rect = new TextureRect();
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        rect.StretchMode = TextureRect.StretchModeEnum.KeepAspect; // 完整显示，不裁切
        rect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rect.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return rect;
    }

    private static PanelContainer _WrapIllust(TextureRect rect, int minWidth)
    {
        var panel = new PanelContainer();
        // 高度由父HBox决定，宽度按比例（高度 × 3/4 = 宽度）。设置足够大的最小宽度避免压缩。
        panel.CustomMinimumSize = new Vector2(minWidth, 0);
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        var ps = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.06f, 0.9f) };
        ps.SetBorderWidthAll(2);
        ps.BorderColor = new Color(0.35f, 0.3f, 0.22f, 0.7f);
        ps.SetCornerRadiusAll(6);
        ps.SetContentMarginAll(4);
        panel.AddThemeStyleboxOverride("panel", ps);
        panel.AddChild(rect);
        return panel;
    }

    // ════════════════════════════════════════════════════════════════
    // 预加载
    // ════════════════════════════════════════════════════════════════
    private static readonly string[] PreloadPaths = {
        "res://src/assets/tiles/overworld/plains_0.png",
        "res://src/assets/tiles/overworld/grassland_0.png",
        "res://src/assets/tiles/overworld/forest_0.png",
        "res://src/assets/tiles/overworld/hills_0.png",
        "res://src/assets/tiles/overworld/mountain_0.png",
        "res://src/assets/tiles/overworld/deep_water_0.png",
        "res://src/scenes/overworld/overworld_scene_3d.tscn",
    };
    private void _PreloadWorldTextures()
    {
        foreach (var p in PreloadPaths)
            if (ResourceLoader.Exists(p))
                ResourceLoader.LoadThreadedRequest(p);
    }
}
