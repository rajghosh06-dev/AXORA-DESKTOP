# Axora Desktop: Features Specification Document

This document provides a granular functional breakdown of the 7 core features for **Axora Desktop**, outlining input formats, core libraries, validation criteria, execution pipelines, error handling, and testing strategies.

---

## Feature 1: File Converter

Provides seamless conversion capabilities between documents, presentations, spreadsheets, and image packages.

### 1. Functional Specification
* **Formats Supported**:
  * `PDF` ➔ Word (`.docx`), PowerPoint (`.pptx`), Excel (`.xlsx`), Images (`.png`, `.jpg`)
  * Word (`.docx`), PowerPoint (`.pptx`), Excel (`.xlsx`) ➔ `PDF`
  * Images (`.png`, `.jpg`, `.webp`) ➔ `PDF` (Packaged document)
* **Modes**: Single-file mode and multi-file batch execution.
* **Layout Preservation**: High-fidelity preservation of font faces, tables, multi-column divisions, and embedded vector objects.

### 2. Core Dependencies & Libraries
* **LibreOffice Headless**: Version `24.x+`. Executed as a background daemon process.
* **`lopdf` (Rust crate)**: Version `0.33.x` for direct PDF object scanning, splitting, and merging.
* **`pdf-writer` (Rust crate)**: Version `0.11.x` for high-performance generation of PDF files from local raster streams.
* **`pdf2image` / `poppler-utils`**: For high-quality, DPI-customized PDF rasterization to images.

### 3. Execution Pipeline & Data Flow
1. **Input Validation**: Check that target paths exist, file format matches the extension (mime-type inspection using `infer` crate), and files are not password-protected or corrupted.
2. **Process Execution**:
   * For **Office to PDF**: Spawn LibreOffice:
     ```bash
     libreoffice --headless --convert-to pdf --outdir [TargetDir] [SourceFile]
     ```
   * For **PDF to Office**: Extract structured elements using Poppler to scan layout positions. If text layer is missing, trigger OCR pipeline.
   * For **Images to PDF**: Create an empty PDF tree in memory using `pdf-writer`, stream each image into an `/XObject` structure, scale to fit standard page bounds (A4/Letter), and compile.
3. **Post-Processing**: Release file descriptors, run garbage collection on temp files, and write output files.

### 4. Error Handling & Testing
* **Timeout Handling**: LibreOffice executions are terminated if processing exceeds 60 seconds (for files < 50MB) or 180 seconds (for files > 50MB).
* **Corrupt File Detection**: Catch exit codes of underlying tools (e.g., Poppler exit code `1` or `2`). Return user-friendly messages rather than stack traces.
* **Testing**:
  * Unit tests verifying PDF page counts after splitting.
  * Integration tests executing Office-to-PDF conversions against standard templates containing tables, charts, and custom fonts.

---

## Feature 2: Intelligent Compressor

Compresses documents and image assets with precision, matching three user-selectable profiles (Low, Medium, High).

### 1. Functional Specification
* **Supported Formats**: PDF, Word (`.docx`), PowerPoint (`.pptx`), Excel (`.xlsx`), Images (PNG, JPG, WebP).
* **Compression Profiles**:
  * **Low (Max Quality)**: Strip document metadata, discard revision history, minimal image compression (95% quality).
  * **Medium (Balanced)**: Rescale embedded images to 150 DPI, set JPEG quality to 75%, compress structural XML files.
  * **High (Min Size)**: Rescale images to 72 DPI, convert RGB to Grayscale (optional), set JPEG quality to 50%, linearize PDF.

### 2. Core Dependencies & Libraries
* **Ghostscript (CLI)**: Version `10.x` for vector and layout compression in PDFs.
* **`zip` (Rust crate)**: For unpacking, modifying, and repacking Office formats (DOCX/PPTX/XLSX are zipped XML packages).
* **`image` (Rust crate)**: For localized image scaling, color conversions, and quality adjustments.

### 3. Execution Pipeline & Data Flow
1. **Office Document Pipeline**:
   * Extract ZIP container of the document (`.docx` / `.pptx` / `.xlsx`).
   * Scan folder structures `/media` or `/word/media` for image resources.
   * Compresses all JPEG/PNG/TIFF files in-place using the selected quality profile.
   * Repack container using deflate algorithm.
2. **PDF Pipeline**:
   * Invoke Ghostscript:
     ```bash
     gswin64c -sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/ebook -dNOPAUSE -dQUIET -dBATCH -sOutputFile=[Output] [Input]
     ```
3. **Telemetry & Preview**:
   * Calculate original size vs. compressed size and report real-time compression ratios.
   * Return a low-resolution rendering of page 1 to the frontend for quality verification.

### 4. Error Handling & Testing
* **Empty Document Handling**: If a document contains no images or metadata to compress, return the original file with a "Fully Compressed" notice.
* **Testing**: Assert that compression output size is strictly less than original size for high-content files. Test that repacked ZIP structures open successfully in Microsoft Word without recovery alerts.

---

## Feature 3: Security Engine

Provides cryptographic encryption/decryption systems and administrative PDF password removal.

### 1. Functional Specification
* **Arbitrary File Encrypter**: Encrypts any local document or media file using strong cryptographic keys.
* **Decryption Engine**: Reconstructs original files upon supplying the valid key/password.
* **Security Clearances**: Strips passwords from protected PDFs (requires the user to provide the owner password first).

### 2. Core Dependencies & Libraries
* **`aes-gcm` (Rust crate)**: Version `0.10.x` for AEAD (Authenticated Encryption with Associated Data) using AES-256-GCM.
* **`argon2` (Rust crate)**: Version `0.5.x` for secure key derivation from user-defined text passwords.
* **`zeroize` (Rust crate)**: For clearing cryptographic key bytes from system memory immediately after use.
* **`lopdf`**: For decrypting and removing security dictionaries within PDFs.

### 3. Execution Pipeline & Data Flow
```
Encrypt Flow:
[User Password] ➔ [Argon2id (Salt + CPU/Memory cost)] ➔ [256-bit Key]
[Raw File Buffer] ➔ [AES-256-GCM (Key + Random Nonce)] ➔ [Encrypted Container]
```
1. Generate a cryptographically secure random 16-byte salt and 12-byte nonce using the `rand` crate.
2. Derive the 256-bit AES key using Argon2id with recommended parameters (1 pass, 64MB memory cost).
3. Read the input file in 1MB chunks to keep memory usage low. Encrypt chunks, prepending salt/nonce, and appending the 16-byte authentication tag to the output.

### 4. Error Handling & Testing
* **Corrupt Ciphertext Guard**: The AEAD authentication tag guarantees file integrity. If a single byte is changed, decryption fails immediately with an `InvalidCiphertext` error, preventing corrupted outputs.
* **Testing**: Test vector validations matching NIST standards. Verify that memory blocks holding derived keys are zeroed and verified using compiler memory checks.

---

## Feature 4: Hardware Scanner Integration

Deep local system hooks to interface with physical flatbed and Document Feeder (ADF) hardware.

### 1. Functional Specification
* **Hardware Interfacing**: Dynamic scanning acquisition supporting TWAIN/WIA (Windows), ImageCaptureCore (macOS), and SANE (Linux).
* **Scan Controls**: Resolution (75 to 1200 DPI), Color (24-bit Color, 8-bit Grayscale, 1-bit Monochrome), Size (A4, Letter, Legal, custom), Source (Flatbed, Auto Document Feeder).
* **Output Modes**: Packaged PDF document, multi-page TIFF package, or a folder of PNG/JPEG images.

### 2. Core Dependencies & Libraries
* **Windows API / WIA 2.0 COM bindings**: Native system COM bindings.
* **`sane-sys` (Rust crate)**: Direct bindings to Linux `libsane.so`.
* **Objective-C Runtime / FFI**: For macOS ImageCapture framework calls.

### 3. Execution Pipeline & Data Flow
1. **Device Discovery**: Native thread calls the OS driver manager and returns connected scanner IDs.
2. **Acquisition Loop**:
   * Lock scanner session.
   * Send parameters (e.g., set DPI to 300, source to ADF).
   * Loop through scanning pages, receiving image data blocks.
   * Write data directly to temporary files.
3. **Post-Process Assembly**: Auto-orient images (using basic layout metrics), execute target format compile, and clear native scanner sessions.

### 4. Error Handling & Testing
* **Driver Lock Detection**: Detect if scanner is busy or offline, returning specific status alerts (e.g., "Scanner paper jam in ADF", "Device locked by another process").
* **Testing**: Implement mock driver interfaces that stream test images to verify DPI scaling and rotation code when physical hardware is absent.

---

## Feature 5: Mass Batch Image Processor

High-performance, async engine capable of processing 1,000–3,000 images in a single batch.

### 1. Functional Specification
* **Scale**: Concurrent processing of huge image lists (up to 3,000 files).
* **Operations**: Format swaps (PNG, JPEG, WebP, TIFF, BMP), physical scaling, watermarking, EXIF meta stripping, and color profile mapping.
* **UI Controls**: Overall batch progress indicator, current active file log, estimated time remaining (ETA), and a cancel trigger.

### 2. Core Dependencies & Libraries
* **`tokio` (Rust crate)**: Async scheduling runtime.
* **ImageMagick (Native binaries / FFI)**: For complex operations (advanced sharpening, edge-detection filters, custom canvas expansions).
* **`image` (Rust crate)**: For simple, fast in-memory tasks (basic scale, rotations, formats swaps).

### 3. Execution Pipeline & Data Flow
1. **Queue Setup**: Create a bounded channel (`tokio::sync::mpsc::channel`) with a capacity of `CPU_CORES * 2`.
2. **Worker Threads Pool**: Spawn worker threads matching the hardware profile, each polling the channel for path payloads.
3. **Execution Loop**:
   * Read next file path from channel.
   * Verify file format and load header metadata.
   * If operation is simple, process in-memory using Rust `image` crate. If complex, trigger ImageMagick.
   * Write outputs to target directory, preserving original folders if requested.
   * Emit progress payload through Tauri Event bridge.
4. **Shutdown / Cancel**: If frontend sends a cancel event, clear the task channel, send stop signals to workers, and delete partially processed files.

### 4. Error Handling & Testing
* **Tolerance Strategy**: Batch does not stop on a single image failure. Errors are logged to a JSON report, and processing continues for remaining assets.
* **Memory Protection**: Enforces strict memory thresholds. If system RAM exceeds 80% capacity, processing pauses until memory usage drops below 70%.
* **Testing**: Batch tests running operations on 1,000 dummy image files. Verify memory allocations remain constant (flat memory usage) over the lifetime of the batch.

---

## Feature 6: Modern UI (Gemini Inspired)

Liquid Glass UI designed for performance and accessibility.

### 1. Functional Specification
* **Visual Language**: Modern dark-mode by default, subtle gradient cards, frosted glass elements, and micro-animations on interactive items.
* **Navigation**: Responsive sidebar navigation with smooth layout transitions.
* **Interactivity**: Drag-and-drop zones with active state colors.

### 2. Core Dependencies & Libraries
* **React + TypeScript**: Declarative UI layer.
* **Tailwind CSS**: Core CSS layout utility.
* **Framer Motion**: Smooth component entry/exit animations.
* **Zustand**: Lightweight global state manager.
* **`virtuoso` (React library)**: Virtualized scrolling components for showing large file lists.

### 3. Execution Pipeline & Data Flow
1. **Drag-and-Drop Ingestion**: Files dropped onto target areas are validated at the JS layer for basic MIME types and forwarded to the Rust backend as absolute paths.
2. **State Store Management**: Active operations (jobs) are registered in the Zustand store. Progress updates from Tauri events update state properties, triggering rendering updates.
3. **Virtualization**: File grids containing thousands of items render only visible elements to keep DOM nodes low and keep UI execution responsive.

### 4. Error Handling & Testing
* **Thread Safety**: UI elements do not execute synchronous file tasks. All communication with the OS file system or hardware goes through async Tauri IPC.
* **Testing**: Playwright testing suites simulating user journeys (e.g., dropping file packages, switching themes, starting queues).

---

## Feature 7: Mobile Ecosystem Hook

Local network pairing and file synchronization connecting Axora Desktop and Axora Mobile.

### 1. Functional Specification
* **Discovery Protocol**: Local Wi-Fi discovery using mDNS.
* **Pairing**: Quick pairing via QR Code containing host IP, port, and security token.
* **Synchronizations**: Secure file transfer using local HTTP API interfaces.

### 2. Core Dependencies & Libraries
* **`mdns-sd` (Rust crate)**: For mDNS advertising and resolution.
* **`axum` (Rust crate)**: High-speed local HTTP backend server.
* **`tokio-tungstenite` (Rust crate)**: WebSocket implementation.
* **`ring`**: For Elliptic Curve Diffie-Hellman (ECDH) key exchanges.

### 3. Execution Pipeline & Data Flow
```
Pairing Sequence:
1. Desktop Core advertises "_Axora._tcp.local" via mdns-sd.
2. Desktop UI shows QR containing: { IP, Port, Token }
3. Mobile app scans QR, sends Pairing Request via local HTTP.
4. ECDH key exchange establishes shared Session Key.
5. Persistent pairing is saved to local storage.
```
* **File Transfer Protocol**:
  * Instead of base64 JSON serialization, files are transferred using **Multipart HTTP Uploads** directly over Wi-Fi.
  * Real-time transfer progress and clipboard syncs are streamed over a persistent WebSocket connection.

### 4. Error Handling & Testing
* **Connection Drop Resilience**: The HTTP file server supports range headers for chunked transfers, enabling automatic resume of interrupted file uploads/downloads.
* **Testing**: Connect mock mobile devices over a local Wi-Fi router to verify discovery, pairing sequences, and file transfer speeds.

---

## Extended Feature Roadmap (Future Integrations)

Features from the legacy codebases are scheduled for future development phases:

| Feature | Legacy Core System | Proposed Tauri/Rust Strategy | Target Phase |
|---|---|---|---|
| **AI Chat & QA** | llama.cpp sidecar + Qwen 1.5B | Spawn `llama-server` sidecar as a controlled subprocess; access via native Rust HTTP client | Phase 11 |
| **PDF RAG Indexer** | ONNX Embeddings + SQLite Vector | Rust `ort` crate for ONNX MiniLM runtime + SQLx SQLite for vector blobs | Phase 11 |
| **Voice Transcriber** | whisper.cpp sidecar | Compile whisper.cpp as a static library wrapper in Rust; direct audio buffer processing | Phase 12 |
| **Math Solver** | SymPy (Python) | Port logic to native Rust math parsing library (e.g., `meval` or calling SymPy via lightweight embedded Python engine) | Phase 13 |
| **Background Remover** | OpenCV Contours (Python) | Leverage Rust Bindings for OpenCV or deploy ONNX-based U2Net model for higher precision | Phase 14 |
