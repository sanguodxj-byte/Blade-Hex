# 装备图标生成流水线

## 概述

使用 gpt-image-2 生成 4x4 sprite sheet，裁剪为单独图标，AI 去背。

## 环境依赖

- Python 3.11 + PIL/Pillow + scipy + numpy + rembg + onnxruntime
- 代理: `http://127.0.0.1:7897`（mukyu API 需要）
- API: `https://imagegen.mukyu.me/v1/chat/completions`

## 流程

### 1. 生成 4x4 Sprite Sheet

**脚本**: `gen_helmet_sheet.py` / `gen_weapons.py` / `gen_all_armor.py`

**提示词模板**:
```
4x4 grid sprite sheet of [物品类型], 45-degree three-quarter view,
pure white background, thin black grid lines separating each cell,
bold black ink outlines, hard edges, cel-shaded flat coloring,
muted earthy metal tones. Each item centered in its cell with padding.
No text, no labels, game asset sprite sheet.
```

**关键点**:
- 要求白色背景 + 黑色网格线（帮助 AI 对齐位置）
- 生成后自动 resize 到 1024x1024（API 实际输出 1254x1254）
- 每张 sheet 输出到独立文件夹，禁止覆盖

### 2. 裁剪

**脚本**: `cut_sheet.py`

```bash
python cut_sheet.py <sheet.png> <output_dir> <name1> <name2> ... <name16>
# 用 _ 跳过空格
```

**做法**:
- 精确等分 1024÷4 = 256px per cell
- 每格内缩 4px 去掉网格线（248x248 → resize 回 256x256）
- 不做网格线检测（之前的检测方法会导致不均匀切割和变形）

### 3. 去背

**方案 A — 护甲/头盔（深色物体，无浅色高光）**:

使用 flood fill 从边缘去白（`remove_bg.py`）:
- 阈值 >180 判定为"亮色"
- 只去除与图片边缘相连的亮色区域
- 膨胀 2px 消除白边

```bash
python remove_bg.py <directory>
```

**方案 B — 武器（有金属高光/浅色部分）**:

使用 rembg AI 去背（`rembg_weapons.py`）:
- 基于 U2Net 模型，能正确识别前景物体
- 需要代理下载模型（首次运行）：`$env:HTTPS_PROXY='http://127.0.0.1:7897'`

```bash
python rembg_weapons.py
```

### 4. 清理噪点

**脚本**: `clean_alpha.py`

去背后可能残留孤立白点（小的不透明区域）。用连通域分析，只保留最大连通区域，删除面积 < 主体 1% 的碎片。

```bash
python clean_alpha.py <directory>
```

## 完整命令示例

```bash
# 生成武器
python gen_weapons.py

# 去背（需要代理环境变量）
$env:HTTPS_PROXY='http://127.0.0.1:7897'
python rembg_weapons.py

# 清理噪点
python clean_alpha.py "D:\123\Blade&Hex\assets\weapons\weapon_sheet1_output"
```

## 文件命名规则

- item ID `chain_mail` → ToPascalCase → `ChainMail` → 文件名 `ChainMail.png`
- 变体后缀 `_a`/`_b`/`_c`：ResourceRegistry 自动注册 base ID
- T2/T3 武器不需要单独图标，共用 T1 的图

## 输出目录

- 头盔: `assets/helmets/`
- 护甲/手套/鞋子: `assets/armor/`（各 sheet 有独立 `_output` 子目录）
- 武器: `assets/weapons/weapon_sheet{N}_output/`

## 已知问题

- gpt-image-2 通过 chat/completions 端点不支持指定输出尺寸，总是 1254x1254
- rembg 去背后仍可能有孤立白点，需要 `clean_alpha.py` 后处理
- 网格线检测裁剪不可靠（行列不均匀），改用精确等分
