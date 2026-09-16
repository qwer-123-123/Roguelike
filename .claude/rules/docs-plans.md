# Rule: Plan Documents

Applies to: `Docs/Plans/**`

## Constraints

- 文件名必须带 `YYYY-MM-DD-` 年月日前缀
- 文件必须包含：目标、范围（含非目标）、整体迭代总览（表格）、推荐推进方向
- 计划完成后必须停止等待用户确认，禁止自动开始第一次迭代
- 跨模块计划推荐先经过 `architecture-review` 角度预审；可复用 `team-review` 中的 `architecture-review` 角色，但不要求在计划阶段运行完整 `team-review`
