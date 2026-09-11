# Phase W3-D Invariant & Rule Matrix: Local Vector Embedding & Hybrid Indexing Stage

**Phase**: `W3-D — Local Vector Embedding & Hybrid Indexing Stage`  
**Status**: `PLANNING REMEDIATED (PASS 2) / AWAITING THIRD INDEPENDENT PLANNING AUDIT`  
**Repository**: `D:\RAJ\GITHUB_REPOSITORY\PROJECTS\AXORA-DESKTOP`  
**Target Solution**: `Axora-Desktop-WinUI\Axora.Desktop.sln`  
**Protected Git Baseline**: `3b2b37b925cf972bddc655143669302386475c17`  
**Preceding Closed Stages**: `W2-F`, `W3-B`, `W3-C.1..C.7.3`  
**Downstream Dependents**: `Phase W3-E`, `DocumentChatService`, `ScholarKitViewModel`

---

## 1. Overview & Invariant Taxonomy

This document establishes the formal, deterministic invariants governing Phase **W3-D**. Every requirement is cataloged with an invariant identifier, trigger condition, expected behavior, failure code, and test verification mapping.

The 31 formal invariants are categorized into seven architectural groups:
1. **Identity & Determinism** (`INV-W3D-01` .. `INV-W3D-04`)
2. **Mathematical Accuracy, Pooling & Normalization** (`INV-W3D-05` .. `INV-W3D-07`, `INV-W3D-26`, `INV-W3D-29`, `INV-W3D-30`)
3. **Storage & Persistence Integrity** (`INV-W3D-08` .. `INV-W3D-11`, `INV-W3D-31`)
4. **Invalidation, Scope & Incremental Indexing** (`INV-W3D-12` .. `INV-W3D-15`, `INV-W3D-27`)
5. **Capability Governance & Resilient Fallback** (`INV-W3D-16` .. `INV-W3D-18`, `INV-W3D-28`)
6. **Privacy & Data Boundaries** (`INV-W3D-19` .. `INV-W3D-21`)
7. **Concurrency, Cancellation & Resource Bounds** (`INV-W3D-22` .. `INV-W3D-25`)

---

## 2. Invariant & Rule Matrix

| Invariant ID | Rule Name | Category | Trigger Condition | Expected Behavior | Edge Cases & Violations | Failure Code | Verification Test ID |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **INV-W3D-01** | Window Identity Ingestion | Identity | Context window ingested from C7.3 | Index records MUST retain exact upstream `WindowId` without truncation or alteration. | Empty, whitespace, or altered `WindowId`. | `ERR_INVALID_WINDOW_ID` | `TEST-W3D-01` |
| **INV-W3D-02** | Composite Index Key Uniqueness | Identity | Registering a vector index record | Composite key MUST be `rec_{DocumentId}_{WindowId}_{ModelId}_{Dimension}`. | Duplicate window within same document index. | `ERR_DUPLICATE_INDEX_KEY` | `TEST-W3D-02` |
| **INV-W3D-03** | Cryptographic Content Fingerprint | Identity | Ingesting `BoundedContextWindow.FormattedText` | Compute 64-hex SHA-256 digest of `FormattedText` for differential change detection. | Claiming "mathematically collision-free" instead of "cryptographic collision-resistant". | `ERR_INVALID_CONTENT_HASH` | `TEST-W3D-03` |
| **INV-W3D-04** | Model & Fingerprint Binding | Identity | Embedding engine initialization | `ModelId` (e.g. `all-MiniLM-L6-v2`) and `ModelFingerprint` (SHA-256 of weights) MUST be recorded in manifest. | Mismatched weights or unrecognized model. | `ERR_MODEL_FINGERPRINT_MISMATCH` | `TEST-W3D-04` |
| **INV-W3D-05** | Strict $L_2$ Unit Normalization | Math | Embedding vector produced by engine | All output vectors MUST be $L_2$-normalized such that $\|\mathbf{v}\|_2 = 1.0 \pm 10^{-5}$. | Unnormalized vector or zero-magnitude vector ($\|\mathbf{v}\| < 10^{-7}$). | `ERR_UNNORMALIZED_VECTOR` | `TEST-W3D-05` |
| **INV-W3D-06** | SIMD Dot-Product Cosine Equivalence | Math | Similarity search execution | Cosine similarity between unit vectors MUST be computed via `SimdVectorHelper.DotProduct(q, w)`. | Dimensions mismatch or non-SIMD fallback deviation. | `ERR_SIMD_DIMENSION_MISMATCH` | `TEST-W3D-06` |
| **INV-W3D-07** | Bounded Similarity Clamping | Math | Query similarity scoring | Output similarity score MUST be strictly clamped to $[-1.0f, 1.0f]$. | Floating-point rounding exceeding $1.00001f$. | `ERR_SIMILARITY_OUT_OF_BOUNDS` | `TEST-W3D-07` |
| **INV-W3D-08** | Dual-File Atomic Persistence | Storage | Saving document vector index | Vectors MUST be written to `vectors.bin` and metadata to `index_manifest.json` via staged `.tmp` rename. | Interrupted write leaving partial `.tmp` or corrupted original file. | `ERR_INDEX_SAVE_FAILED` | `TEST-W3D-08` |
| **INV-W3D-09** | 64-Byte Binary Header Invariant | Storage | Generating `vectors.bin` | File header MUST be exactly 64 bytes with magic `AXORAVEC`, version `1`, dimensions, record count, and SHA-256 payload hash. | Corrupt magic bytes or incorrect payload checksum. | `ERR_INVALID_BINARY_HEADER` | `TEST-W3D-09` |
| **INV-W3D-10** | Source Document Isolation | Storage | Any index create, update, or rebuild | Source files in `%APPDATA%\Axora\Scholar\documents\` and original disk files MUST NEVER be modified or deleted. | Deleting ground-truth source document during index purge. | `ERR_SOURCE_DOCUMENT_VIOLATION` | `TEST-W3D-10` |
| **INV-W3D-11** | Automatic Corruption Quarantine | Storage | Index loading checksum failure | Corrupt index directory MUST be relocated to `%APPDATA%\Axora\Scholar\quarantine\indexes_{timestamp}\`. | Silent overwriting or unhandled exception crash. | `WARN_INDEX_QUARANTINED` | `TEST-W3D-11` |
| **INV-W3D-12** | Model Mismatch Invalidation | Invalidation | Manifest loaded with different model | If `manifest.ModelFingerprint != activeEngine.ModelFingerprint`, status MUST be `Stale_ModelMismatch`. | Querying obsolete vector space across model generations. | `WARN_INDEX_STALE_MODEL` | `TEST-W3D-12` |
| **INV-W3D-13** | Document Modification Invalidation | Invalidation | Manifest loaded with different source hash | If `manifest.SourceHash != document.SourceHash`, status MUST be `Stale_DocumentModified`. | Stale vectors serving obsolete document revisions. | `WARN_INDEX_STALE_CONTENT` | `TEST-W3D-13` |
| **INV-W3D-14** | Incremental Re-Embedding Skip | Invalidation | Reindexing existing document | Windows whose `ContentHash` matches existing record MUST reuse existing vector without neural re-computation. | Redundant re-embedding of identical text spans. | `INFO_INCREMENTAL_REUSED` | `TEST-W3D-14` |
| **INV-W3D-15** | Future Schema Version Refusal | Invalidation | Manifest loaded with `SchemaVersion > 1` | Loader MUST refuse mutation of future schema and return `Unsupported_FutureSchema`. | Blindly deserializing unknown future properties. | `ERR_UNSUPPORTED_SCHEMA_VERSION` | `TEST-W3D-15` |
| **INV-W3D-16** | Lean Base Capability Decoupling | Capability | Application startup without model | When ONNX model is absent, system MUST initialize Class A Lexical Feature Projector without error. | Crashing or blocking startup because optional model is missing. | `INFO_CAPABILITY_LEXICAL_ACTIVE` | `TEST-W3D-16` |
| **INV-W3D-17** | DirectML GPU to CPU Fallback | Capability | DirectML session throws hardware error | On DirectX 12 device removal/reset (TDR), engine MUST fall back to CPU provider without crashing. | Crash upon GPU driver reset during background indexing. | `WARN_DIRECTML_FALLBACK_CPU` | `TEST-W3D-17` |
| **INV-W3D-18** | User-Controlled Installation Only | Capability | Model download or activation | Capability Manager MUST require explicit user consent before downloading neural weights (~80 MB estimate). | Silent background download without user notification. | `ERR_UNAUTHORIZED_DOWNLOAD` | `TEST-W3D-18` |
| **INV-W3D-19** | Zero Network Egress (Classes A/B/D) | Privacy | Indexing or searching locally | Local indexing and search MUST make ZERO network requests or socket transmissions. | Transmitting text, queries, or embeddings to remote hosts. | `ERR_UNAUTHORIZED_NETWORK_CALL` | `TEST-W3D-19` |
| **INV-W3D-20** | Zero Private Text in Diagnostics | Privacy | Generating logs or telemetry | Log entries MUST NOT contain document text, window text, query text, or user notes. | Writing user excerpt to diagnostic log file. | `ERR_PRIVACY_LEAK_IN_LOGS` | `TEST-W3D-20` |
| **INV-W3D-21** | Optional Network Preview (Class C) | Privacy | User initiates remote embedding query | Mandatory modal dialog MUST preview outbound text length, destination URL, and require explicit confirmation. | Auto-forwarding prompts to remote API without dialog. | `ERR_UNCONFIRMED_REMOTE_TRANSMISSION` | `TEST-W3D-21` |
| **INV-W3D-22** | Bounded Batch Size Limits | Resources | Processing windows in `IEmbeddingEngine` | Batch size MUST be bounded in $[1, 32]$. Reject $<1$ or $>32$. | Passing unbounded array (> 32 items) directly to GPU. | `ERR_BATCH_SIZE_OUT_OF_RANGE` | `TEST-W3D-22` |
| **INV-W3D-23** | Cooperative Cancellation Responsiveness | Concurrency | Cancellation requested during indexing | Engine MUST inspect `CancellationToken` at every batch boundary; cleanly abort in $<100$ ms target. | Hanging background task ignoring user cancellation. | `ERR_INDEXING_CANCELLED` | `TEST-W3D-23` |
| **INV-W3D-24** | Lock-Free Concurrent Readers | Concurrency | Simultaneous queries during idle or read | Reading vector spans for search MUST NOT lock or block concurrent reader threads. | Thread contention or UI freeze during simultaneous queries. | `ERR_READER_LOCK_CONTENTION` | `TEST-W3D-24` |
| **INV-W3D-25** | Single-Writer Synchronization | Concurrency | Concurrent indexing calls on same document | Write operations on a document index MUST be serialized via `SemaphoreSlim(1, 1)`. | Corrupting files via concurrent write streams. | `ERR_CONCURRENT_WRITE_COLLISION` | `TEST-W3D-25` |
| **INV-W3D-26** | DirectML / CPU Numerical Equivalence | Math | Cross-provider embedding validation | DirectML and CPU embeddings for identical text MUST satisfy $\cos(\mathbf{v}_{	ext{dml}}, \mathbf{v}_{	ext{cpu}}) \ge 0.999$ and $\max_i \|v_{	ext{dml}, i} - v_{	ext{cpu}, i}\| \le 10^{-3}$. | Assuming bit-identical outputs or exceeding numerical tolerance. | `ERR_PROVIDER_NUMERICAL_DIVERGENCE` | `TEST-W3D-26` |
| **INV-W3D-27** | Comprehensive Composite Ingestion Exclusion | Invalidation | Calling `IndexDocumentAsync` with composite window | Must reject if `DocumentId == "composite" \|\| PageNumber == 0 \|\| WindowId.StartsWith("comp_") \|\| FocalChunk == null` with `ArgumentException`. | Indexing unanchored query-time prompt assemblies as persistent records. | `ERR_COMPOSITE_WINDOW_NOT_INDEXABLE` | `TEST-W3D-27` |
| **INV-W3D-28** | Lexical Projection Naming Integrity | Capability | Inquiring capability state for Class A | Class A capability state MUST be labeled "Ready (Lexical Only)" and MUST NOT claim neural semantic understanding. | Mislabelling lexical hashing projection as neural semantic embedding. | `ERR_INVALID_CAPABILITY_DESCRIPTION` | `TEST-W3D-28` |
| **INV-W3D-29** | Attention-Masked Mean Pooling & 384-Dim Vector | Math | Unpooled transformer output generation | Engine MUST perform attention-mask mean pooling over raw token hidden states, followed by $L_2$ normalization to 384-dim vector. | Omitting pooling and returning 3D tensor $B 	imes L 	imes 384$. | `ERR_TENSOR_DIMENSION_MISMATCH` | `TEST-W3D-29` |
| **INV-W3D-30** | Deterministic Hybrid Fusion & Lexical Normalization | Math | Executing hybrid search | Candidate pool BM25 scores MUST be Min-Max normalized to $[0, 1]$ before convex fusion; ties broken deterministically by 7-level multi-key. | Raw unbounded BM25 scores swamping cosine or non-deterministic tie sorting. | `ERR_INVALID_SCORE_NORMALIZATION` | `TEST-W3D-30` |
| **INV-W3D-31** | MemoryMappedFile Reader Lifecycle & Safe Replacement | Storage | Staged replacement of vectors.bin | Buffers $\le 5,000$ vectors in memory without active file handles; disposes mapped views before atomic `File.Move`. | Windows NTFS Access Denied error due to active memory-mapped file lock. | `ERR_MAPPED_FILE_LOCK_COLLISION` | `TEST-W3D-31` |

---

## 3. Normative Enforcement

1. **Zero Silent Fallback Violations**: Whenever fallback occurs (e.g. `INV-W3D-16` or `INV-W3D-17`), the engine must log the specified warning code and update `ActiveProviderDescription` in `IEmbeddingEngine`.
2. **Immutable Provenance**: In accordance with `INV-W3D-01` and `INV-W3D-02`, the retrieval engine must guarantee that every search hit references the exact `DocumentId`, `PageNumber`, `ChunkIndex`, and `WindowId` from which it was derived.
3. **Data Integrity Over Speed**: In accordance with `INV-W3D-09` and `INV-W3D-11`, if `vectors.bin` does not match its SHA-256 payload checksum in `index_manifest.json`, the index is quarantined immediately; corrupted data is never served.
4. **Deterministic Ranking Guarantee**: In accordance with `INV-W3D-30`, repeated searches with identical queries across identical indexes must return bit-for-bit identical ranking orders.

Phase W3-D rule matrix remediation pass 2 is complete.
