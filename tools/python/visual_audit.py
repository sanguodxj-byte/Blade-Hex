#!/usr/bin/env python
# -*- coding: utf-8 -*-
# visual_audit.py — 截图物理校验 + Gemini 视觉模型审核
# 规范：
#   - 向视觉模型只提两个裸问题，禁止任何预设、背景说明或诱导性内容
#   - 不做任何像素级统计或内容判断逻辑
#   - AI Agent 只读取本脚本输出的 JSON 报告

import os
import sys
import json
import base64
import urllib.request
import urllib.error
import io

GEMINI_API_KEY = "AIzaSyBhOAsGgTgyMha1Iq3I6HQ5aZJSnL4nI6s"
GEMINI_MODEL   = "gemini-3.1-flash-lite"
MAX_B64_BYTES  = 3_800_000   # inline_data 安全上限（base64 字节数）


def load_and_shrink(image_path):
    """读取图片，必要时降分辨率使 base64 体积 < MAX_B64_BYTES。
    优先用 Pillow；不可用时直接读取原始字节（可能超限）。"""
    try:
        from PIL import Image
        img = Image.open(image_path).convert("RGB")
        quality = 85
        scale   = 1.0
        while True:
            buf = io.BytesIO()
            w = int(img.width  * scale)
            h = int(img.height * scale)
            resized = img.resize((w, h), Image.LANCZOS) if scale < 1.0 else img
            resized.save(buf, format="JPEG", quality=quality)
            b64 = base64.b64encode(buf.getvalue())
            if len(b64) <= MAX_B64_BYTES:
                return b64.decode("utf-8"), "image/jpeg"
            # 缩减：先降画质，再降分辨率
            if quality > 50:
                quality -= 15
            else:
                scale *= 0.75
            if scale < 0.1:
                return b64.decode("utf-8"), "image/jpeg"   # 放弃，直接用
    except ImportError:
        # 无 Pillow，直接原始字节
        with open(image_path, "rb") as f:
            raw = f.read()
        return base64.b64encode(raw).decode("utf-8"), "image/png"


def audit_file(image_path):
    report = {
        "image_path": image_path,
        "file_exists": False,
        "file_size": "未知",
        "file_corrupted": True,
        "vision_response": None,
        "comments": []
    }

    if not os.path.exists(image_path):
        report["comments"].append("错误：截图文件不存在。")
        return report

    report["file_exists"] = True
    fsize = os.path.getsize(image_path)
    report["file_size"] = f"{fsize / (1024*1024):.2f} MB"

    if fsize < 1024:
        report["comments"].append("错误：文件大小异常偏小（小于1KB），数据流无效。")
        return report

    # ── 物理结构校验 ─────────────────────────────────────────────────────
    try:
        with open(image_path, "rb") as f:
            header = f.read(8)
            if header != b"\x89PNG\r\n\x1a\n":
                report["comments"].append("错误：文件头部不匹配 PNG 格式标准，可能已损坏。")
                return report
    except Exception as e:
        report["comments"].append(f"错误：无法打开或读取图像数据流: {str(e)}")
        return report

    report["file_corrupted"] = False
    report["comments"].append("物理结构验证通过：文件存在且头部格式符合 PNG 标准。")

    # ── Gemini 视觉模型审核 ───────────────────────────────────────────────
    try:
        img_b64, mime = load_and_shrink(image_path)

        url = (
            f"https://generativelanguage.googleapis.com/v1beta/models/"
            f"{GEMINI_MODEL}:generateContent?key={GEMINI_API_KEY}"
        )

        payload = {
            "contents": [
                {
                    "parts": [
                        {
                            "inline_data": {
                                "mime_type": mime,
                                "data": img_b64
                            }
                        },
                        {
                            "text": "图上是什么？视觉质量如何？"
                        }
                    ]
                }
            ]
        }

        body = json.dumps(payload).encode("utf-8")
        req  = urllib.request.Request(
            url,
            data=body,
            headers={"Content-Type": "application/json"}
        )

        with urllib.request.urlopen(req, timeout=60) as resp:
            result = json.loads(resp.read().decode("utf-8"))

        vision_text = result["candidates"][0]["content"]["parts"][0]["text"]
        report["vision_response"] = vision_text
        report["comments"].append("视觉模型审核完成。")

    except urllib.error.HTTPError as e:
        err_body = e.read().decode("utf-8", errors="replace")
        report["comments"].append(f"视觉模型调用失败 HTTP {e.code}: {err_body[:400]}")
    except Exception as e:
        report["comments"].append(f"视觉模型调用失败: {str(e)}")

    return report


def main():
    if len(sys.argv) < 2:
        print("Usage: python visual_audit.py <image_path>")
        sys.exit(1)

    img_path = sys.argv[1]
    print(f"[Visual Audit] 开始审核: {img_path}")

    report = audit_file(img_path)

    report_dir  = os.path.dirname(os.path.abspath(img_path))
    report_path = os.path.join(report_dir, "vision_audit_report.json")

    try:
        with open(report_path, "w", encoding="utf-8") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
        print(f"[Visual Audit] 报告已生成: {report_path}")
    except Exception as e:
        print(f"[Visual Audit] 写入报告失败: {str(e)}")

    print("\n=================== 视觉审核结果 ===================")
    print(f" 文件路径  : {report['image_path']}")
    print(f" 文件大小  : {report['file_size']}")
    print(f" 是否损坏  : {'是' if report['file_corrupted'] else '否'}")
    if report.get("vision_response"):
        print(f" 视觉模型回答:\n{report['vision_response']}")
    print(" 日志:")
    for c in report["comments"]:
        print(f"   - {c}")
    print("=====================================================")

    sys.exit(0 if not report["file_corrupted"] else 1)


if __name__ == "__main__":
    main()
