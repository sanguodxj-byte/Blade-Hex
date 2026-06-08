#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
RimTalk 极速反向代理服务 (RimTalk Reverse Proxy Server)
这是一个纯透明的本地 API 反向代理服务器，用来充当 RimTalk 客户端与云端官方大模型（如 Google Gemini）之间的透明网关。

运作逻辑：
1. 监听本地的 17200 端口。
2. 当 RimTalk 客户端发送请求到 `http://127.0.0.1:17200/v1/chat/completions` 时，本反向代理直接捕获该请求。
3. 自动注入你配置好的云端 API Key（如 Gemini Key），并自动通过本地的科学上网代理（如 Clash 127.0.0.1:7897）建立加密连接。
4. 将请求透传给官方 API，获得响应后再 100% 透明返回给游戏。
5. 此过程完全越过任何人工中转与智能体文件读写，为游戏提供毫秒级的瞬时回复，并彻底解决国内网络连接障碍。
"""

import os
import sys
import json
import urllib.request
import urllib.error
from http.server import HTTPServer, BaseHTTPRequestHandler

# ==================== 核心配置区 ====================
# 1. 目标官方大模型的 OpenAI 兼容 Completions 端点
# Google Gemini 官方端点: https://generativelanguage.googleapis.com/v1beta/openai/v1/chat/completions
# OpenAI 官方端点: https://api.openai.com/v1/chat/completions
TARGET_API_URL = "https://generativelanguage.googleapis.com/v1beta/openai/v1/chat/completions"

# 2. 你的官方大模型 API 密匙 (如果是 Gemini 则是 AIzaSy... 开头的 Key)
API_KEY = ""  # 在此填入你的真实密匙

# 3. 强制重写的模型名称（如果为空，则透传游戏内填写的模型）
# 推荐填入 gemini-1.5-flash，速度极快且极省 Token
FORCE_MODEL = "gemini-1.5-flash"

# 4. 本地科学上网代理配置
USE_PROXY = True
PROXY_URL = "http://127.0.0.1:7897"  # 默认的 Clash 端口

# 5. 本地服务监听配置
PORT = 17200
HOST = "127.0.0.1"
# ====================================================

# 终端字符高亮
COLOR_RESET = "\033[0m"
COLOR_BOLD = "\033[1m"
COLOR_GREEN = "\033[32m"
COLOR_YELLOW = "\033[33m"
COLOR_RED = "\033[31m"
COLOR_CYAN = "\033[36m"

if sys.platform == "win32":
    try:
        import ctypes
        kernel32 = ctypes.windll.kernel32
        kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)
    except Exception:
        pass

class ReverseProxyHandler(BaseHTTPRequestHandler):
    
    def log_message(self, format, *args):
        # 隐藏常规的 HTTP 输出，保持控制台清爽
        pass

    def _send_error(self, status_code, message):
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(json.dumps({"error": message}, ensure_ascii=False).encode('utf-8'))

    def do_OPTIONS(self):
        """支持跨域 OPTIONS 预检"""
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()

    def do_GET(self):
        """健康检查"""
        if self.path in ["/", "/healthz", "/v1/models"]:
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(json.dumps({
                "status": "online",
                "proxy_target": TARGET_API_URL,
                "proxy_active": bool(API_KEY)
            }).encode('utf-8'))
        else:
            self._send_error(404, "Not Found")

    def do_POST(self):
        if not self.path.endswith("/v1/chat/completions"):
            self._send_error(404, "Endpoint unsupported")
            return

        # 1. 读取 RimTalk 发送过来的请求 Body
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length)
        
        try:
            payload = json.loads(post_data.decode('utf-8'))
        except Exception as e:
            self._send_error(400, f"Invalid JSON body: {str(e)}")
            return

        # 2. 注入/改写请求参数
        # 自动注入反向代理中配置好的官方 API Key
        auth_key = API_KEY if API_KEY else self.headers.get("Authorization", "").replace("Bearer ", "").strip()
        
        if not auth_key:
            self._send_error(401, "API Key is required but not configured in proxy.")
            return

        # 强制指定模型名以防止 RimTalk 发送的模型名不兼容
        if FORCE_MODEL:
            payload["model"] = FORCE_MODEL

        forward_data = json.dumps(payload).encode('utf-8')

        # 3. 配置向目标 API 转发的请求头
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {auth_key}"
        }

        # 4. 配置科学上网代理句柄
        opener = urllib.request.build_opener()
        if USE_PROXY and PROXY_URL:
            proxy_handler = urllib.request.ProxyHandler({'http': PROXY_URL, 'https': PROXY_URL})
            opener = urllib.request.build_opener(proxy_handler)

        req = urllib.request.Request(TARGET_API_URL, data=forward_data, headers=headers, method="POST")
        
        print(f"{COLOR_CYAN}[Proxy] {COLOR_RESET}收到小人对话请求，正在反向代理至云端 API...", end="", flush=True)

        # 5. 执行反向代理转发并捕获响应
        try:
            start_time = time.time()
            with opener.open(req, timeout=30) as response:
                res_body = response.read()
                res_code = response.status
                
                # 回写响应
                self.send_response(res_code)
                # 复制响应头信息
                for key, val in response.headers.items():
                    if key.lower() not in ["content-length", "transfer-encoding", "connection"]:
                        self.send_header(key, val)
                self.send_header("Access-Control-Allow-Origin", "*")
                self.end_headers()
                self.wfile.write(res_body)
                
                elapsed = time.time() - start_time
                print(f"\r{COLOR_GREEN}[Proxy] (Success){COLOR_RESET} 代理转发成功！耗时: {elapsed:.2f}s")
                
        except urllib.error.HTTPError as e:
            err_body = e.read().decode('utf-8')
            print(f"\r{COLOR_RED}[Proxy] (Error){COLOR_RESET} 转发失败，目标服务器返回错误: HTTP {e.code}")
            self._send_error(e.code, f"Upstream error: {err_body}")
        except Exception as e:
            print(f"\r{COLOR_RED}[Proxy] (Error){COLOR_RESET} 网络请求失败: {str(e)}")
            self._send_error(502, f"Bad Gateway: {str(e)}")

def main():
    try:
        httpd = HTTPServer((HOST, PORT), ReverseProxyHandler)
    except OSError:
        print(f"❌ 端口 {PORT} 被占用，请确认是否有其他桥接脚本正在运行并将其关闭。")
        sys.exit(1)

    print("\n" + "=" * 70)
    print(f"{COLOR_BOLD}{COLOR_GREEN}🔄 【RimTalk 本地极速反向代理服务】启动成功！{COLOR_RESET}")
    print(f"🌐 本地监听地址: {COLOR_BOLD}http://{HOST}:{PORT}/v1/chat/completions{COLOR_RESET}")
    print(f"🎯 转发目标 API : {COLOR_YELLOW}{TARGET_API_URL}{COLOR_RESET}")
    print(f"🔑 API Key 注入 : {COLOR_GREEN}{'已配置' if API_KEY else '等待请求头携带'}{COLOR_RESET}")
    print(f"✈️ 科学上网代理: {COLOR_YELLOW}{PROXY_URL if USE_PROXY else '已禁用'}{COLOR_RESET}")
    print(f"💡 运作逻辑     : RimTalk ➔ 本地反向代理 ➔ 网络代理 ➔ 官方大模型 API")
    print("=" * 70)
    print(f"{COLOR_CYAN}现在游戏可以直接高速直接对话，所有请求将静默通过此代理完美越过！{COLOR_RESET}")
    print("=" * 70 + "\n")

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n(Exit) 正在关闭反向代理服务。")
        httpd.server_close()

if __name__ == "__main__":
    main()
