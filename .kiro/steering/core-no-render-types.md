# Steering Rule: Core 禁渲染类型

## 规则

`BladeHexCore/**` 目录下的所有 C# 文件**不得出现**以下 Godot 渲染类型：

- `Texture2D`
- `SpriteFrames`
- `Material` / `StandardMaterial3D` / `ShaderMaterial`
- `Mesh` / `CylinderMesh` / `BoxMesh` 等
- `MultiMesh`
- `Viewport`
- `Node` / `Node3D` / `Control` 及其子类

## 原因

Core 层是纯逻辑、纯数据层，不应依赖 Godot 渲染引擎。所有渲染资源通过 `string IconId` / `string SpriteFramesId` 在 Core 层引用，在 View 层由 `ResourceRegistry` 运行时解析。

## 违反处理

1. 发现 `BladeHexCore/**` 中出现渲染类型引用 → **立刻修正**（搬迁到 `BladeHexFrontend` 或替换为 string ID）。
2. 若搬迁会导致临时编译失败（如 GDScript 引用），在 `BladeHexCore` 内创建一个 `[GlobalClass]` 桥接类驻留在 `BladeHexFrontend`，待 GDScript 迁移后删除。

## 例外

- `Godot.Collections.Dictionary` / `Godot.Collections.Array` — 允许用于 GDScript 互操作
- `Godot.Vector2` / `Godot.Vector3` / `Godot.Color` 等数学类型 — 允许（纯数据结构）
- `Godot.Resource` / `Godot.RefCounted` / `Godot.Node` 继承的**基类** — 仅当类本身是纯数据模型时允许，不持渲染字段
