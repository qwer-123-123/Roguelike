#!/usr/bin/env python3
"""hook_runner.py — Claude Code 默认 hook 入口（跨平台）。

支持四种模式：
  session-start  显示 session-active.md 状态摘要 + REVIEW-CONFIG
  detect-gaps    检查 Plans/Current/Architecture 与源码目录缺口
  pre-compact    追加 COMPACT-NOTE 到 session-active.md
  post-compact   输出恢复上下文提示

用法：python .claude/hooks/hook_runner.py <mode>
"""

import os
import sys
from datetime import datetime

SESSION_FILE = "Docs/Current/session-active.md"
PLANS_DIR = "Docs/Plans"
ARCHITECTURE_DIR = "Docs/Architecture"

SOURCE_EXTS = (".cs", ".js", ".ts", ".gd", ".cpp", ".py", ".java")

# 源码扫描时跳过的目录（Unity 生成目录 + 常见依赖目录）
SKIP_DIRS = {".git", "Library", "Temp", "node_modules", "obj", "bin", "Packages"}


def _read_session():
    if not os.path.isfile(SESSION_FILE):
        return None
    with open(SESSION_FILE, "r", encoding="utf-8") as f:
        return f.read()


def _count_sources():
    count = 0
    for root, dirs, files in os.walk("."):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            if name.endswith(SOURCE_EXTS):
                count += 1
    return count


def _extract_block(text, start_tag, end_tag):
    """抽取 text 中 start_tag 与 end_tag 之间的非空行。"""
    lines = []
    inside = False
    for line in text.splitlines():
        if line.strip() == start_tag:
            inside = True
            continue
        if line.strip() == end_tag:
            inside = False
            continue
        if inside and line.strip():
            lines.append(line.strip())
    return lines


def session_start():
    text = _read_session()
    if text is None:
        print("[session-start] 未找到 %s，建议初始化 Docs/Current/session-active.md" % SESSION_FILE)
        return

    print("")
    print("=== 当前项目状态 ===")
    for line in _extract_block(text, "<!-- STATUS -->", "<!-- /STATUS -->"):
        print(line)

    review = _extract_block(text, "<!-- REVIEW-CONFIG -->", "<!-- /REVIEW-CONFIG -->")
    if review:
        print("")
        print("--- REVIEW-CONFIG ---")
        for line in review:
            print(line)

    print("")
    print("完整状态见: %s" % SESSION_FILE)
    print("")


def detect_gaps():
    source_count = _count_sources()
    session_exists = os.path.isfile(SESSION_FILE)

    # 检测1: Plans 有文档但源码极少
    if source_count < 5:
        if os.path.isdir(PLANS_DIR) and os.listdir(PLANS_DIR):
            print("")
            print("[detect-gaps] 检测到 Plans/ 下已有计划文档，但源码文件极少（%d 个）。" % source_count)
            print("建议检查是否需要启动项目开发。")
            print("")

    # 检测2: 无 session-active.md 但 Current/ 目录存在
    if not session_exists and os.path.isdir("Docs/Current"):
        print("")
        print("[detect-gaps] 未找到 Docs/Current/session-active.md，但 Current/ 目录存在。")
        print("建议创建统一状态文件以替代分散的进度记录。")
        print("")

    # 检测3: 代码存在但无架构文档
    has_arch = os.path.isfile(os.path.join(ARCHITECTURE_DIR, "项目约定.md")) or \
        os.path.isfile(os.path.join(ARCHITECTURE_DIR, "README.md"))
    if source_count >= 5 and not has_arch:
        print("")
        print("[detect-gaps] 检测到源码目录存在，但 Docs/Architecture/ 下缺少架构文档。")
        print("建议创建项目约定或架构说明文档。")
        print("")


def pre_compact():
    if not os.path.isfile(SESSION_FILE):
        return
    with open(SESSION_FILE, "a", encoding="utf-8") as f:
        f.write("\n<!-- COMPACT-NOTE -->\n")
        f.write("Session 于 %s 触发 compaction。\n" % datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
        f.write("恢复时请重新读取本文件获取完整上下文。\n")
        f.write("<!-- /COMPACT-NOTE -->\n")


def post_compact():
    if os.path.isfile(SESSION_FILE):
        print("")
        print("[post-compact] Session 已压缩。请重新读取 %s 恢复上下文。" % SESSION_FILE)
        print("")


def main():
    if len(sys.argv) < 2:
        print("用法: python .claude/hooks/hook_runner.py <mode>", file=sys.stderr)
        sys.exit(2)

    handlers = {
        "session-start": session_start,
        "detect-gaps": detect_gaps,
        "pre-compact": pre_compact,
        "post-compact": post_compact,
    }

    mode = sys.argv[1]
    if mode not in handlers:
        print("未知模式: %s" % mode, file=sys.stderr)
        sys.exit(2)

    handlers[mode]()


if __name__ == "__main__":
    main()
