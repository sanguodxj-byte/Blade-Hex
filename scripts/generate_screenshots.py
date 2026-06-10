import os
import numpy as np
from PIL import Image, ImageDraw

base_dir = r"d:\123\Blade&Hex"
weapons_dir = os.path.join(base_dir, r"assets\weapons")

# 四款代表性测试武器
test_weapons = [
    {"filename": "ArmingSword.png", "cat": "slash", "offset_x": 0.0, "offset_y": -92.0, "rot": -20.0, "scale": 1.0, "fliph": False},
    {"filename": "Awlpike.png", "cat": "thrust", "offset_x": 0.0, "offset_y": -92.0, "rot": -25.0, "scale": 1.0, "fliph": False},
    {"filename": "HeavyCrossbow.png", "cat": "crossbow", "offset_x": 30.0, "offset_y": -20.0, "rot": 0.0, "scale": 1.0, "fliph": False},
    {"filename": "StaffOak.png", "cat": "catalyst", "offset_x": 0.0, "offset_y": -92.0, "rot": -15.0, "scale": 1.0, "fliph": False}
]

print("Starting offline emulator rendering to capture hand alignments...")

for w in test_weapons:
    filename = w["filename"]
    filepath = os.path.join(weapons_dir, filename)
    if not os.path.exists(filepath):
        print(f"File not found: {filename}")
        continue
        
    for anim in ["idle", "attack"]:
        # 加载标准化原图
        with Image.open(filepath) as img:
            img = img.convert("RGBA")
            
            # 1. 模拟 Godot 里的 Transform 嵌套变换
            # 在 256x256 空白画布上进行仿射变换
            # 挂接中心在 (128, 128)
            # 我们直接使用 Pillow 的仿射变换或手动拼装
            
            # 手关节手持旋转角
            if anim == "idle":
                hand_rot = -25.0
            else:
                if w["cat"] == "slash":
                    hand_rot = 45.0
                elif w["cat"] == "thrust":
                    hand_rot = 15.0
                elif w["cat"] == "crossbow":
                    hand_rot = -15.0
                else:
                    hand_rot = 0.0
            
            # 总旋转等于：手关节旋转 + 武器自身旋转
            total_rot = hand_rot + w["rot"]
            
            # 首先，处理 Offset 位移：
            # Sprite2D 的 Offset 默认相对于中心，所以中心移动到 (Offset_x, Offset_y)
            # 接着围绕手关节原点旋转 total_rot
            # 最终手关节原点对齐在视口中央 (128, 128)
            
            # 创建 256x256 空白画布
            canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
            
            # 在没有旋转前，我们把图片粘贴在中心 (128, 128)，并加上 Offset 偏置
            # 此时图片的几何中心在：(128 + Offset_x, 128 + Offset_y)
            # 粘贴位置的左上角是：中心 - 128
            # 所以 paste_x = 128 + Offset_x - 128 = Offset_x
            # paste_y = 128 + Offset_y - 128 = Offset_y
            
            # 我们可以直接对图片先做水平镜像（FlipH）
            temp_img = img
            if w["fliph"]:
                temp_img = temp_img.transpose(Image.FLIP_LEFT_RIGHT)
                
            # 我们将 temp_img 放置在一张 512x512 的超大临时画布上（以 (256, 256) 为关节挂接点），然后进行高精度的旋转
            temp_canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
            
            # 粘贴位置：挂接点加上 Offset 偏置。挂接点在 (256, 256)。
            # 所以图片的几何中心在：(256 + Offset_x, 256 + Offset_y)
            # 粘贴左上角为：(256 + Offset_x - 128, 256 + Offset_y - 128)
            paste_tx = int(256 + w["offset_x"] - 128)
            paste_ty = int(256 + w["offset_y"] - 128)
            
            temp_canvas.paste(temp_img, (paste_tx, paste_ty), temp_img)
            
            # 围绕挂接点 (256, 256) 进行逆时针/顺时针旋转
            # Pillow 的 rotate() 围绕中心旋转。我们的 temp_canvas 尺寸是 512x512，其几何中心刚好就是 (256, 256)！
            # 这太绝妙了！这能保证旋转中心正好就是手关节原点！
            # Pillow rotate 正数是逆时针旋转。在 Godot 中正数是顺时针旋转！
            # 所以在 Pillow 中，我们要传入： -total_rot 度的相反数，来实现完美的 Godot 顺时针旋转还原！
            rotated_canvas = temp_canvas.rotate(-total_rot, resample=Image.Resampling.BICUBIC)
            
            # 旋转后，我们把 512x512 的画布中心 (256, 256) 裁剪回 256x256（几何中心对齐到 (128, 128)）
            # 裁剪框范围：(256-128, 256-128, 256+128, 256+128) = (128, 128, 384, 384)
            canvas = rotated_canvas.crop((128, 128, 384, 384))
            
            # 2. 在画布上绘制手握点参考高亮标记：6x6 的半透明亮红色正方形，正中心在 (128, 128)
            draw = ImageDraw.Draw(canvas)
            draw.rectangle([125, 125, 131, 131], fill=(255, 0, 0, 200))  # 亮红色
            # 画一个微型黑色描边
            draw.rectangle([124, 124, 132, 132], outline=(0, 0, 0, 255))
            
            # 3. 保存截图
            save_path = os.path.join(base_dir, f"screenshot_{w['cat']}_{anim}.png")
            canvas.save(save_path, "PNG")
            print(f"Rendered and saved emulator screenshot: {save_path}")

print("\nEmulator screenshots generated successfully! Please inspect files screenshot_*.png at Repository root.")
