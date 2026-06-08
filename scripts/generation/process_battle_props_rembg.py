import os
import sys
import shutil
import numpy as np
from PIL import Image
from scipy import ndimage

# 19 种物体的生成配置 (ID, 核心Prompt, 是否为2:1高瘦比例)
PROPS_CONFIG = [
    # 纵向 2:1 高瘦比例
    ("tree_oak", True),
    ("tree_pine", True),
    ("tree_dead", True),
    ("banner_red", True),
    ("banner_blue", True),

    # 1:1 比例
    ("bush_green", False),
    ("bush_dry", False),
    ("rock_small", False),
    ("rock_large", False),
    ("rock_moss", False),
    ("barrel", False),
    ("crate", False),
    ("skull", False),
    ("fence_wood", False),
    ("fence_stone", False),
    ("poison_mushroom_prop", False),
    ("campfire", False),
    ("tent", False),
    ("grave", False)
]

def backup_dir(src, backup):
    if os.path.exists(src) and not os.path.exists(backup):
        print(f"Backing up: {src} -> {backup}")
        shutil.copytree(src, backup)
    elif os.path.exists(src):
        print(f"Backup already exists at {backup}, skipping backup.")

def remove_bg_rembg(img: Image.Image) -> Image.Image:
    from rembg import remove
    
    # 1. 运行 rembg 抠图
    rembg_out = remove(img)
    
    # 2. 转换成 numpy RGBA array
    d = np.array(rembg_out.convert("RGBA"))
    alpha = d[:, :, 3]
    
    # 3. 连通域滤波去噪（无白点）
    solid_mask = (alpha > 5)
    labeled, num_features = ndimage.label(solid_mask)
    if num_features > 1:
        sizes = ndimage.sum(solid_mask, labeled, range(1, num_features + 1))
        largest_label = int(np.argmax(sizes)) + 1
        max_size = sizes[largest_label - 1]
        
        # 仅保留面积大于最大连通域面积 1.5% 且大于 300 像素的连通域
        keep_threshold = max(300, int(max_size * 0.015))
        
        for label_idx in range(1, num_features + 1):
            if label_idx == largest_label:
                continue
            if sizes[label_idx - 1] < keep_threshold:
                d[labeled == label_idx, 3] = 0
                
    # 4. 主体内部二值填孔与半透明虚点精化（消除所有内部透明/半透明洞，保留真实边缘）
    alpha_cleaned = d[:, :, 3]
    # 使用最真实的 alpha > 0 作为实心主体，防止污染距离变换
    solid_mask_cleaned = (alpha_cleaned > 0)
    filled_mask_all = ndimage.binary_fill_holes(solid_mask_cleaned)
    
    # 提取所有真实闭合小孔洞（面积 < 100px），强制设为 255
    hole_mask = filled_mask_all & (~solid_mask_cleaned)
    labeled_holes, num_holes = ndimage.label(hole_mask)
    small_holes_mask = np.zeros_like(hole_mask)
    if num_holes > 0:
        hole_sizes = ndimage.sum(hole_mask, labeled_holes, range(1, num_holes + 1))
        if num_holes == 1:
            hole_sizes = [hole_sizes]
        for idx in range(1, num_holes + 1):
            if hole_sizes[idx - 1] < 100:
                small_holes_mask[labeled_holes == idx] = True
    d[small_holes_mask, 3] = 255
    
    # 最终的主体掩膜为真实主体 + 填平的微小孔洞
    filled_mask = solid_mask_cleaned | small_holes_mask
    
    # 距离变换：计算到真实背景（~filled_mask）的距离
    dist_to_bg = ndimage.distance_transform_edt(filled_mask)
    
    # 只要距离真实的外部背景 >= 2 像素的内部区域，其 Alpha 强制填充为 255
    # 这能消灭所有深部残留的半透明噪点、高光斑点
    internal_mask = dist_to_bg >= 2
    d[internal_mask & (d[:, :, 3] < 255), 3] = 255
    
    # 5. Matte Bleeding (RGB拉伸) 杜绝白边
    opaque_mask = (d[:, :, 3] == 255)
    if np.any(opaque_mask):
        indices = ndimage.distance_transform_edt(~opaque_mask, return_indices=True, return_distances=False)
        ny, nx = indices[0], indices[1]
        
        r = d[:, :, 0]
        g = d[:, :, 1]
        b = d[:, :, 2]
        d[:, :, 0] = r[ny, nx]
        d[:, :, 1] = g[ny, nx]
        d[:, :, 2] = b[ny, nx]
        
    return Image.fromarray(d)

def auto_crop(img: Image.Image, padding: int = 4) -> Image.Image:
    d = np.array(img.convert("RGBA"))
    a = d[:, :, 3]
    rows = np.any(a > 8, axis=1)
    cols = np.any(a > 8, axis=0)
    if not rows.any() or not cols.any():
        return img
    rmin, rmax = np.where(rows)[0][[0, -1]]
    cmin, cmax = np.where(cols)[0][[0, -1]]
    rmin = max(0, rmin - padding)
    rmax = min(d.shape[0] - 1, rmax + padding)
    cmin = max(0, cmin - padding)
    cmax = min(d.shape[1] - 1, cmax + padding)
    return img.crop((cmin, rmin, cmax + 1, rmax + 1))

def resize_to_fit_bottom(img: Image.Image, target_size: tuple, fill_ratio: float = 0.95) -> Image.Image:
    tw, th = target_size
    w, h = img.size
    
    max_w = int(tw * fill_ratio)
    max_h = int(th * fill_ratio)
    
    scale = min(max_w / w, max_h / h)
    new_w = int(w * scale)
    new_h = int(h * scale)
    
    resized = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    offset_x = (tw - new_w) // 2
    offset_y = th - new_h
    canvas.paste(resized, (offset_x, offset_y))
    return canvas

def process_sheet(sheet_path: str, prop_id: str, is_vertical: bool, out_dirs: list):
    print(f"Processing sheet: {sheet_path} for prop_id: {prop_id}")
    raw_img = Image.open(sheet_path)
    if raw_img.size != (1024, 1024):
        raw_img = raw_img.resize((1024, 1024), Image.Resampling.LANCZOS)
        
    cell_w, cell_h = 512, 512
    coords = [
        (0, 0, 512, 512),
        (512, 0, 1024, 512),
        (0, 512, 512, 1024),
        (512, 512, 1024, 1024)
    ]
    
    if is_vertical:
        target_size = (256, 512)
    else:
        target_size = (256, 256)
        
    for i, coord in enumerate(coords):
        cell_img = raw_img.crop(coord)
        
        # 高精 rembg 去背与后处理
        img = remove_bg_rembg(cell_img)
        # 边缘自适应裁剪
        img = auto_crop(img, padding=4)
        # 底部对齐缩放
        img = resize_to_fit_bottom(img, target_size, fill_ratio=0.92)
        
        # 保存到所有输出目录
        for out_dir in out_dirs:
            os.makedirs(out_dir, exist_ok=True)
            out_path = os.path.join(out_dir, f"{prop_id}_{i}.png")
            img.save(out_path, "PNG")
            print(f"  Saved -> {out_path}")

def main():
    root_dir = r"d:\123\Blade&Hex"
    raw_dir = os.path.join(root_dir, "scratch", "raw_props")
    
    out_dir_src = os.path.join(root_dir, "src", "assets", "props", "battle")
    out_dir_frontend = os.path.join(root_dir, "BladeHexFrontend", "src", "assets", "props", "battle")
    
    backup_dir_src = os.path.join(root_dir, "src", "assets", "props", "battle_backup")
    backup_dir_frontend = os.path.join(root_dir, "BladeHexFrontend", "src", "assets", "props", "battle_backup")
    
    # 1. 执行目录备份
    print(">>> 1. Backing up original directories <<<")
    backup_dir(out_dir_src, backup_dir_src)
    backup_dir(out_dir_frontend, backup_dir_frontend)
    
    # 2. 批量处理立牌
    print("\n>>> 2. Processing battle props with rembg pipeline <<<")
    success_count = 0
    for prop_id, is_vertical in PROPS_CONFIG:
        raw_path = os.path.join(raw_dir, f"raw_{prop_id}.png")
        if not os.path.exists(raw_path):
            print(f"WARNING: Raw image not found for {prop_id} at {raw_path}")
            continue
            
        try:
            process_sheet(raw_path, prop_id, is_vertical, [out_dir_src, out_dir_frontend])
            success_count += 1
        except Exception as e:
            print(f"ERROR: Failed to process prop {prop_id}: {e}")
            
    print(f"\n========================================\nFinished: {success_count}/{len(PROPS_CONFIG)} props processed.\n========================================")

if __name__ == "__main__":
    main()
