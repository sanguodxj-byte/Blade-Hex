import os
import sys
from PIL import Image, ImageDraw, ImageFont

base_dir = r"d:\123\Blade&Hex"
assets_ui_dir = os.path.join(base_dir, r"src\assets\ui")

# 代替原有的纹理九宫格拉伸，直接采用 Pillow 绘制《骑砍2》风格的极简“半透明黑铁 + 掐丝暗金”程序扁平样式
def draw_nine_slice(src_img, dest_size, margins):
    w_dst, h_dst = dest_size
    dst = Image.new("RGBA", dest_size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(dst)
    
    # 区分面板和按钮 (按钮大小通常较小)
    if w_dst < 150:
        # 按钮：85% 半透明深灰底 (13, 13, 15, 215) + 1px 极细暗金边框 (140, 115, 75, 100)
        draw.rounded_rectangle(
            [(0, 0), (w_dst - 1, h_dst - 1)],
            radius=1,
            fill=(13, 13, 15, 215),
            outline=(140, 115, 75, 100),
            width=1
        )
    else:
        # 面板（右下角属性底盒、时钟挂耳）：76% 半透明哑光黑铁底色 (20, 20, 26, 195) + 1px 极细掐丝古金色外框 (184, 148, 89, 165)
        draw.rounded_rectangle(
            [(0, 0), (w_dst - 1, h_dst - 1)],
            radius=2,
            fill=(20, 20, 26, 195),
            outline=(184, 148, 89, 165),
            width=1
        )
    return dst

print("Loading textures for UI pre-assembly...")

# 1. 尝试读取游戏大地图背景图，若无则创建一个米黄色地图占位
bg_path = os.path.join(base_dir, "ChatGPT Image 2026年6月2日 14_08_56.png")
if os.path.exists(bg_path):
    canvas = Image.open(bg_path).convert("RGBA").resize((1920, 1080), Image.Resampling.LANCZOS)
else:
    canvas = Image.new("RGBA", (1920, 1080), (210, 195, 145, 255)) # 沙地底色

# 2. 读取面板图集并切片 (Region: Rect2(12, 350, 488, 145))
panel_sheet_path = os.path.join(assets_ui_dir, "overworld_hud_sheets_4x4.png")
if not os.path.exists(panel_sheet_path):
    print("Error: overworld_hud_sheets_4x4.png not found!")
    sys.exit(1)

with Image.open(panel_sheet_path) as sheet:
    # 截取金丝面板
    raw_panel = sheet.crop((12, 350, 12 + 488, 350 + 145)).convert("RGBA")

# 3. 读取按钮图集并切片 (Region: Rect2(270, 218, 228, 72))
btn_sheet_path = os.path.join(assets_ui_dir, "ui_buttons_sheet_8x8.png")
if not os.path.exists(btn_sheet_path):
    print("Error: ui_buttons_sheet_8x8.png not found!")
    sys.exit(1)

with Image.open(btn_sheet_path) as sheet:
    raw_btn = sheet.crop((270, 218, 270 + 228, 218 + 72)).convert("RGBA")

# 4. 加载日夜轮盘
wheel_path = os.path.join(assets_ui_dir, "DayNight_Wheel.png")
frame_path = os.path.join(assets_ui_dir, "DayNight_Frame.png")
# 内轮盘缩小到 220x220 以防溢出外盖边缘
wheel = Image.open(wheel_path).convert("RGBA").resize((220, 220), Image.Resampling.LANCZOS)
frame = Image.open(frame_path).convert("RGBA").resize((240, 240), Image.Resampling.LANCZOS)

# 旋转底盘模拟下午 17:00
wheel_rotated = wheel.rotate(-255, resample=Image.Resampling.BICUBIC)

# ==========================================
# 组装 UI 元素到 1920x1080 画布
# ==========================================

# A. 顶部中心日夜时钟 + 左右两翼滚轮
clock_x = (1920 - 240) // 2
clock_y = -86
# 内盘与外盖同轴居中贴入 (240 - 220) // 2 = 10 像素偏移，防止偏心旋转晃动
wheel_x = clock_x + (240 - 220) // 2
wheel_y = clock_y + (240 - 220) // 2
canvas.paste(wheel_rotated, (wheel_x, wheel_y), wheel_rotated)
canvas.paste(frame, (clock_x, clock_y), frame)

# 左右两翼滚动卡片面板 (75x45) — 保持在屏幕绝对 Y 轴 5 像素
wing_panel = draw_nine_slice(raw_panel, (75, 45), (40, 40, 20, 20))
canvas.paste(wing_panel, (clock_x - 90, 5), wing_panel) # 左翼: 季节
canvas.paste(wing_panel, (clock_x + 240 + 15, 5), wing_panel) # 右翼: 天气

# B. 右下角悬浮资源框卡片 (280x130)
top_left_panel = draw_nine_slice(raw_panel, (280, 130), (40, 40, 20, 20))
canvas.paste(top_left_panel, (1620, 878), top_left_panel)

# C. 左下角快捷栏包裹底座面板及去背无字图标按钮
# 盒子底座 X=0, Y=984, 宽=336, 高=96
box_w, box_h = 336, 96
bottom_box = draw_nine_slice(raw_panel, (box_w, box_h), (40, 40, 20, 20))
canvas.paste(bottom_box, (0, 984), bottom_box)

# 4 个快捷键去背小图标 (部队、王国、领地、任务)
icons_list = ["icon_army.png", "icon_kingdom.png", "icon_territory.png", "icon_quest.png"]
btn_size = 80
box_padding = 8
btn_spacing = 0

for i, icon_name in enumerate(icons_list):
    icon_path = os.path.join(assets_ui_dir, icon_name)
    if os.path.exists(icon_path):
        with Image.open(icon_path) as img:
            icon_img = img.convert("RGBA").resize((btn_size, btn_size), Image.Resampling.LANCZOS)
            bx = 0 + box_padding + i * (btn_size + btn_spacing)
            by = 984 + box_padding
            canvas.paste(icon_img, (bx, by), icon_img)
    else:
        print(f"Warning: Icon {icon_name} not found at {icon_path}!")

# ==========================================
# 文字绘制与信息排版 (使用微软雅黑或系统默认字体)
# ==========================================
draw = ImageDraw.Draw(canvas)
try:
    font_path = r"C:\Windows\Fonts\msyh.ttc" # 微软雅黑
    font_body = ImageFont.truetype(font_path, 13)
    font_title = ImageFont.truetype(font_path, 14)
except:
    font_body = ImageFont.load_default()
    font_title = ImageFont.load_default()

# 1. 绘制右下角资源框文本 (X偏移1600, Y偏移858)
draw.text((1636, 890), "时间: 1250年 1月 2日", font=font_title, fill=(242, 216, 140, 255))
draw.text((1800, 890), "时钟: 17:00", font=font_body, fill=(217, 209, 199, 255))


draw.text((1636, 918), "1000 金币", font=font_title, fill=(242, 216, 140, 255))
draw.text((1750, 918), "口粮: 19/0", font=font_body, fill=(217, 209, 199, 255))

draw.text((1636, 940), "速度: 正常", font=font_body, fill=(217, 209, 199, 255))
draw.text((1750, 940), "士气: 正常", font=font_body, fill=(217, 209, 199, 255))

draw.text((1636, 960), "声望: 0", font=font_body, fill=(217, 209, 199, 255))
draw.text((1750, 960), "地形: 森林", font=font_body, fill=(217, 209, 199, 255))

# 2. 绘制时钟两翼的滚动值
# 左翼: 季节
draw.text((clock_x - 90 + 22, 17), "春季", font=font_title, fill=(242, 216, 140, 255))
# 右翼: 天气
draw.text((clock_x + 240 + 15 + 22, 17), "晴天", font=font_title, fill=(242, 216, 140, 255))

# 3. 绘制地区名称 (OffsetTop = 190)
draw.text(((1920 - len("雾霍洛") * 14) // 2, 190), "雾霍洛", font=font_title, fill=(242, 216, 140, 255))

# 3. 绘制快捷按钮上的文本（已重构为左下角无文字图标，无须绘制文本）
pass

# 4. 保存为 1920x1080 预览大图
save_path = os.path.join(base_dir, "ui_layout_preview_1920x1080.png")
canvas.save(save_path, "PNG")

print(f"\n[Assembly] UI pre-assembly completed successfully! Saved to: {save_path}")
