import os
from PIL import Image

def main():
    brain_dir = r"C:\Users\Administrator\.gemini\antigravity\brain\89c3bd04-76d9-4d09-9e68-d87aa775648e"
    clock_file = os.path.join(brain_dir, "new_clock_frame.png")
    
    if not os.path.exists(clock_file):
        print("Error: clock file not found")
        return
        
    img = Image.open(clock_file).convert("RGBA")
    w, h = img.size
    cx = w // 2
    
    print(f"Image size: {w}x{h}, center_x = {cx}")
    
    # 打印中轴线上不透明像素段
    runs = []
    current_run = None
    
    for y in range(h):
        p = img.getpixel((cx, y))
        is_opaque = p[3] > 10
        if is_opaque:
            if current_run is None:
                current_run = [y, y, p] # [start, end, first_pixel_color]
            else:
                current_run[1] = y
        else:
            if current_run is not None:
                runs.append(current_run)
                current_run = None
    if current_run is not None:
        runs.append(current_run)
        
    for i, r in enumerate(runs):
        print(f"Band {i}: Y={r[0]} to Y={r[1]}, height={r[1]-r[0]+1}, color at start={r[2]}")

if __name__ == "__main__":
    main()
