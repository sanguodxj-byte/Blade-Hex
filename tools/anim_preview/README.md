# anim_preview

让视觉 AI 帮忙写 7 骨骼上半身骨骼动画的小工具集。

## 文件清单

| 文件 | 用途 |
|------|------|
| `preview_animation.py` | 把 `assets/animations/**/*.json` 渲染成关键帧拼图 PNG，给 AI 看 |
| `AI_PROMPT_TEMPLATE.md` | 给视觉 AI 的完整 prompt（含骨骼定义、坐标约定、schema、cheatsheet） |
| `README.md` | 本文件 |

## 依赖

```
pip install pillow
```

## 工作流

### 1. 第一次创建动画（AI 盲写）

a. 准备 1~N 张参考姿态图（截屏、手绘、AI 出图都行，按时间顺序排列）。
b. 把 `AI_PROMPT_TEMPLATE.md` 全文 + 参考图发给视觉 AI。
c. 把 `attack_melee.json` 完整粘进 prompt 末尾的 "已有样例" 占位符里，给 AI 一个数值风格锚。
d. AI 输出 JSON → 保存到 `assets/animations/<category>/<name>.json`。

### 2. 渲染预览图验证

```cmd
python tools\anim_preview\preview_animation.py assets\animations\slash\attack_melee.json
```

默认输出到同目录的 `<name>.preview.png`，每个关键帧一格横向拼接。

可选参数：

```cmd
:: 等距 12 fps 采样，看完整运动曲线
python tools\anim_preview\preview_animation.py assets\animations\slash\attack_melee.json --fps 12

:: 自定义输出路径
python tools\anim_preview\preview_animation.py assets\animations\slash\attack_melee.json -o D:\preview.png

:: 输出放大 1.5 倍
python tools\anim_preview\preview_animation.py assets\animations\slash\attack_melee.json --scale 1.5
```

### 3. 迭代修订

a. 把 **参考姿态图** + **当前 .preview.png** 一起发给 AI，附上 `AI_PROMPT_TEMPLATE.md` 末尾的 "迭代修订" 段。
b. AI 输出修订后的 JSON。
c. 重跑步骤 2，肉眼对比直到满意。

通常 2~3 轮迭代就能收敛。在 Godot 里跑一遍真实渲染做最终校验。

## 预览图说明

- 灰底带十字辅助线，地面是棕色矩形（底座）。
- 白色粗杆 = Torso，肤色圆 = Head。
- 蓝色 = 左臂（远离观察者那侧），红色 = 右臂（持武器侧）。
- 黄色细杆 = Weapon。
- 关节用白色圆点标记，便于 AI 识别。

预览图刻意做得抽象，目的是让 AI 关注骨骼几何而不是贴图细节，估角度更准。

## 局限

- 预览只画骨骼线条，**不渲染贴图**（角色不同尺寸贴图错位用真实 Godot 渲染才看得出）。
- 只支持 `rotation_z` + `position_x/y` + `scale_x/y`，不处理 `sprite_rotation`（业务上目前也没用）。
- `easing` 字段在 fps 等距采样下不会生效（脚本走线性插值），但关键帧模式下 easing 不影响呈现。
- 武器长度 `WEAPON_LEN=28` 是预览常量，与具体武器贴图无关。

## 与真实渲染的差异

预览脚本的旋转方向、骨骼层级、坐标约定都对照 `docs/32-上半身骨骼动画系统设计.md` 实现。但 Godot 端最终是 Sprite3D + Billboard FixedY，加上每件装备 `SortOffset` 决定遮挡顺序，AI 在数值估计上会有 ±5° 误差，**最后一定要在游戏里跑一遍**。
