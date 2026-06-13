# AI 骨骼动画生成 Prompt 模板

把这份完整内容连同**参考姿态图**一起发给视觉 AI（Claude / GPT‑4V / Gemini 等），它会输出一份合规的关键帧 JSON。

---

## ROLE

你是一位 2D 棋子风格游戏动画师，专门为 7 骨骼上半身角色（无下半身）K 帧。你的任务是看参考图，输出 **完全合法** 的关键帧 JSON，让游戏在 Godot 中直接播放。

## BONE RIG（固定不变，不要改名）

角色只有上半身，骨骼层级：

```
Torso          躯干（根，可平移、可前倾后仰）
├── Head       头（绕脖子转）
├── ArmL       左大臂（绕左肩转）
│   └── ForearmL  左前臂（绕左肘转）
└── ArmR       右大臂（绕右肩转）
    └── ForearmR  右前臂（绕右肘转）
        └── Weapon  武器挂载（绕右手腕转）
```

骨骼几何参数（像素，仅作直觉参考）：

| 名称 | 值 | 含义 |
|------|----|------|
| TorsoHeight | 48 | 肩到腰高度 |
| ShoulderWidth | 24 | 肩中到肩外侧 |
| UpperArmLen | 20 | 上臂长 |
| ForearmLen | 18 | 前臂长 |
| HeadOffset | 52 | 头中心相对躯干底部 Y |

## 坐标 / 角度约定（关键，别搞反）

- 角色面向屏幕，画面 +X 向右、+Y 向上。
- `rotation_z = 0` 表示该骨骼回到默认朝向：
  - **Torso/Head**：竖直向上
  - **ArmL/ArmR/ForearmL/ForearmR**：自然下垂，沿 -Y 方向
  - **Weapon**：沿前臂方向延伸（默认与前臂同向）
- `rotation_z` 单位：度。**正值 = 逆时针**（向左转），**负值 = 顺时针**（向右转）。
- 旋转是**相对父骨骼**的局部旋转。例：`ArmR.rotation_z = -90` 表示右大臂相对躯干向右抬到水平。
- `position_x / position_y` 单位：像素。一般只 Torso 用，用来表达整体冲刺/收缩。
- `scale_x / scale_y` 用于挤压拉伸（命中帧常用，例如 `1.1 / 0.9`）。
- `easing` 取值：`Linear` / `EaseIn` / `EaseOut` / `BackOut` / `ElasticOut`。冲击命中常用 `BackOut`。

## 数值参考区间（防止数值飞掉）

| 字段 | 常见范围 | 备注 |
|------|----------|------|
| Torso.rotation_z | -25 ~ +30 | 前倾后仰，超出会显得鬼畜 |
| Torso.position_x | -30 ~ +30 | 前冲/后撤位移 |
| Torso.position_y | -12 ~ +12 | 跳跃/下蹲 |
| Head.rotation_z | -15 ~ +15 | 头部摆动 |
| Arm*.rotation_z | -180 ~ +180 | 大臂可绕一圈 |
| Forearm*.rotation_z | -135 ~ +10 | 肘弯曲，正常不向后翻 |
| Weapon.rotation_z | -90 ~ +90 | 武器与前臂的相对偏转 |
| scale_x/y | 0.85 ~ 1.15 | 挤压拉伸 |

## OUTPUT SCHEMA（严格遵守）

```json
{
  "name": "<动画名，snake_case>",
  "weapon_category": "<slash|thrust|crush|bow|crossbow|catalyst|throw|unarmed|common>",
  "duration": 0.6,
  "loop": false,
  "keyframes": [
    {
      "time": 0.0,
      "bones": {
        "Torso":    { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" },
        "Head":     { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" },
        "ArmL":     { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" },
        "ForearmL": { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" },
        "ArmR":     { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" },
        "ForearmR": { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" },
        "Weapon":   { "rotation_z": 0.0, "position_x": 0.0, "position_y": 0.0, "sprite_rotation": 0.0, "scale_x": 1.0, "scale_y": 1.0, "easing": "Linear" }
      }
    }
  ]
}
```

**强制规则**：

1. 每个关键帧都要写满 7 根骨骼，缺一不可。
2. 第 1 帧（`time: 0.0`）和最后一帧 **必须回到中性姿态**（除非显式说明是循环动画的循环点）。
3. 关键帧时间从小到大排列，`duration` ≥ 最后一帧的 `time`。
4. 数值用浮点（写 `0.0` 而不是 `0`）。
5. 不要添加任何额外字段，不要写注释，输出 **纯 JSON**。

## 风格 CHEATSHEET（动作设计参考）

**slash（横劈）** windup → strike → recover：
- windup（约 t=0.15）：`Torso.rotation_z=-10`，`ArmR.rotation_z≈-90`，`ForearmR≈-35`，`Weapon≈-60`，整体能量集中在右后方。
- strike（约 t=0.35）：`Torso.rotation_z=+25`，前冲 `position_x≈+24`，`ArmR≈+65`，`Weapon≈+45`，挤压 `scale_x=1.1, scale_y=0.9`，`easing=BackOut`。
- recover（约 t=0.6）：全部归零。

**thrust（突刺）**：
- windup：Torso 微后仰，ArmR 朝后回收。
- strike：Torso 大幅 `position_x` 前冲，ArmR/ForearmR 拉直（接近 0），Weapon 与前臂同向。
- 重点是直线位移，旋转幅度小。

**crush（重砸）**：
- windup：ArmR 高举（`rotation_z` 正向，过头顶），躯干微下蹲（`position_y` 负值）。
- strike：ArmR 急速向下（`rotation_z` 大幅负向），Torso 前倾下沉，scale 强压扁。
- recover 慢一些，体现重量。

## 已有样例（slash/attack_melee.json）

```json
<!-- 这里附上你的现有 attack_melee.json 全文，让 AI 锚定数值风格 -->
```

## TASK

1. 我会附上 1~N 张参考姿态图（手绘、截屏、AI 出图都行）。
2. 第一张图 = 第一关键帧，最后一张图 = 最后关键帧；中间帧按图序对应。
3. 你需要：
   - 估计每张图对应的 `time`（按动作节奏，不必平均分）。
   - 估计每根骨骼的 `rotation_z`，必要时调整 `Torso.position_x/y` 和 `scale_x/y`。
   - 给命中/冲击帧设 `BackOut`，其他默认 `Linear`。
4. 直接输出符合上述 schema 的 JSON，**不要解释**。

---

# 迭代修订（Round 2+）

我会附两张图给你：

1. **REFERENCE**：原始参考姿态。
2. **CURRENT**：当前 JSON 渲染出的关键帧拼图（由 `tools/anim_preview/preview_animation.py` 生成）。

请只针对差异最大的骨骼修订 `rotation_z`，每次别动太多（±15° 内），并保留其他骨骼数值不变。再次输出完整 JSON。
