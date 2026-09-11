# Phase W3-D Verification Plan: Local Vector Embedding & Hybrid Indexing Stage

**Phase**: `W3-D — Local Vector Embedding & Hybrid Indexing Stage`  
**Status**: `PLANNING REMEDIATED (PASS 2) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `3b2b37b925cf972bddc655143669302386475c17`  
**Associated Documents**:
- `docs/W3D_EMBEDDING_INDEXING_PRODUCT_CONTRACT.md`
- `docs/W3D_EMBEDDING_INDEXING_ARCHITECTURE.md`
- `docs/W3D_EMBEDDING_INDEXING_RULE_MATRIX.md`
- `docs/qa/W3D-D_PLANNING_REPORT.md`

---

## 1. Verification Strategy & Objectives

The primary objective of this verification plan is to guarantee that the Phase W3-D Local Vector Embedding & Hybrid Indexing subsystem executes with deterministic precision, mathematical correctness, strict local-first privacy, and resilient fallback across all Windows 11 hardware tiers.

### 1.1 Two-Tier Test Suite Architecture (AUD2-W3D-04)
To ensure acceptance criteria are objectively testable in any environment (including headless CI runners without DirectML GPU hardware or downloaded optional models), tests are partitioned into two distinct tiers:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      W3-D TWO-TIER TESTING ARCHITECTURE                 │
├─────────────────────────────────────────────────────────────────────────┤
│  TIER 1: UNCONDITIONAL / DETERMINISTIC ACCEPTANCE SUITE                 │
│  - Executes unconditionally on all developer machines and CI runners.   │
│  - Zero GPU hardware dependency; zero network dependency.               │
│  - Zero dependency on external downloaded Class B ONNX models.          │
│  - Uses in-memory synthetic fixtures, Class A lexical projection, and   │
│    deterministic mock/synthetic embedding engines.                      │
│  - Acceptance Gate: 100% PASS with ZERO failures and ZERO skipped.      │
├─────────────────────────────────────────────────────────────────────────┤
│  TIER 2: ENVIRONMENT-DEPENDENT HARDWARE & MODEL INTEGRATION SUITE       │
│  - Executes only when prerequisites are met:                            │
│    (a) DirectX 12 DirectML-capable GPU hardware present.                │
│    (b) all-MiniLM-L6-v2 ONNX model downloaded in Capabilities namespace.│
│  - Distinct Status Reporting: PASS / FAIL / NOT-AVAILABLE.              │
│  - If hardware or model is absent, status is reported as NOT-AVAILABLE  │
│    (never masked as "skipped = pass").                                  │
│  - Mandatory on developer workstation validation; optional on headless  │
│    generic CI runners.                                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Invariant-to-Test Mapping Matrix

Every invariant defined in `docs/W3D_EMBEDDING_INDEXING_RULE_MATRIX.md` maps directly to an automated verification test case:

| Test ID | Target Invariant | Test Title | Suite Tier | Primary Verification Method |
| :--- | :--- | :--- | :---: | :--- |
| `TEST-W3D-01` | `INV-W3D-01` | Window Identity Preservation | Tier 1 | Ingest C7.3 windows; verify `WindowId` matches exactly in manifest. |
| `TEST-W3D-02` | `INV-W3D-02` | Composite Key Uniqueness | Tier 1 | Verify `rec_{DocId}_{WindowId}_{ModelId}_{Dim}` uniqueness across windows. |
| `TEST-W3D-03` | `INV-W3D-03` | SHA-256 Content Fingerprint | Tier 1 | Verify content hash equals 64-hex lowercase SHA-256 of `FormattedText`. |
| `TEST-W3D-04` | `INV-W3D-04` | Model Fingerprint Binding | Tier 1 | Verify model weight hash is stored in manifest and checked on load. |
| `TEST-W3D-05` | `INV-W3D-05` | $L_2$ Normalization Precision | Tier 1 | Verify $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$ across DirectML, CPU, and Lexical vectors. |
| `TEST-W3D-06` | `INV-W3D-06` | SIMD Dot-Product Equivalence | Tier 1 | Compare `SimdVectorHelper.DotProduct` against naive cosine formula; assert delta $< 10^{-6}$. |
| `TEST-W3D-07` | `INV-W3D-07` | Similarity Score Clamping | Tier 1 | Inject nearly collinear floating-point vectors; verify clamped to $[-1.0f, 1.0f]$. |
| `TEST-W3D-08` | `INV-W3D-08` | Dual-File Staged Atomic Save | Tier 1 | Mock process kill during `.tmp` write; verify original files remain intact. |
| `TEST-W3D-09` | `INV-W3D-09` | 64-Byte Header Specification | Tier 1 | Verify magic `AXORAVEC`, version `1`, dimensions, counts, and SHA-256 payload hash. |
| `TEST-W3D-10` | `INV-W3D-10` | Source Document Protection | Tier 1 | Verify index rebuild/purge does not alter or delete files in `Scholar/documents/`. |
| `TEST-W3D-11` | `INV-W3D-11` | Corruption Quarantine | Tier 1 | Mutate single byte in `vectors.bin`; assert quarantine directory creation. |
| `TEST-W3D-12` | `INV-W3D-12` | Model Mismatch Detection | Tier 1 | Load manifest with different `ModelFingerprint`; assert `Stale_ModelMismatch`. |
| `TEST-W3D-13` | `INV-W3D-13` | Document Modified Invalidation| Tier 1 | Change `document.SourceHash`; assert `Stale_DocumentModified`. |
| `TEST-W3D-14` | `INV-W3D-14` | Incremental Re-Embedding Skip | Tier 1 | Reindex document with 1 modified window; assert $N-1$ windows skipped without neural run. |
| `TEST-W3D-15` | `INV-W3D-15` | Future Schema Refusal | Tier 1 | Load manifest with `SchemaVersion = 999`; assert `Unsupported_FutureSchema`. |
| `TEST-W3D-16` | `INV-W3D-16` | Class A Lexical Fallback | Tier 1 | Delete model ONNX; assert system initializes Lexical Feature Projector without error. |
| `TEST-W3D-17` | `INV-W3D-17` | DirectML Device Loss Recovery | Tier 2 | Simulate `DXGI_ERROR_DEVICE_REMOVED`; verify automatic CPU fallback. |
| `TEST-W3D-18` | `INV-W3D-18` | User-Controlled Download | Tier 1 | Verify model installer raises confirmation event before download. |
| `TEST-W3D-19` | `INV-W3D-19` | Zero Network Egress | Tier 1 | Run indexing with network monitor hook; assert 0 HTTP/socket calls. |
| `TEST-W3D-20` | `INV-W3D-20` | Privacy Boundary in Logs | Tier 1 | Ingest known sensitive string; search log file output; assert 0 occurrences. |
| `TEST-W3D-21` | `INV-W3D-21` | Class C Transmission Preview | Tier 1 | Trigger remote provider; assert modal payload preview is generated. |
| `TEST-W3D-22` | `INV-W3D-22` | Batch Size Bounding [1, 32] | Tier 1 | Assert batch size 0 and 33 throw `ArgumentOutOfRangeException`; batches $\le 32$ succeed. |
| `TEST-W3D-23` | `INV-W3D-23` | Cancellation Responsiveness | Tier 1 | Cancel token during large document indexing; assert clean abort & `.tmp` cleaned. |
| `TEST-W3D-24` | `INV-W3D-24` | Lock-Free Reader Concurrency | Tier 1 | Execute 20 concurrent queries across 4 threads while idle; assert 0 deadlocks. |
| `TEST-W3D-25` | `INV-W3D-25` | Single-Writer Synchronization | Tier 1 | Invoke concurrent `IndexDocumentAsync` calls; assert sequential write execution. |
| `TEST-W3D-26` | `INV-W3D-26` | DirectML / CPU Equivalence | Tier 2 | Assert $\cos(\mathbf{v}_{	ext{dml}}, \mathbf{v}_{	ext{cpu}}) \ge 0.999$ and $\max \|v_{	ext{dml}, i} - v_{	ext{cpu}, i}\| \le 10^{-3}$. |
| `TEST-W3D-27` | `INV-W3D-27` | Comprehensive Composite Rejection | Tier 1 | Assert rejection for `DocumentId == "composite"`, `PageNumber == 0`, `WindowId` starting with `comp_`, or `FocalChunk == null`. |
| `TEST-W3D-28` | `INV-W3D-28` | Lexical UI Labeling | Tier 1 | Assert Class A capability status exposes badge text `Ready (Lexical Only)`. |
| `TEST-W3D-29` | `INV-W3D-29` | Attention-Masked Mean Pooling | Tier 1 | Assert attention-mask mean pooling on token tensor produces normalized 384-dim vector. |
| `TEST-W3D-30` | `INV-W3D-30` | BM25 Normalization & Tie-Breaking | Tier 1 | Assert Min-Max lexical normalization in $[0, 1]$ and deterministic 7-level tie-breaking. |
| `TEST-W3D-31` | `INV-W3D-31` | MemoryMappedFile View Disposal | Tier 1 | Assert atomic file replacement succeeds without `IOException` after mapped view disposal. |

---

## 3. Detailed Test Specifications

### 3.1 Token Pooling, Math & SIMD Tests

#### `TEST-W3D-29`: Attention-Masked Mean Pooling & 384-Dim Output (AUD2-W3D-01)
- **Precondition**: Synthetic tensor fixture representing unpooled transformer output:
  - Shape: `[1, 5, 384]` (1 sequence of 5 tokens, dimension 384).
  - Attention mask: `[1, 1, 1, 0, 0]` (first 3 real tokens, last 2 padding).
- **Execution**:
  - Run engine-side pooling algorithm:
    $$\text{pooled} = \frac{\sum_{i=0}^2 \text{hidden}_i}{3}$$
  - Apply $L_2$ unit normalization.
- **Assertion**:
  - Verify tokens 3 and 4 (padding) contributed zero to the output.
  - Verify output vector length is exactly `384`.
  - Verify $L_2$ norm $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$.
  - Test all-padding sequence `[0, 0, 0, 0, 0]`: assert divisor evaluates to 1 and returns safe zero vector without divide-by-zero exception.

#### `TEST-W3D-05`: $L_2$ Normalization Precision
- **Precondition**: `IEmbeddingEngine` initialized.
- **Input**: Heterogeneous text strings (short sentence, 500-char paragraph, code snippet).
- **Execution**: Compute embedding vector $\mathbf{v} = \text{GenerateEmbeddingAsync}(text)$.
- **Assertion**:
  - `v.Length == 384`
  - Magnitude: $M = \sqrt{\sum v_i^2}$
  - Assert $|M - 1.0f| < 10^{-5}$

#### `TEST-W3D-06`: SIMD Dot-Product Cosine Equivalence
- **Precondition**: Two unit vectors $\mathbf{a}, \mathbf{b} \in \mathbb{R}^{384}$.
- **Execution**:
  - $D_{\text{simd}} = \text{SimdVectorHelper.DotProduct}(\mathbf{a}, \mathbf{b})$
  - $D_{\text{scalar}} = \sum_{i=0}^{383} a_i \cdot b_i$
- **Assertion**:
  - $|D_{\text{simd}} - D_{\text{scalar}}| < 10^{-6}$

#### `TEST-W3D-26`: DirectML / CPU Numerical Equivalence (Tier 2 Integration · AUD1-W3D-02)
- **Precondition**: DirectML GPU and multithreaded CPU inference sessions initialized with identical ONNX weights.
- **Execution**:
  - Compute $\mathbf{v}_{\text{dml}} = \text{DirectMlSession.Run}(text)$ and $\mathbf{v}_{\text{cpu}} = \text{CpuSession.Run}(text)$.
- **Assertion**:
  - Verify $\text{CosineSimilarity}(\mathbf{v}_{\text{dml}}, \mathbf{v}_{\text{cpu}}) \ge 0.999f$.
  - Verify $\max_i |v_{\text{dml}, i} - v_{\text{cpu}, i}| \le 1.0 \times 10^{-3}f$.
  - Do NOT assert bit-identical byte equality ($v_{\text{dml}} \neq v_{\text{cpu}}$ is valid).
  - Status reported as `PASS` if verified, or `NOT-AVAILABLE` if DirectML GPU hardware is absent.

---

### 3.2 Ingestion Boundary & Composite Rejection Tests (AUD2-W3D-02)

#### `TEST-W3D-27`: Comprehensive Composite Window Ingestion Exclusion
- **Execution**: Test each discriminator condition independently against `IScholarIndexService.IndexDocumentAsync(doc, [window])`:
  1. **Case A (`DocumentId == "composite"`)**: Window with `DocumentId = "composite"`, `PageNumber = 1`, `FocalChunk = validChunk`.
  2. **Case B (`PageNumber == 0`)**: Window with `DocumentId = doc.DocumentId`, `PageNumber = 0`, `FocalChunk = validChunk`.
  3. **Case C (`WindowId.StartsWith("comp_")`)**: Window with `WindowId = "comp_a1b2c3d4e5f6..."`, `DocumentId = doc.DocumentId`, `PageNumber = 1`.
  4. **Case D (`FocalChunk == null`)**: Window with `FocalChunk = null`, `DocumentId = doc.DocumentId`, `PageNumber = 1`.
  5. **Case E (Canonical Valid Window)**: Canonical single-document page window (`DocumentId = doc.DocumentId`, `PageNumber = 1`, `WindowId = "win_doc1_p1_f0_c0_2"`, `FocalChunk = validChunk`).
- **Assertion**:
  - Cases A, B, C, and D each throw `ArgumentException` with message containing `"Cannot index composite prompt window"`.
  - Error diagnostic code is `ERR_COMPOSITE_WINDOW_NOT_INDEXABLE`.
  - Zero files and zero bytes written to `%APPDATA%\Axora\Scholar\indexes\`.
  - Case E succeeds without exception, writing valid manifest and vector binary.

---

### 3.3 Retrieval, BM25 Normalization & Tie-Breaking Tests (AUD2-W3D-03)

#### `TEST-W3D-30`: Deterministic Hybrid Fusion, Min-Max Lexical Normalization & Tie-Breaking
- **Execution**:
  - Test Case 1 (Empty Query): Call `SearchHybridAsync("")` and `SearchHybridAsync("   ")`. Assert returns `[]` without error.
  - Test Case 2 (Single Candidate Pool): Candidate pool of 1 match with raw BM25 score $b_0 = 14.2$. Assert $S_{\text{lexical}} = 1.0f$.
  - Test Case 3 (Multi-Candidate Min-Max Normalization): Candidates with raw scores $\{5.0, 10.0, 15.0\}$. Assert normalized lexical scores evaluate to $\{0.0f, 0.5f, 1.0f\}$ respectively.
  - Test Case 4 (Identical Hybrid Scores Tie-Breaking): Two candidates with identical $S_{\text{hybrid}} = 0.8500f$, candidate A on Page 1 and candidate B on Page 2. Assert candidate A is deterministically ranked before candidate B based on `PageNumber` ascending tie-breaking.
  - Test Case 5 (No Lexical Matches): Query with semantic hits but 0 lexical matches. Assert $S_{\text{lexical}} = 0.0f$ and $S_{\text{hybrid}} = \alpha S_{\text{vector}}$.

---

### 3.4 Persistence & MemoryMappedFile Tests (AUD2-W3D-05)

#### `TEST-W3D-31`: MemoryMappedFile Reader Lifecycle & Safe Replacement
- **Engineering Policy**: Indices $\le 5,000$ vectors are held in managed memory (`float[]`), eliminating file locks during search. Larger indices ($> 5,000$) use `MemoryMappedFile` with explicit view disposal.
- **Precondition**: Persisted index file with 10 records.
- **Execution**:
  - Open reader view on `vectors.bin`.
  - Dispose reader mapped view accessor.
  - Execute index rebuild / atomic replacement via `File.Move(tmp, target, overwrite: true)`.
- **Assertion**:
  - Replacement executes successfully with 0 `IOException` (no Access Denied collisions).
  - Re-opening reader validates updated 64-byte header and updated SHA-256 payload checksum.

#### `TEST-W3D-09`: 64-Byte Binary Header Validation
- **Validation**:
  - Bytes 0..7: ASCII string `"AXORAVEC"`.
  - Bytes 8..9: `uint16` value `1`.
  - Bytes 10..11: `uint16` value `384`.
  - Bytes 12..15: `uint32` value `10`.
  - Bytes 16..31: 16 zeroed bytes.
  - Bytes 32..63: 32-byte SHA-256 hash of payload.
  - Total file length: $64 + (N \times 384 \times 4)$ bytes.

#### `TEST-W3D-11`: Automatic Corruption Quarantine
- **Execution**: Flip single bit in `vectors.bin` payload. Call `ValidateIndexAsync`.
- **Assertion**: Returns `Corrupt_ChecksumMismatch`; corrupt directory moved to quarantine; search falls back to on-the-fly lexical search.

---

### 3.5 Resource Bounds & Concurrency Tests

#### `TEST-W3D-22`: Batch Size Bounding [1, 32]
- **Assertion**: `BatchSize = 0` and `BatchSize = 33` throw `ArgumentOutOfRangeException`; batches $\le 32$ succeed.

#### `TEST-W3D-23`: Cooperative Cancellation Responsiveness
- **Execution**: Trigger indexing on 100-window document; cancel `CancellationToken` during batch 1.
- **Assertion**: Indexing cleanly aborts; `.tmp` files removed; zero orphaned handles.

---

## 4. Acceptance Criteria & Pass Gates

Phase W3-D will be considered successfully verified only when:
1. **Tier 1 Acceptance Suite (100% PASS)**: All 29 Tier 1 unconditional unit tests pass with zero failures and zero skipped.
2. **Tier 2 Integration Suite**: All Tier 2 environment-dependent tests report `PASS` on reference hardware or `NOT-AVAILABLE` with verified prerequisites on headless environments.
3. **Dynamic Baseline Preservation**: The existing 1,327 upstream baseline assertions remain 100% green.
4. **Deterministic Reproducibility**: Mathematical assertions (pooling, normalization, SIMD dot product, tie-breaking) execute deterministically across all test runs.
5. **Zero Leaks**: Diagnostic privacy scan and network socket audit verify zero egress.
6. **Clean Baseline Preserved**: Git HEAD remains `3b2b37b925cf972bddc655143669302386475c17` with zero uncommitted production edits during planning.

Phase W3-D verification plan remediation pass 2 is complete.
