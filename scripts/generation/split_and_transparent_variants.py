import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageChops

def flood_fill_background(img_rgba):
    w, h = img_rgba.size
    # 255 表示保留，0 表示透明
    mask_data = bytearray([255] * (w * h))
    
    visited = set()
    # 从四个角落启动 BFS 泛洪
    queue = [(0, 0), (w-1, 0), (0, h-1), (w-1, h-1)]
    for q in queue:
        visited.add(q)
        
    while queue:
        x, y = queue.pop(0)
        mask_data[y * w + x] = 0 # 标记为背景
        
        # 遍历上下左右 4 邻域
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h:
                n_key = (nx, ny)
                if n_key not in visited:
                    r, g, b, a = img_rgba.getpixel(n_key)
                    # 泛洪阈值：如果像素 RGB 最大值小于 15，判定为外部背景黑色，防止渗入内部纹理
                    if max(r, g, b) < 15:
                        visited.add(n_key)
                        queue.append(n_key)
                        
    # 创建 Mask 图像并应用 to A 通道
    mask_img = Image.new("L", (w, h))
    mask_img.putdata(mask_data)
    
    # 边缘收缩 1 像素以平滑边缘，防止过度吞噬花纹细节（从 MinFilter(5) 改为 MinFilter(3)）
    eroded_alpha = mask_img.filter(ImageFilter.MinFilter(3))
    
    # 写入透明通道
    result = img_rgba.copy()
    result.putalpha(eroded_alpha)
    return result

def main():
    brain_dir = r"C:\Users\Administrator\.gemini\antigravity\brain\89c3bd04-76d9-4d09-9e68-d87aa775648e"
    src_sheet = os.path.join(brain_dir, "clock_frame_4x4_img2img_1780546636860.png")
    output_dir = r"d:\123\Blade&Hex\extracted_ui\clock_variants_split"
    os.makedirs(output_dir, exist_ok=True)
    
    if not os.path.exists(src_sheet):
        print(f"Error: {src_sheet} not found")
        return
        
    sheet = Image.open(src_sheet).convert("RGBA")
    sheet_w, sheet_h = sheet.size
    print(f"Sheet size: {sheet_w}x{sheet_h}")
    
    cell_size = 256
    rows, cols = 4, 4
    
    for r in range(rows):
        for c in range(cols):
            x_min = c * cell_size
            y_min = r * cell_size
            x_max = x_min + cell_size
            y_max = y_min + cell_size
            
            cell = sheet.crop((x_min, y_min, x_max, y_max))
            
            # 使用连续区域泛洪抠除背景
            rgba_cell = flood_fill_background(cell)
            
            # 自动检测真实表盘的包围框来做紧凑裁剪
            bg_black = Image.new("RGB", (cell_size, cell_size), (0, 0, 0))
            diff = ImageChops.difference(rgba_cell.convert("RGB"), bg_black)
            bbox = diff.getbbox()
            
            if not bbox:
                print(f"Warning: Could not detect boundary for cell R{r}C{c}")
                bbox = (16, 16, 240, 240)
                
            x0, y0, x1, y1 = bbox
            w_box = x1 - x0
            h_box = y1 - y0
            cx = x0 + w_box / 2.0
            cy = y0 + h_box / 2.0
            radius = max(w_box, h_box) / 2.0
            
            # 重新裁剪紧贴边界的图片并保持正方形
            final_box = (
                int(cx - radius),
                int(cy - radius),
                int(cx + radius),
                int(cy + radius)
            )
            final_box = (
                max(0, final_box[0]),
                max(0, final_box[1]),
                min(cell_size, final_box[2]),
                min(cell_size, final_box[3])
            )
            
            cropped_variant = rgba_cell.crop(final_box)
            # 缩放到 240x240 大小
            final_var = cropped_variant.resize((240, 240), Image.Resampling.LANCZOS)
            
            var_idx = r * cols + c
            out_path = os.path.join(output_dir, f"clock_variant_{var_idx}.png")
            final_var.save(out_path, "PNG")
            
            print(f"Processed and split: R{r}C{c} -> {out_path} (size: 240x240, transparent background)")

    print("\nAll 16 clock variants successfully split, floodfilled, and saved.")

if __name__ == "__main__":
    main()
