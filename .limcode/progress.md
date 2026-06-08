# 战争闭环 MVP 实施进度 (.limcode/progress.md)

## 关联设计: `.limcode/design/war-system-mvp.md`
## 状态: 已完成 (M3 阶段 A/B/C 全量测试 207/207 Passed!)

---

## 任务进度快照

### 阶段 A: 战争状态实质化 (Core, 无 UI) — 100% 已完成
- [x] `#war-a1` 扩展 WarState 数据结构 (兼容旧存档反序列化)
- [x] `#war-a2` WarObjective 与 WarObjectivePlanner (每5天攻势POI动态刷新分配)
- [x] `#war-a3` PoiTransferService 与 PoiTransferred 事件 (收拢POI易手，触发全局重置与广播)
- [x] `#war-a4` InfluenceTracker (大地图影响力账户，支持Add/Spend/上限200)
- [x] `#war-a5` WarLordOrders + DecideLordArmy 接入 (领主战争AI主动行军与围攻)
- [x] `#war-a6` InfluenceTracker 接入战斗结果 (玩家击败敌军或加入攻防战时计算在场并加成)
- [x] `#war-a7` 战争闭环模拟脚本 (反射动态加载 headless 60 天长跑模拟)
- [x] `#war-a8` 阶段 A 构建验证 (构建编译通过 + 新增单元测试全数通过)

### 阶段 B: 玩家可介入 — 100% 已完成
- [x] `#war-b1` KingdomDecisionService (Core层宣战/媾和影响力消耗决策接口)
- [x] `#war-b2` 玩家所属国家解析 (PlayerNationResolver 声望最高且过阈值，带7天平滑稳定窗口)
- [x] `#war-b3` WarBattleJoinService (周边战事与围攻状态可参战机会精准检索)
- [x] `#war-b4` JoinBattlePrompt UI (毛玻璃风格大地图参战对话框 HUD 与帧查询挂接)
- [x] `#war-b5` 加入后战斗结果回写 (战后结算大地图应用：POI易手、领主败退、影响力派发)
- [x] `#war-b6` KingdomPanel UI (国家外交事务大厅，集成战分、当前战争列表、宣战媾和决策)
- [x] `#war-b7` WorldNewsPanel UI (大地图世界新闻大厅面板，支持未读红点、实时Toast弹窗订阅)
- [x] `#war-b8` 阶段 B 构建验证 (构建通过，跑通宣战 -> 领主出征 -> 玩家参战 -> 战后重塑 -> 外交媾和闭环)

### 阶段 C: 抛光与副作用闭环 — 100% 已完成
- [x] `#war-c1` POI 易手副作用之 RecruitPool (重置并生成占领国特有种族招募池)
- [x] `#war-c2` POI 易手副作用之 MarketStockService (繁荣度暴跌30，市场货架瞬间刷出新国贸易品)
- [x] `#war-c3` POI 易手副作用之 QuestManager (敌占任务冻结挂起失败，3日内光复重开，超期永久失效)
- [x] `#war-c4` POI 易手副作用之 FogOfWar (丢失聚落重盖迷雾遮蔽视野，光复重获大范围视野)
- [x] `#war-c5` 大地图视觉提示 (被围攻聚落 Mesh 红色呼吸闪烁，NPC领主头顶显示 `⚔️围攻`、`🏃溃退` 状态气泡)
- [x] `#war-c6` Debug 命令补完 (DebugConsole 整合 `war_declare`、`influence`、`capture` 等5大快捷秘籍命令)
- [x] `#war-c7` 60天完整模拟 (利用 LastRefreshDay=-999 机制解决首天与重置自动刷新问题，堵住招空立刻刷新的刷兵漏洞，并全量通过自动化断言)
- [x] `#war-c8` 阶段 C 构建验证 + Progress 更新 (207 passed 测试大满贯通过，progress登记归档)

### 阶段 M3.7: 大地图性能与 LOD 优化 (100% 已完成)
- [x] `#perf-a1` EntitySpatialIndex 数据结构实现 (二维网格位拼接 signed long 键)
- [x] `#perf-a2` EntitySpatialIndex 单元测试覆盖 (随机大样本绝对一致性断言)
- [x] `#perf-b1` OverworldEntityManager 接入空间网格与物理帧增量 Update
- [x] `#perf-c1` BattleResolver 交互检测空间化 (O(N^2) 降低为 O(N * k))
- [x] `#perf-c2` SiegeProcessor 回援检查网格邻域检索 (POI 周围 800px 圈定)
- [x] `#perf-c3` GetVisibleEntities 视野检索空间索引化与 100px 微动缓存
- [x] `#perf-c4` UpdateEntities 增量视觉管理与 EntityMeshPool 渲染池 (杜绝每帧动态分配)
- [x] `#perf-d1` EntityLod 字段与 EntityLodController 缓冲双向消抖控制
- [x] `#perf-d2` Hibernated 实体行动剪枝与每日离散直线推进 (跳过逐帧物理与 A* 规划)
- [x] `#perf-e1` WarObjectivePlanner 质心近似算法优化 (O(D * A) 降为 O(D + A))
- [x] `#perf-f1` EntityPerformanceBenchmark 开发与微秒级高频测试输出
- [x] `#perf-f2` Simulation_200Entities_60Days_Stable 稳定性长跑断言 (200实体大沙盒)
- [x] `#perf-g1` 阶段构建验证与 progress.md 登记完成 (252 passed)

---

## 下一步计划
1. **M3.5 军团与集结 (Army & Rallies)**: 引入多领主野外集结军团对中大型POI进行联合攻坚。
2. **M4 英雄与人际网络 (Hero Networks)**: 领主之间的恩怨情仇、派系斗争以及对国家决议的游说机制。
