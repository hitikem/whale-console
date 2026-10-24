"""QQ Bot 数据采集：聊天记录 + token 用量 + 会话统计
输出 JSON 供控制台读取
"""
import json
import os
import sys
import time
import zstandard as zstd

SESSIONS_DIR = r"C:\Users\Administrator\.dsh\qqbot-home\sessions"
OUT_FILE = r"D:\ds\qqbot\logs\chat-data.json"


def find_sessions():
    out = []
    if not os.path.isdir(SESSIONS_DIR):
        return out
    for root, dirs, files in os.walk(SESSIONS_DIR):
        for fn in files:
            if fn.endswith(".jsonl.zstd"):
                out.append(os.path.join(root, fn))
    out.sort(key=lambda p: os.path.getmtime(p))
    return out


def load_lines(path):
    dctx = zstd.ZstdDecompressor()
    with open(path, "rb") as fh:
        raw = dctx.stream_reader(fh).read()
    rows = []
    for line in raw.decode("utf-8", errors="replace").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            rows.append(json.loads(line))
        except Exception:
            pass
    return rows


def extract_text(content):
    """从 content 数组提取文本"""
    if isinstance(content, str):
        return content
    if not isinstance(content, list):
        return ""
    parts = []
    for c in content:
        if isinstance(c, dict):
            if c.get("type") == "text" and c.get("text"):
                parts.append(c["text"])
    return "\n".join(parts)


def is_noise(text):
    """过滤系统注入的消息"""
    if not text:
        return True
    t = text.strip()
    if t.startswith("<system-reminder>"):
        return True
    if t.startswith("<") and "system" in t[:40].lower():
        return True
    return False


def classify_user(text):
    """判断消息来源：群聊 / 私聊 / 系统"""
    # 群聊格式: [nick (QQ号)] 内容  或 [group:xxx]
    if text.startswith("[") and "(" in text[:80] and ")" in text[:100]:
        return "group"
    return "c2c"


def collect():
    files = find_sessions()
    messages = []
    daily = {}          # 日期 -> {input, output, cache, calls}
    by_model = {}       # 模型 -> {input, output, calls}
    session_stats = []

    for path in files:
        sid = os.path.basename(os.path.dirname(path))
        rows = load_lines(path)

        s_in = s_out = s_cache = s_calls = 0
        s_start = None
        s_msgs = 0

        for r in rows:
            t = r.get("type", "")
            ts = r.get("time", 0) or 0
            d = r.get("data", {}) or {}

            if t == "session" and d.get("createdAt"):
                s_start = d["createdAt"]

            # 聊天消息
            if t == "user/message":
                src = (d.get("source") or {}).get("kind", "")
                if src == "user":
                    txt = extract_text(d.get("content"))
                    if not is_noise(txt):
                        messages.append({
                            "t": ts,
                            "sid": sid,
                            "kind": classify_user(txt),
                            "role": "user",
                            "text": txt[:2000],
                        })
                        s_msgs += 1

            # 助手回复
            if t == "assistant/message":
                msg = d.get("message", {}) or {}
                txt = extract_text(msg.get("content"))
                if txt:
                    messages.append({
                        "t": ts,
                        "sid": sid,
                        "kind": "bot",
                        "role": "assistant",
                        "text": txt[:2000],
                    })
                    s_msgs += 1

            # token 用量
            usage = None
            model = ""
            if t == "assistant/chunk":
                ch = d.get("chunk", {}) or {}
                if ch.get("type") == "usage":
                    usage = ch.get("usage")
            elif t == "assistant/message":
                usage = d.get("usage")
                model = str(d.get("model", ""))

            if usage and isinstance(usage, dict):
                i = int(usage.get("inputTokens", 0) or 0)
                o = int(usage.get("outputTokens", 0) or 0)
                c = int(usage.get("cacheReadTokens", 0) or 0)

                s_in += i
                s_out += o
                s_cache += c
                s_calls += 1

                day = time.strftime("%Y-%m-%d", time.localtime((ts or time.time() * 1000) / 1000))
                bucket = daily.setdefault(day, {"input": 0, "output": 0, "cache": 0, "calls": 0})
                bucket["input"] += i
                bucket["output"] += o
                bucket["cache"] += c
                bucket["calls"] += 1

                m = model or "deepseek-v4.1-flash"
                mb = by_model.setdefault(m, {"input": 0, "output": 0, "calls": 0})
                mb["input"] += i
                mb["output"] += o
                mb["calls"] += 1

        session_stats.append({
            "sid": sid[:8],
            "msgs": s_msgs,
            "input": s_in,
            "output": s_out,
            "cache": s_cache,
            "calls": s_calls,
            "start": s_start or 0,
        })

    # 聊天记录按时间排序，只留最近 500 条
    messages.sort(key=lambda x: x["t"])
    messages = messages[-500:]

    # 按日期排序
    days = []
    for day in sorted(daily.keys()):
        v = daily[day]
        days.append({
            "day": day,
            "input": v["input"],
            "output": v["output"],
            "cache": v["cache"],
            "calls": v["calls"],
            "total": v["input"] + v["output"],
        })

    # 汇总
    total_in = sum(d["input"] for d in days)
    total_out = sum(d["output"] for d in days)
    total_cache = sum(d["cache"] for d in days)
    total_calls = sum(d["calls"] for d in days)

    result = {
        "ok": True,
        "generated": time.strftime("%Y-%m-%d %H:%M:%S"),
        "sessions": len(files),
        "messages": messages,
        "msgCount": len(messages),
        "daily": days,
        "byModel": by_model,
        "sessionStats": session_stats,
        "total": {
            "input": total_in,
            "output": total_out,
            "cache": total_cache,
            "calls": total_calls,
            "total": total_in + total_out,
        },
    }

    os.makedirs(os.path.dirname(OUT_FILE), exist_ok=True)
    with open(OUT_FILE, "w", encoding="utf-8") as fh:
        json.dump(result, fh, ensure_ascii=False)
    return result


if __name__ == "__main__":
    try:
        r = collect()
        print(f"OK sessions={r['sessions']} msgs={r['msgCount']} "
              f"tokens_in={r['total']['input']} tokens_out={r['total']['output']} "
              f"days={len(r['daily'])}")
    except Exception as e:
        print(f"ERR {e}", file=sys.stderr)
        with open(OUT_FILE, "w", encoding="utf-8") as fh:
            json.dump({"ok": False, "error": str(e)}, fh, ensure_ascii=False)
        sys.exit(1)
