# Rule 02: AI Engineering & Verification Discipline

## Golden Rule of AI Coding
> **"Code that looks correct is not proof that the feature works."**

### 5-Tier Verification Hierarchy
For every claim of completion or feature verification, explicitly categorize your confidence:
1. `SOURCE VERIFIED`: Code has been inspected and appears structurally sound.
2. `BUILD VERIFIED`: Project compiles cleanly (0 errors) using the official compiler.
3. `TEST VERIFIED`: Relevant automated unit or integration tests have run and passed.
4. `RUNTIME VERIFIED`: Application was launched, executed, and runtime logs/behavior were validated.
5. `VISUAL VERIFIED`: Rendered UI was inspected via screenshots, DevTools, or interactive testing.

### Strict Prohibitions
- **Never claim completion before building**: Never tell the user a fix is done without running the project compiler.
- **Never fabricate results**: Never state that tests passed or an application launched unless the command was actually executed and returned successful exit codes.
- **No speculative fixes**: Diagnose root causes before editing code. Avoid mass rewrites of functioning code to solve isolated bugs.
- **Record pre-existing defects**: If an existing issue causes a build/test failure, mark it `PRE-EXISTING FAILURE` rather than attributing it to new changes.
