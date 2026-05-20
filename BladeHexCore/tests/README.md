# BladeHexCore 测试套件

本目录提供 BladeHexCore 的回归与单元测试。  
项目沿用 Godot SDK（`Godot.NET.Sdk/4.6.2`），不引入 xUnit / NUnit；测试以纯静态类的形式由 `TerrainTestRunner` 这个 Node 在 Godot 内触发。

## 目录

```
tests/
├── TerrainTestRunner.cs      统一入口（按 TEST_MODE 环境变量切换）
├── Combat/
│   └── CombatRuleEngineTests.cs    伤害规则、暴击阈值、反击伤害
├── Map/
│   ├── HexOverworldAStarTests.cs   六边形大地图寻路
│   └── ChunkAStarTests.cs          跨 chunk 寻路、缓存一致性
├── Strategic/
│   ├── README.md                   Golden Seed 工作流（详见该文档）
│   ├── WorldHasher.cs              WorldData 序列化为 SHA256 hash
│   ├── WorldPipelineGoldenSeedTest.cs  WorldPipeline 等价性回归
│   ├── SaveSystemRoundtripTests.cs 存档数据 JSON 往返
│   └── TriggerEngineTests.cs       触发引擎前置条件、历史去重
├── Quest/
│   └── QuestGeneratorTests.cs      委托池刷新、接取、独立池
└── TerrainGenerationTest.cs        地形分布分析（已有）
```

## 运行方式

### 模式 1: 在 Godot 编辑器内

1. 打开 `BladeHexCore` 项目（注意：tests 目录的入口是一个 `Node`，需要挂在场景根节点）
2. 创建一个临时 `.tscn`，根节点 `Node`，附加脚本 `TerrainTestRunner.cs`
3. 在编辑器顶部菜单 → 项目 → 项目设置 → 设置环境变量 `TEST_MODE`，或直接修改代码默认值
4. 运行该场景

### 模式 2: --headless

```bash
godot --path . --headless BladeHexCore/tests/test_runner.tscn
```

设置 `TEST_MODE` 环境变量后再调用即可切换模式：

```powershell
# Windows PowerShell
$env:TEST_MODE = "unit"
& "C:\Path\To\Godot_v4.6.2-stable_mono_win64_console.exe" --path . --headless BladeHexCore/tests/test_runner.tscn
```

`TerrainTestRunner` 检测到 headless 后会自动 `GetTree().Quit()`，输出末尾会打印聚合统计（如 `TOTAL: 60 passed, 0 failed`）。

## 测试模式（TEST_MODE）

| 模式            | 行为                                     |
|-----------------|------------------------------------------|
| `terrain`（默认）| 运行 `TerrainGenerationTest.RunAnalysis` |
| `golden_record`  | 记录 WorldPipeline 的基线 hash         |
| `golden_verify`  | 验证 WorldPipeline 等价性               |
| `unit`           | 运行架构优化 R7 引入的全部单元测试     |
| `ui`             | UI 联通性测试（数据契约 + 信号接线）   |

## 编写新测试

模式约定（参照已有套件，例如 `CombatRuleEngineTests`）：

1. 创建静态类 `XxxTests`
2. 实现 `public static (int passed, int failed, List<string> details) RunAll()`
3. 内部用 `EnumerateTests()` 列出所有测试方法
4. 每个测试方法签名 `(bool ok, string failureMsg)`
5. 在 `TerrainTestRunner.RunAllUnitTests()` 中追加 `RunSuite(...)` 调用

## 不使用 xUnit 的原因

BladeHexCore 引用 `Godot.Vector2I` / `GD.Print` 等运行时类型。标准 .NET Test SDK 无法直接加载 Godot 程序集，需要复杂的 mock 层。使用 Godot 自带 Node 作为入口最低成本，且能在 CI 中通过 `godot --headless` 跑。
