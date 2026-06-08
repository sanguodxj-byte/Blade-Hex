import os
from PIL import Image

def clean_outer_file(path):
    if not os.path.exists(path):
        return
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    cx, cy = w // 2, h // 2
    datas = img.getdata()
    newData = []
    
    # 仅清理圆盘半径（约 114 像素）外部最外圈边界的杂色暗像素
    # 圆盘内部（包括文字显示卡槽和天空盘）完全受保护，不受任何颜色阈值的影响
    for y in range(h):
        for x in range(w):
            idx = y * w + x
            item = datas[idx]
            r, g, b, a = item
            
            # 计算到圆心的距离
            dist = ((x - cx)**2 + (y - cy)**2)**0.5
            
            if dist > 114:
                # 处于极外圈边缘：如果是背景暗色（极黑，或者接近无色中性灰的暗色），则强行设为完全透明
                # 这样可以保留有色彩倾向的古铜暗色纹理（例如 R=28, G=21, B=10，其 max-min 差值较大）
                is_very_dark = (r < 15 and g < 15 and b < 15)
                is_neutral_dark = (max(r, g, b) < 40 and (max(r, g, b) - min(r, g, b)) < 8)
                if is_very_dark or is_neutral_dark or (a < 15):
                    newData.append((0, 0, 0, 0))
                else:
                    newData.append(item)
            else:
                # 处于表盘内侧：保留所有原始像素，禁止任何透明度修改
                newData.append(item)
                
    img.putdata(newData)
    img.save(path, "PNG")

def main():
    split_dir = r"d:\123\Blade&Hex\extracted_ui\clock_variants_split"
    for i in range(16):
        file_path = os.path.join(split_dir, f"clock_variant_{i}.png")
        clean_outer_file(file_path)
        print(f"Protected interior and cleaned outer border of {file_path}")

if __name__ == "__main__":
    main()
