# Requirements Document

## Introduction

POI 二级面板内容编辑功能。当玩家在大地图进入城镇/村庄/港口等 POI 后，从 TownPanel（一级面板）选择设施进入对应的二级面板。所有二级面板共享完全一致的布局结构（插画-信息-描述-列表），通过基类固定布局，子类只填充数据。训练场功能完整移除。竞技场需要完整接入战斗场景。任务系统需要对接 POI/实体生成系统并补充大量任务模板和描述。港口与普通城镇一致，仅多一个"租船出海"选项。

禁止在任何 UI 文本中使用 emoji 符号。

## 系统现状分析

### 需要的二级面板（最终列表）

| 面板 | 类名 | 对接系统 | 说明 |
|------|------|----------|------|
| 市场/商店 | PartyPanel (Shop模式) | EconomyManager, EquipmentGenerator | 已完成，不在本次范围 |
| 酒馆/招募 | RecruitPanel | RecruitService, PartyRoster | 需中文化+布局统一 |
| 铁匠铺 | SmithyPanel | EconomyManager, UnitData.装备耐久 | 需对接真实装备数据 |
| 药师所 | TemplePanel | EconomyManager, PartyRoster HP/状态 | 需对接真实 HP/状态 |
| 竞技场 | ArenaPanel | BattleContext, 战斗场景切换 | 需完整接入战斗系统 |
| 委托/布告栏 | QuestBoardPanel | QuestGenerator, QuestManager, EntityFactory | 需大量任务模板补充 |
| 港口 | PortPanel | 同普通城镇 + 租船出海 | 仅多一个航行入口 |
| 休息 | RestPanel | EconomyManager, PartyRoster HP/MP | 需对接真实恢复 |

### 已移除

- TrainingPanel（训练场）：完整移除，不再作为设施出现

### 需要对接的后端系统

| 系统 | 类名/位置 | 职责 |
|------|-----------|------|
| 经济系统 | EconomyManager | 金币收支、时间推进 |
| 队伍系统 | PartyRoster, OverworldParty | 队员管理、HP/MP/状态 |
| 背包系统 | PartyInventory | 物品存取 |
| 装备系统 | EquipmentGenerator, ItemData | 装备生成、耐久 |
| 招募系统 | RecruitService | 可招募单位池 |
| 委托系统 | QuestGenerator, QuestManager | 任务生成、接取、追踪 |
| 战斗系统 | BattleContext, CombatScene | 竞技场实战入口 |
| 实体生成 | EncounterUnitFactory, EntityFactory | 竞技场/任务敌人生成 |
| 声望系统 | EconomyManager.Reputation | 声望值读写 |
| 角色数据 | UnitData | 属性、等级、装备、HP/MP |

## Glossary

- **POIPanelBase**: POI 面板统一基类，提供主题、脚手架、动画、工厂方法
- **SecondaryPanel**: 二级面板，从 TownPanel 进入的具体功能面板
- **统一布局**: 所有二级面板从上到下固定为：插画区 - 信息行 - 描述文本 - 功能列表区
- **BattleContext**: 战斗上下文，封装进入战斗所需的全部参数（敌人配置、地图模板、奖励等）
- **QuestGenerator**: 委托生成器，根据 POI 类型、繁荣度、时间生成可接取任务
- **QuestManager**: 任务管理器，追踪已接取任务的进度和完成状态

## Requirements

### Requirement 1: 统一面板布局基类

**User Story:** 作为开发者，我需要所有二级面板共享完全一致的布局结构，这样视觉风格统一且维护成本低。

#### Acceptance Criteria

1. POIPanelBase 的 BuildContent 脚手架 SHALL 固定以下从上到下的布局顺序：插画区（固定高度色块+图标文字）、信息行（设施名+金币等状态）、描述文本（RichTextLabel）、功能列表区（ScrollContainer 内的 VBoxContainer）、底部离开按钮
2. 子类 SHALL 只通过重写数据填充方法（如 GetIllustColor、GetTitle、GetInfoText、GetDescription、PopulateActions）来定制内容，不得修改布局结构
3. 所有面板 SHALL 使用相同的宽度、高度、内边距、圆角、边框颜色
4. 所有 UI 文本 SHALL 使用中文，禁止出现任何 emoji 符号
5. 插画区 SHALL 使用纯色背景+居中文字标题的方式（后续可替换为真实插画资源）

### Requirement 2: 铁匠铺面板对接装备系统

**User Story:** 作为玩家，我希望铁匠铺能真正修理我的装备并显示实际耐久值，这样我能管理装备状态。

#### Acceptance Criteria

1. WHEN 铁匠铺面板打开时，功能列表区 SHALL 显示"全副修理"、"磨砺武器"、"加固防具"三个服务按钮，每个按钮显示费用和效果描述
2. WHEN 玩家选择"全副修理"，SmithyPanel SHALL 遍历 PartyRoster 中当前角色的所有装备，将 CurrentArmorPoints 恢复到 MaxArmorPoints，并从 EconomyManager 扣除费用
3. WHEN 玩家选择"磨砺武器"，SmithyPanel SHALL 对当前角色主手武器的 BonusDamage 永久+1，并从 EconomyManager 扣除费用
4. WHEN 玩家选择"加固防具"，SmithyPanel SHALL 对当前角色铠甲的 DrThreshold 永久+1，并从 EconomyManager 扣除费用
5. IF 金币不足，THEN 对应按钮 SHALL 显示为禁用状态

### Requirement 3: 药师所面板对接 HP 和状态系统

**User Story:** 作为玩家，我希望药师所能真正治疗我的队伍并移除负面状态。

#### Acceptance Criteria

1. WHEN 药师所面板打开时，功能列表区 SHALL 显示"轻度治疗"、"深度治疗"、"净化诅咒"、"购买净化药水"四个服务按钮
2. WHEN 玩家选择"轻度治疗"，TemplePanel SHALL 遍历 PartyRoster 所有成员，将每人 CurrentHp 恢复到 MaxHp 的 50%（取较大值），并扣除费用
3. WHEN 玩家选择"深度治疗"，TemplePanel SHALL 将所有成员 CurrentHp 恢复到 MaxHp，并扣除费用
4. WHEN 玩家选择"净化诅咒"，TemplePanel SHALL 清除所有成员的负面状态效果列表，并扣除费用
5. WHEN 玩家选择"购买净化药水"，TemplePanel SHALL 向 PartyInventory 添加一个 HolyWater ConsumableData 实例，并扣除费用

### Requirement 4: 竞技场面板完整接入战斗系统

**User Story:** 作为玩家，我希望竞技场能触发真实的战术战斗而不是随机数判定，这样我能通过操作技巧赢得比赛。

#### Acceptance Criteria

1. WHEN 竞技场面板打开时，功能列表区 SHALL 显示三个难度档位（新手/精英/冠军），每档显示报名费、奖金、对手描述
2. WHEN 玩家选择一个档位，ArenaPanel SHALL 从 EconomyManager 扣除报名费，然后使用 EncounterUnitFactory 生成对应难度的敌人编队
3. ArenaPanel SHALL 构建 BattleContext（包含敌人列表、竞技场地图模板、奖励配置），并触发场景切换进入 CombatScene
4. WHEN 战斗结束且玩家胜利，ArenaPanel SHALL 通过 EconomyManager 发放奖金并增加声望
5. WHEN 战斗结束且玩家失败，ArenaPanel SHALL 返回竞技场面板显示失败信息，不额外扣除金币

### Requirement 5: 委托面板对接任务系统并补充大量任务模板

**User Story:** 作为玩家，我希望布告栏有丰富多样的任务可接取，任务内容与当前 POI 和世界状态相关。

#### Acceptance Criteria

1. QuestGenerator SHALL 维护至少 30 种不同的任务模板，覆盖以下类型：讨伐（清除指定敌人）、护送（保护 NPC 到目的地）、采集（收集指定物品）、侦察（探索指定区域）、悬赏（击杀特定精英/Boss）
2. 每种任务模板 SHALL 包含：任务名称模板（支持变量替换如地名/敌人名）、详细描述文本（至少 2-3 句话的叙事描述）、难度等级、奖励公式、时间限制
3. QuestGenerator SHALL 根据当前 POI 的类型、繁荣度、周边实体（通过 EntityFactory 查询附近敌对势力）动态生成任务目标
4. WHEN 玩家接取任务，QuestBoardPanel SHALL 通过 QuestManager 注册任务，使其出现在任务日志中并开始追踪进度
5. 所有任务文本 SHALL 使用中文，包含叙事性描述（如"据报告，北方森林中出现了一群哥布林劫匪，他们袭击了数支商队..."）

### Requirement 6: 港口面板（与普通城镇一致 + 租船出海）

**User Story:** 作为玩家，我希望港口提供与普通城镇相同的设施，并额外提供租船出海的选项。

#### Acceptance Criteria

1. 港口的设施列表 SHALL 与普通城镇一致（市场、酒馆、铁匠铺、药师所、竞技场、布告栏），使用相同的二级面板
2. 港口 SHALL 额外在设施列表中显示"租船出海"选项
3. WHEN 玩家选择"租船出海"，PortPanel SHALL 从 EconomyManager 扣除租船费用，然后将玩家队伍状态切换为"海上航行"模式（具体航行系统为后续功能，本次只需提供入口和扣费）
4. 港口面板 SHALL 使用与其他二级面板完全一致的布局（插画-信息-描述-列表）

### Requirement 7: 休息面板对接真实恢复

**User Story:** 作为玩家，我希望在酒馆休息能真正恢复队伍的 HP 和 MP。

#### Acceptance Criteria

1. WHEN 休息面板打开时，功能列表区 SHALL 显示"短休息"和"长休息"两个选项
2. WHEN 玩家选择"短休息"，RestPanel SHALL 遍历 PartyRoster 所有成员，恢复每人 50% 的 MaxMana，推进时间 4 小时，不花费金币
3. WHEN 玩家选择"长休息"，RestPanel SHALL 恢复所有成员 100% HP 和 MP，推进时间 8 小时，从 EconomyManager 扣除费用
4. WHEN 队伍已满 HP 和 MP，长休息按钮 SHALL 显示为禁用状态并提示"队伍状态良好"

### Requirement 8: 招募面板中文化和布局统一

**User Story:** 作为玩家，我希望酒馆招募界面使用中文且与其他面板风格一致。

#### Acceptance Criteria

1. RecruitPanel SHALL 使用统一布局（插画-信息-描述-列表），功能列表区显示可招募单位卡片
2. 所有文本 SHALL 使用中文（"招募"、"金币不足"、"队伍已满"等）
3. 每个招募单位卡片 SHALL 显示：名称、等级、种族、核心属性（力/敏/体）、招募费用、周薪

### Requirement 9: 城镇面板布局改为单列列表

**User Story:** 作为玩家，我希望城镇设施列表以单列形式显示，每个设施有名称和简短描述，更易阅读。

#### Acceptance Criteria

1. TownPanel 的设施区域 SHALL 改为单列 VBoxContainer，每行一个设施按钮
2. 每个设施按钮 SHALL 占满面板宽度，左侧显示设施名称，右侧显示简短描述
3. TownPanel 自身 SHALL 也使用统一布局（插画-信息-描述-列表），其中列表区就是设施按钮列表

### Requirement 10: 移除训练场

**User Story:** 作为开发者，训练场功能不再需要，应从代码和设施列表中完整移除。

#### Acceptance Criteria

1. TrainingPanel.cs SHALL 被删除
2. TownFacility 的 FacilityType 枚举 SHALL 移除 Training 项
3. 所有 CreateDefaultFacilities / CreateCastleFacilities 等方法 SHALL 不再包含训练场设施
4. TownPanel 的设施选择逻辑 SHALL 不再处理 Training 类型

### Requirement 11: 二级面板统一导航与生命周期

**User Story:** 作为玩家，我希望在二级面板和城镇面板之间导航流畅，状态正确。

#### Acceptance Criteria

1. WHEN 二级面板关闭时，SHALL 返回 TownPanel 显示同一城镇的设施列表
2. WHEN 玩家按 ESC 时，SHALL 关闭当前二级面板并返回 TownPanel
3. 所有二级面板 SHALL 使用懒初始化，首次访问时才创建实例
4. WHEN 玩家从 TownPanel 选择"离开城镇"，SHALL 关闭所有面板并恢复大地图游戏
5. 每次打开二级面板 SHALL 传入正确的 EconomyManager、PartyRoster、PartyInventory 引用并刷新数据
