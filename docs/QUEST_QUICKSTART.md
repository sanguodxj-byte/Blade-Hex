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

```gdscript
extends Node

var quest_manager: QuestManager

func _ready():
    # 1. 创建任务管理器
    quest_manager = QuestManager.new()
    add_child(quest_manager)
    
    # 2. 设置玩家数据
    quest_manager.player_gold = 500
    quest_manager.player_reputation = 10
    
    # 3. 创建布告栏UI
    var quest_board = preload("res://src/ui/quest/QuestBoard.tscn").instantiate()
    quest_board.set_quest_manager(quest_manager)
    add_child(quest_board)
    
    # 4. 显示布告栏
    quest_board.show_board()
```

### 监听任务事件

```gdscript
func _ready():
    # 连接信号
    quest_manager.quest_completed.connect(_on_quest_completed)

func _on_quest_completed(quest: QuestData):
    print("恭喜！完成任务：", quest.quest_name)
    print("获得金币：", quest.reward_gold)
```

### 更新任务进度

```gdscript
# 在战斗中击杀敌人时
func on_enemy_killed():
    quest_manager.update_quest_progress("goblin_extermination_01", 1)
```

## 创建自定义任务

### 方法1：代码创建

```gdscript
var quest = QuestData.new()
quest.quest_id = "my_quest"
quest.quest_name = "我的任务"
quest.description = "这是一个测试任务"
quest.quest_type = QuestData.QuestType.EXTERMINATION
quest.difficulty = QuestData.QuestDifficulty.EASY
quest.target_count = 5
quest.reward_gold = 100

# 添加到管理器
quest_manager.quest_templates[quest.quest_id] = quest
quest_manager.available_quests.append(quest.duplicate_quest())
```

### 方法2：资源文件

1. 在Godot中右键点击文件系统
2. 新建 → Resource
3. 选择 QuestData
4. 在检查器中填写任务属性
5. 保存为 .tres 文件

## 常见问题

### Q: 如何让任务在战斗后自动完成？

```gdscript
func on_battle_end():
    # 假设这是一个讨伐任务
    var quest_id = "goblin_extermination_01"
    var enemies_killed = 8
    quest_manager.update_quest_progress(quest_id, enemies_killed)
```

### Q: 如何添加时间限制？

```gdscript
quest.has_time_limit = true
quest.time_limit_days = 5  # 5天内完成
```

### Q: 如何设置前置条件？

```gdscript
quest.required_reputation = 20  # 需要20点声望
quest.required_quests = ["quest_01"]  # 需要先完成quest_01
```

### Q: 如何保存任务进度？

```gdscript
# 保存
var save_data = quest_manager.save_quest_data()
var file = FileAccess.open("user://quest_save.dat", FileAccess.WRITE)
file.store_var(save_data)
file.close()

# 加载
var file = FileAccess.open("user://quest_save.dat", FileAccess.READ)
var save_data = file.get_var()
file.close()
quest_manager.load_quest_data(save_data)
```

## 下一步

查看完整文档：`docs/quest_system_guide.md`
