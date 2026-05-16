# BladeHexCore.tests/Strategic

战略层（世界生成、寻路、存档）回归测试。

## Golden Seed 测试

服务于 [架构优化 spec R3 — WorldPipeline 重构](../../../.kiro/specs/architecture-optimization/) 的等价性回归。

### 文件

- `WorldHasher.cs`：将 `WorldData` 序列化为确定性 SHA256 hash（浮点量化、字典排序）
- `WorldPipelineGoldenSeedTest.cs`：3 个固定 seed 的 baseline 记录与验证

### 工作流程

#### 重构前（记录基线）

1. 在 Godot 编辑器中打开 `OverworldScene3D` 或任何能 `_Ready` 的场景
2. 临时在某处调用：
   ```csharp
   GD.Print(BladeHex.Tests.Strategic.WorldPipelineGoldenSeedTest.RecordBaseline());
   ```
3. 把控制台输出的三条 `["Small/seed=42"] = "..."` 粘到 `WorldPipelineGoldenSeedTest.BASELINES` 字典
4. 提交一次 commit："record golden seed baseline"

#### 重构期间（持续验证）

每改完一个 Stage 后调用：
```csharp
GD.Print(BladeHex.Tests.Strategic.WorldPipelineGoldenSeedTest.VerifyAll());
```

输出示例：
```
✓ Small/seed=42: PASS (a3f2b1c4...)
✓ Small/seed=1337: PASS (d8e7f6c5...)
✗ Small/seed=2025: FAIL
   expected: 1234567890abcdef...
   actual  : fedcba0987654321...
   分段诊断:
     Chunks       : a1b2c3d4...
     POIs         : <差异在这>
     Territories  : 9876fedc...
     SpecialChars : 5544aabb...
```

通过 `分段诊断` 可定位差异 Stage（Chunks ≠ TerrainStage 改坏了；POIs ≠ POIStage 改坏了，依此类推）。

### 为什么不用 xUnit？

BladeHexCore 是 Godot SDK 项目（`Godot.NET.Sdk`），引用 `Godot.Vector2I` / `GD.Print` 等运行时类型。
标准 .NET Test SDK 无法直接加载，需要复杂的 Godot mock 层。
当前简化方案：测试代码作为普通静态类放在 `tests/` 下，由开发者在 Godot 内手动触发。

如果未来需要自动化运行（CI），可考虑：
- 创建专用 `GoldenSeedRunner.cs` Node + 关联 .tscn
- 把 `.uid` 文件 commit 进版本库
- CI 脚本：`godot --headless <runner.tscn>`

### 浮点量化策略

`WorldHasher.QuantizeFloat`：`(int)Math.Round(f * 1000.0)`

这样可以容忍约 ±0.0005 的浮点误差，足够防止跨平台编译/运行时抖动，又足够敏感以发现真正的逻辑差异。
