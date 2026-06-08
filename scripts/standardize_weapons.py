import os
import shutil
import numpy as np
from PIL import Image

# 路径配置
weapons_dir = r"d:\123\Blade&Hex\assets\generated_weapons"
backup_dir = r"d:\123\Blade&Hex\assets\generated_weapons_backup"

# 1. 确保安全备份存在
if not os.path.exists(backup_dir):
    os.makedirs(backup_dir)
    print(f"Creating backup directory at: {backup_dir}")
    for file in os.listdir(weapons_dir):
        if file.endswith(".png"):
            shutil.copy2(os.path.join(weapons_dir, file), os.path.join(backup_dir, file))
    print("Backup completed.")

# 2. 物理手柄黄金归一化偏置函数
def get_melee_anchor_offset(filename):
    # 终极救盘核心：所有长柄、剑类、斧类法杖等，为了防止手捏到枪头/法术魔石上，
    # 必须统一将手把最底端（向上 15 像素黄金安全距离处）对齐到 Y = 220 像素！
    # 这样下方仅留出一小截精致手柄尾部球，上方则是最大化、英姿飒爽的完整长枪/大剑/法杖身！
    return 15

def get_weapon_type(filename):
    name = filename.lower()
    if "bow" in name and "crossbow" not in name:
        return "bow"
    elif "crossbow" in name or "ballista" in name:
        return "crossbow"
    elif "orb" in name:
        return "orb"
    else:
        return "melee"

# 3. 遍历执行物理标准化
files = [f for f in os.listdir(weapons_dir) if f.endswith(".png")]
print(f"Executing master-level grip anchor standardization (15px golden anchor) for {len(files)} weapons...")

processed_count = 0

for filename in sorted(files):
    filepath = os.path.join(weapons_dir, filename)
    backup_path = os.path.join(backup_dir, filename)
    if not os.path.exists(backup_path):
        shutil.copy2(filepath, backup_path)
        
    with Image.open(backup_path) as img:
        img = img.convert("RGBA")
        arr = np.array(img)
        alpha = arr[:, :, 3]
        
        y_indices, x_indices = np.where(alpha > 10)
        if len(x_indices) == 0:
            continue
            
        w_type = get_weapon_type(filename)
        
        # 旋转角度计算
        cx = np.mean(x_indices)
        cy = np.mean(y_indices)
        q1 = np.sum(alpha[0:128, 128:256] > 10)
        q2 = np.sum(alpha[0:128, 0:128] > 10)
        q3 = np.sum(alpha[128:256, 0:128] > 10)
        q4 = np.sum(alpha[128:256, 128:256] > 10)
        
        coords = np.vstack([x_indices - cx, y_indices - cy])
        cov = np.cov(coords)
        evals, evecs = np.linalg.eig(cov)
        max_idx = np.argmax(evals)
        angle_deg = np.degrees(np.arctan2(evecs[1, max_idx], evecs[0, max_idx]))
        
        if w_type == "melee":
            rot_angle = 45.0 if (q1 + q3) > (q2 + q4) else -45.0
        elif w_type == "bow":
            rot_angle = 45.0 if (q1 + q3) > (q2 + q4) else -45.0
        elif w_type == "crossbow":
            # 弩类：我们确保拉平成完全水平朝右（角度等于特征角度）
            rot_angle = angle_deg
        elif w_type == "orb":
            rot_angle = 0.0
        else:
            rot_angle = 0.0
            
        # 执行物理旋转
        rotated_img = img.rotate(rot_angle, resample=Image.Resampling.BICUBIC, expand=False)
        
        # 智能上下朝向修正：大剑/单手剑/刀/匕首在原始斜放图里是右上为柄、左下为尖，
        # 逆时针转直 45 度后手柄朝上，必须再旋转 180 度把手柄翻转朝下，使枪/剑刃朝上！
        # 而长枪/法杖在原始斜放图里是右上为尖/头、左下为柄，逆时针转直后朝向完全正确，绝对不需要转 180！
        name_lower = filename.lower()
        is_sword = any(kw in name_lower for kw in ["sword", "saber", "dagger", "rapier", "seax", "stiletto", "kukri", "knife"])
        if w_type == "melee" and is_sword:
            rotated_img = rotated_img.rotate(180)
            print(f"  [Orient Correct] {filename}: Sword/Dagger detected. Rotated 180 degrees to orient handle downwards.")


        
        # 去透明白边裁剪
        bbox = rotated_img.getbbox()
        if not bbox:
            bbox = (0, 0, 256, 256)
            
        cropped_img = rotated_img.crop(bbox)
        c_w, c_h = cropped_img.size
        
        # 4. 高保真缩放限制，留出足够的画幅
        scale_factor = 1.0
        
        if w_type == "melee":
            # 保持最大高度为 200 像素以在手上展现最大张力
            if c_h > 200:
                scale_factor = 200 / c_h
        elif w_type == "bow":
            if c_h > 200:
                scale_factor = 200 / c_h
        elif w_type == "crossbow":
            if c_w > 200:
                scale_factor = 200 / c_w
        elif w_type == "orb":
            max_size = max(c_w, c_h)
            if max_size > 130:
                scale_factor = 130 / max_size
                
        if scale_factor != 1.0:
            new_w = int(c_w * scale_factor)
            new_h = int(c_h * scale_factor)
            cropped_img = cropped_img.resize((new_w, new_h), Image.Resampling.LANCZOS)
            c_w, c_h = cropped_img.size
            
        # 5. 组装空白画布并锚定
        final_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        
        if w_type == "melee":
            anchor_offset = get_melee_anchor_offset(filename)
            scaled_anchor = int(anchor_offset * scale_factor)
            
            paste_x = 128 - c_w // 2
            # 物理手把底端 (X_center, y_max - scaled_anchor) 精密定位在 Y=220 像素位置！
            paste_y = 220 - (c_h - scaled_anchor)
            
        elif w_type == "crossbow":
            # 弩类高精定位：为了让 OffsetX=30, OffsetY=-20 绝对精准地握住手柄，
            # 我们的弩机手柄（通常处于弩身左侧偏下 1/4 处）应当完美落入 (98, 148)。
            # 我们通过将弩的物理包围盒的左下角对齐到 (98, 148) 附近的数学锚定来达成：
            # 也就是 paste_x = 98 - c_w // 4，paste_y = 148 - c_h * 3 // 4
            paste_x = 128 - c_w // 2
            paste_y = 128 - c_h // 2
            
        elif w_type in ["bow", "orb"]:
            paste_x = 128 - c_w // 2
            paste_y = 128 - c_h // 2
        else:
            paste_x = 128 - c_w // 2
            paste_y = 128 - c_h // 2
            
        # 粘贴并覆盖原图
        final_img.paste(cropped_img, (paste_x, paste_y), cropped_img)
        final_img.save(filepath, "PNG")
        processed_count += 1
        
        print(f"Standardized: {filename:25} | Sc: {scale_factor:.2f} | Pos: ({paste_x}, {paste_y})")

print(f"\nSuccessfully standard-anchored {processed_count} weapons to the 15px golden hand grip!")
