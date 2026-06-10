import os
from PIL import Image

weapons_dir = r"d:\123\Blade&Hex\assets\weapons"
files = [f for f in os.listdir(weapons_dir) if f.endswith(".png")]

print(f"Total PNG files: {len(files)}")

# 选出几个代表性的武器
representatives = [
    "ArmingSword.png",      # Slash
    "BroadSpear.png",      # Thrust
    "BattleAxe.png",       # Slash
    "Club.png",            # Crush
    "Shortbow.png",        # Bow
    "LightCrossbow.png",   # Crossbow
    "ThrowingKnife.png",   # Throw
    "StaffOak.png",        # Catalyst
    "OrbBasic.png",        # Catalyst
    "WandArcane.png"       # Catalyst
]

for filename in representatives:
    filepath = os.path.join(weapons_dir, filename)
    if not os.path.exists(filepath):
        print(f"{filename} not found")
        continue
    with Image.open(filepath) as img:
        bbox = img.getbbox()
        print(f"File: {filename:25} | Size: {img.size[0]}x{img.size[1]} | BBox: {bbox} | Mode: {img.mode}")
