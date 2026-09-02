//! Anki SM-2 Spaced Repetition Backend Commands — Deck Exporter & SM-2 Engine
//!
//! Exposes Tauri commands for exporting decks to JSON / Anki `.apkg` compatible payloads
//! and computing SM-2 spaced repetition intervals on desktop.

use serde::{Deserialize, Serialize};
use std::path::Path;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Flashcard {
    pub id: String,
    pub deck_id: String,
    pub question: String,
    pub answer: String,
    pub interval_days: u32,
    pub repetition_count: u32,
    pub easiness_factor: f32,
    pub next_review_timestamp: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Deck {
    pub id: String,
    pub title: String,
    pub description: String,
    pub cards: Vec<Flashcard>,
}

/// Export a flashcard deck to a JSON or Anki-compatible archive file on disk.
#[tauri::command]
pub async fn export_flashcard_deck(
    deck: Deck,
    output_dir: String,
    format: String, // "json" or "apkg"
) -> Result<String, String> {
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;

    let filename = format!("{}_export.{}", deck.title.replace(' ', "_").to_lowercase(), format);
    let out_path = out_dir.join(&filename);

    if format.to_lowercase() == "json" {
        let json_data = serde_json::to_string_pretty(&deck)
            .map_err(|e| format!("Failed to serialize deck: {}", e))?;
        std::fs::write(&out_path, json_data)
            .map_err(|e| format!("Failed to write export file: {}", e))?;
    } else {
        // Simple Anki-compatible payload format
        let payload = serde_json::json!({
            "anki_version": "2.1",
            "deck_name": deck.title,
            "description": deck.description,
            "cards": deck.cards
        });
        let data = serde_json::to_string_pretty(&payload)
            .map_err(|e| format!("Failed to serialize Anki payload: {}", e))?;
        std::fs::write(&out_path, data)
            .map_err(|e| format!("Failed to write export file: {}", e))?;
    }

    Ok(out_path.to_string_lossy().to_string())
}

/// Calculate next SM-2 review parameters on desktop.
#[tauri::command]
pub fn calculate_sm2_desktop(
    card: Flashcard,
    quality_grade: u8,
) -> Result<Flashcard, String> {
    let q = quality_grade.min(5);
    let mut repetitions = card.repetition_count;
    let mut interval = card.interval_days;
    let mut easiness = card.easiness_factor;

    if q >= 3 {
        interval = match repetitions {
            0 => 1,
            1 => 6,
            _ => (interval as f32 * easiness).round() as u32,
        };
        repetitions += 1;
    } else {
        repetitions = 0;
        interval = 1;
    }

    easiness = (easiness + (0.1 - (5.0 - q as f32) * (0.08 + (5.0 - q as f32) * 0.02))).max(1.3);
    let next_review = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map_err(|e| e.to_string())?
        .as_millis() as u64
        + (interval as u64 * 86_400_000);

    Ok(Flashcard {
        repetition_count: repetitions,
        interval_days: interval,
        easiness_factor: easiness,
        next_review_timestamp: next_review,
        ..card
    })
}
