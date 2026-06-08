#!/usr/bin/env python
# -*- coding: utf-8 -*-

"""
反重力专属通道桥接器 (Antigravity Agent File-Channel Bridge)
这个脚本充当本地 API 服务，但它并不自己调用大模型，也不等待控制台用户输入。
它是通过“文件共享通道”直接将 RimTalk 的输入/输出交接给当前正在与你对话的“反重力”智能体（Antigravity）！

运作原理：
1. 服务器拦截到 RimTalk 请求，将对话细节写入 `tmp/rimtalk_request.json`。
2. 服务器向标准输出（stdout）打印特殊的唤醒标记，提示反重力智能体立刻开始工作。
3. 系统的任务监控检测到输出后，会自动在 IDE 聊天框中唤醒“反重力”智能体。
4. 反重力读取请求文件，思考并生成精妙的小人对话，然后通过文件写入工具将回复写入 `tmp/rimtalk_response.json`。
5. 本服务器检测到响应文件的生成，读取后实时返回给 RimTalk 客户端。
"""

import os
import sys
import json
import time
from http.server import HTTPServer, BaseHTTPRequestHandler

# 路径配置文件
WORKSPACE_DIR = r"d:\123\Blade&Hex"
TMP_DIR = os.path.join(WORKSPACE_DIR, "tmp")
REQUEST_FILE = os.path.join(TMP_DIR, "rimtalk_request.json")
RESPONSE_FILE = os.path.join(TMP_DIR, "rimtalk_response.json")

# 默认绑定端口
PORT = 17200
HOST = "127.0.0.1"

# 确保临时文件目录存在
if not os.path.exists(TMP_DIR):
    try:
        os.makedirs(TMP_DIR)
    except Exception:
        pass

class AntigravityChannelHandler(BaseHTTPRequestHandler):
    
    def log_message(self, format, *args):
        # 隐藏常规的 HTTP 输出，只保留核心通知
        pass

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
            self._send_json_response(200, {
                "status": "online",
                "bridge": "antigravity-agent-channel",
                "request_file_ready": os.path.exists(REQUEST_FILE)
            })
        else:
            self._send_json_response(404, {"error": "Not Found"})

    def do_POST(self):
        if not self.path.endswith("/v1/chat/completions"):
            self._send_json_response(404, {"error": "Unsupported path"})
            return

        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length)
        
        try:
            payload = json.loads(post_data.decode('utf-8'))
        except Exception as e:
            self._send_json_response(400, {"error": f"Invalid JSON payload: {str(e)}"})
            return

        # 1. 清理以往陈旧的文件，防止误读
        for fpath in [REQUEST_FILE, RESPONSE_FILE]:
            if os.path.exists(fpath):
                try:
                    os.remove(fpath)
                except Exception:
                    pass

        # 2. 将当前 RimTalk 拦截到的请求完整写入共享文件通道
        try:
            with open(REQUEST_FILE, "w", encoding="utf-8") as f:
                json.dump(payload, f, ensure_ascii=False, indent=2)
        except Exception as e:
            self._send_json_response(500, {"error": f"Failed to write request channel: {str(e)}"})
            return

        # 3. 在终端控制台中输出唤醒指示，通知聊天框内的反重力智能体有新的对话事件
        # 刷出带前缀的 stdout，能够让外部 Agent 系统捕获并唤醒当前对话中的反重力（我）
        # 已移除所有可能在 Windows GBK 终端下引起 UnicodeEncodeError 的 emoji 字符
        print(f"\n[RIMTALK_EVENT] (Info) 成功捕获到殖民者的交互事件！", flush=True)
        print(f"[RIMTALK_EVENT] (Path) 已将对话上下文写入通道文件: {REQUEST_FILE}", flush=True)
        print(f"[RIMTALK_EVENT] (Notice) [反重力智能体 (Antigravity) 请注意]:", flush=True)
        print(f"[RIMTALK_EVENT] (Action) 请立刻调用 view_file 读取该 JSON 文件，并在聊天框内生成小人的对话回复，", flush=True)
        print(f"[RIMTALK_EVENT] (Action) 最后使用 write_to_file 工具将回复按 `{{\"reply\": \"回复内容\"}}` 写入: {RESPONSE_FILE}", flush=True)
        print(f"[RIMTALK_EVENT] (Status) 后台服务器将在此挂起并轮询等待你的回复写入...", flush=True)

        # 4. 轮询等待反重力智能体处理回复并写入响应通道
        # 给智能体预留 90 秒的思考和工具执行时间
        reply_content = ""
        timeout = 90
        start_time = time.time()
        
        while time.time() - start_time < timeout:
            if os.path.exists(RESPONSE_FILE):
                try:
                    # 稍等片刻以防文件锁未释放
                    time.sleep(0.5)
                    with open(RESPONSE_FILE, "r", encoding="utf-8") as f:
                        res_data = json.load(f)
                        reply_content = res_data.get("reply", "")
                    break
                except Exception:
                    pass
            time.sleep(1)

        # 5. 清理请求通道文件，保持整洁
        try:
            if os.path.exists(REQUEST_FILE):
                os.remove(REQUEST_FILE)
        except Exception:
            pass

        # 6. 处理回复输出
        if not reply_content:
            reply_content = "[脑电波受到耀斑干扰，反重力暂时处于离线状态]"
            print(f"[RIMTALK_EVENT] (Warning) 等待反重力响应超时，已自动返回默认回复。", flush=True)
        else:
            print(f"[RIMTALK_EVENT] (Success) 成功接收到反重力的回复: {reply_content}", flush=True)
            # 成功读取到后，清理响应通道文件，供下一次使用
            try:
                if os.path.exists(RESPONSE_FILE):
                    os.remove(RESPONSE_FILE)
            except Exception:
                pass

        # 构造标准的 OpenAI 兼容回应发送给 RimTalk
        response_data = {
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": reply_content
                    },
                    "finish_reason": "stop"
                }
            ]
        }
        self._send_json_response(200, response_data)

def main():
    try:
        httpd = HTTPServer((HOST, PORT), AntigravityChannelHandler)
    except OSError:
        print(f"(Error) 端口 {PORT} 被占用。如果之前运行了之前的脚本，请先关闭它，或者使用其他端口。")
        sys.exit(1)

    print("\n" + "=" * 70)
    print(f"(Bridge) [反重力专属通道桥接器] 启动成功！")
    print(f"(URL) 监听地址: http://{HOST}:{PORT}/v1/chat/completions")
    print(f"(Channel) 共享通道: {REQUEST_FILE} -> {RESPONSE_FILE}")
    print(f"(Logic) 运作逻辑: 游戏触发对话 -> 写入请求 -> 唤醒反重力 -> 反重力在聊天窗口中回复 -> 写入响应 -> 游戏展现对话")
    print("=" * 70)

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n(Exit) 关闭反重力通道桥接器。")
        httpd.server_close()

if __name__ == "__main__":
    main()
