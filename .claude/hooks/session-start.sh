#!/bin/bash
# session-start.sh — Session 打开时显示当前状态摘要

set -e

SESSION_FILE="Docs/Current/session-active.md"

if [ -f "$SESSION_FILE" ]; then
    echo ""
    echo "=== 当前项目状态 ==="
    echo ""
    if grep -q "<!-- STATUS -->" "$SESSION_FILE" 2>/dev/null; then
        grep -A2 "<!-- STATUS -->" "$SESSION_FILE" | grep -v "STATUS" | head -3
        echo ""
    fi
    if grep -q "## 当前任务" "$SESSION_FILE"; then
        sed -n '/## 当前任务/,/## /p' "$SESSION_FILE" | head -5 | tail -n +3
        echo ""
    fi
    echo "--- 待办进度 ---"
    grep "\[ \]" "$SESSION_FILE" | head -5 || echo "(暂无未完成任务)"
    echo ""
    echo "完整状态见: $SESSION_FILE"
    echo ""
else
    echo "[session-start] 未找到 $SESSION_FILE，建议初始化 Docs/Current/session-active.md"
fi
