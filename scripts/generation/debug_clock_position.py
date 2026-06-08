import os
from PIL import Image, ImageDraw

def main():
    brain_dir = r"C:\Users\Administrator\.gemini\antigravity\brain\89c3bd04-76d9-4d09-9e68-d87aa775648e"
    clock_file = os.path.join(brain_dir, "new_clock_frame.png")
    bg_file = os.path.join(brain_dir, "ui_layout_preview_1920x1080.png")
    
    if not os.path.exists(clock_file) or not os.path.exists(bg_file):
        print("Error: Missing required files for overlay preview")
        return
        
    clock = Image.open(clock_file).convert("RGBA")
    clock_resized = clock.resize((180, 180), Image.Resampling.LANCZOS)
    
    wing_w, wing_h = 75, 45
    wing = Image.new("RGBA", (wing_w, wing_h), (0, 0, 0, 0))
    draw_w = ImageDraw.Draw(wing)
    draw_w.rounded_rectangle(
        [(0, 0), (wing_w - 1, wing_h - 1)],
        radius=2,
        fill=(20, 20, 26, 195),
        outline=(184, 148, 89, 165),
        width=1
    )
    
    # 测试较小的负 Y 偏移
    offsets = [-20, -30, -40, -50]
    
    for offset_y in offsets:
        bg = Image.open(bg_file).convert("RGBA")
        
        # 1. 贴时钟
        clock_x = (1920 - 180) // 2
        clock_y = offset_y
        bg.paste(clock_resized, (clock_x, clock_y), clock_resized)
        
        # 2. 贴两翼 (在屏幕中的绝对 Y 保持为 20)
        left_x = clock_x - 90
        bg.paste(wing, (left_x, 20), wing)
        
        right_x = clock_x + 180 + 15
        bg.paste(wing, (right_x, 20), wing)
        
        # 保存
        out_name = f"preview_composite_y_{offset_y}.png"
        bg.save(os.path.join(brain_dir, out_name), "PNG")
        print(f"Generated small offset preview with Y={offset_y} at {out_name}")

if __name__ == "__main__":
    main()
