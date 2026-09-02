# Axora Ecosystem: Codebase Issues Audit Log

This document cataloges all bugs, security vulnerabilities, anti-patterns, memory leaks, and logical flaws identified during the architectural audit of the legacy **Axora** (Desktop) and **Axora-Mobile** repositories.

---

## Issue Summary Matrix

| Codebase | Critical (Security / Crash) | High (Data Loss / Leak / Broken Core) | Medium (Anti-Pattern / Code Smell) | Total |
|---|---|---|---|---|
| **Axora (Desktop)** | 6 | 9 | 11 | 26 |
| **Axora-Mobile** | 4 | 7 | 9 | 20 |
| **Total** | **10** | **16** | **20** | **46** |

---

## 1. Axora Desktop Codebase Audit (`D:\RAJ\GITHUB_REPOSITORY\PROJECTS\Axora`)

### Critical Security Vulnerabilities

| Issue ID | File Path | Line(s) | Severity | Description & Impact |
|---|---|---|---|---|
| **DT-SEC-01** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L2216-L2235) | 2216-2235 | **Critical** | **Remote Code Execution (RCE)**: `/api/sandbox/run-python` runs arbitrary strings via `subprocess.run()` without isolation. |
| **DT-SEC-02** | [main.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/main.py#L348) | 348 | **Critical** | **Network Bind Exposure**: Flask binds to `0.0.0.0`, exposing all unauthenticated local files, systems, and RCE endpoints to the local network. |
| **DT-SEC-03** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L477-L493) | 477-493 | **Critical** | **Arbitrary File Write**: `/api/ui/write-file` writes data to any system path without validation or restrictions. |
| **DT-SEC-04** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L17-L31) | 17-31 | **Critical** | **Wildcard CORS Settings**: Exposes the system to Cross-Origin Resource Sharing attacks from any web browser context. |
| **DT-SEC-05** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L272-L318) | 272-318 | **Critical** | **Unauthenticated Dependency Installation**: `/api/system/install-dependency` installs arbitrary Python packages. |
| **DT-SEC-06** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L2207-L2214) | 2207-2214 | **Critical** | **Unauthenticated Shutdown Hook**: Anyone on the local network can shut down the host application. |

### High Severity Issues

| Issue ID | File Path | Line(s) | Severity | Description & Impact |
|---|---|---|---|---|
| **DT-HI-01** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L169-L179) | 169-179 | **High** | **Fabricated GPU Telemetry**: Renders mock telemetry when no NVIDIA GPU is detected, showing simulated metrics. |
| **DT-HI-02** | [manager.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/models/manager.py#L334) | 334 | **High** | **Hardcoded Path**: `local_repo_dir` uses a hardcoded developer path (`D:\RAJ\GITHUB_REPOSITORY\PROJECTS\Axora-gguf-models`), breaking executions on other environments. |
| **DT-HI-03** | [manager.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/models/manager.py#L482-L494) | 482-494 | **High** | **File Handle Leak**: Closes log file handles immediately after starting the sidecar process, closing streams prematurely. |
| **DT-HI-04** | [main.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/main.py#L149) | 149 | **High** | **Incorrect Enum Comparison**: Compares `reply == StandardButton.Yes.value`, which can fail on certain systems depending on the Qt binding version. |
| **DT-HI-05** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L130) | 130 | **High** | **Import Inside Hot Loop**: Executes `import shutil` within the 1-second telemetry loop, causing CPU overhead. |
| **DT-HI-06** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py#L152-L153) | 152-153 | **High** | **Import Inside Hot Loop**: Executes `import subprocess` inside the telemetry loop, causing CPU overhead. |
| **DT-HI-07** | [power_manager.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/power_manager.py#L112) | 112 | **High** | **User Settings Overwrite**: Periodically overwrites `cpu_threads` configuration to the database, ignoring user changes. |
| **DT-HI-08** | [qa_agent.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/modules/pdf_qa/qa_agent.py#L15) | 15 | **High** | **Invalid Embeddings Path**: Loads embeddings from the root model directory instead of `/embeddings/`, causing initialization failures. |
| **DT-HI-09** | [Launcher.cs](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/Launcher.cs#L11) | 11 | **High** | **Hardcoded System Path**: Restricts launcher executable to a hardcoded path (`C:\MyEnv\Scripts\python.exe`), preventing cross-environment usage. |

### Medium Severity Issues

| Issue ID | File Path | Line(s) | Severity | Description & Impact |
|---|---|---|---|---|
| **DT-MED-01** | [server.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/controller/server.py) | Full file | **Medium** | **Monolithic Controller**: server.py contains 2361 lines of code with no modular routing structure. |
| **DT-MED-02** | [index.html](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/ui/templates/index.html) | Full file | **Medium** | **Enormous Front-end File**: Monolithic SPA structure (189KB index.html) that is difficult to maintain. |
| **DT-MED-03** | [app.js](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/ui/static/js/app.js) | Full file | **Medium** | **Enormous Front-end Logic File**: Single JS file containing 189KB of code. |
| **DT-MED-04** | [requirements.txt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/requirements.txt) | Full file | **Medium** | **Missing Core Dependencies**: `psutil` and `cryptography` are imported in code but missing from requirements.txt. |
| **DT-MED-05** | [requirements.txt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/requirements.txt) | Full file | **Medium** | **Unused Dependencies**: `httpx` and `huggingface_hub` are listed in requirements.txt but never imported. |
| **DT-MED-06** | [device_analyzer.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/device_analyzer.py) | Full file | **Medium** | **Dead Code**: device_analyzer.py is a standalone script duplicating logic found in models/hardware.py. |
| **DT-MED-07** | [bugreport-sdk_gphone16k_x86_64-CP21.260330.012-2026-06-11-21-22-49.zip](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/bugreport-sdk_gphone16k_x86_64-CP21.260330.012-2026-06-11-21-22-49.zip) | - | **Medium** | **Repository Bloat**: Large 5MB Android emulator debug archive committed to the repository. |
| **DT-MED-08** | [agent_popup.html](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/ui/templates/agent_popup.html#L86) | 86, 155 | **Medium** | **Emoji Cleanup Incomplete**: Emojis are still present in popup resources, despite log entries claiming they were removed. |
| **DT-MED-09** | [agent_popup.html](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/ui/templates/agent_popup.html#L141) | 141 | **Medium** | **API JSON Property Extraction Bug**: Attempting to extract `.data_path` from a JSON response that was already unwrapped by the helper. |
| **DT-MED-10** | [main.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/main.py#L407-L421) | 407-421 | **Medium** | **Database Polling Anti-Pattern**: 500ms QTimer constantly queries the database to check if a popup is active. |
| **DT-MED-11** | [organizer.py](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora/src/modules/code_helper/organizer.py#L9) | 9 | **Medium** | **Duplicate Classification Array Entry**: `.pdf` is declared twice in the document extensions classification array. |

---

## 2. Axora Mobile Codebase Audit (`D:\RAJ\GITHUB_REPOSITORY\PROJECTS\Axora-Mobile`)

### Critical Issues

| Issue ID | File Path | Line(s) | Severity | Description & Impact |
|---|---|---|---|---|
| **MB-SEC-01** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L516-L517) | 516-517, 553-554 | **Critical** | **Insecure Initialization Vector**: AES encryption uses a hardcoded zero IV (`ByteArray(16)`), making the encryption insecure. |
| **MB-HI-01** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L633-L643) | 633-643 | **Critical** | **Bitmap Memory Leak (OOM)**: Bitmaps decoded in a loop are never recycled, leading to out-of-memory crashes on multi-page scans. |
| **MB-HI-02** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L903-x913) | 903-913 | **Critical** | **Bitmap Memory Leak (OOM)**: getCompiledPdfSize decodes all page bitmaps without recycling them. |
| **MB-HI-03** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L1004-L1013) | 1004-1013 | **Critical** | **Bitmap Memory Leak (OOM)**: Sharing images as PDFs decodes page assets without recycling the bitmaps. |

### High Severity Issues

| Issue ID | File Path | Line(s) | Severity | Description & Impact |
|---|---|---|---|---|
| **MB-HI-04** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L89-L94) | 89-94, 330-335 | **High** | **Overloaded Javascript Interface**: Overloads `setStatusBarTheme` (Boolean vs String). WebView bindings do not support overloading, causing one to fail. |
| **MB-HI-05** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L1120) | 1120 | **High** | **Application Hard Exit**: Calls `System.exit(0)`, bypassing Android lifecycle callbacks and risking data corruption. |
| **MB-HI-06** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L109-L140) | 109-140 | **High** | **Blocked WebView Interface Thread**: `requestDownloadPermission` uses a CountDownLatch that blocks the JS bridge thread for up to 60 seconds, risking ANRs. |
| **MB-HI-07** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L62-L73) | 62-73 | **High** | **Silently Swallowed Scanner Failures**: Scanner errors are caught but never sent back to the WebView, leaving the user interface in a frozen state. |
| **MB-HI-08** | [app.js](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/assets/www/app.js#L6374-L6624) | 6374-6624 | **High** | **Simulated Device Pairing**: AxoraShare functions only simulate pairing on the local storage engine instead of executing actual network calls. |
| **MB-HI-09** | [AndroidBridge.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/AndroidBridge.kt#L171) | 171 | **High** | **Mock CPU Telemetry**: Telemetry queries generate a random number for CPU usage instead of using real data. |
| **MB-HI-10** | [MainActivity.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/MainActivity.kt#L135) | 135 | **High** | **Production WebView Debugging**: WebView debugging is hardcoded to active instead of being gated by debug flags. |

### Medium Severity Issues

| Issue ID | File Path | Line(s) | Severity | Description & Impact |
|---|---|---|---|---|
| **MB-MED-01** | Multiple UI / Nav Files | - | **Medium** | **Vestigial Compose UI Layout**: UI files are present in the repository but completely bypassed by the WebView launcher. |
| **MB-MED-02** | [build.gradle.kts](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/build.gradle.kts#L57-L61) | 57-61 | **Medium** | **Unused CameraX Dependencies**: CameraX dependencies are listed in the build configuration but never imported or used. |
| **MB-MED-03** | [build.gradle.kts](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/build.gradle.kts#L20) | 20 | **Medium** | **R8 Obfuscation Disabled**: Minification and shrinking are disabled for release builds. |
| **MB-MED-04** | [file_paths.xml](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/res/xml/file_paths.xml) | Full file | **Medium** | **Broad File Provider Access**: Exposes the entire external storage directory and cache structure instead of specific folders. |
| **MB-MED-05** | [MainActivity.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/MainActivity.kt#L136) | 136 | **Medium** | **WebView Cache Cleared on Every Launch**: Unconditionally clears the cache on start, increasing page load times. |
| **MB-MED-06** | [MainActivity.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/MainActivity.kt#L220) | 220 | **Medium** | **Deprecated API Usage**: Uses `startActivityForResult` for picking files. |
| **MB-MED-07** | [MainActivity.kt](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/java/com/rajghosh/Axora/MainActivity.kt#L287-L293) | 287-293 | **Medium** | **Deprecated API Override**: Overrides `onActivityResult` for file chooser results. |
| **MB-MED-08** | [ISSUE.md](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/ISSUE.md) | 60-68 | **Medium** | **File Encoding Corruption**: Null bytes and corrupted UTF-16 character sequences exist within the document. |
| **MB-MED-09** | [model-manager.html](file:///D:/RAJ/GITHUB_REPOSITORY/PROJECTS/Axora-Mobile/app/src/main/assets/www/model-manager.html#L8) | 8 | **Medium** | **Internet Fonts Load**: Loads Google fonts over the internet, causing failures when the device is offline. |

---

## 3. Structural & Architectural Anti-Patterns Found

1. **God Object Anti-Pattern (Desktop Backend)**:
   The legacy `server.py` is a 2361-line Flask script that handles everything from system diagnostics and AI prompts to custom OCR tasks. It has no blueprint configurations or modular route setups, making it difficult to maintain and scale.
2. **Vestigial Code Scaffolding (Mobile Architecture)**:
   The mobile codebase has two parallel, conflicting architectures. It includes a complete Jetpack Compose MVVM + Hilt layout, but the entry activity ignores this scaffolding to launch a single WebView shell directly. The unused native UI code should be removed to reduce package sizes.
3. **Implicit Base64 Payload Transfers**:
   Using Base64 encoding to transfer files over JavaScript bridges and REST APIs adds 33% overhead and requires loading entire files into memory. The rewritten system will use multipart streams to prevent OOM errors on large files.
