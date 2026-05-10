# 任务/委托系统使用指南

## 概述

任务/委托系统是游戏战略层的核心玩法循环之一，实现了"村庄接委托 → 大地图行军 → 遭遇战斗 → 回村领赏"的完整流程。

## 系统架构

### 核心组件

1. **QuestData.gd** - 任务数据定义
   - 定义任务的所有属性（类型、难度、奖励等）
   - 包含任务状态管理和进度追踪
   - 支持时间限制和前置条件

2. **QuestManager.gd** - 任务管理器（单例）
   - 管理所有任务的生命周期
   - 处理任务接取、进度更新、完成和失败
   - 发放奖励和管理玩家数据

3. **QuestBoard.gd** - 布告栏UI
   - 显示可用任务列表
   - 展示任务详情
   - 处理任务接取

4. **QuestLog.gd** - 任务日志UI
   - 显示进行中和已完成的任务
   - 实时更新任务进度
   - 支持放弃任务

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

```gdscript
# 1. 添加QuestManager节点
var quest_manager = QuestManager.new()
add_child(quest_manager)

# 2. 设置玩家数据
quest_manager.player_gold = 500
quest_manager.player_reputation = 10

# 3. 创建UI并关联
var quest_board = preload("res://src/ui/quest/QuestBoard.tscn").instantiate()
quest_board.set_quest_manager(quest_manager)
add_child(quest_board)

var quest_log = preload("res://src/ui/quest/QuestLog.tscn").instantiate()
quest_log.set_quest_manager(quest_manager)
add_child(quest_log)
```

### 创建自定义任务

```gdscript
var quest = QuestData.new()
quest.quest_id = "custom_quest_01"
quest.quest_name = "自定义任务"
quest.description = "这是一个自定义任务的描述"
quest.quest_type = QuestData.QuestType.EXTERMINATION
quest.difficulty = QuestData.QuestDifficulty.MEDIUM
quest.target_count = 10
quest.reward_gold = 200
quest.reward_reputation = 10

# 添加到任务管理器
quest_manager.quest_templates[quest.quest_id] = quest
quest_manager.available_quests.append(quest.duplicate_quest())
```

### 更新任务进度

```gdscript
# 在战斗中击杀敌人时
func on_enemy_killed(enemy_type: String):
    # 查找相关任务并更新进度
    for quest in quest_manager.active_quests:
        if quest.quest_type == QuestData.QuestType.EXTERMINATION:
            quest_manager.update_quest_progress(quest.quest_id, 1)
```

### 监听任务事件

```gdscript
func _ready():
    quest_manager.quest_accepted.connect(_on_quest_accepted)
    quest_manager.quest_completed.connect(_on_quest_completed)
    quest_manager.quest_progress_updated.connect(_on_quest_progress_updated)

func _on_quest_accepted(quest: QuestData):
    print("接取任务: ", quest.quest_name)

func _on_quest_completed(quest: QuestData):
    print("完成任务: ", quest.quest_name)
    # 可以在这里触发特殊事件或解锁新内容

func _on_quest_progress_updated(quest: QuestData, progress: int):
    print("任务进度: %d/%d" % [progress, quest.target_count])
```

## 任务奖励系统

任务完成后会自动发放以下奖励：

1. **金币** - 直接添加到玩家金币
2. **声望** - 增加玩家在特定势力的声望
3. **物品** - 添加到玩家背包（需要背包系统支持）

```gdscript
# 奖励配置示例
quest.reward_gold = 300
quest.reward_reputation = 15
quest.reward_faction = "绿谷村"
quest.reward_items = ["potion_health", "scroll_fireball"]
```

## 前置条件系统

任务可以设置前置条件，只有满足条件才能接取：

```gdscript
# 声望要求
quest.required_reputation = 20

# 前置任务要求
quest.required_quests = ["quest_01", "quest_02"]

# 检查是否可接取
if quest.can_accept(player_reputation, completed_quest_ids):
    # 可以接取
    pass
```

## 时间限制系统

任务可以设置时间限制，超时后自动失败：

```gdscript
# 设置时间限制
quest.has_time_limit = true
quest.time_limit_days = 5  # 5天内完成

# 获取剩余时间
var remaining = quest.get_remaining_days(quest_manager.game_time)
print("剩余时间: %.1f天" % remaining)
```

## 保存/加载系统

任务系统支持保存和加载：

```gdscript
# 保存
var save_data = quest_manager.save_quest_data()
# 将save_data保存到文件

# 加载
quest_manager.load_quest_data(save_data)
```

## 测试场景

运行 `res://src/scenes/quest_test/QuestTest.tscn` 来测试任务系统：

- **Q键** - 打开布告栏
- **L键** - 打开任务日志
- **ESC键** - 关闭窗口
- **模拟任务进度按钮** - 模拟完成任务目标

## 与其他系统的集成

### 与战斗系统集成

```gdscript
# 在战斗结束时更新任务进度
func on_battle_end(battle_result: Dictionary):
    var enemies_killed = battle_result.get("enemies_killed", [])
    
    for quest in quest_manager.active_quests:
        if quest.quest_type == QuestData.QuestType.EXTERMINATION:
            quest_manager.update_quest_progress(quest.quest_id, enemies_killed.size())
```

### 与大地图系统集成

```gdscript
# 在大地图上显示任务目标位置
func show_quest_markers():
    for quest in quest_manager.active_quests:
        var marker = create_map_marker(quest.target_location)
        marker.set_text(quest.quest_name)
```

### 与声望系统集成

```gdscript
# 任务完成后更新势力关系
quest_manager.quest_completed.connect(func(quest: QuestData):
    if quest.reward_faction:
        faction_system.add_reputation(quest.reward_faction, quest.reward_reputation)
)
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

1. QuestManager应该作为单例使用，确保全局只有一个实例
2. 任务模板和任务实例要区分清楚，使用`duplicate_quest()`创建实例
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
