---
inclusion: fileMatch
fileMatchPattern: "**/Skeleton/**,**/Unit/**,**/animations/**,**/TextureScaleConfig*"
---

# 角色渲染架构 & 坐标系约定

## 渲染管线概览

```
素材纹理 (256px) → SubViewport 2D 画布 (512×640) → Sprite3D Billboard → 3D 正交相机 → 屏幕
                   ↑ 1:1 像素，不缩放              ↑ PixelSize=1.5
```

**设计原则**：
- 暗黑地牢风格：手绘部件 + 2D 骨骼动画
- 素材按目标尺寸制作，代码中 **不做缩放**（Scale = 1:1）
- SubViewport 解决 3D 深度排序闪烁问题
- Billboard (FixedY) 让角色始终面向相机

## 核心常量

| 参数 | 值 | 说明 |
|------|-----|------|
| SubViewport 尺寸 | 512×640 px | 角色画布 |
| CanvasCenter | (256, 544) | 画布底部 85%，角色脚底 |
| Billboard PixelSize | 1.5 (标准) / 2.0 (大型) | 每纹理像素 = 多少世界单位 |
| Billboard Offset | (0, 224) | 对齐脚底到 3D 原点 |
| SkeletonRoot Y | PedestalTopY × PixelSize = 60 | 3D 中骨骼根高度 |

## 坐标空间层级

### 1. SubViewport 2D 画布
- **坐标系**: X 右正，**Y 下正**（Godot 2D 标准）
- **BoneRoot** 位于 CanvasCenter (256, 544)

### 2. 骨骼本地坐标
```
BoneRoot (at CanvasCenter)
└── BoneTorso (0, 0)
    ├── BoneHead (0, -HeadOffsetY)        ← 负Y = 向上
    ├── BoneArmL (-ShoulderWidth, -ShoulderY)
    │   └── BoneForearmL (0, +UpperArmLength)  ← 正Y = 向下
    │       └── BoneShield
    ├── BoneArmR (+ShoulderWidth, -ShoulderY)
    │   └── BoneForearmR (0, +UpperArmLength)
    │       └── BoneWeapon
    └── Sprites (Body, Costume, Head, Helmet...)
```

### 3. BonePose 数据模型
- `RotationZ`: 正值 = 顺时针
- `PositionY` (Torso): 正值 = 向上。**应用时取反**: `node.Position.Y = -PositionY`
- `PositionY` (Weapon): 直接赋值到 Sprite2D.Offset.Y，**不取反**
- `PositionX` (Weapon): 直接赋值到 Sprite2D.Offset.X
- `SpriteRotation` (Weapon): 直接赋值到 Sprite2D.RotationDegrees

### 4. 纹理加载
- **不缩放**：`sprite.Scale = Vector2.One`
- 素材本身按正确尺寸制作（头盔 ~256px，武器 ~256×长条）
- `EquipmentOffsetConfig.Scale` 是用户手动微调的倍率，默认 1.0

### 5. SubViewport → 3D
- Billboard: `BillboardMode.FixedY`, `Centered = true`
- Offset 补偿: `(0, -(ViewportHeight/2 - ViewportHeight*0.85))` = `(0, 224)`
- SkeletonRoot 3D 位置: `(0, PedestalTopY * PixelSize, 0)`

### 6. Gizmo 屏幕投影（仅编辑器）
- `screenOffset = bone2DPos * PixelSize * worldToScreen`
- **Y 不取反**：2D Y↓ = 屏幕 Y↓，方向一致

## TextureScaleConfig 的用途

**仅用于投射物 (ProjectileView)**。装备纹理不再使用此配置。
- `GetProjectilePixelSize(type, texture)` — 计算投射物 Sprite3D 的 PixelSize
- 装备相关的 API (`GetEquipmentPixelSize`, `GetEquipmentTargetSize`) 已废弃，不应使用

## 关键规则

1. **纹理不缩放**：素材 1:1 显示在 SubViewport 内
2. **Torso Y 取反**：`node.Position.Y = -pose.PositionY`
3. **Weapon Offset 不取反**：直接赋值
4. **朝向翻转**：`facingLeft=false`(面朝右) → `Scale.X=1`(不翻转)，`facingLeft=true`(面朝左) → `Scale.X=-1`(翻转)。注：渲染输出整体镜像了一次，所以代码里 `Scale.X` 与字面方向相反——`SetFacing(false) → Scale.X = -1`，`SetFacing(true) → Scale.X = 1`。
5. **Billboard 无 X 镜像**：实测确认 2D 正 X = 屏幕右侧，不存在 billboard 镜像。
6. **Gizmo Y 不取反**：2D 和屏幕方向一致
7. **EquipmentOffsetConfig 在动画后叠加**：会覆盖 Weapon 的 Offset
8. **SubViewport 内是纯 2D**：与 3D 相机角度无关
9. **武器偏移按类别+动画名保存**：`weapon/{category}_{animName}.json`
