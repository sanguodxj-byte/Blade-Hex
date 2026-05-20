# 构建 / 测试 / 模拟脚本

本目录提供统一入口脚本，**所有日常操作请通过这些脚本进行**，避免直接调用 `dotnet build BladeHexCore.csproj` 这类只构建单项目的命令。

## 为什么要这套脚本

`BladeHexCore` 与 `BladeHexFrontend` 是两个 C# 项目，frontend 通过 `<ProjectReference>` 引用 core。
Godot 启动时只加载 `BladeHexFrontend\.godot\mono\temp\bin\Debug\` 下的 DLL。
单独跑 `dotnet build BladeHexCore.csproj` 会把新 DLL 写到 core 自己的输出目录，但 frontend 输出目录里的 `BladeHexCore.dll` 副本不会更新——结果 Godot 加载到的仍是旧版本，看起来"代码改了但没生效"。

**统一脚本永远只构建 `BladeHexFrontend.csproj`**，让 ProjectReference 自己处理依赖、保证 frontend 输出目录里的 core DLL 永远是最新的。

## 入口脚本

| 命令 | 用途 |
|---|---|
| `build.bat` / `build.ps1` | 构建（默认 Debug） |
| `test.bat` / `test.ps1` | headless 单元测试 |
| `sim.bat` / `sim.ps1` | headless 大规模战斗 / AI 模拟 |
| `run.bat` / `run.ps1` | 启动游戏（带窗口） |

`.bat` 是 cmd 入口，内部调用 `.ps1`。命令行参数完全透传。

## Godot 可执行文件查找顺序

1. 命令行参数 `-GodotExe "C:\..."`
2. 环境变量 `$env:GODOT`
3. PATH 上的 `godot_console.exe` / `godot.exe`

推荐设置 `GODOT` 环境变量指向 `godot_console.exe`（headless 模式 stdout 才能正常显示）：

```powershell
[Environment]::SetEnvironmentVariable('GODOT', 'C:\Tools\Godot_v4.6.2-stable_mono_win64_console.exe', 'User')
```

## 常用流程

```cmd
:: 构建
build

:: 跑全套单元测试
test

:: 只跑地形分析
test -Mode terrain

:: 跑 1000 场战斗模拟，固定种子
sim -Battles 1000 -Seed 42

:: 跑战略层 AI 模拟，输出到文件
sim -Scenario overworld_ai -OutFile sim_log.txt

:: 启动编辑器（先自动构建）
run -Editor

:: 跑游戏（先自动构建）
run
```

## TEST_MODE 取值

`tools/scripts/test.ps1` 通过 `TEST_MODE` 环境变量驱动 `BladeHexCore/tests/TerrainTestRunner.cs`：

| Mode | 行为 |
|---|---|
| `unit` (默认) | 跑架构优化 R7 全套单元测试 |
| `terrain` | 地形生成分析 |
| `golden_record` | 记录 WorldPipeline 基线 hash |
| `golden_verify` | 校验 WorldPipeline 等价性 |
| `sim` | 跑 SimulationHarness（由 `sim.ps1` 调用） |

## 远期规划

- **服务端权威**：core 不依赖 Godot 运行时的部分（纯算法、纯数据）可以拆出去做 server。短期看，先把 `using Godot;` 收敛到必要的 `Resource`/`RefCounted`/`Vector2I` 等数据类型上，让 core 可以在不启 godot.exe 的情况下被普通 .NET test runner 加载。
- **CI**：`test.bat` / `sim.bat` 可直接给到 CI runner。返回非零即视为失败。
