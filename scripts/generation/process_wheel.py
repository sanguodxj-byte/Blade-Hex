import os
from PIL import Image

def main():
    path = r"d:\123\Blade&Hex\src\assets\ui\DayNight_Wheel.png"
    if not os.path.exists(path):
        print("Project wheel not found")
        return
        
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    
    opaque_count = 0
    transparent_count = 0
    semi_count = 0
    
    # 检查非透明区域的平均 RGB，看是不是全黑的
    r_sum, g_sum, b_sum = 0, 0, 0
    
    for y in range(h):
        for x in range(w):
            r, g, b, a = img.getpixel((x, y))
            if a == 255:
                opaque_count += 1
                r_sum += r
                g_sum += g
                b_sum += b
            elif a == 0:
                transparent_count += 1
            else:
                semi_count += 1
                
    print(f"Image {path} stats:")
    print(f"  Dimensions: {w}x{h}")
    print(f"  Fully Opaque pixels (a=255): {opaque_count}")
    print(f"  Fully Transparent pixels (a=0): {transparent_count}")
    print(f"  Semi-transparent pixels (0<a<255): {semi_count}")
    
    if opaque_count > 0:
        avg_r = r_sum / opaque_count
        avg_g = g_sum / opaque_count
        avg_b = b_sum / opaque_count
        print(f"  Average color of opaque pixels: R={avg_r:.2f}, G={avg_g:.2f}, B={avg_b:.2f}")

if __name__ == "__main__":
    main()
