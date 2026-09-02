---
description: Run automated test suites across Axora Desktop applications and produce a structured pass/fail report.
---

# Workflow: /axora-test

## Objective
Execute automated tests, stress harnesses, and unit tests, reporting exact counts of passed, failed, and blocked tests without fabrication.

## Execution Steps

1. **Execute Central Test Runner**:
   ```powershell
   .\scripts\qa\run-tests.ps1
   ```

2. **Verify Test Harness Outputs**:
   - WinUI Adversarial Test Suite:
     - M3: Resume PDF Vector Compiler Stress Testing (Empty resume, 5000+ words, wrapping, markdown sanitization, 75 font/margin matrix, 9-section full CV).
     - M4: Flashcards SM-2 & Deck Reactivity (Observable properties, 1000-iteration rating stress, empty decks, text parser).
     - M4: Batch Image Processor Queue (Observable size formatting, 0-byte defensive error handling, rapid 50-file failure callbacks, folder scanner).

3. **Report Outcome**:
   - Output exact numbers: Total, Passed, Failed.
   - If any test failed, output the failure message and stack trace.
   - Return exit code `0` for 100% passing, `1` for any failure.
