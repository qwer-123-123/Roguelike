# Docs 文档系统导航

> Roguelike 项目的文档单一入口。按认知价值维护，不按文件数量维护。

## 目录结构

| 目录 | 用途 | 说明 |
|------|------|------|
| [Current/](Current/) | 当前状态单一源 | `session-active.md`（当前任务/进度）、`project-fingerprint.md`（项目身份） |
| [Architecture/](Architecture/) | 架构与项目约定 | 模块边界、分层规则、项目约定 |
| [Plans/](Plans/) | 计划文档 | 文件名必须带 `YYYY-MM-DD-` 前缀，必须含迭代总览 |
| [IterationLogs/](IterationLogs/) | 迭代日志 | 每个迭代独立文件，`YYYY-MM-DD-` 前缀 |
| [Templates/](Templates/) | 文档模板 | 计划模板、迭代日志模板 |
| [Archive/](Archive/) | 归档系统 | 阶段切换/计划完成/日志堆积时归档 |

## 权威顺序

当多份文档冲突时，按以下顺序判断：

1. `AGENTS.md`
2. `Docs/Architecture/`
3. `Docs/Current/`
4. `Docs/Plans/`
5. `Docs/IterationLogs/`
6. `Docs/README.md`

## 快速入口

- 恢复上下文 → [Current/session-active.md](Current/session-active.md)
- 验证项目身份 → [Current/project-fingerprint.md](Current/project-fingerprint.md)
- 查看项目约定 → [Architecture/项目约定.md](Architecture/项目约定.md)
