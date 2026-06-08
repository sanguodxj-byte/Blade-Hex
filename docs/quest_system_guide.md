# 任务/委托系统使用指南

## 概述

任务/委托系统是游戏战略层的核心玩法循环之一，实现了"村庄接委托 → 大地图行军 → 遭遇战斗 → 回村领赏"的完整流程。

## 系统架构

### 核心组件

1. **QuestData.cs** (`BladeHex.Data`) - 任务数据定义
   - 定义任务的所有属性（类型、难度、奖励等）
   - 包含任务状态管理和进度追踪
   - 支持时间限制和前置条件

2. **QuestManager.cs** (`BladeHex.Strategic`) - 任务管理器（场景服务）
   - 管理所有任务的生命周期
   - 处理任务接取、进度更新、完成和失败
   - 发放奖励和管理玩家数据

3. **QuestBoard.cs** (`BladeHex.UI`) - 布告栏UI
   - 显示可用任务列表
   - 展示任务详情
   - 处理任务接取

4. **QuestLog.cs** (`BladeHex.UI`) - 任务日志UI
   - 显示进行中和已完成的任务
   - 实时更新任务进度
   - 支持放弃任务

> 所有任务相关 C# 类均以 `[GlobalClass]` 注册，可在 Godot 编辑器中直接作为节点或资源使用。

## 任务类型

根据策划案，系统支持以下任务类型：

| 类型 | 说明 | 示例 |
|------|------|------|
| EXTERMINATION | 讨伐型 | 清除哥布林营地 |
| ESCORT | 护送型 | 护送商队到达目的地 |
| EXPLORATION | 探索型 | 探索古代遗迹 |
| DEFENSE | 防御型 | 守卫村庄抵御外族 |
| EMERGENCY | 紧急型 | 特殊事件触发的紧急任务 |

## 任务难度

| 难度 | 颜色 | 说明 |
|------|------|------|
| EASY | 白色 | 入门级，适合新手 |
| MEDIUM | 黄色 | 中等难度 |
| HARD | 橙色 | 困难，需要准备 |
| BOSS | 红色 | BOSS级，极具挑战 |

## 使用方法

### 在场景中使用

```csharp
using Godot;
using BladeHex.Strategic;
using BladeHex.UI;

public partial class MyScene : Node
{
    private QuestManager _questManager;
    private QuestBoard _questBoard;
    private QuestLog _questLog;

    public override void _Ready()
    {
        // 1. 添加 QuestManager 节点
        _questManager = new QuestManager();
        AddChild(_questManager);

        // 2. 设置玩家数据（QuestManager 会在 _Ready 中初始化）
        _questManager.PlayerGold = 500;
        _questManager.PlayerReputation = 10;

        // 3. 创建 UI 并关联（通过 Open() 传入 QuestManager 引用）
        _questBoard = GD.Load<PackedScene>("res://src/ui/quest/QuestBoard.tscn").Instantiate<QuestBoard>();
        _questBoard.Open(_questManager, _questManager.AvailableQuests);
        AddChild(_questBoard);

        _questLog = GD.Load<PackedScene>("res://src/ui/quest/QuestLog.tscn").Instantiate<QuestLog>();
        _questLog.Open(_questManager);
        AddChild(_questLog);
    }
}
```

### 创建自定义任务

```csharp
using BladeHex.Data;

var quest = new QuestData
{
    QuestId = "custom_quest_01",
    QuestName = "自定义任务",
    Description = "这是一个自定义任务的描述",
    questType = QuestData.QuestType.Extermination,
    difficulty = QuestData.QuestDifficulty.Medium,
    TargetCount = 10,
    RewardGold = 200,
    RewardReputation = 10
};

// 添加到任务管理器
_questManager.QuestTemplates[quest.QuestId] = quest;
_questManager.AvailableQuests.Add((QuestData)quest.Duplicate());
```

### 更新任务进度

```csharp
// 在战斗中击杀敌人时
void OnEnemyKilled(string enemyType)
{
    // 查找相关任务并更新进度
    foreach (var quest in _questManager.ActiveQuests)
    {
        if (quest.questType == QuestData.QuestType.Extermination)
        {
            _questManager.UpdateQuestProgress(quest.QuestId, 1);
        }
    }
}
```

### 监听任务事件

```csharp
public override void _Ready()
{
    _questManager.QuestAccepted += OnQuestAccepted;
    _questManager.QuestCompleted += OnQuestCompleted;
    _questManager.QuestProgressUpdated += OnQuestProgressUpdated;
}

private void OnQuestAccepted(QuestData quest)
{
    GD.Print($"接取任务: {quest.QuestName}");
}

private void OnQuestCompleted(QuestData quest)
{
    GD.Print($"完成任务: {quest.QuestName}");
    // 可以在这里触发特殊事件或解锁新内容
}

private void OnQuestProgressUpdated(QuestData quest, int progress)
{
    GD.Print($"任务进度: {progress}/{quest.TargetCount}");
}
```

## 任务奖励系统

任务完成后会自动发放以下奖励：

1. **金币** - 直接添加到玩家金币
2. **声望** - 增加玩家在特定势力的声望
3. **物品** - 添加到玩家背包（需要背包系统支持）

```csharp
// 奖励配置示例
quest.RewardGold = 300;
quest.RewardReputation = 15;
quest.RewardFaction = "绿谷村";
quest.RewardItems = new[] { "potion_health", "scroll_fireball" };
```

## 前置条件系统

任务可以设置前置条件，只有满足条件才能接取：

```csharp
// 声望要求
quest.RequiredReputation = 20;

// 前置任务要求
quest.RequiredQuests = new[] { "quest_01", "quest_02" };

// 检查是否可接取（需要在 QuestData 中实现 CanAccept 方法）
// if (quest.CanAccept(playerReputation, completedQuestIds))
// {
//     // 可以接取
// }
```

## 时间限制系统

任务可以设置时间限制，超时后自动失败：

```csharp
// 设置时间限制
quest.HasTimeLimit = true;
quest.TimeLimitDays = 5;  // 5天内完成

// 获取剩余时间（需要在 QuestData 中实现 GetRemainingDays 方法）
// float remaining = quest.GetRemainingDays(_questManager.GameTime);
// GD.Print($"剩余时间: {remaining:F1}天");
```

## 保存/加载系统

任务系统支持保存和加载：

```csharp
// 保存
var saveData = _questManager.Serialize();
// 将 saveData 保存到文件（Godot.Collections.Dictionary 格式）

// 加载（需要自行实现反序列化逻辑，从 saveData 还原至 QuestManager 字段）
// _questManager.ActiveQuests = ...;
// _questManager.AvailableQuests = ...;
// _questManager.CompletedQuestIds = ...;
```

## 测试场景

运行 `res://src/scenes/quest_test/QuestTest.tscn` 来测试任务系统：

- **Q键** - 打开布告栏
- **L键** - 打开任务日志
- **ESC键** - 关闭窗口
- **模拟任务进度按钮** - 模拟完成任务目标

## 与其他系统的集成

### 与战斗系统集成

```csharp
// 在战斗结束时更新任务进度
void OnBattleEnd(Godot.Collections.Dictionary battleResult)
{
    var enemiesKilled = battleResult.GetValueOrDefault("enemies_killed", new Godot.Collections.Array());
    int count = enemiesKilled.AsGodotArray().Count;

    foreach (var quest in _questManager.ActiveQuests)
    {
        if (quest.questType == QuestData.QuestType.Extermination)
        {
            _questManager.UpdateQuestProgress(quest.QuestId, count);
        }
    }
}
```

### 与大地图系统集成

```csharp
// 在大地图上显示任务目标位置
void ShowQuestMarkers()
{
    foreach (var quest in _questManager.ActiveQuests)
    {
        var targetSite = _questManager.GetActiveTargetSite(quest.QuestId);
        if (targetSite != null)
        {
            // 在大地图上标记目标位置
            // var marker = CreateMapMarker(targetSite.WorldPosition);
            // marker.SetText(quest.QuestName);
        }
    }
}
```

### 与声望系统集成

```csharp
// 任务完成后更新势力关系
_questManager.QuestCompleted += (QuestData quest) =>
{
    if (!string.IsNullOrEmpty(quest.RewardFaction))
    {
        // factionSystem.AddReputation(quest.RewardFaction, quest.RewardReputation);
    }
};
```

## 扩展建议

1. **动态任务生成** - 根据玩家等级和位置动态生成任务
2. **任务链** - 实现多步骤的连续任务
3. **随机事件** - 任务过程中触发随机事件
4. **任务失败惩罚** - 失败后降低声望或其他惩罚
5. **任务评级** - 根据完成速度和方式给予不同评级
6. **每日任务** - 每天刷新的可重复任务
7. **势力任务** - 不同势力提供的专属任务

## 注意事项

1. QuestManager 由 OverworldScene 创建并管理生命周期，CombatScene 可通过 `GetParent().GetNodeOrNull<QuestManager>("QuestManager")` 跨场景查找
2. 任务模板和任务实例要区分清楚，使用 `(QuestData)quest.Duplicate()` 创建实例
3. 任务进度更新要在合适的时机调用，避免重复计数
4. 时间限制的游戏时间需要与实际游戏时间系统同步
5. 保存系统要确保所有任务状态都被正确序列化

## 未来计划

- [ ] 支持任务分支和多结局
- [ ] 实现任务追踪UI（HUD上的小窗口）
- [ ] 添加任务提示系统（箭头指向目标）
- [ ] 实现任务对话系统
- [ ] 支持隐藏任务和成就系统
- [ ] 添加任务统计和排行榜
