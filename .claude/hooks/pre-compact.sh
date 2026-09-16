#!/bin/bash
# pre-compact.sh — 压缩前保存 session 进度

set -e

SESSION_FILE="Docs/Current/session-active.md"

if [ -f "$SESSION_FILE" ]; then
    echo "" >> "$SESSION_FILE"
    echo "<!-- COMPACT-NOTE -->" >> "$SESSION_FILE"
    echo "Session 于 $(date '+%Y-%m-%d %H:%M:%S') 触发 compaction。" >> "$SESSION_FILE"
    echo "恢复时请重新读取本文件获取完整上下文。" >> "$SESSION_FILE"
    echo "<!-- /COMPACT-NOTE -->" >> "$SESSION_FILE"
fi
