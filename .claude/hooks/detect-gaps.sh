#!/bin/bash
# detect-gaps.sh — 检测项目通用缺口

set -e

PLANS_DIR="Docs/Plans"
SESSION_FILE="Docs/Current/session-active.md"

# 检测1: 有 Plans 文档但源码极少
SOURCE_COUNT=$(find . -maxdepth 3 -type f \( -name "*.cs" -o -name "*.js" -o -name "*.ts" -o -name "*.gd" -o -name "*.cpp" -o -name "*.py" \) | wc -l)
if [ "$SOURCE_COUNT" -lt 5 ]; then
    if [ -d "$PLANS_DIR" ] && [ "$(ls -A "$PLANS_DIR" 2>/dev/null)" ]; then
        echo ""
        echo "[detect-gaps] 检测到 Plans/ 下已有计划文档，但源码文件极少（$SOURCE_COUNT 个）。"
        echo "建议检查是否需要启动项目开发。"
        echo ""
    fi
fi

# 检测2: 无 session-active.md
if [ ! -f "$SESSION_FILE" ] && [ -d "Docs/Current" ]; then
    echo ""
    echo "[detect-gaps] 未找到 Docs/Current/session-active.md，但 Current/ 目录存在。"
    echo "建议创建统一状态文件以替代分散的进度记录。"
    echo ""
fi

# 检测3: 代码存在但无 Architecture 文档
if [ "$SOURCE_COUNT" -ge 5 ] && [ ! -f "Docs/Architecture/项目约定.md" ] && [ ! -f "Docs/Architecture/README.md" ]; then
    echo ""
    echo "[detect-gaps] 检测到源码目录存在，但 Docs/Architecture/ 下缺少架构文档。"
    echo "建议创建项目约定或架构说明文档。"
    echo ""
fi
