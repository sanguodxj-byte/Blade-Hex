---
inclusion: always
---

# 决策前必须搜索业界方案

## 强制规则

在做出任何技术方案、架构、算法、API 选择决定之前，**必须**先用 `remote_web_search` 搜索业界已有的成熟做法。

**禁止**：不搜索就凭直觉/记忆给出方案。

## 适用范围

- 渲染、shader、纹理、UV、网格 — 必搜
- 算法选型（路径、寻路、空间索引、生成）— 必搜
- 引擎/框架特定 API 的"正确用法"— 必搜
- 性能优化方案 — 必搜
- 游戏机制的实现模式 — 必搜
- 任何"我以为这样可以"的猜测性方案 — 必搜

## 搜索关键词原则

- 用具体的引擎/技术名（"godot"、"unity"、"unreal"）
- 加上具体问题描述（不要太泛）
- 优先找已知作品的实现（"civilization 6 hex"、"endless legend terrain"、"battle brothers map"）
- 优先找经典教程（"catlike coding"、"red blob games"）

## 反例

- 错：直接给出 shader 代码 → 试错 → 失败 → 改另一种 → 再失败
- 对：先搜 "godot hex grid texture seamless tiling" → 看 3~5 个结果 → 选已被验证的方案 → 实现

## 例外

只有在以下情况可以不搜：
- 单纯的语法问题（如何在 C# 里声明数组）
- 当前项目已有明确约定的代码风格调整
- 修复明显的 bug（如 null 检查、拼写错误）
