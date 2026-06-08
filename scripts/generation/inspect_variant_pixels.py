import os
from PIL import Image

def main():
    path = r"d:\123\Blade&Hex\extracted_ui\clock_variants_split\clock_variant_0.png"
    if not os.path.exists(path):
        print(f"Error: {path} not found")
        return
        
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    print(f"Inspecting {path} size: {w}x{h}")
    
    # 查找 A > 0 且 R, G, B 很低（接近黑色，比如 < 30）的边缘像素
    black_edge_pixels = []
    for y in range(h):
        for x in range(w):
            r, g, b, a = img.getpixel((x, y))
            if a > 0:
                # 检查是否非常黑且接近图像边缘（或者在四个方向的顶部/底部/最左/最右）
                is_black = (r < 40 and g < 40 and b < 40)
                # 靠近圆弧的四个方向顶点：
                # (cx, 0), (cx, h-1), (0, cy), (w-1, cy)
                cx, cy = w // 2, h // 2
                # 如果这个黑色像素在最外围
                dist_to_center = ((x - cx)**2 + (y - cy)**2)**0.5
                if is_black and dist_to_center > 110:
                    black_edge_pixels.append((x, y, (r, g, b, a)))
                    
    print(f"Found {len(black_edge_pixels)} dark pixels near the edge.")
    if black_edge_pixels:
        print("First 10 samples:")
        for p in black_edge_pixels[:10]:
            print(f"  Pixel at ({p[0]}, {p[1]}): Color={p[2]}")
            
if __name__ == "__main__":
    main()
