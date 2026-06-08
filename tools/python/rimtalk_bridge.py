#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
RimTalk 终端控制与 Gemini API 代理交接桥接器 (RimTalk Terminal Gemini Bridge)
充当本地 OpenAI 兼容的 API 端点。
它能够：
1. 原生桥接 Gemini API：通过配置 Gemini API Key，在后台自动调用 Gemini (我所代表的模型) 为小人生成对话。
2. 完美支持网络代理：内置 Clash 等本地代理转发，解决国内网络无法直连 Google API 的问题。
3. 终端交接控制：允许你在控制台随时按键切换为“手动回复模式”，直接在终端打字控制小人的发言。
"""

import os
import sys
import json
import time
import re
import urllib.request
import urllib.error
from http.server import HTTPServer, BaseHTTPRequestHandler

# ==================== 核心配置区 ====================
# 如果你想使用自动代理模式，请在此填入你的 Gemini API Key
# 申请地址：https://aistudio.google.com/ (完全免费)
GEMINI_API_KEY = ""  # 填入你的 sk-... 或 AIzaSy... 格式的 Key

# 网络代理配置（国内网络通常需要开启本地代理以连接 Google 服务）
USE_PROXY = True
PROXY_URL = "http://127.0.0.1:7897"  # 默认的 Clash / Starry / v2ray 端口

# 选用的 Gemini 模型名称
# 推荐：gemini-1.5-flash 或 gemini-2.5-flash
GEMINI_MODEL = "gemini-1.5-flash"

# 本地服务监听配置
DEFAULT_PORT = 17200
DEFAULT_HOST = "127.0.0.1"
# ====================================================

# 终端颜色代码
COLOR_RESET = "\033[0m"
COLOR_BOLD = "\033[1m"
COLOR_UNDERLINE = "\033[4m"
COLOR_RED = "\033[31m"
COLOR_GREEN = "\033[32m"
COLOR_YELLOW = "\033[33m"
COLOR_BLUE = "\033[34m"
COLOR_MAGENTA = "\033[35m"
COLOR_CYAN = "\033[36m"
COLOR_WHITE = "\033[37m"
BG_BLUE = "\033[44m"
BG_GREEN = "\033[46m"

# 启用 Windows 控制台 ANSI 支持
if sys.platform == "win32":
    try:
        import ctypes
        kernel32 = ctypes.windll.kernel32
        kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)
    except Exception:
        pass

# 状态映射字典
TRANSLATION_DICT = {
    "depressed": "沮丧", "happy": "快乐", "angry": "愤怒", "anxious": "焦虑",
    "exhausted": "精疲力竭", "hungry": "饥饿", "injured": "受伤", "pain": "疼痛",
    "eating": "正在进食", "sleeping": "正在睡觉", "working": "正在工作",
    "relaxing": "正在娱乐", "wandering": "正在闲逛", "fighting": "正在战斗",
    "socializing": "正在社交", "friend": "朋友", "rival": "对手",
    "spouse": "配偶", "lover": "恋人", "sibling": "兄弟姐妹"
}

def translate_word(word):
    lower_word = word.lower().strip(",.!?\"'")
    return TRANSLATION_DICT.get(lower_word, word)

def extract_pawn_info(system_prompt):
    """从 System Prompt 中提炼出角色核心信息"""
    info = {"name": "未知殖民者", "traits": [], "situation": "正常", "target": ""}
    
    name_match = re.search(r"(?:You are|Your name is)\s+([A-Za-z0-9_\s'-]+)(?:,|\.|\s+who)", system_prompt, re.IGNORECASE)
    if name_match:
        info["name"] = name_match.group(1).strip()
        
    traits_match = re.search(r"(?:Traits|Your traits)(?:\s*are)?:\s*\[?([A-Za-z0-9_\s,-]+)\]?", system_prompt, re.IGNORECASE)
    if traits_match:
        traits_raw = traits_match.group(1).split(",")
        info["traits"] = [translate_word(t.strip()) for t in traits_raw if t.strip()]

    activity_match = re.search(r"(?:Currently you are|Current activity|Activity):\s*([A-Za-z0-9_\s'-]+)(?:\.|\b)", system_prompt, re.IGNORECASE)
    if activity_match:
        info["situation"] = translate_word(activity_match.group(1).strip())
        
    target_match = re.search(r"(?:talking to|interacting with|socializing with)\s+([A-Za-z0-9_\s'-]+)", system_prompt, re.IGNORECASE)
    if target_match:
        info["target"] = target_match.group(1).strip()

    return info

def call_gemini_api(messages):
    """利用 urllib.request 标准库，通过官方 OpenAI 兼容接口向 Gemini 发生网络请求"""
    # 官方 Gemini OpenAI 兼容接口端点
    url = "https://generativelanguage.googleapis.com/v1beta/openai/v1/chat/completions"
    
    # 构造请求体，匹配 RimTalk 发送的模型格式，但强制修改为配置好的 Gemini 模型
    payload = {
        "model": GEMINI_MODEL,
        "messages": messages,
        "temperature": 0.7
    }
    
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {GEMINI_API_KEY}"
    }
    
    data = json.dumps(payload).encode('utf-8')
    
    # 配置网络代理
    opener = urllib.request.build_opener()
    if USE_PROXY and PROXY_URL:
        proxy_handler = urllib.request.ProxyHandler({'http': PROXY_URL, 'https': PROXY_URL})
        opener = urllib.request.build_opener(proxy_handler)
    
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    
    try:
        with opener.open(req, timeout=30) as response:
            res_data = json.loads(response.read().decode('utf-8'))
            return res_data["choices"][0]["message"]["content"]
    except urllib.error.HTTPError as e:
        error_msg = e.read().decode('utf-8')
        print(f"{COLOR_RED}❌ 调用 Gemini API 失败: HTTP {e.code} - {error_msg}{COLOR_RESET}")
        return None
    except Exception as e:
        print(f"{COLOR_RED}❌ 连接 Gemini API 发生错误: {str(e)}{COLOR_RESET}")
        return None

# 当前控制模式：'gemini' (由 Gemini 自动答复), 'manual' (在控制台手动打字交接)
CURRENT_MODE = "gemini" if GEMINI_API_KEY else "manual"

class RimTalkBridgeHandler(BaseHTTPRequestHandler):
    
    def log_message(self, format, *args):
        # 隐藏正常的 HTTP 请求输出，防止杂乱
        if "40" in format or "50" in format:
            sys.stderr.write("%s\n" % (format % args))

    def _send_json_response(self, status_code, data):
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode('utf-8'))

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()

    def do_GET(self):
        if self.path in ["/", "/healthz", "/v1/models"]:
            status_data = {
                "status": "online",
                "message": "RimTalk Terminal Gemini Bridge is active",
                "time": time.strftime("%Y-%m-%d %H:%M:%S"),
                "current_mode": CURRENT_MODE,
                "gemini_api_configured": bool(GEMINI_API_KEY)
            }
            self._send_json_response(200, status_data)
        else:
            self._send_json_response(404, {"error": "Not Found"})

    def do_POST(self):
        global CURRENT_MODE
        
        if not self.path.endswith("/v1/chat/completions"):
            self._send_json_response(404, {"error": f"Endpoint {self.path} not supported"})
            return

        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length)
        
        try:
            payload = json.loads(post_data.decode('utf-8'))
        except Exception as e:
            self._send_json_response(400, {"error": f"Invalid JSON payload: {str(e)}"})
            return

        messages = payload.get("messages", [])
        
        # 提炼对话背景
        system_prompt = ""
        user_message = ""
        chat_context = []
        
        for msg in messages:
            role = msg.get("role")
            content = msg.get("content", "")
            if role == "system":
                system_prompt = content
            elif role == "user":
                user_message = content
                chat_context.append(f"对方说: {content}")
            elif role == "assistant":
                chat_context.append(f"我说: {content}")

        pawn = extract_pawn_info(system_prompt)
        
        print("\n" + "=" * 65)
        print(f"{COLOR_BOLD}{BG_BLUE} 📥 RimTalk 对话事件已捕获 {COLOR_RESET} [模式: {COLOR_YELLOW}{CURRENT_MODE.upper()}{COLOR_RESET}]")
        print("=" * 65)
        print(f"{COLOR_CYAN}👤 角色名称:{COLOR_RESET} {COLOR_BOLD}{pawn['name']}{COLOR_RESET}")
        if pawn['traits']:
            print(f"{COLOR_CYAN}🧠 角色特质:{COLOR_RESET} {', '.join(pawn['traits'])}")
        print(f"{COLOR_CYAN}🔄 当前状态:{COLOR_RESET} {pawn['situation']}")
        if pawn['target']:
            print(f"{COLOR_CYAN}💬 交互目标:{COLOR_RESET} {COLOR_YELLOW}{pawn['target']}{COLOR_RESET}")
        print("-" * 65)
        
        if len(chat_context) > 1:
            print(f"{COLOR_BLUE}📜 对话背景/上下文:{COLOR_RESET}")
            for ctx in chat_context[:-1]:
                print(f"  {ctx}")
            print("-" * 65)
            
        print(f"{COLOR_GREEN}👉 当前对方所言:{COLOR_RESET}")
        print(f"  {COLOR_BOLD}{user_message or '[触发初始交互]'}{COLOR_RESET}")
        print("=" * 65)

        reply_content = ""

        # 第一优先级：若当前为 Gemini 自动应答模式，且填入了 API Key
        if CURRENT_MODE == "gemini" and GEMINI_API_KEY:
            print(f"{COLOR_YELLOW}🧠 正在通过本地代理发送请求给 Gemini API ({GEMINI_MODEL})...{COLOR_RESET}")
            reply_content = call_gemini_api(messages)
            
            if reply_content:
                print(f"{COLOR_BOLD}{COLOR_GREEN}🤖 [Gemini 自动回复]:{COLOR_RESET} {reply_content}")
                print("-" * 65)
                print(f"{COLOR_WHITE}提示：你可以输入 /manual 临时切回手动打字接管{COLOR_RESET}")
            else:
                print(f"{COLOR_RED}⚠️ Gemini 接口未响应，自动切换为手动终端打字接管...{COLOR_RESET}")
                CURRENT_MODE = "manual"

        # 第二优先级：手动打字模式（或 Gemini API 失败退回手动模式）
        if not reply_content or CURRENT_MODE == "manual":
            print(f"{COLOR_BOLD}{COLOR_MAGENTA}⌨️ [手动终端接管模式] 请输入该殖民者的回复内容:{COLOR_RESET}")
            if GEMINI_API_KEY:
                print(f"   {COLOR_WHITE}指令：输入 /gemini 切换回 Gemini 自动代答; 输入 /quit 退出{COLOR_RESET}")
            else:
                print(f"   {COLOR_WHITE}指令：输入 /quit 退出桥接服务{COLOR_RESET}")
            
            while True:
                try:
                    user_input = input(f"{COLOR_GREEN}回复内容 >> {COLOR_RESET}").strip()
                except KeyboardInterrupt:
                    print(f"\n{COLOR_RED}已强制终止输入，返回默认静默值。{COLOR_RESET}")
                    user_input = "..."
                
                if user_input.lower() == "/quit":
                    print(f"{COLOR_RED}退出桥接服务...{COLOR_RESET}")
                    self._send_error_and_close("桥接已断开")
                    return
                elif user_input.lower() == "/gemini" and GEMINI_API_KEY:
                    CURRENT_MODE = "gemini"
                    print(f"{COLOR_YELLOW}🔄 已切回 Gemini 自动代答模式，重新发送请求...{COLOR_RESET}")
                    reply_content = call_gemini_api(messages)
                    if not reply_content:
                        reply_content = "..."
                    break
                elif not user_input:
                    print(f"{COLOR_YELLOW}⚠️ 回复不能为空，请输入字句！{COLOR_RESET}")
                    continue
                else:
                    reply_content = user_input
                    break

        # 构造标准返回
        response_data = {
            "id": f"chatcmpl-{int(time.time())}",
            "object": "chat.completion",
            "created": int(time.time()),
            "model": "gemini-terminal-bridge",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": reply_content
                    },
                    "finish_reason": "stop"
                }
            ]
        }
        
        self._send_json_response(200, response_data)
        print(f"{COLOR_BOLD}{COLOR_GREEN}✅ 回复内容已成功送达《环世界》客户端！{COLOR_RESET}\n")

    def _send_error_and_close(self, msg):
        self._send_json_response(200, {
            "choices": [{"message": {"role": "assistant", "content": f"[{msg}]"}}]
        })
        sys.exit(0)

def print_banner(host, port):
    print("\n" + "★" * 72)
    print(f"{COLOR_BOLD}{COLOR_GREEN}    __ _          _____ ___  _     _  __    ____  ____  _ ____  ____ ______{COLOR_RESET}")
    print(f"{COLOR_BOLD}{COLOR_GREEN}    |  \\ | |    |  ___/   \\| |    | |/ /   / __ \\|  _ \\| |  _ \\/ ___| ____|{COLOR_RESET}")
    print(f"{COLOR_BOLD}{COLOR_GREEN}    |   \\| |    | |_  | O || |    |   /   | |  | | |_) | | | | | |   |  _|  {COLOR_RESET}")
    print(f"{COLOR_BOLD}{COLOR_GREEN}    | |\\   |    |  _| |   || |___ |   \\   | |__| |  _ <| | |_| | |___| |___ {COLOR_RESET}")
    print(f"{COLOR_BOLD}{COLOR_GREEN}    |_| \\__|    |_|   |_|_||_____||_|\\_\\   \\____/|_| \\_\\_|____/\\____|____|{COLOR_RESET}")
    print(f"{COLOR_BOLD}{COLOR_YELLOW}                   RimTalk ➔ Gemini 终端极速桥接器 (Active){COLOR_RESET}")
    print("★" * 72)
    print(f"{COLOR_CYAN}🌐 监听地址：{COLOR_RESET}{COLOR_BOLD}http://{host}:{port}/v1/chat/completions{COLOR_RESET}")
    
    if GEMINI_API_KEY:
        print(f"{COLOR_CYAN}🧠 自动代理：{COLOR_RESET}{COLOR_GREEN}已激活 (使用 {GEMINI_MODEL}){COLOR_RESET}")
        print(f"{COLOR_CYAN}✈️ 网络代理：{COLOR_RESET}{COLOR_YELLOW}{PROXY_URL if USE_PROXY else '已禁用'}{COLOR_RESET}")
        print(f"{COLOR_CYAN}⚙️ 初始模式：{COLOR_RESET}{COLOR_GREEN}Gemini 自动代答{COLOR_RESET}")
    else:
        print(f"{COLOR_CYAN}🧠 自动代理：{COLOR_RESET}{COLOR_RED}未激活 (尚未在脚本中配置 GEMINI_API_KEY){COLOR_RESET}")
        print(f"{COLOR_CYAN}⚙️ 初始模式：{COLOR_RESET}{COLOR_MAGENTA}终端纯手动接管模式{COLOR_RESET}")
        
    print(f"{COLOR_CYAN}🔧 环世界配置：{COLOR_RESET}")
    print(f"   1. RimTalk Mod Settings 中 AI Provider 选择为 {COLOR_BOLD}Custom/Local (OpenAI 兼容){COLOR_RESET}。")
    print(f"   2. API Base URL 输入：{COLOR_BOLD}http://{host}:{port}/v1{COLOR_RESET}")
    print(f"   3. 保持本控制台运行。你将在此亲眼见证并控制所有小人的对话！")
    print("-" * 72)
    print(f"{COLOR_WHITE}提示：如果是在手动模式下，你打的字将成为小人的发言；如果想切回 Gemini 自动，输入 /gemini 即可。{COLOR_RESET}")
    print("=" * 72 + "\n")

def main():
    import argparse
    parser = argparse.ArgumentParser(description="RimTalk 终端接管控制桥接服务器")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help=f"服务器监听端口 (默认: {DEFAULT_PORT})")
    parser.add_argument("--host", type=str, default=DEFAULT_HOST, help=f"服务器绑定地址 (默认: {DEFAULT_HOST})")
    args = parser.parse_args()

    server_address = (args.host, args.port)
    
    try:
        httpd = HTTPServer(server_address, RimTalkBridgeHandler)
    except OSError:
        print(f"{COLOR_RED}❌ 端口绑定失败：端口 {args.port} 可能已被占用！{COLOR_RESET}")
        print(f"{COLOR_YELLOW}   提示：请先关闭原本占用该端口的服务，或指定其他端口运行：{COLOR_RESET}")
        print(f"   python rimtalk_bridge.py --port 17205")
        sys.exit(1)

    print_banner(args.host, args.port)

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print(f"\n{COLOR_YELLOW}👋 正在关闭 RimTalk ➔ Gemini 终端桥接服务。祝你游戏愉快！{COLOR_RESET}")
        httpd.server_close()
        sys.exit(0)

if __name__ == "__main__":
    main()
