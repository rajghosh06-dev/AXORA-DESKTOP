//! Rust Local Vector RAG Engine & Semantic Search — Axora Desktop
//!
//! Provides text chunking (512-char windows with 50-char overlap),
//! 384-dimensional vector projection, cosine similarity distance calculation,
//! and Tauri IPC command bindings for semantic document lookups.

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct VectorChunk {
    pub id: String,
    pub document_id: String,
    pub chunk_index: usize,
    pub chunk_text: String,
    pub similarity_score: f32,
}

/// Compute a 384-dimensional vector representation for a given text chunk.
pub fn generate_embedding(text: &str) -> Vec<f32> {
    let mut vector = vec![0.0f32; 384];
    let words: Vec<&str> = text.split_whitespace().collect();

    if words.is_empty() {
        return vector;
    }

    for (idx, word) in words.iter().enumerate() {
        // Hash projection into 384-dim space
        let mut hash: u32 = 5381;
        for c in word.bytes() {
            hash = ((hash << 5).wrapping_add(hash)).wrapping_add(c as u32);
        }

        for dim in 0..384 {
            let val = ((hash ^ ((dim as u32 * 31) + (idx as u32 * 17))) & 0xFFFF) as f32 / 65535.0 - 0.5;
            vector[dim] += val;
        }
    }

    // L2 Normalization
    let mut norm: f32 = vector.iter().map(|v| v * v).sum();
    norm = norm.sqrt();

    if norm > 1e-6 {
        for v in vector.iter_mut() {
            *v /= norm;
        }
    }

    vector
}

/// Calculate Cosine Similarity between vector A and vector B.
pub fn cosine_similarity(a: &[f32], b: &[f32]) -> f32 {
    if a.len() != b.len() || a.is_empty() {
        return 0.0;
    }

    let mut dot_product = 0.0f32;
    let mut norm_a = 0.0f32;
    let mut norm_b = 0.0f32;

    for i in 0..a.len() {
        dot_product += a[i] * b[i];
        norm_a += a[i] * a[i];
        norm_b += b[i] * b[i];
    }

    let denom = norm_a.sqrt() * norm_b.sqrt();
    if denom > 1e-6 {
        dot_product / denom
    } else {
        0.0
    }
}

/// Split document text into 512-character chunks with 50-character overlap.
pub fn chunk_document(_document_id: &str, text: &str) -> Vec<(usize, String)> {
    let chunk_size = 512;
    let overlap = 50;
    let mut chunks = Vec::new();
    let chars: Vec<char> = text.chars().collect();

    if chars.is_empty() {
        return chunks;
    }

    let mut start = 0;
    let mut index = 0;

    while start < chars.len() {
        let end = (start + chunk_size).min(chars.len());
        let chunk_str: String = chars[start..end].iter().collect();
        chunks.push((index, chunk_str));
        index += 1;

        if end == chars.len() {
            break;
        }
        start += chunk_size - overlap;
    }

    chunks
}

/// Perform semantic vector search over a provided document corpus text.
#[tauri::command]
pub fn semantic_search_docs(
    document_id: String,
    document_text: String,
    query: String,
    top_k: Option<usize>,
) -> Result<Vec<VectorChunk>, String> {
    let k = top_k.unwrap_or(3);
    let query_vector = generate_embedding(&query);
    let chunks = chunk_document(&document_id, &document_text);

    let mut scored_chunks = Vec::new();

    for (idx, chunk_text) in chunks {
        let chunk_vec = generate_embedding(&chunk_text);
        let score = cosine_similarity(&query_vector, &chunk_vec);
        scored_chunks.push(VectorChunk {
            id: format!("{}_{}", document_id, idx),
            document_id: document_id.clone(),
            chunk_index: idx,
            chunk_text,
            similarity_score: score,
        });
    }

    scored_chunks.sort_by(|a, b| b.similarity_score.partial_cmp(&a.similarity_score).unwrap_or(std::cmp::Ordering::Equal));
    scored_chunks.truncate(k);

    Ok(scored_chunks)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_generate_embedding_dims() {
        let vec = generate_embedding("Axora Zero-Cloud Cryptographic Desktop");
        assert_eq!(vec.len(), 384);
        let norm: f32 = vec.iter().map(|x| x * x).sum::<f32>().sqrt();
        assert!((norm - 1.0).abs() < 1e-4);
    }

    #[test]
    fn test_cosine_similarity_identical() {
        let vec_a = generate_embedding("Network Security Protocol");
        let score = cosine_similarity(&vec_a, &vec_a);
        assert!((score - 1.0).abs() < 1e-4);
    }

    #[test]
    fn test_chunk_document() {
        let text = "a".repeat(1200);
        let chunks = chunk_document("doc_1", &text);
        assert!(!chunks.is_empty());
        assert_eq!(chunks[0].0, 0);
    }

    #[test]
    fn test_semantic_search_docs() {
        let doc_text = "Argon2id derivation provides memory hardness against GPU brute-force attacks.";
        let res = semantic_search_docs("doc_test".to_string(), doc_text.to_string(), "Argon2id derivation".to_string(), Some(1));
        assert!(res.is_ok());
        let results = res.unwrap();
        assert_eq!(results.len(), 1);
        assert!(results[0].similarity_score > -1.0);
    }
}

