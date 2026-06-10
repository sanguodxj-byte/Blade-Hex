import os
import numpy as np
from PIL import Image

weapons_dir = r"d:\123\Blade&Hex\assets\weapons"
files = [f for f in os.listdir(weapons_dir) if f.endswith(".png")]

print(f"Analyzing {len(files)} weapon images...")

for filename in sorted(files):
    filepath = os.path.join(weapons_dir, filename)
    with Image.open(filepath) as img:
        img = img.convert("RGBA")
        arr = np.array(img)
        alpha = arr[:, :, 3]
        
        # 找到所有非透明像素的坐标
        y_indices, x_indices = np.where(alpha > 10)
        
        if len(x_indices) == 0:
            print(f"{filename}: Empty image")
            continue
            
        # 计算重心 (center of mass)
        cx = np.mean(x_indices)
        cy = np.mean(y_indices)
        
        # 拟合一条直线或者计算协方差以得出旋转倾斜度
        # 我们使用主成分分析 (PCA) 获取长轴的方向
        coords = np.vstack([x_indices - cx, y_indices - cy])
        cov = np.cov(coords)
        evals, evecs = np.linalg.eig(cov)
        
        # 最大特征值对应的特征向量就是主轴方向
        max_idx = np.argmax(evals)
        main_dir = evecs[:, max_idx]
        
        # 角度计算 (角度从x轴正方向逆时针旋转，这里换算成弧度/度数)
        angle_rad = np.arctan2(main_dir[1], main_dir[0])
        angle_deg = np.degrees(angle_rad)
        
        # 确定长轴方向上，哪个方向的包围框更远，以推导哪一端是头部，哪一端是手柄。
        # 我们可以通过分析 x 坐标和 y 坐标的极值。例如对于大多数剑，手柄在左下 (大x，大y? 还是小x，大y?)
        # 让我们把图像的四个象限(以中心 128,128 为原点)的非透明像素统计出来
        q1 = np.sum(alpha[0:128, 128:256] > 10) # 右上
        q2 = np.sum(alpha[0:128, 0:128] > 10)   # 左上
        q3 = np.sum(alpha[128:256, 0:128] > 10)  # 左下
        q4 = np.sum(alpha[128:256, 128:256] > 10) # 右下
        
        print(f"File: {filename:25} | Center: ({cx:.1f}, {cy:.1f}) | Q1..Q4: ({q1}, {q2}, {q3}, {q4}) | MainDirAngle: {angle_deg:.1f}")
