import os
from PIL import Image, ImageDraw

def create_checkerboard(width, height, cell_size=12):
    # 创建灰白相间的棋盘格背景，用于展示透明度
    img = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    draw = ImageDraw.Draw(img)
    for y in range(0, height, cell_size):
        for x in range(0, width, cell_size):
            if ((x // cell_size) + (y // cell_size)) % 2 == 1:
                draw.rectangle([x, y, x + cell_size - 1, y + cell_size - 1], fill=(230, 230, 230, 255))
    return img

def main():
    split_dir = r"d:\123\Blade&Hex\extracted_ui\clock_variants_split"
    brain_dir = r"C:\Users\Administrator\.gemini\antigravity\brain\89c3bd04-76d9-4d09-9e68-d87aa775648e"
    
    output_grid_path = os.path.join(brain_dir, "clock_variants_grid_preview.png")
    
    # 变体尺寸是 240x240
    cell_w, cell_h = 240, 240
    rows, cols = 4, 4
    
    grid_w = cell_w * cols
    grid_h = cell_h * rows
    
    # 创建棋盘格背景
    background = create_checkerboard(grid_w, grid_h, cell_size=16)
    
    for r in range(rows):
        for c in range(cols):
            var_idx = r * cols + c
            file_path = os.path.join(split_dir, f"clock_variant_{var_idx}.png")
            
            if not os.path.exists(file_path):
                print(f"Warning: {file_path} not found")
                continue
                
            var_img = Image.open(file_path).convert("RGBA")
            
            px = c * cell_w
            py = r * cell_h
            background.paste(var_img, (px, py), var_img)
            
    background.save(output_grid_path, "PNG")
    print(f"Successfully assembled 4x4 variants grid into: {output_grid_path}")

if __name__ == "__main__":
    main()
