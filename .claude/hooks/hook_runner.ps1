<#
hook_runner.ps1 — Claude Code 默认 hook 入口（Windows / PowerShell 版）

支持四种模式：
  session-start  显示 session-active.md 状态摘要 + REVIEW-CONFIG
  detect-gaps    检查 Plans/Current/Architecture 与源码目录缺口
  pre-compact    追加 COMPACT-NOTE 到 session-active.md
  post-compact   输出恢复上下文提示

用法：powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/hook_runner.ps1 <mode>
#>

param([Parameter(Position = 0)][string]$Mode = "")

$SessionFile     = "Docs/Current/session-active.md"
$PlansDir        = "Docs/Plans"
$ArchitectureDir = "Docs/Architecture"
$SourceExts      = @(".cs", ".js", ".ts", ".gd", ".cpp", ".py", ".java")

function Extract-Block {
    param([string]$Text, [string]$StartTag, [string]$EndTag)
    $lines = @()
    $inside = $false
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line.Trim() -eq $StartTag) { $inside = $true; continue }
        if ($line.Trim() -eq $EndTag)   { $inside = $false; continue }
        if ($inside -and $line.Trim())  { $lines += $line.Trim() }
    }
    return $lines
}

function Get-SourceCount {
    $files = Get-ChildItem -Path . -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            ($SourceExts -contains $_.Extension.ToLower()) -and
            ($_.FullName -notmatch '[\\/](Library|Temp|node_modules|obj|bin|Packages)[\\/]') -and
            ($_.FullName -notmatch '[\\/]\.git[\\/]')
        }
    return @($files).Count
}

function Invoke-SessionStart {
    if (-not (Test-Path $SessionFile)) {
        Write-Output "[session-start] 未找到 $SessionFile，建议初始化 Docs/Current/session-active.md"
        return
    }
    $text = Get-Content -Path $SessionFile -Raw -Encoding UTF8

    Write-Output ""
    Write-Output "=== 当前项目状态 ==="
    Extract-Block -Text $text -StartTag "<!-- STATUS -->" -EndTag "<!-- /STATUS -->" |
        ForEach-Object { Write-Output $_ }

    $review = Extract-Block -Text $text -StartTag "<!-- REVIEW-CONFIG -->" -EndTag "<!-- /REVIEW-CONFIG -->"
    if ($review) {
        Write-Output ""
        Write-Output "--- REVIEW-CONFIG ---"
        $review | ForEach-Object { Write-Output $_ }
    }

    Write-Output ""
    Write-Output "完整状态见: $SessionFile"
    Write-Output ""
}

function Invoke-DetectGaps {
    $sourceCount    = Get-SourceCount
    $sessionExists  = Test-Path $SessionFile

    # 检测1: Plans 有文档但源码极少
    if ($sourceCount -lt 5) {
        $planDocs = Get-ChildItem -Path $PlansDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne "README.md" }
        if ((Test-Path $PlansDir) -and @($planDocs).Count -gt 0) {
            Write-Output ""
            Write-Output "[detect-gaps] 检测到 Plans/ 下已有计划文档，但源码文件极少（$sourceCount 个）。"
            Write-Output "建议检查是否需要启动项目开发。"
            Write-Output ""
        }
    }

    # 检测2: 无 session-active.md 但 Current/ 存在
    if (-not $sessionExists -and (Test-Path "Docs/Current")) {
        Write-Output ""
        Write-Output "[detect-gaps] 未找到 Docs/Current/session-active.md，但 Current/ 目录存在。"
        Write-Output "建议创建统一状态文件以替代分散的进度记录。"
        Write-Output ""
    }

    # 检测3: 代码存在但无架构文档
    $hasArch = (Test-Path (Join-Path $ArchitectureDir "项目约定.md")) -or (Test-Path (Join-Path $ArchitectureDir "README.md"))
    if ($sourceCount -ge 5 -and -not $hasArch) {
        Write-Output ""
        Write-Output "[detect-gaps] 检测到源码目录存在，但 Docs/Architecture/ 下缺少架构文档。"
        Write-Output "建议创建项目约定或架构说明文档。"
        Write-Output ""
    }
}

function Invoke-PreCompact {
    if (-not (Test-Path $SessionFile)) { return }
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $block = "`n<!-- COMPACT-NOTE -->`nSession 于 $stamp 触发 compaction。`n恢复时请重新读取本文件获取完整上下文。`n<!-- /COMPACT-NOTE -->`n"
    $utf8  = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::AppendAllText((Resolve-Path $SessionFile).Path, $block, $utf8)
}

function Invoke-PostCompact {
    if (Test-Path $SessionFile) {
        Write-Output ""
        Write-Output "[post-compact] Session 已压缩。请重新读取 $SessionFile 恢复上下文。"
        Write-Output ""
    }
}

switch ($Mode) {
    "session-start" { Invoke-SessionStart }
    "detect-gaps"   { Invoke-DetectGaps }
    "pre-compact"   { Invoke-PreCompact }
    "post-compact"  { Invoke-PostCompact }
    default {
        Write-Error "用法: powershell -File .claude/hooks/hook_runner.ps1 <mode>"
        exit 2
    }
}
