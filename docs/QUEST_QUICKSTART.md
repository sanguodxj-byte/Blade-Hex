# 任务系统快速入门

## 5分钟上手指南

### 1. 运行测试场景

打开Godot编辑器，运行：
```
res://src/scenes/quest_test/QuestTest.tscn
```

### 2. 基本操作

- **Q键** - 打开任务布告栏
- **L键** - 打开任务日志
- **ESC键** - 关闭当前窗口

### 3. 接取任务

1. 按Q键打开布告栏
2. 点击任务列表中的任务
3. 查看右侧的任务详情
4. 点击"接取任务"按钮

### 4. 查看进度

1. 按L键打开任务日志
2. 在"进行中"标签查看已接取的任务
3. 点击任务查看详细进度

### 5. 模拟完成

点击右上角的"模拟任务进度"按钮，每次点击会增加1点进度。

## 在你的项目中使用

### 最简单的集成方式

```csharp
using Godot;
using BladeHex.Strategic;
using BladeHex.Data;

public partial class MyNode : Node
{
    private QuestManager _questManager;

    public override void _Ready()
    {
        // 1. 创建任务管理器
        _questManager = new QuestManager();
        AddChild(_questManager);

        // 2. 设置玩家数据
        _questManager.PlayerGold = 500;
        _questManager.PlayerReputation = 10;

        // 3. 创建布告栏UI
        var questBoard = GD.Load<PackedScene>("res://src/ui/quest/QuestBoard.tscn").Instantiate<QuestBoard>();
        questBoard.Open(_questManager, _questManager.AvailableQuests);
        AddChild(questBoard);

        // 4. 显示布告栏
        questBoard.Visible = true;
    }
}
```

### 监听任务事件

```csharp
public override void _Ready()
{
    // 连接信号
    _questManager.QuestCompleted += OnQuestCompleted;
}

private void OnQuestCompleted(QuestData quest)
{
    GD.Print($"恭喜！完成任务：{quest.QuestName}");
    GD.Print($"获得金币：{quest.RewardGold}");
}
```

### 更新任务进度

```csharp
// 在战斗中击杀敌人时
void OnEnemyKilled()
{
    _questManager.UpdateQuestProgress("goblin_extermination_01", 1);
}
```

## 创建自定义任务

### 方法1：代码创建

```csharp
var quest = new QuestData
{
    QuestId = "my_quest",
    QuestName = "我的任务",
    Description = "这是一个测试任务",
    questType = QuestData.QuestType.Extermination,
    difficulty = QuestData.QuestDifficulty.Easy,
    TargetCount = 5,
    RewardGold = 100
};

// 添加到管理器
_questManager.QuestTemplates[quest.QuestId] = quest;
_questManager.AvailableQuests.Add((QuestData)quest.Duplicate());
```

### 方法2：资源文件

1. 在Godot中右键点击文件系统
2. 新建 → Resource
3. 选择 QuestData（已通过 `[GlobalClass]` 注册）
4. 在检查器中填写任务属性
5. 保存为 .tres 文件

## 常见问题

### Q: 如何让任务在战斗后自动完成？

```csharp
void OnBattleEnd()
{
    // 假设这是一个讨伐任务
    string questId = "goblin_extermination_01";
    int enemiesKilled = 8;
    _questManager.UpdateQuestProgress(questId, enemiesKilled);
}
```

### Q: 如何添加时间限制？

```csharp
quest.HasTimeLimit = true;
quest.TimeLimitDays = 5;  // 5天内完成
```

### Q: 如何设置前置条件？

```csharp
// QuestData 字段
quest.RequiredReputation = 20;  // 需要20点声望
quest.RequiredQuests = new[] { "quest_01" };  // 需要先完成 quest_01
```

### Q: 如何保存任务进度？

```csharp
// 保存
var saveData = _questManager.Serialize();
var file = FileAccess.Open("user://quest_save.dat", FileAccess.ModeFlags.Write);
file.StoreVar(saveData);
file.Close();

// 加载（反序列化需要自行还原至 QuestManager 字段）
var file2 = FileAccess.Open("user://quest_save.dat", FileAccess.ModeFlags.Read);
var loadedData = file2.GetVar().AsGodotDictionary();
file2.Close();
// 从 loadedData 恢复 ActiveQuests / AvailableQuests / CompletedQuestIds 等
```

## 下一步

查看完整文档：`docs/quest_system_guide.md`
