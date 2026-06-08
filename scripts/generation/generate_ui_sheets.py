from __future__ import annotations

import argparse
import base64
import io
import os
import re
import socket
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path

import requests
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "src" / "assets" / "ui"

LOCAL_API_URL = "http://127.0.0.1:17200/v1/images/generations"
LOCAL_API_KEY = "sk-klein-q-bdA7G5SelTQFtfvrvHl5ybZnzPvAyoX2RGzbVq"

MUKYU_API_URL = "https://imagegen.mukyu.me/v1/chat/completions"
MUKYU_API_KEY = "sk-jaDJDPxOVqrIpQjOITGtiYFQ3LyXWPQtfKhupSq4yNIBPjra"
MUKYU_PROXY = "http://127.0.0.1:7897"


@dataclass(frozen=True)
class UISheetSpec:
    key: str
    out: str
    prompt: str
    role: str


PROMPTS = (
    UISheetSpec(
        key="panel_sheets_2x6",
        out="overworld_hud_sheets_4x4.png",
        role="2x6 grid sprite sheet of medieval fantasy RPG game UI panels and frames.",
        prompt=(
            "2x6 grid sprite sheet of medieval fantasy RPG game UI panels and frames. "
            "Highly textured dark grey rock slate, vintage rustic wood, antique brass metal trims, "
            "intricate gold filigree, ornate borders, RPG game interface assets. "
            "Even layout, no text, no icons, dark beige parchment background, 1024x1024 total size."
        ),
    ),
    UISheetSpec(
        key="button_sheets_4x11",
        out="ui_buttons_sheet_8x8.png",
        role="4x11 grid sprite sheet of medieval fantasy RPG game buttons in multiple states.",
        prompt=(
            "4x11 grid sprite sheet of medieval fantasy RPG game buttons. "
            "Solid dark iron borders, shiny gold metal filigree embellishments, leather texture, stone carvings. "
            "The sheet contains rectangular buttons in various states: normal, hover, pressed, and disabled. "
            "Muted dark colors. No text, no labels, no icons, dark brown leather background, 1024x1024 total size."
        ),
    ),
)


def is_port_listening(port: int) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.settimeout(1.0)
        try:
            s.connect(("127.0.0.1", port))
            return True
        except Exception:
            return False


def start_daemon_engines() -> bool:
    print("=== Checking Local Image Engine Dependencies ===")
    
    # 1. 启动 Redis
    if not is_port_listening(6379):
        print("  [REDIS] Offline. Launching in independent new console...")
        redis_path = r"C:\Users\Administrator\Desktop\sub2\Redis-8.6.3-Windows-x64-msys2-with-Service\redis-server.exe"
        try:
            subprocess.Popen([redis_path], creationflags=subprocess.CREATE_NEW_CONSOLE)
            time.sleep(2)
        except Exception as e:
            print(f"  [REDIS] Failed to launch: {e}")
    else:
        print("  [REDIS] Online (6379)")

    # 2. 启动 MySQL
    if not is_port_listening(13306):
        print("  [MYSQL] Offline. Launching in independent new console...")
        mysql_path = r"D:\mysql\mysql-8.0.42-winx64\bin\mysqld.exe"
        try:
            subprocess.Popen(
                [
                    mysql_path,
                    "--basedir=D:\\mysql\\mysql-8.0.42-winx64",
                    "--datadir=D:\\mysql\\data",
                    "--port=13306",
                    "--console",
                ],
                creationflags=subprocess.CREATE_NEW_CONSOLE,
            )
            time.sleep(4)
        except Exception as e:
            print(f"  [MYSQL] Failed to launch: {e}")
    else:
        print("  [MYSQL] Online (13306)")

    # 3. 启动 gpt2api
    if not is_port_listening(17200):
        print("  [LOCAL_API] Offline. Launching in independent new console...")
        api_path = r"D:\gpt2api\backend\bin\openai.exe"
        cwd = r"D:\gpt2api\backend"
        
        env = os.environ.copy()
        env["KLEIN_DB_DSN"] = "root:@tcp(127.0.0.1:13306)/klein_ai?charset=utf8mb4&parseTime=True&loc=Asia%2FShanghai"
        env["KLEIN_REDIS_ADDR"] = "127.0.0.1:6379"
        env["KLEIN_REDIS_PASSWORD"] = ""
        env["KLEIN_JWT_SECRET"] = "aaaabbbbccccddddeeeeffffgggghhhh"
        env["KLEIN_JWT_REFRESH_SECRET"] = "hhhhggggffffeeeeddddccccbbbbaaaa"
        env["KLEIN_AES_KEY"] = "00000000000000000000000000000000"
        env["KLEIN_PROVIDER_GPT"] = "real"
        env["KLEIN_NODE_ID"] = "1"
        env["KLEIN_ENV"] = "local"
        
        try:
            subprocess.Popen([api_path], cwd=cwd, env=env, creationflags=subprocess.CREATE_NEW_CONSOLE)
            time.sleep(3)
        except Exception as e:
            print(f"  [LOCAL_API] Failed to launch: {e}")
    else:
        print("  [LOCAL_API] Online (17200)")

    print("\n=== Engine Status Check ===")
    r_ok = is_port_listening(6379)
    m_ok = is_port_listening(13306)
    a_ok = is_port_listening(17200)
    print(f"  Redis: {'ONLINE' if r_ok else 'OFFLINE'}")
    print(f"  MySQL: {'ONLINE' if m_ok else 'OFFLINE'}")
    print(f"  Local API: {'ONLINE' if a_ok else 'OFFLINE'}")
    return r_ok and m_ok and a_ok


def request_local_image(prompt: str, timeout: int) -> bytes:
    payload = {
        "model": "gpt-image-2",
        "prompt": prompt,
        "n": 1,
        "size": "1024x1024",
        "response_format": "b64_json",
    }
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {LOCAL_API_KEY}",
    }
    resp = requests.post(
        LOCAL_API_URL,
        json=payload,
        headers=headers,
        proxies={"http": None, "https": None},
        verify=False,
        timeout=timeout,
    )
    if resp.status_code != 200:
        raise RuntimeError(f"Local HTTP {resp.status_code}: {resp.text[:800]}")
    
    resp_data = resp.json()
    b64_json = resp_data.get("data", [{}])[0].get("b64_json", "")
    if b64_json:
        return base64.b64decode(b64_json)
    raise ValueError("b64_json not found in local API response")


def request_mukyu_image(prompt: str, timeout: int) -> bytes:
    payload = {
        "model": "gpt-image-2",
        "messages": [
            {
                "role": "user",
                "content": [{"type": "text", "text": f"Generate a 1024x1024 image: {prompt}"}],
            }
        ],
    }
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {MUKYU_API_KEY}",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)",
    }
    proxies = {"http": MUKYU_PROXY, "https": MUKYU_PROXY}
    
    resp = requests.post(
        MUKYU_API_URL,
        json=payload,
        headers=headers,
        proxies=proxies,
        verify=False,
        timeout=timeout,
    )
    if resp.status_code != 200:
        raise RuntimeError(f"Mukyu HTTP {resp.status_code}: {resp.text[:800]}")
    
    data = resp.json()
    content = data.get("choices", [{}])[0].get("message", {}).get("content", "")
    
    img_bytes = None
    if isinstance(content, str) and "data:image" in content:
        match = re.search(r"data:image/[^;]+;base64,([A-Za-z0-9+/=\s]+)", content)
        if match:
            b64 = match.group(1).replace("\n", "").replace(" ", "")
            img_bytes = base64.b64decode(b64)
    elif isinstance(content, list):
        for part in content:
            if isinstance(part, dict) and part.get("type") == "image":
                b64 = part.get("image", {}).get("data", "")
                if b64:
                    img_bytes = base64.b64decode(b64)
                    break
    if img_bytes is None and isinstance(content, str) and len(content) > 1000:
        try:
            img_bytes = base64.b64decode(content.strip())
        except Exception:
            pass
            
    if img_bytes:
        return img_bytes
    raise ValueError("Failed to extract image bytes from Mukyu API response")


def backup_existing_file(filepath: Path) -> None:
    if filepath.exists():
        backup_path = filepath.with_suffix(filepath.suffix + ".bak")
        try:
            if backup_path.exists():
                backup_path.unlink()
            filepath.rename(backup_path)
            print(f"Backed up existing file to: {backup_path}")
        except Exception as e:
            print(f"Warning: Failed to backup {filepath}: {e}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate UI Sheet assets for Blade & Hex via local or remote API.")
    parser.add_argument(
        "--api",
        choices=["local", "mukyu"],
        default="local",
        help="Which API to use (local gpt2api on 17200, or mukyu on 7897 proxy).",
    )
    parser.add_argument("--only", action="append", choices=["panel_sheets_2x6", "button_sheets_4x11"], help="Only generate specific spec.")
    parser.add_argument("--force", action="store_true", help="Force regenerate even if file exists.")
    parser.add_argument("--dry-run", action="store_true", help="Dry run, printing prompts without requesting API.")
    parser.add_argument("--timeout", type=int, default=300, help="API request timeout in seconds.")
    args = parser.parse_args()

    specs = list(PROMPTS)
    if args.only:
        specs = [s for s in PROMPTS if s.key in args.only]

    print(f"Target UI assets directory: {OUT_DIR}")
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    if args.api == "local" and not args.dry_run:
        # 尝试拉起本地引擎
        if not start_daemon_engines():
            print("[WARNING] Could not ensure all local API engines are online. Attempting generation anyway...")

    for spec in specs:
        out_path = OUT_DIR / spec.out
        print(f"\n--- Processing Spec: {spec.key} ---")
        print(f"Role: {spec.role}")
        print(f"Output Path: {out_path}")
        print(f"Prompt: {spec.prompt}")

        if out_path.exists() and not args.force:
            print(f"File already exists: {out_path}. Use --force to regenerate. Skipping.")
            continue

        if args.dry_run:
            print("[DRY-RUN] Request skipped.")
            continue

        backup_existing_file(out_path)

        try:
            if args.api == "local":
                print(f"Requesting from LOCAL API at {LOCAL_API_URL}...")
                img_bytes = request_local_image(spec.prompt, args.timeout)
            else:
                print(f"Requesting from MUKYU API at {MUKYU_API_URL} using proxy {MUKYU_PROXY}...")
                img_bytes = request_mukyu_image(spec.prompt, args.timeout)

            # 验证与写入
            if len(img_bytes) < 1000:
                print(f"[ERROR] Received invalid image data (too small: {len(img_bytes)} bytes)")
                continue

            # 用 PIL 打开以确保它是个合法的图像并 resize 到 1024x1024
            img = Image.open(io.BytesIO(img_bytes))
            if img.size != (1024, 1024):
                print(f"Resizing image from {img.size} to (1024, 1024)")
                img = img.resize((1024, 1024), Image.Resampling.LANCZOS)

            img.save(out_path, "PNG")
            print(f"[SUCCESS] UI Sheet generated and saved to: {out_path}")

        except Exception as e:
            print(f"[ERROR] Generation failed for {spec.key}: {e}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
