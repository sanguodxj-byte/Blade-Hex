import os
from PIL import Image

def clean_file(path):
    if not os.path.exists(path):
        return
    img = Image.open(path).convert("RGBA")
    datas = img.getdata()
    newData = []
    
    # 只要像素接近黑色（RGB均值极低，或者R==0, G==0, B==0），就将其Alpha通道强行设为0
    for item in datas:
        r, g, b, a = item
        # 如果颜色极暗，说明是边缘残留或纯黑背景
        if (r < 15 and g < 15 and b < 15) or (a < 15):
            newData.append((0, 0, 0, 0))
        else:
            newData.append(item)
            
    img.putdata(newData)
    img.save(path, "PNG")

def main():
    split_dir = r"d:\123\Blade&Hex\extracted_ui\clock_variants_split"
    for i in range(16):
        file_path = os.path.join(split_dir, f"clock_variant_{i}.png")
        clean_file(file_path)
        print(f"Cleaned up residual pixels in {file_path}")

if __name__ == "__main__":
    main()
