import os
from PIL import Image

def main():
    # 路径配置
    brain_dir = r"C:\Users\Administrator\.gemini\antigravity\brain\89c3bd04-76d9-4d09-9e68-d87aa775648e"
    src_image_path = os.path.join(brain_dir, "media__1780540042621.jpg")
    output_dir = os.path.join(brain_dir, "extracted_ui")
    os.makedirs(output_dir, exist_ok=True)
    
    if not os.path.exists(src_image_path):
        print(f"Error: Source image not found at {src_image_path}")
        return
        
    img = Image.open(src_image_path).convert("RGBA")
    w, h = img.size
    print(f"Loaded image size: {w}x{h}")
    
    # 针对 1024x576 分辨率的 UI 位置进行剪裁
    # 1. 顶部时间轮盘 (DayNight Clock)
    # 轮盘在顶端居中，大概宽度为 w * 0.1，高度约同等。
    clock_box = (
        int(w * 0.40),  # x_min
        int(0),         # y_min
        int(w * 0.60),  # x_max
        int(h * 0.28)   # y_max
    )
    img.crop(clock_box).save(os.path.join(output_dir, "clock_wheel.png"))
    
    # 2. 右下角资源底盒 (Resource Panel)
    resource_box = (
        int(w * 0.82),  # x_min
        int(h * 0.77),  # y_min
        int(w * 0.99),  # x_max
        int(h * 0.97)   # y_max
    )
    img.crop(resource_box).save(os.path.join(output_dir, "resource_panel.png"))
    
    # 3. 底部快捷按钮组 (Shortcut Buttons Area)
    buttons_box = (
        int(0),         # x_min
        int(h * 0.75),  # y_min
        int(w * 0.45),  # x_max
        int(h * 0.99)   # y_max
    )
    img.crop(buttons_box).save(os.path.join(output_dir, "shortcut_buttons_area.png"))
    
    # 4. 右上角小地图窗口 (Minimap Window)
    minimap_box = (
        int(w * 0.81),  # x_min
        int(h * 0.04),  # y_min
        int(w * 0.995), # x_max
        int(h * 0.52)   # y_max
    )
    img.crop(minimap_box).save(os.path.join(output_dir, "minimap_window.png"))
    
    # 5. 底部指南针按钮 (Bottom Compass)
    compass_box = (
        int(w * 0.45),  # x_min
        int(h * 0.83),  # y_min
        int(w * 0.55),  # x_max
        int(h * 0.99)   # y_max
    )
    img.crop(compass_box).save(os.path.join(output_dir, "bottom_compass.png"))
    
    print(f"UI extraction completed. Cropped images saved to {output_dir}")

if __name__ == "__main__":
    main()
