---
description: Plan complex architecture modifications, refactors, or multi-step engineering tasks for AXORA Desktop.
---

# Workflow: /axora-plan

## Objective
Author a comprehensive, non-destructive implementation plan before making modifications to the AXORA Desktop workspace. Adhere strictly to planning mode invariants and obtain user approval prior to execution.

## Execution Steps

1. **Research & Codebase Inspection**:
   - Inspect existing architecture, models, viewmodels, services, or Rust commands.
   - **DO NOT** modify any source code files during the research and planning phase.
   - Respect implementation boundaries: keep WinUI and MaterialUI strictly decoupled.

2. **Formulate Implementation Plan Artifact**:
   - Create or update `implementation_plan.md` in the conversation artifact directory.
   - Structure the plan with:
     - **Goal Description**: Objective and architectural motivation.
     - **User Review Required**: Critical design decisions, breaking changes, or trade-offs.
     - **Proposed Changes**: Grouped logically by component (WinUI, MaterialUI, Scripts, Docs).
     - **Verification Plan**: Exact commands for automated stress tests, builds, smoke tests, and manual verification protocols.
   - Set `UserFacing = true` and `RequestFeedback = true`.

3. **Stop & Await User Approval**:
   - Present the plan to the user.
   - **DO NOT** execute source code modifications until the user explicitly approves.

4. **Execution & Vibe Coding Quality Gate**:
   - Once approved, execute changes adhering to the 9-Stage Vibe Coding Quality Gate:
     `IMPLEMENT -> STATIC CHECK -> COMPILE -> LOGIC TEST -> RUNTIME SMOKE -> INTERACTION -> VISUAL AUDIT -> REGRESSION -> EVIDENCE REPORT`.
