using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Encyclopedia;
using BladeHex.View.UI.Overworld;
using BladeHex.UI.Common;
using BladeHex.UI.Tutorial;

namespace BladeHex.View.UI.Encyclopedia;

/// <summary>
/// 世界百科全书主面板
/// </summary>
public partial class EncyclopediaIndexPanel : PanelContainer
{
    private static readonly Color BgPanel = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);

    private OverworldEntityManager _entityMgr = null!;
    private string _currentTab = "heroes"; // heroes, families, factions, items, pois, bestiary, guide
    private string _searchFilter = "";
    private BestiaryDataRoot? _bestiaryData;
    private TutorialDataRoot? _tutorialData;
    private DiscoveryJournal? _journal;
    private TutorialDetailPanel? _guideTooltip;

    private LineEdit _searchBar = null!;
    private VBoxContainer _listContainer = null!;
    private Label _titleLabel = null!;
    private readonly List<(Button Button, string TabId)> _tabButtons = new();

    public override void _Ready()
    {
        // 1. Sleek glassmorphism flat theme with shadows
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 0.97f),
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.6f, 0.5f, 0.35f, 0.85f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ContentMarginLeft = 25,
            ContentMarginRight = 25,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            ShadowSize = 12,
            ShadowColor = new Color(0, 0, 0, 0.6f)
        };
        AddThemeStyleboxOverride("panel", style);

        CustomMinimumSize = new Vector2(780, 620);
        OverlayPanelLayout.Center(this);

        // 2. 主体布局
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddThemeConstantOverride("separation", 10);
        AddChild(mainVbox);

        // Header 行
        var headerHbox = new HBoxContainer();
        mainVbox.AddChild(headerHbox);

        _titleLabel = new Label { Text = "📖 卡拉迪亚百科全书" };
        _titleLabel.AddThemeColorOverride("font_color", TextAccent);
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        headerHbox.AddChild(_titleLabel);

        var closeBtn = new Button();
        _StyleCloseButton(closeBtn);
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => Visible = false;
        headerHbox.AddChild(closeBtn);

        var headerSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.25f) };
        mainVbox.AddChild(headerSep);

        // 导航 Tab 栏
        var tabHbox = new HBoxContainer();
        tabHbox.AddThemeConstantOverride("separation", 8);
        mainVbox.AddChild(tabHbox);

        _tabButtons.Clear();
        _CreateTabButton(tabHbox, "具名英雄", "heroes");
        _CreateTabButton(tabHbox, "显赫家族", "families");
        _CreateTabButton(tabHbox, "势力阵营", "factions");
        _CreateTabButton(tabHbox, "珍奇物品", "items");
        _CreateTabButton(tabHbox, "地理据点", "pois");
        _CreateTabButton(tabHbox, "种族百科", "races");
        _CreateTabButton(tabHbox, "生物图鉴", "bestiary");
        _CreateTabButton(tabHbox, "教程指南", "guide");

        var tabSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        mainVbox.AddChild(tabSep);

        // 搜索条
        var searchHbox = new HBoxContainer();
        searchHbox.AddThemeConstantOverride("separation", 10);
        mainVbox.AddChild(searchHbox);

        var searchLabel = new Label { Text = "🔍 模糊检索:" };
        searchLabel.AddThemeFontSizeOverride("font_size", 14);
        searchLabel.AddThemeColorOverride("font_color", TextSecondary);
        searchHbox.AddChild(searchLabel);

        _searchBar = new LineEdit { PlaceholderText = "输入名称或属性进行过滤...", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _searchBar.AddThemeFontSizeOverride("font_size", 14);
        
        var editStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.35f),
            BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            BorderColor = new Color(1, 1, 1, 0.1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 6, ContentMarginBottom = 6
        };
        _searchBar.AddThemeStyleboxOverride("normal", editStyle);
        _searchBar.AddThemeStyleboxOverride("focus", new StyleBoxFlat {
            BgColor = new Color(0f, 0f, 0f, 0.45f),
            BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            BorderColor = TextAccent,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 6, ContentMarginBottom = 6
        });

        _searchBar.TextChanged += (string text) =>
        {
            _searchFilter = text.Trim();
            RefreshList();
        };
        searchHbox.AddChild(_searchBar);

        var searchSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        mainVbox.AddChild(searchSep);

        // 列表 Scroll 视图
        var scroll = new ScrollContainer { 
            SizeFlagsHorizontal = SizeFlags.ExpandFill, 
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _listContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _listContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_listContainer);
        mainVbox.AddChild(scroll);

        Visible = false;
    }

    public void Initialize(OverworldEntityManager entityMgr, DiscoveryJournal? journal = null)
    {
        _entityMgr = entityMgr;
        _journal = journal;
        _currentTab = "heroes";
        _searchFilter = "";
        _searchBar.Text = "";
        LoadBestiaryData();
        LoadTutorialData();
        RefreshList();
    }

    public void LoadBestiaryData()
    {
        try
        {
            var root = new BestiaryDataRoot();

            // 1. 加载普通怪物
            var monsterTpls = UnitTemplateDB.GetMonsterTemplates();
            foreach (var tpl in monsterTpls)
            {
                if (tpl == null || !tpl.ContainsKey("template_id")) continue;
                string templateId = tpl["template_id"].AsString();
                string name = tpl.ContainsKey("name") ? tpl["name"].AsString() : templateId;

                var enemyType = tpl.ContainsKey("enemy_type") ? (EnemyType)tpl["enemy_type"].AsInt32() : EnemyType.Humanoid;
                string category = enemyType switch
                {
                    EnemyType.Humanoid => "人形",
                    EnemyType.Beast => "野兽",
                    EnemyType.Undead => "亡灵",
                    EnemyType.Demon => "魔物",
                    EnemyType.Giant => "巨人",
                    EnemyType.Construct => "构造体",
                    EnemyType.Dragon => "龙类",
                    _ => "其他"
                };

                string description = tpl.ContainsKey("description") ? tpl["description"].AsString() : "";
                if (string.IsNullOrEmpty(description))
                {
                    description = $"一种活跃在大陆上的{category}生物，属于野外常见威胁。";
                }

                float cr = UnitTemplateDB.CalculateCrFromTemplate(tpl);
                int threatLevel = Mathf.Clamp(Mathf.RoundToInt(cr / 2.0f) + 1, 1, 5);

                string habitat = category switch
                {
                    "野兽" => "森林、平原、丘陵",
                    "亡灵" => "墓穴遗迹、被诅咒的土地",
                    "魔物" => "荒野、深渊巢穴",
                    "构造体" => "古代遗迹、废弃神殿",
                    "龙类" => "极巅火山、深山峡谷",
                    _ => "广袤荒野"
                };

                if (tpl.ContainsKey("biome"))
                {
                    var biomes = tpl["biome"].AsGodotArray();
                    if (biomes.Count > 0)
                    {
                        var bNames = new List<string>();
                        foreach (var b in biomes)
                        {
                            string bStr = b.AsString();
                            string bCn = bStr switch
                            {
                                "Plains" => "平原",
                                "Desert" => "沙漠",
                                "Forest" => "森林",
                                "Hills" => "丘陵",
                                "Mountain" => "山地",
                                "Swamp" => "沼泽",
                                "Snow" => "雪地",
                                "Bog" => "湿地沼泽",
                                "Ruins" => "遗迹废墟",
                                _ => bStr
                            };
                            bNames.Add(bCn);
                        }
                        habitat = string.Join("、", bNames);
                    }
                }

                var traits = tpl.ContainsKey("traits") ? tpl["traits"].AsStringArray() : Array.Empty<string>();
                string traitsStr = traits.Length > 0 ? string.Join("，", traits) : "普通战斗特征";
                int baseAc = 8 + (tpl.ContainsKey("ac_bonus") ? tpl["ac_bonus"].AsInt32() : 0);
                int moveRange = tpl.ContainsKey("move_range") ? tpl["move_range"].AsInt32() : 4;
                string combatHint = $"基础护甲等级(AC): {baseAc}，移动距离: {moveRange}格。战斗特性: {traitsStr}。";

                root.Creatures.Add(new CreatureEntry
                {
                    Id = templateId,
                    Name = name,
                    Category = category,
                    Description = description,
                    ThreatLevel = threatLevel,
                    Habitat = habitat,
                    CombatHint = combatHint
                });
            }

            // 2. 加载传奇生物
            var legendaryTpls = UnitTemplateDB.GetLegendaryTemplates();
            foreach (var tpl in legendaryTpls)
            {
                if (tpl == null || !tpl.ContainsKey("template_id")) continue;
                string templateId = tpl["template_id"].AsString();
                string name = tpl.ContainsKey("name") ? tpl["name"].AsString() : templateId;

                var enemyType = tpl.ContainsKey("enemy_type") ? (EnemyType)tpl["enemy_type"].AsInt32() : EnemyType.Legendary;
                string category = enemyType switch
                {
                    EnemyType.Humanoid => "人形",
                    EnemyType.Beast => "野兽",
                    EnemyType.Undead => "亡灵",
                    EnemyType.Demon => "魔物",
                    EnemyType.Giant => "巨人",
                    EnemyType.Construct => "构造体",
                    EnemyType.Dragon => "龙类",
                    EnemyType.Legendary => "传奇",
                    _ => "传说级生物"
                };

                string description = tpl.ContainsKey("description") ? tpl["description"].AsString() : "";
                string loreDescription = $"传闻在大陆的某些险恶地带，沉睡着极为恐怖的【{name}】。学者们在古籍中警告，它是毁灭与力量的化身，它的双翼能掀起吞噬一切的暴风，坚如磐石的外壳令凡人兵刃难以伤其分毫。在击败它之前，这终究只是个令人战栗的古老传说。";
                if (!string.IsNullOrEmpty(description))
                {
                    loreDescription = $"{description}（击败前仅为传闻）";
                }

                string fullDescription = $"【{name}】是世间罕见的半神级上古主宰。";
                if (!string.IsNullOrEmpty(description))
                {
                    fullDescription += $" {description}";
                }
                var traits = tpl.ContainsKey("traits") ? tpl["traits"].AsStringArray() : Array.Empty<string>();
                if (traits.Length > 0)
                {
                    fullDescription += $" 战斗中它具有如下超凡特质：{string.Join("，", traits)}。";
                }

                string habitat = category switch
                {
                    "野兽" => "森林、平原、丘陵",
                    "亡灵" => "墓穴遗迹、被诅咒的土地",
                    "魔物" => "荒野、深渊巢穴",
                    "构造体" => "古代遗迹、废弃神殿",
                    "龙类" => "极巅火山、深山峡谷",
                    _ => "广袤荒野"
                };

                if (tpl.ContainsKey("biome"))
                {
                    var biomes = tpl["biome"].AsGodotArray();
                    if (biomes.Count > 0)
                    {
                        var bNames = new List<string>();
                        foreach (var b in biomes)
                        {
                            string bStr = b.AsString();
                            string bCn = bStr switch
                            {
                                "Plains" => "平原",
                                "Desert" => "沙漠",
                                "Forest" => "森林",
                                "Hills" => "丘陵",
                                "Mountain" => "山地",
                                "Swamp" => "沼泽",
                                "Snow" => "雪地",
                                "Bog" => "湿地沼泽",
                                "Ruins" => "遗迹废墟",
                                _ => bStr
                            };
                            bNames.Add(bCn);
                        }
                        habitat = string.Join("、", bNames);
                    }
                }

                var resist = tpl.ContainsKey("resistances") ? tpl["resistances"].AsStringArray() : Array.Empty<string>();
                var immune = tpl.ContainsKey("immunities") ? tpl["immunities"].AsStringArray() : Array.Empty<string>();
                var weak = tpl.ContainsKey("weaknesses") ? tpl["weaknesses"].AsStringArray() : Array.Empty<string>();

                var analysisLines = new List<string>();
                if (resist.Length > 0) analysisLines.Add($"🛡【抗性】：对 {string.Join("、", resist)} 具有强大防护力。");
                if (immune.Length > 0) analysisLines.Add($"🚫【免疫】：完全免疫 {string.Join("、", immune)} 等效果。");
                if (weak.Length > 0) analysisLines.Add($"⚡【弱点】：对 {string.Join("、", weak)} 属性攻击较为脆弱。");

                if (tpl.ContainsKey("legendary_actions"))
                {
                    analysisLines.Add($"🔥【传奇行动】：具备传奇反击点数，能在玩家回合结束后以极高威胁度强行出招。");
                }
                if (analysisLines.Count == 0)
                {
                    analysisLines.Add("无明显属性偏向，建议使用重装部队和高爆发魔法进行压制。");
                }
                string combatAnalysis = string.Join("\n", analysisLines);

                string uniqueDrop = tpl.ContainsKey("unique_drop_id") ? tpl["unique_drop_id"].AsString() : "";
                string rewardHint = string.IsNullOrEmpty(uniqueDrop)
                    ? "击败后可获得巨额威望以及传说级别的战利品装备。"
                    : $"击败后必掉落传说级专属圣物【{uniqueDrop}】与珍稀的强化材料。";

                root.Legendaries.Add(new LegendaryEntry
                {
                    Id = templateId,
                    Name = name,
                    Category = category,
                    LoreDescription = loreDescription,
                    FullDescription = fullDescription,
                    ThreatLevel = 5,
                    Habitat = habitat,
                    CombatAnalysis = combatAnalysis,
                    RewardHint = rewardHint
                });
            }

            _bestiaryData = root;

            // 3. 把自动生成的最新数据持久化到 bestiary_entries.json 中
            const string path = "res://BladeHexFrontend/src/View/UI/Encyclopedia/bestiary_entries.json";
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
                var json = JsonSerializer.Serialize(root, options);
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
                if (file != null)
                {
                    file.StoreString(json);
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Encyclopedia] 自动回写图鉴数据 JSON 失败: {ex.Message}");
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"[Encyclopedia] 从数据库生成图鉴数据失败: {e.Message}");
        }
    }

    private void LoadTutorialData()
    {
        const string path = "res://BladeHexFrontend/src/View/UI/Tutorial/tutorial_pages.json";
        if (!FileAccess.FileExists(path)) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return;
        try
        {
            var json = file.GetAsText();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _tutorialData = JsonSerializer.Deserialize<TutorialDataRoot>(json, options);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[Encyclopedia] 加载教程数据失败: {e.Message}");
        }
    }

    private void _CreateTabButton(HBoxContainer parent, string labelText, string tabId)
    {
        var btn = new Button { Text = labelText, CustomMinimumSize = new Vector2(85, 36) };
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.Pressed += () =>
        {
            _currentTab = tabId;
            _searchFilter = "";
            _searchBar.Text = "";
            RefreshList();
        };
        parent.AddChild(btn);
        _tabButtons.Add((btn, tabId));
    }

    public void RefreshList()
    {
        if (_entityMgr == null) return;

        // 动态高亮刷新 Tab 按钮样式
        foreach (var tBtn in _tabButtons)
        {
            _StyleTabButton(tBtn.Button, tBtn.TabId == _currentTab);
        }

        // 清空列表
        foreach (var child in _listContainer.GetChildren())
        {
            child.QueueFree();
        }

        switch (_currentTab)
        {
            case "heroes":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 具名英雄";
                var heroes = EncyclopediaService.GetAllHeroes(_entityMgr.Heroes);

                // 如果有发现日志，只显示已遭遇的领主
                if (_journal != null)
                    heroes = heroes.Where(h => _journal.EncounteredLords.Contains(h.HeroId)).ToList();

                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    heroes = heroes.Where(h => h.DisplayName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) || 
                                               h.FamilyName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                               h.FactionId.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                foreach (var h in heroes)
                {
                    string factionName = h.FactionId;
                    var nat = _entityMgr.Nations.FirstOrDefault(n => n.Id == h.FactionId);
                    if (nat != null) factionName = nat.DisplayName;
                    
                    var btn = _CreateListRow($"{h.DisplayName} ({h.FamilyName}氏) - 所属: {factionName}");
                    btn.Pressed += () => HeroDetailPanel.ShowDetail(h, _entityMgr, GetParent());
                    _listContainer.AddChild(btn);
                }
                break;

            case "families":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 显赫家族";
                var families = EncyclopediaService.GetAllFamilies(_entityMgr.Heroes);
                var familyKeys = families.Keys.ToList();
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    familyKeys = familyKeys.Where(f => f.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                foreach (var fKey in familyKeys)
                {
                    var members = families[fKey];
                    var btn = _CreateListRow($"{fKey} 家族 (成员: {members.Count}人，第一封地: {members.FirstOrDefault(m => !string.IsNullOrEmpty(m.BoundPoiName))?.BoundPoiName ?? "无"})");
                    btn.Pressed += () => FamilyDetailPanel.ShowDetail(fKey, members, _entityMgr, GetParent());
                    _listContainer.AddChild(btn);
                }
                break;

            case "factions":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 势力阵营";
                var factions = EncyclopediaService.GetAllFactions(_entityMgr.Nations);
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    factions = factions.Where(f => f.DisplayName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                                   f.Race.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                foreach (var f in factions)
                {
                    var btn = _CreateListRow($"{f.DisplayName} (主导种族: {f.Race}，领土规模: {f.MinTerritoryTiles}Tiles)");
                    btn.Pressed += () => FactionDetailPanel.ShowDetail(f, _entityMgr, GetParent());
                    _listContainer.AddChild(btn);
                }
                break;

            case "items":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 珍奇物品";
                var items = EncyclopediaService.GetAllItems();
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    items = items.Where(i => i.ItemName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                             i.ItemId.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                foreach (var item in items)
                {
                    var btn = _CreateListRow($"{item.ItemName} ({item.GetRarityName()}品质) - 单价: {item.Price}G，分类: {item.EquipSlotTarget}");
                    btn.Pressed += () => ItemDetailPanel.ShowDetail(item, GetParent());
                    _listContainer.AddChild(btn);
                }
                break;

            case "pois":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 地理据点";
                var pois = EncyclopediaService.GetAllKnownPois(_entityMgr.Pois);

                // 如果有发现日志，只显示已发现的 POI
                if (_journal != null)
                    pois = pois.Where(p => _journal.IsPoiDiscovered(p.PoiName)).ToList();

                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    pois = pois.Where(p => p.PoiName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                           p.OwningFaction.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                foreach (var poi in pois)
                {
                    string factionName = poi.OwningFaction;
                    var nat = _entityMgr.Nations.FirstOrDefault(n => n.Id == poi.OwningFaction);
                    if (nat != null) factionName = nat.DisplayName;
                    else if (poi.OwningFaction == "neutral") factionName = "中立";

                    var btn = _CreateListRow($"{poi.PoiName} ({poi.PoiTypeEnum}) - 归属: {factionName}，繁荣: {poi.Prosperity}");
                    btn.Pressed += () => PoiDetailPanel.ShowDetail(poi, _entityMgr, GetParent());
                    _listContainer.AddChild(btn);
                }
                break;

            case "races":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 种族百科";
                var races = EncyclopediaService.GetAllRaces();
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    races = races.Where(r => r.RaceName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                             r.raceId.ToString().Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                foreach (var race in races)
                {
                    string traits = race.RacialTraits.Length > 0 ? string.Join(", ", race.RacialTraits) : "无";
                    var btn = _CreateListRow($"{race.RaceName} (特性: {traits})");
                    btn.Pressed += () => _ShowRaceDetail(race);
                    _listContainer.AddChild(btn);
                }
                break;

            case "bestiary":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 生物图鉴";
                _PopulateBestiary();
                break;

            case "guide":
                _titleLabel.Text = "📖 卡拉迪亚百科全书 - 教程指南";
                _PopulateGuide();
                break;
        }

        if (_listContainer.GetChildCount() == 0)
        {
            var emptyLabel = new Label { Text = "没有查找到符合该过滤条件的百科实体信息。" };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _listContainer.AddChild(emptyLabel);
        }
    }

    private Button _CreateListRow(string text)
    {
        var btn = new Button
        {
            Text = $"  {text}",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 38)
        };
        _StyleListButton(btn, TextPrimary, TextAccent);
        return btn;
    }

    private static void _StyleCloseButton(Button closeBtn)
    {
        closeBtn.Text = "✕";
        closeBtn.FocusMode = Control.FocusModeEnum.None;
        var btnStyleNormal = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0f) };
        var btnStyleHover = new StyleBoxFlat {
            BgColor = new Color(0.9f, 0.3f, 0.25f, 0.4f),
            CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15
        };
        var btnStylePressed = new StyleBoxFlat {
            BgColor = new Color(0.9f, 0.3f, 0.25f, 0.6f),
            CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15
        };
        closeBtn.AddThemeStyleboxOverride("normal", btnStyleNormal);
        closeBtn.AddThemeStyleboxOverride("hover", btnStyleHover);
        closeBtn.AddThemeStyleboxOverride("pressed", btnStylePressed);
        closeBtn.AddThemeStyleboxOverride("focus", btnStyleNormal);
        closeBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.63f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
        closeBtn.AddThemeFontSizeOverride("font_size", 16);
        closeBtn.CustomMinimumSize = new Vector2(30, 30);
    }

    private static void _StyleListButton(Button btn, Color fontColor, Color accentColor)
    {
        btn.FocusMode = Control.FocusModeEnum.None;
        var btnNormal = new StyleBoxFlat {
            BgColor = new Color(1, 1, 1, 0.03f),
            BorderWidthBottom = 1,
            BorderColor = new Color(1, 1, 1, 0.08f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10,
            ContentMarginRight = 10
        };
        var btnHover = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.12f),
            BorderWidthBottom = 1,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.3f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14, // 悬浮偏移动效
            ContentMarginRight = 6
        };
        var btnPressed = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.22f),
            BorderWidthBottom = 1,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.5f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginRight = 6
        };
        btn.AddThemeStyleboxOverride("normal", btnNormal);
        btn.AddThemeStyleboxOverride("hover", btnHover);
        btn.AddThemeStyleboxOverride("pressed", btnPressed);
        btn.AddThemeStyleboxOverride("focus", btnNormal);
        btn.AddThemeColorOverride("font_color", fontColor);
        btn.AddThemeColorOverride("font_hover_color", accentColor);
        btn.AddThemeFontSizeOverride("font_size", 14);
    }

    private static void _StyleTabButton(Button btn, bool active)
    {
        btn.FocusMode = Control.FocusModeEnum.None;
        var styleNormal = new StyleBoxFlat
        {
            BgColor = active ? new Color(0.9f, 0.8f, 0.5f, 0.15f) : new Color(1, 1, 1, 0.02f),
            BorderWidthBottom = active ? 3 : 1,
            BorderColor = active ? new Color(0.9f, 0.8f, 0.5f, 0.8f) : new Color(1, 1, 1, 0.08f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8, ContentMarginRight = 8
        };
        var styleHover = new StyleBoxFlat
        {
            BgColor = active ? new Color(0.9f, 0.8f, 0.5f, 0.2f) : new Color(1, 1, 1, 0.08f),
            BorderWidthBottom = active ? 3 : 1,
            BorderColor = active ? new Color(0.9f, 0.8f, 0.5f, 0.9f) : new Color(0.9f, 0.8f, 0.5f, 0.4f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8, ContentMarginRight = 8
        };
        var stylePressed = new StyleBoxFlat
        {
            BgColor = new Color(0.9f, 0.8f, 0.5f, 0.25f),
            BorderWidthBottom = 3,
            BorderColor = new Color(0.9f, 0.8f, 0.5f, 1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8, ContentMarginRight = 8
        };
        btn.AddThemeStyleboxOverride("normal", styleNormal);
        btn.AddThemeStyleboxOverride("hover", styleHover);
        btn.AddThemeStyleboxOverride("pressed", stylePressed);
        btn.AddThemeStyleboxOverride("focus", styleNormal);
        btn.AddThemeColorOverride("font_color", active ? new Color(0.9f, 0.8f, 0.5f) : new Color(0.85f, 0.83f, 0.78f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.9f, 0.7f));
    }

    // T03: Show race detail popup
    private void _ShowRaceDetail(RaceData race)
    {
        var popup = new AcceptDialog();
        popup.Title = $"种族详情 - {race.RaceName}";
        popup.MinSize = new Vector2I(400, 300);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        // Race name
        var nameLabel = new Label { Text = $"种族: {race.RaceName}" };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        nameLabel.AddThemeColorOverride("font_color", TextAccent);
        vbox.AddChild(nameLabel);

        // Attribute modifiers
        var attrLabel = new Label();
        attrLabel.AddThemeFontSizeOverride("font_size", 14);
        var mods = new List<string>();
        if (race.StrMod != 0) mods.Add($"力量{race.StrMod:+#;-#;0}");
        if (race.DexMod != 0) mods.Add($"敏捷{race.DexMod:+#;-#;0}");
        if (race.ConMod != 0) mods.Add($"体质{race.ConMod:+#;-#;0}");
        if (race.IntMod != 0) mods.Add($"智力{race.IntMod:+#;-#;0}");
        if (race.WisMod != 0) mods.Add($"感知{race.WisMod:+#;-#;0}");
        if (race.ChaMod != 0) mods.Add($"魅力{race.ChaMod:+#;-#;0}");
        attrLabel.Text = mods.Count > 0 ? $"属性修正: {string.Join(", ", mods)}" : "属性修正: 无";
        vbox.AddChild(attrLabel);

        // Racial traits
        if (race.RacialTraits.Length > 0)
        {
            var traitsLabel = new Label { Text = $"种族特性: {string.Join(", ", race.RacialTraits)}" };
            traitsLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(traitsLabel);
        }

        // Description
        if (!string.IsNullOrEmpty(race.TraitsDescription))
        {
            var descLabel = new Label { Text = race.TraitsDescription };
            descLabel.AddThemeFontSizeOverride("font_size", 14);
            descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            vbox.AddChild(descLabel);
        }

        // Recruitment difficulty
        var recruitLabel = new Label { Text = $"招募难度: {race.RecruitmentDifficulty:F1}" };
        recruitLabel.AddThemeFontSizeOverride("font_size", 14);
        recruitLabel.AddThemeColorOverride("font_color", TextSecondary);
        vbox.AddChild(recruitLabel);

        popup.AddChild(vbox);
        GetParent().AddChild(popup);
        popup.PopupCentered(new Vector2I(400, 300));
    }

    // ============================================================================
    // 生物图鉴 Tab
    // ============================================================================

    private void _PopulateBestiary()
    {
        if (_bestiaryData == null)
        {
            var lbl = new Label { Text = "图鉴数据加载失败" };
            lbl.AddThemeColorOverride("font_color", TextSecondary);
            _listContainer.AddChild(lbl);
            return;
        }

        // 普通生物区
        var creatureHeader = new Label { Text = "━━ 已知生物 ━━" };
        creatureHeader.AddThemeFontSizeOverride("font_size", 16);
        creatureHeader.AddThemeColorOverride("font_color", TextAccent);
        creatureHeader.HorizontalAlignment = HorizontalAlignment.Center;
        _listContainer.AddChild(creatureHeader);

        var creatures = _bestiaryData.Creatures.AsEnumerable();

        // 如果有发现日志，只显示已遭遇的
        if (_journal != null)
            creatures = creatures.Where(c => _journal.EncounteredCreatures.Contains(c.Id));

        if (!string.IsNullOrEmpty(_searchFilter))
            creatures = creatures.Where(c => c.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                             c.Category.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var creature in creatures)
        {
            string threatStars = new string('★', creature.ThreatLevel) + new string('☆', 5 - creature.ThreatLevel);
            var btn = _CreateListRow($"🐾 {creature.Name} [{creature.Category}] 威胁: {threatStars}");
            var c = creature; // capture
            btn.Pressed += () => _ShowCreatureDetail(c);
            _listContainer.AddChild(btn);
        }

        // 传奇生物区
        _listContainer.AddChild(new HSeparator());
        var legendHeader = new Label { Text = "━━ 传奇生物 ━━" };
        legendHeader.AddThemeFontSizeOverride("font_size", 16);
        legendHeader.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 0.2f));
        legendHeader.HorizontalAlignment = HorizontalAlignment.Center;
        _listContainer.AddChild(legendHeader);

        var legendaries = _bestiaryData.Legendaries.AsEnumerable();

        if (!string.IsNullOrEmpty(_searchFilter))
            legendaries = legendaries.Where(l => l.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                                  l.Category.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var legend in legendaries)
        {
            bool defeated = _journal?.IsLegendaryDefeated(legend.Id) ?? false;
            string status = defeated ? "✓ 已击败" : "？ 传闻";
            var btn = _CreateListRow($"🔥 {legend.Name} [{legend.Category}] {status}");
            var l = legend; // capture
            btn.Pressed += () => _ShowLegendaryDetail(l);
            _listContainer.AddChild(btn);
        }

        // 如果没有遭遇任何普通生物
        if (_journal != null && !_journal.EncounteredCreatures.Any())
        {
            var hint = new Label { Text = "尚未遭遇任何普通生物。探索世界以解锁普通生物图鉴。" };
            hint.AddThemeColorOverride("font_color", TextSecondary);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            _listContainer.AddChild(hint);
        }
    }

    private void _ShowCreatureDetail(CreatureEntry creature)
    {
        var popup = new AcceptDialog();
        popup.Title = $"生物图鉴 - {creature.Name}";
        popup.MinSize = new Vector2I(450, 350);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label { Text = $"🐾 {creature.Name}" };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        nameLabel.AddThemeColorOverride("font_color", TextAccent);
        vbox.AddChild(nameLabel);

        var catLabel = new Label { Text = $"分类: {creature.Category}  |  威胁等级: {creature.ThreatLevel}/5" };
        catLabel.AddThemeFontSizeOverride("font_size", 14);
        catLabel.AddThemeColorOverride("font_color", TextSecondary);
        vbox.AddChild(catLabel);

        vbox.AddChild(new HSeparator());

        var descLabel = new Label { Text = creature.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        descLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(descLabel);

        var habitatLabel = new Label { Text = $"\n📍 出没地: {creature.Habitat}" };
        habitatLabel.AddThemeFontSizeOverride("font_size", 14);
        habitatLabel.AddThemeColorOverride("font_color", TextSecondary);
        vbox.AddChild(habitatLabel);

        var combatLabel = new Label { Text = $"\n⚔ 战斗提示: {creature.CombatHint}", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        combatLabel.AddThemeFontSizeOverride("font_size", 14);
        combatLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.5f));
        vbox.AddChild(combatLabel);

        popup.AddChild(vbox);
        GetParent().AddChild(popup);
        popup.PopupCentered(new Vector2I(450, 350));
    }

    private void _ShowLegendaryDetail(LegendaryEntry legend)
    {
        bool defeated = _journal?.IsLegendaryDefeated(legend.Id) ?? false;

        var popup = new AcceptDialog();
        popup.Title = defeated ? $"传奇生物 - {legend.Name} [已击败]" : $"传奇生物 - {legend.Name} [传闻]";
        popup.MinSize = new Vector2I(500, 400);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label { Text = $"🔥 {legend.Name}" };
        nameLabel.AddThemeFontSizeOverride("font_size", 22);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 0.2f));
        vbox.AddChild(nameLabel);

        var catLabel = new Label { Text = $"分类: {legend.Category}  |  威胁等级: {legend.ThreatLevel}/5" };
        catLabel.AddThemeFontSizeOverride("font_size", 14);
        catLabel.AddThemeColorOverride("font_color", TextSecondary);
        vbox.AddChild(catLabel);

        vbox.AddChild(new HSeparator());

        if (defeated)
        {
            // 击败后显示完整信息
            var fullDesc = new Label { Text = legend.FullDescription, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            fullDesc.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(fullDesc);

            var habitatLabel = new Label { Text = $"\n📍 巢穴: {legend.Habitat}" };
            habitatLabel.AddThemeFontSizeOverride("font_size", 14);
            habitatLabel.AddThemeColorOverride("font_color", TextSecondary);
            vbox.AddChild(habitatLabel);

            var combatLabel = new Label { Text = $"\n⚔ 战斗分析:\n{legend.CombatAnalysis}", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            combatLabel.AddThemeFontSizeOverride("font_size", 14);
            combatLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.5f));
            vbox.AddChild(combatLabel);

            var rewardLabel = new Label { Text = $"\n🎁 战利品: {legend.RewardHint}" };
            rewardLabel.AddThemeFontSizeOverride("font_size", 14);
            rewardLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.85f, 0.3f));
            vbox.AddChild(rewardLabel);
        }
        else
        {
            // 未击败：只显示故事性传闻
            var loreLabel = new Label { Text = legend.LoreDescription, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            loreLabel.AddThemeFontSizeOverride("font_size", 14);
            loreLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.65f));
            vbox.AddChild(loreLabel);

            var unknownLabel = new Label { Text = "\n\n[ 击败此传奇生物后解锁完整信息 ]" };
            unknownLabel.AddThemeFontSizeOverride("font_size", 13);
            unknownLabel.AddThemeColorOverride("font_color", TextSecondary);
            unknownLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(unknownLabel);
        }

        popup.AddChild(vbox);
        GetParent().AddChild(popup);
        popup.PopupCentered(new Vector2I(500, 400));
    }

    // ============================================================================
    // 教程指南 Tab
    // ============================================================================

    private void _PopulateGuide()
    {
        if (_tutorialData == null || _tutorialData.Chapters.Count == 0)
        {
            var lbl = new Label { Text = "教程数据加载失败或为空" };
            lbl.AddThemeColorOverride("font_color", TextSecondary);
            _listContainer.AddChild(lbl);
            return;
        }

        foreach (var chapter in _tutorialData.Chapters)
        {
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                bool match = chapter.Title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                             chapter.Pages.Any(p => p.Title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                                    p.Content.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;
            }

            // 章节标题
            var chapterHeader = new Label { Text = $"📖 {chapter.Title}" };
            chapterHeader.AddThemeFontSizeOverride("font_size", 16);
            chapterHeader.AddThemeColorOverride("font_color", TextAccent);
            _listContainer.AddChild(chapterHeader);

            // 各页面作为子条目
            foreach (var page in chapter.Pages)
            {
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !page.Title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) &&
                    !page.Content.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var btn = _CreateListRow($"  📄 {page.Title}");
                var p = page; // capture
                var ch = chapter;
                btn.Pressed += () => _ShowGuideDetail(ch.Title, p);
                _listContainer.AddChild(btn);
            }

            _listContainer.AddChild(new HSeparator());
        }
    }

    private void _ShowGuideDetail(string chapterTitle, TutorialPage page)
    {
        if (_guideTooltip == null)
        {
            _guideTooltip = new TutorialDetailPanel();
            GetParent().AddChild(_guideTooltip);
        }

        _guideTooltip.ShowTutorial(chapterTitle, page.Title, page.Content);
    }
}
