#!/bin/bash
# post-compact.sh — 压缩后提示恢复上下文

set -e

SESSION_FILE="Docs/Current/session-active.md"

if [ -f "$SESSION_FILE" ]; then
    echo ""
    echo "[post-compact] Session 已压缩。请重新读取 Docs/Current/session-active.md 恢复上下文。"
    echo ""
fi
