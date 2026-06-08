import os
from PIL import Image, ImageChops, ImageFilter

def main():
    brain_dir = r"C:\Users\Administrator\.gemini\antigravity\brain\89c3bd04-76d9-4d09-9e68-d87aa775648e"
    src_image = os.path.join(brain_dir, "media__1780545150728.png")
    output_image = os.path.join(brain_dir, "new_clock_frame.png")
    project_ui_dir = r"d:\123\Blade&Hex\src\assets\ui"
    project_output = os.path.join(project_ui_dir, "DayNight_Frame_New.png")
    
    if not os.path.exists(src_image):
        print(f"Error: {src_image} not found")
        return
        
    img = Image.open(src_image).convert("RGBA")
    
    # 自动寻找非白色的边界
    # 转换为 RGB 后计算差异，以避免 Alpha 通道全 0 导致 getbbox 返回 None
    bg = Image.new("RGB", img.size, (255, 255, 255))
    diff = ImageChops.difference(img.convert("RGB"), bg)
    bbox = diff.getbbox()
    
    if not bbox:
        print("Error: Could not detect the bounding box of the clock")
        return
        
    print(f"Detected bounding box: {bbox}")
    
    # 裁剪出时钟区域
    cropped = img.crop(bbox)
    w_crop, h_crop = cropped.size
    print(f"Cropped size: {w_crop}x{h_crop}")
    
    # 设为正方形
    size = max(w_crop, h_crop)
    square_img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    # 居中贴入
    offset_x = (size - w_crop) // 2
    offset_y = (size - h_crop) // 2
    square_img.paste(cropped, (offset_x, offset_y))
    
    # 抠除白色背景并做防白边处理（对接近白色的边缘像素计算透明度并修正混色）
    datas = square_img.getdata()
    newData = []
    for item in datas:
        r, g, b, a = item
        w = min(r, g, b)
        if w > 190:
            # 阈值从 190 到 255 线性过渡透明度
            alpha_factor = (255 - w) / (255 - 190)
            new_a = int(255 * alpha_factor)
            if new_a == 0:
                newData.append((0, 0, 0, 0))
            else:
                # 反向去底公式，修正白底混入的颜色，还原前景色
                new_r = max(0, min(255, int((r - (1 - alpha_factor) * 255) / alpha_factor)))
                new_g = max(0, min(255, int((g - (1 - alpha_factor) * 255) / alpha_factor)))
                new_b = max(0, min(255, int((b - (1 - alpha_factor) * 255) / alpha_factor)))
                newData.append((new_r, new_g, new_b, new_a))
        else:
            newData.append(item)
    square_img.putdata(newData)

    # 核心修改：收缩 2 像素边缘 (Erosion / MinFilter)
    # 提取 Alpha 通道，使用大小为 5 的最小值滤波器（等价于半径为 2 像素的收缩）
    alpha = square_img.getchannel("A")
    eroded_alpha = alpha.filter(ImageFilter.MinFilter(5))
    square_img.putalpha(eroded_alpha)
    
    # 保存结果到 brain 目录和项目目录
    square_img.save(output_image, "PNG")
    square_img.save(project_output, "PNG")
    print(f"Saved extracted new clock frame (eroded by 2px) to {output_image} and {project_output}")

if __name__ == "__main__":
    main()
