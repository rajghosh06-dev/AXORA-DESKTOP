# Axora Desktop — Architecture Specification

This document details the architectural design, cryptographic protocols, data flows, and IPC command pipelines powering **Axora Desktop**.

---

## 1. System Overview

```mermaid
graph TD
    UI[React 18 SPA + Framer Motion] -->|Tauri IPC Invoke| Commands[Rust Command Layer]
    Commands --> Vault[AxoraVault Cryptography]
    Commands --> OCR[Windows 11 Native OCR]
    Commands --> RAG[Rust ONNX Vector RAG Engine]
    Commands --> Anki[Anki SM-2 Exporter]
    
    Vault -->|Argon2id + AES-256-GCM| FileSystem[(Encrypted Storage)]
    RAG -->|384-dim Cosine Search| Vectors[(Local Vector Index)]
```

---

## 2. Cryptographic Protocol & Vault Specifications

Axora Desktop implements a zero-trust per-file encryption protocol:

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VaultUI as Security Page (React)
    participant RustVault as Vault Module (Rust)
    participant OS as OS RNG (rand::thread_rng)
    participant Disk as Local File System

    User->>VaultUI: Select File & Input Master Password
    VaultUI->>RustVault: invoke("encrypt_file", { path, password })
    RustVault->>OS: Generate 16-byte random salt
    OS-->>RustVault: Salt bytes
    RustVault->>RustVault: Argon2id KDF (Salt + Password) -> 256-bit Key
    RustVault->>RustVault: AES-256-GCM Encrypt Payload + Tag
    RustVault->>Disk: Write Payload [16b Salt + Nonce + Ciphertext + Tag]
    Disk-->>VaultUI: Success Confirmation
```

---

## 3. Local ONNX Vector RAG Engine

```mermaid
sequenceDiagram
    autonumber
    participant UI as Academic.tsx UI
    participant IPC as Tauri IPC
    participant RAG as Rust RAG Engine (rag.rs)

    UI->>IPC: invoke("semantic_search_docs", { documentId, text, query, topK: 3 })
    IPC->>RAG: chunk_document(512-char windows with 50-char overlap)
    RAG->>RAG: generate_embedding(query) -> 384-dim vector
    loop For each text chunk
        RAG->>RAG: generate_embedding(chunk) -> 384-dim vector
        RAG->>RAG: cosine_similarity(query_vec, chunk_vec)
    end
    RAG->>RAG: Sort chunks descending by similarity score
    RAG-->>UI: Return top 3 ranked VectorChunk matches
```

---

## 4. SuperMemo-2 (SM-2) Spaced Repetition Protocol

Interval update rules calculated by the Rust SM-2 engine:

$$EF' = EF + (0.1 - (5 - q) \times (0.08 + (5 - q) \times 0.02))$$

$$I(n) = \begin{cases} 1 & n = 1 \\ 6 & n = 2 \\ I(n-1) \times EF' & n > 2 \end{cases}$$

Where $q \in [0, 5]$ represents the user quality grade rating.
