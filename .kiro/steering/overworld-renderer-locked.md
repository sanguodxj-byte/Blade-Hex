---
inclusion: always
---

# 大地图渲染层已锁定

## 强制规则

以下文件 / 资产**已锁定**，未经用户明确授权禁止修改：

### Shader
- `src/assets/shaders/overworld_parchment.gdshader`

### 纹理资产
- `src/assets/tiles/tileable/parchment_tile.png`

### 渲染代码（仅大地图渲染相关部分）
- `BladeHexFrontend/src/View/Map/HexOverworldRenderer3D.cs`
  - `CreateGroundMaterial()`
  - `LoadParchmentTexture()`
  - `RebuildGround()` — 合并 mesh 与 UV 计算
  - 所有 shader 参数赋值（`texture_scale` / `hex_tile_size` / `blend_sharpness` / `use_rotation`）

### 纹理生成脚本
- `D:/123/gen_parchment_texture.py`
- `D:/123/make_seamless_parchment.py`

## 禁止行为

- 调整 shader 中的 uniform 默认值
- 修改 hex tiling / 采样 / 混合算法
- 替换为"更好的"方案（如 triplanar / detail map / 多层混合）
- 修改 UV 计算公式
- 修改纹理生成 prompt
- 重新运行纹理生成（调用付费 API）
- 删除或重写上述任何函数

## 例外条件

仅当用户**明确、直接、当前对话中**请求修改时才允许变更。例如：
- 用户："修改大地图羊皮纸纹理的色调"
- 用户："调整 hex tiling 的 hex_tile_size 参数"
- 用户："重新生成羊皮纸纹理"

不接受的"暗示性"理由：
- "我看到这里可以优化..."
- "我顺便改进一下..."
- "这个参数似乎不太合适..."
- "为了配合其他改动..."

## 用户原话

> 添加注释，禁止对大地图使用的shader和纹理做任何修改

这条规则反映了用户已经在视觉效果上完成了大量调试迭代，目前状态是稳定的可接受版本。任何"未授权的改进"都会破坏已稳定的效果，浪费用户时间。
