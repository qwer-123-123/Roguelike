# Archive 归档总索引

> 防止 `Docs/Plans/` 与 `Docs/IterationLogs/` 无限堆积。归档时在此登记。

## 归档触发条件

| 触发条件 | 归档动作 |
|----------|----------|
| 阶段切换（如 Sprint 0 → 1） | 归档上阶段所有计划和日志 |
| 计划全部迭代执行完毕 | 归档该计划和关联日志 |
| `Docs/IterationLogs/` 条目 ≥ 10 | 归档最早 5 条日志 |
| 计划被废弃或替换 | 归档到 `Archive/Plans/` |

## 归档目录

```text
Archive/
├── README.md      ← 本文件
├── Plans/         ← 已归档计划
├── IterationLogs/ ← 已归档日志
└── Current/       ← 历史阶段状态存档
```

## 归档记录

> 每条记录：归档时间、原始路径、归档原因、关联文档。

（暂无）
