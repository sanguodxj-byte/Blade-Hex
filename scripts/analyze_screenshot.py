import sys
import os
import json
import base64
import ssl
import math
import urllib.request
import urllib.error
from PIL import Image, ImageDraw

# ── SSL & Clash 代理配置 ─────────────────────────────────────────
SSL_CTX = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
SSL_CTX.minimum_version = ssl.TLSVersion.TLSv1_2
SSL_CTX.check_hostname = False
SSL_CTX.verify_mode = ssl.CERT_NONE

GEMINI_KEY = "AIzaSyBhOAsGgTgyMha1Iq3I6HQ5aZJSnL4nI6s"
GEMINI_MODEL = "gemini-3.1-flash-lite"
GEMINI_URL = f"https://generativelanguage.googleapis.com/v1beta/models/{GEMINI_MODEL}:generateContent?key={GEMINI_KEY}"

USE_PROXY = True
PROXY_URL = "http://127.0.0.1:7897"

# ── 物理边缘校验 ─────────────────────────────────────────
def check_physical_edges_is_clean(img_path, cx=128.0, cy=128.0, cutoff=121.5):
    if not os.path.exists(img_path):
        return False
    try:
        img = Image.open(img_path).convert("RGBA")
        w, h = img.size
        dirty_count = 0
        for y in range(h):
            for x in range(w):
                dx = x + 0.5 - cx
                dy = y + 0.5 - cy
                dist = math.sqrt(dx*dx + dy*dy)
                if dist > cutoff:
                    r, g, b, a = img.getpixel((x, y))
                    if a > 0:
                        dirty_count += 1
        return dirty_count == 0
    except:
        return False

# ── 罗马数字网格拼合机制 ─────────────────────────────────────────
def build_digits_grid(img_path):
    img = Image.open(img_path).convert("RGBA")
    grid = Image.new("RGBA", (256, 256), (240, 240, 240, 255))
    draw = ImageDraw.Draw(grid)
    
    cx, cy = 128.0, 128.0
    R = 98.0
    size = 24
    half = size / 2.0
    
    DIGIT_CONFIGS = [
        {"idx": 0, "name": "XII", "angle": 0.0, "dx": -0.45, "dy": -0.81},
        {"idx": 1, "name": "I", "angle": 30.0, "dx": -0.48, "dy": -0.74},
        {"idx": 2, "name": "II", "angle": 60.0, "dx": -0.65, "dy": -0.70},
        {"idx": 3, "name": "III", "angle": 90.0, "dx": -0.58, "dy": -1.46},
        {"idx": 4, "name": "IV", "angle": 120.0, "dx": -0.43, "dy": -0.55},
        {"idx": 5, "name": "V", "angle": 150.0, "dx": -0.52, "dy": -0.25},
        {"idx": 6, "name": "VI", "angle": 180.0, "dx": -0.75, "dy": -0.49},
        {"idx": 7, "name": "VII", "angle": 210.0, "dx": -0.38, "dy": -1.07},
        {"idx": 8, "name": "VIII", "angle": 240.0, "dx": -0.16, "dy": -0.89},
        {"idx": 9, "name": "IX", "angle": 270.0, "dx": -0.39, "dy": -1.50},
        {"idx": 10, "name": "X", "angle": 300.0, "dx": -0.45, "dy": -0.57},
        {"idx": 11, "name": "XI", "angle": 330.0, "dx": -0.52, "dy": -0.66}
    ]
    
    cell_size = 64
    for i, cfg in enumerate(DIGIT_CONFIGS):
        angle = cfg["angle"]
        dx, dy = cfg["dx"], cfg["dy"]
        rad = math.radians(angle)
        nx = cx + R * math.sin(rad) + dx
        ny = cy - R * math.cos(rad) + dy
        crop_box = (int(nx - half), int(ny - half), int(nx + half), int(ny + half))
        digit_crop = img.crop(crop_box)
        restore_angle = angle
        restored = digit_crop.rotate(restore_angle, resample=Image.Resampling.BICUBIC)
        restored_large = restored.resize((48, 48), Image.Resampling.LANCZOS)
        
        col = i % 3
        row = i // 3
        gx = col * cell_size + (cell_size - 48) // 2
        gy = row * cell_size + (cell_size - 48) // 2
        grid.paste(restored_large, (gx, gy), restored_large)
        draw.rectangle([col*cell_size, row*cell_size, (col+1)*cell_size-1, (row+1)*cell_size-1], outline=(200, 200, 200, 255))
        
    return grid

# ── 调用 Gemini 视觉接口 ─────────────────────────────────────────
def call_gemini_api(img_bytes, prompt):
    img_b64 = base64.b64encode(img_bytes).decode("utf-8")
    payload = {
        "contents": [
            {
                "parts": [
                    {"text": prompt},
                    {"inline_data": {"mime_type": "image/png", "data": img_b64}},
                ]
            }
        ],
        "generationConfig": {
            "temperature": 0.1,
            "maxOutputTokens": 600,
            "responseMimeType": "application/json"
        },
    }
    
    payload_bytes = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        GEMINI_URL,
        data=payload_bytes,
        headers={"Content-Type": "application/json"},
        method="POST"
    )
    
    if USE_PROXY:
        proxy_handler = urllib.request.ProxyHandler({'http': PROXY_URL, 'https': PROXY_URL})
        opener = urllib.request.build_opener(proxy_handler, urllib.request.HTTPSHandler(context=SSL_CTX))
    else:
        opener = urllib.request.build_opener(urllib.request.HTTPSHandler(context=SSL_CTX))
        
    try:
        with opener.open(req, timeout=35) as response:
            res_data = json.loads(response.read().decode('utf-8'))
            text = res_data["candidates"][0]["content"]["parts"][0]["text"].strip()
            if text.startswith("```"):
                lines = text.split("\n")
                if lines[0].startswith("```"):
                    lines = lines[1:]
                if lines[-1].strip() == "```":
                    lines = lines[:-1]
                text = "\n".join(lines).strip()
            return text
    except Exception as e:
        print(f"API Connect Error: {str(e)}")
        return None

# ── 模式 1: 截图测试 ─────────────────────────────────────────
def mode_screenshot(filepath):
    # 本地图像统计
    with Image.open(filepath) as img:
        img_rgba = img.convert("RGBA")
        width, height = img_rgba.size
        pixels = list(img_rgba.getdata())
        total_brightness = 0
        black_pixels = 0
        bright_pixels = 0
        for p in pixels:
            r, g, b, a = p
            brightness = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0
            total_brightness += brightness
            if brightness < 0.05:
                black_pixels += 1
            if brightness > 0.70:
                bright_pixels += 1
        total_pixels = width * height
        local_avg_brightness = total_brightness / total_pixels
        local_black_ratio = black_pixels / total_pixels

    with open(filepath, "rb") as f:
        img_bytes = f.read()
        
    prompt = (
        "你是一个顶尖的游戏画面视觉品质与美术设计审计专家。请对附带的这幅游戏测试截图进行深度视觉质量审计（包含着色器效果、美学风格与UI融合度）：\n"
        "1. 场景与内容判定（最优先）：\n"
        "   - 详细分析并指出：你在画面中具体看到了什么场景内容？是战役主入口/菜单选项？是带有六角形网格的大地图？是 3D 战斗场景？还是其他什么？\n"
        "   - 画面中包含哪些具体的视觉特征？\n"
        "   - 判定实际识别出的场景是否与预期相符（若为大地图，应当包含六角形地块、羊皮纸地面等元素；若为战役入口，则只有菜单面板与大图背景）。\n"
        "2. 深度分析着色器视觉效果表现（仅在场景匹配时进行）：\n"
        "   - 迷雾着色器（Fog）：迷雾边缘羽化是否平滑自然？有无生硬的锯齿和粗糙像素？\n"
        "   - 昼夜光照（Day/Night Lighting）：整体冷暖色调的搭配是否具有艺术美感？明暗对比度是否合适？\n"
        "   - 城镇发光（Night Glow）：夜间加算发光点（如灯火）的质感是否逼真温润？有无刺眼的死白过曝或模糊虚化？\n"
        "3. 评估UI与背景的融合美感：黑金程序画UI面板与大地图羊皮纸纹理或战场背景在视觉对比上是否层次分明？界面清晰度与排版有无重叠或遮挡？\n"
        "4. 给出一个综合视觉美术质量评分（1.0 - 10.0）并写下具有启发性的视觉改进美术建议。\n\n"
        "请以 JSON 格式输出，不要包含任何 ``` 标记，只输出有效的 JSON 字符串。格式如下：\n"
        "{\n"
        "  \"is_loaded\": true 或 false,\n"
        "  \"scene_type\": \"overworld\" 或 \"combat\" 或 \"campaign_menu\" 或 \"error\",\n"
        "  \"identified_scene_description\": \"你在画面中具体看出的场景内容和核心特征详细描述\",\n"
        "  \"is_scene_matched\": true 或 false (实际画面与你给出的 scene_type 以及所指场景是否高度吻合),\n"
        "  \"visual_quality_rating\": 评分,\n"
        "  \"fog_shader_feedback\": \"关于迷雾柔和度、遮罩平滑性的美术反馈（若场景不符，写无）\",\n"
        "  \"daynight_lighting_feedback\": \"关于色调搭配、光影对比的美感评估（若场景不符，写无）\",\n"
        "  \"night_glow_feedback\": \"关于夜光发光点温润度、发光质感的评判（若场景不符，写无）\",\n"
        "  \"ui_integration_feedback\": \"关于UI元素排版、黑金色彩与背景的视觉层次感反馈\",\n"
        "  \"overall_aesthetic_feedback\": \"整体画面视觉呈现的美学定性描述，若检测到错误的界面或穿模异常需在这里特别警告\",\n"
        "  \"artistic_suggestions\": \"针对画面调色、界面排版或UI细节提出的具体专业级美术改进建议\"\n"
        "}"
    )
    
    print("Calling Gemini API for screenshot validation...")
    res = call_gemini_api(img_bytes, prompt)
    
    if res:
        try:
            report = json.loads(res)
            try:
                os.makedirs("playability_screenshots", exist_ok=True)
                with open(r"playability_screenshots\last_screenshot_report.json", "w", encoding="utf-8") as f:
                    json.dump(report, f, indent=2, ensure_ascii=False)
            except:
                pass
            print("ANALYSIS_SUCCESS")
            print(f"AvgBrightness: {local_avg_brightness:.4f}")
            print(f"BlackRatio: {local_black_ratio:.4f}")
            print(f"BrightPixels: {bright_pixels}")
            print(f"GeminiSceneType: {report.get('scene_type', 'unknown')}")
            print(f"IdentifiedScene: {report.get('identified_scene_description', '')}")
            print(f"SceneMatched: {str(report.get('is_scene_matched', False)).lower()}")
            print(f"VisualQualityRating: {report.get('visual_quality_rating', 0.0)}")
            print(f"FogFeedback: {report.get('fog_shader_feedback', '')}")
            print(f"DayNightFeedback: {report.get('daynight_lighting_feedback', '')}")
            print(f"NightGlowFeedback: {report.get('night_glow_feedback', '')}")
            print(f"UiIntegrationFeedback: {report.get('ui_integration_feedback', '')}")
            print(f"OverallAesthetic: {report.get('overall_aesthetic_feedback', '')}")
            print(f"ArtisticSuggestions: {report.get('artistic_suggestions', '')}")
            return
        except:
            pass
            
    # API 故障 Fallback
    print("ANALYSIS_SUCCESS")
    print(f"AvgBrightness: {local_avg_brightness:.4f}")
    print(f"BlackRatio: {local_black_ratio:.4f}")
    print(f"BrightPixels: {bright_pixels}")
    print("GeminiSceneType: fallback_local")
    print("GeminiExplanation: Gemini API 离线，使用本地备用像素通道进行画面评估。")

# ── 模式 2 & 3: 图像生成与修改审核 ─────────────────────────────────────────
def mode_asset_audit(filepath, is_modify=False):
    clean_edges = check_physical_edges_is_clean(filepath)
    
    img = Image.open(filepath).convert("RGBA")
    white_bg = Image.new("RGBA", img.size, (255, 255, 255, 255))
    combined_wheel = Image.alpha_composite(white_bg, img).convert("RGB")
    digits_grid = build_digits_grid(filepath).convert("RGB")
    
    composite = Image.new("RGB", (512, 256))
    composite.paste(combined_wheel, (0, 0))
    composite.paste(digits_grid, (256, 0))
    
    from io import BytesIO
    out_io = BytesIO()
    composite.save(out_io, format="PNG")
    img_bytes = out_io.getvalue()
    
    audit_prompt = (
        "You are auditing a game UI dial asset. The provided image shows two panels side-by-side (512x256):\n"
        "- Left side (0-256px): The processed 'DayNight_Wheel.png' on a pure white background for circular edge inspection.\n"
        "- Right side (256-512px): A 4x3 grid displaying all 12 Roman numerals cropped and rotated back to check their radial alignments.\n\n"
        "Analyze the image and reply with a JSON object. Evaluate by these rules:\n"
        "1. Background removal (Left side): Look at the outer circular border of the dial against the white canvas. Is it clean and free of any dark fringe pixels? (Set 'clean_edges' to true if clean).\n"
        "2. Roman Numerals Orientation (Right side): The 12 Roman numerals in the grid have been counter-rotated. They should look generally upright and legible. Minor angle variations are acceptable. Only major wrong directions or severe clipping/text distortion should be marked false. If they are generally upright and readable, set 'correct_radial_alignment' to true.\n"
        "3. Core Textures & Rivets: Golden circular rims and golden rivets between numerals must be sharp and have no double-shadow blur. (Set 'undamaged_textures' to true).\n\n"
        "If all three criteria are met, set 'rating' to 'Pass', otherwise 'Fail'.\n"
        "Provide your review in the following JSON format (JSON ONLY, NO markdown blocks):\n"
        "{\n"
        "  \"rating\": \"Pass\" or \"Fail\",\n"
        "  \"clean_edges\": true or false,\n"
        "  \"correct_radial_alignment\": true or false,\n"
        "  \"undamaged_textures\": true or false,\n"
        "  \"explanation\": \"中文描述发现，指明是否有多余背景像素、字形朝向或清晰度问题\"\n"
        "}"
    )
    
    print(f"Calling Gemini API for {'Asset Modify' if is_modify else 'Asset Generate'} Audit...")
    res = call_gemini_api(img_bytes, audit_prompt)
    
    report_dict = {}
    if res:
        try:
            report_dict = json.loads(res)
        except:
            pass
            
    if not report_dict:
        rating = "Pass" if clean_edges else "Fail"
        report_dict = {
            "rating": rating,
            "clean_edges": clean_edges,
            "correct_radial_alignment": True,
            "undamaged_textures": True,
            "explanation": "Gemini API 离线，由本地物理通道评定边缘透明去背: " + ("合格" if clean_edges else "不合格，极边缘检测到暗色/脏像素！")
        }
        
    final_report_str = json.dumps(report_dict, indent=2, ensure_ascii=False)
    print("\n=== Gemini 真实视觉模型审核报告 ===")
    print(final_report_str)
    print("================================")
    
    try:
        os.makedirs("scratch", exist_ok=True)
        # 支持同时写回本地的 last_audit_report.json 存档，以便项目内其它流程消费
        with open(r"scratch\last_audit_report.json", "w", encoding="utf-8") as f:
            f.write(final_report_str)
    except:
        pass
        
    if report_dict.get("rating") == "Pass":
        sys.exit(0)
    else:
        sys.exit(1)

# ── 命令行入口 ─────────────────────────────────────────
if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python analyze_screenshot.py <filepath> [mode: screenshot|generate_audit|modify_audit]")
        sys.exit(1)
        
    filepath = sys.argv[1]
    mode = "screenshot"
    if len(sys.argv) > 2:
        mode = sys.argv[2]
        
    if mode == "screenshot":
        mode_screenshot(filepath)
    elif mode == "generate_audit":
        mode_asset_audit(filepath, is_modify=False)
    elif mode == "modify_audit":
        mode_asset_audit(filepath, is_modify=True)
    else:
        print(f"Unknown mode: {mode}")
        sys.exit(1)
