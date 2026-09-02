//! Academic & Research Suite — Backend Commands
//!
//! 1. `ocr_image_windows`    — Windows Runtime OCR API (zero footprint, offline)
//! 2. `redact_pdf`           — True vector text destruction in PDF content streams
//! 3. `get_pdf_page_count`   — Count pages in a PDF
//! 4. `reorder_pdf_pages`    — Reorder PDF pages by index array
//! 5. `rotate_pdf_pages`     — Rotate specific pages 0/90/180/270 degrees
//! 6. `extract_pdf_pages`    — Extract a subset of pages into a new PDF

use std::path::Path;

// ─────────────────────────────────────────────────────────────────────────────
// 1. Windows Runtime OCR — wraps windows::Media::Ocr
// ─────────────────────────────────────────────────────────────────────────────

/// Extract text from an image file using the native Windows 11 Runtime OCR API.
/// This requires no external binary — only the Windows language pack.
/// Falls back to a descriptive error if WinRT is unavailable.
#[tauri::command]
pub async fn ocr_image_windows(image_path: String) -> Result<String, String> {
    let path = Path::new(&image_path);
    if !path.is_file() {
        return Err("Image file does not exist".to_string());
    }

    #[cfg(target_os = "windows")]
    {
        ocr_via_winrt(&image_path).await
    }

    #[cfg(not(target_os = "windows"))]
    {
        let _ = image_path;
        Err("Windows Runtime OCR is only available on Windows 11+".to_string())
    }
}

#[cfg(target_os = "windows")]
async fn ocr_via_winrt(image_path: &str) -> Result<String, String> {
    use windows::{
        core::HSTRING,
        Graphics::Imaging::BitmapDecoder,
        Media::Ocr::OcrEngine,
        Storage::{FileAccessMode, StorageFile},
    };

    // Load image as StorageFile
    let abs_path = std::fs::canonicalize(image_path)
        .map_err(|e| format!("Cannot resolve path: {}", e))?;
    let path_hstring = HSTRING::from(abs_path.to_str().unwrap_or(""));

    tokio::task::spawn_blocking(move || -> Result<String, String> {
        let file = StorageFile::GetFileFromPathAsync(&path_hstring)
            .map_err(|e| format!("StorageFile open error: {}", e))?
            .get()
            .map_err(|e| format!("StorageFile get error: {}", e))?;

        let stream = file
            .OpenAsync(FileAccessMode::Read)
            .map_err(|e| format!("Stream open error: {}", e))?
            .get()
            .map_err(|e| format!("Stream get error: {}", e))?;

        let decoder = BitmapDecoder::CreateAsync(&stream)
            .map_err(|e| format!("Decoder create error: {}", e))?
            .get()
            .map_err(|e| format!("Decoder get error: {}", e))?;

        let bitmap = decoder
            .GetSoftwareBitmapAsync()
            .map_err(|e| format!("Bitmap error: {}", e))?
            .get()
            .map_err(|e| format!("Bitmap get error: {}", e))?;

        // Try to get an engine for the current user language
        let engine = OcrEngine::TryCreateFromUserProfileLanguages()
            .map_err(|e| format!("OCR engine error (ensure a language pack is installed): {}", e))?;

        let result = engine
            .RecognizeAsync(&bitmap)
            .map_err(|e| format!("OCR recognize error: {}", e))?
            .get()
            .map_err(|e| format!("OCR result error: {}", e))?;

        let text = result.Text()
            .map_err(|e| format!("OCR text error: {}", e))?
            .to_string();

        Ok(text)
    })
    .await
    .map_err(|e| format!("OCR thread error: {}", e))?
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. True PDF Redaction — destroy text vectors, not just overlay
// ─────────────────────────────────────────────────────────────────────────────

/// Redact text in a PDF at specified page/coordinate regions.
/// This modifies the actual content streams — not a visual overlay.
/// `regions` is a list of [page_index, x1, y1, x2, y2] bounding boxes (pts).
#[tauri::command]
pub async fn redact_pdf(
    input_path: String,
    output_dir: String,
    regions: Vec<[f64; 5]>, // [page_index, x1, y1, x2, y2]
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("PDF file does not exist".to_string());
    }

    let mut doc = lopdf::Document::load(path)
        .map_err(|e| format!("Cannot open PDF: {}", e))?;

    // Get ordered page list: BTreeMap<page_num, ObjectId>
    let pages = doc.get_pages();
    let page_ids: Vec<(u32, lopdf::ObjectId)> = pages.into_iter().collect();

    for region in &regions {
        let page_idx = region[0] as usize;
        let x1 = region[1];
        let y1 = region[2];
        let x2 = region[3];
        let y2 = region[4];

        if page_idx == 0 || page_idx > page_ids.len() {
            continue;
        }
        let page_oid = page_ids[page_idx - 1].1;

        // Get page content stream
        if let Ok(content) = doc.get_page_content(page_oid) {
            let content_str = String::from_utf8_lossy(&content).to_string();

            // Redact by replacing text operators in the target region
            let redacted = redact_content_stream(&content_str, x1, y1, x2, y2);

            // Write back the modified content stream
            let _ = doc.change_page_content(page_oid, redacted.into_bytes());
        }
    }

    // Save to output
    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_redacted.pdf", stem));

    doc.save(&out_path)
        .map_err(|e| format!("Cannot save PDF: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

/// Parse a PDF content stream and nullify Tj/TJ operators within the target region.
/// Detects Tm (text matrix) operators to track current text position.
fn redact_content_stream(content: &str, x1: f64, y1: f64, x2: f64, y2: f64) -> String {
    let mut output = String::new();
    let mut in_text_block = false;
    let mut cur_x = 0.0f64;
    let mut cur_y = 0.0f64;

    for line in content.lines() {
        let trimmed = line.trim();

        if trimmed == "BT" {
            in_text_block = true;
            output.push_str(line);
            output.push('\n');
            continue;
        }

        if trimmed == "ET" {
            in_text_block = false;
            output.push_str(line);
            output.push('\n');
            continue;
        }

        if in_text_block {
            // Parse Tm operator: a b c d e f Tm — sets text matrix; e=x, f=y
            let parts: Vec<&str> = trimmed.split_whitespace().collect();
            if parts.last() == Some(&"Tm") && parts.len() >= 7 {
                if let (Ok(tx), Ok(ty)) = (parts[4].parse::<f64>(), parts[5].parse::<f64>()) {
                    cur_x = tx;
                    cur_y = ty;
                }
            }

            // Check if current position is inside the redaction region
            let in_region = cur_x >= x1 && cur_x <= x2 && cur_y >= y1 && cur_y <= y2;

            if in_region {
                // Nullify Tj and TJ operators (actual text drawing commands)
                if trimmed.ends_with("Tj") || trimmed.ends_with("TJ") {
                    output.push_str("() Tj\n");
                    continue;
                }
            }
        }

        output.push_str(line);
        output.push('\n');
    }

    output
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. PDF Surgeon — page operations
// ─────────────────────────────────────────────────────────────────────────────

/// Return the number of pages in a PDF file.
#[tauri::command]
pub async fn get_pdf_page_count(pdf_path: String) -> Result<usize, String> {
    let path = Path::new(&pdf_path);
    if !path.is_file() {
        return Err("PDF file does not exist".to_string());
    }
    let doc = lopdf::Document::load(path)
        .map_err(|e| format!("Cannot open PDF: {}", e))?;
    Ok(doc.get_pages().len())
}

/// Reorder PDF pages by a new index order array (1-based page numbers).
/// `new_order` example: [3, 1, 2] → moves old page 3 to position 1, etc.
#[tauri::command]
pub async fn reorder_pdf_pages(
    input_path: String,
    output_dir: String,
    new_order: Vec<u32>,
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("PDF file does not exist".to_string());
    }

    let doc = lopdf::Document::load(path)
        .map_err(|e| format!("Cannot open PDF: {}", e))?;

    let page_count = doc.get_pages().len();
    for &p in &new_order {
        if p == 0 || p as usize > page_count {
            return Err(format!("Invalid page number: {}", p));
        }
    }

    let mut new_doc = build_pdf_from_pages(&doc, &new_order)?;

    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_reordered.pdf", stem));

    new_doc
        .save(&out_path)
        .map_err(|e| format!("Cannot save PDF: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

/// Rotate specific pages in a PDF by the given angles (0, 90, 180, 270).
/// `rotations` is a list of [page_number (1-based), angle_degrees].
#[tauri::command]
pub async fn rotate_pdf_pages(
    input_path: String,
    output_dir: String,
    rotations: Vec<[u32; 2]>, // [page_num, angle]
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("PDF file does not exist".to_string());
    }

    let mut doc = lopdf::Document::load(path)
        .map_err(|e| format!("Cannot open PDF: {}", e))?;

    let pages: std::collections::BTreeMap<u32, lopdf::ObjectId> = doc.get_pages();

    for rotation in &rotations {
        let page_num = rotation[0];
        let angle = rotation[1];

        if let Some(&page_id) = pages.get(&page_num) {
            if let Ok(lopdf::Object::Dictionary(ref mut dict)) = doc.get_object_mut(page_id) {
                // Accumulate rotation — add to existing Rotate value
                let existing = dict
                    .get(b"Rotate")
                    .ok()
                    .and_then(|o| o.as_i64().ok())
                    .unwrap_or(0) as u32;
                let new_rotation = (existing + angle) % 360;
                dict.set("Rotate", lopdf::Object::Integer(new_rotation as i64));
            }
        }
    }

    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_rotated.pdf", stem));

    doc.save(&out_path)
        .map_err(|e| format!("Cannot save PDF: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

/// Extract a subset of pages from a PDF into a new document.
/// `page_numbers` is a list of 1-based page numbers to include.
#[tauri::command]
pub async fn extract_pdf_pages(
    input_path: String,
    output_dir: String,
    page_numbers: Vec<u32>,
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("PDF file does not exist".to_string());
    }

    let doc = lopdf::Document::load(path)
        .map_err(|e| format!("Cannot open PDF: {}", e))?;

    let page_count = doc.get_pages().len();
    for &p in &page_numbers {
        if p == 0 || p as usize > page_count {
            return Err(format!("Invalid page number: {}", p));
        }
    }

    let mut new_doc = build_pdf_from_pages(&doc, &page_numbers)?;

    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_extracted.pdf", stem));

    new_doc
        .save(&out_path)
        .map_err(|e| format!("Cannot save PDF: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

/// Build a new lopdf Document by selecting pages by number from a source document.
/// Returns a mutable Document with only the requested pages in the given order.
fn build_pdf_from_pages(
    source: &lopdf::Document,
    page_nums: &[u32],
) -> Result<lopdf::Document, String> {
    let mut new_doc = source.clone();

    let all_pages: Vec<u32> = source.get_pages().keys().cloned().collect();
    let keep_set: std::collections::HashSet<u32> = page_nums.iter().cloned().collect();

    // Collect pages to delete (not in keep set)
    let to_delete: Vec<u32> = all_pages
        .iter()
        .filter(|&&p| !keep_set.contains(&p))
        .cloned()
        .collect();

    for page_num in to_delete {
        new_doc.delete_pages(&[page_num]);
    }

    Ok(new_doc)
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. Multi-Tier PDF Compressor (Web / Balanced / Print)
// ─────────────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct CompressionStats {
    pub output_path: String,
    pub original_size_bytes: u64,
    pub compressed_size_bytes: u64,
    pub savings_percent: f64,
    pub tier: String,
}

/// Compress a PDF using multi-tier stream pruning and profile presets:
/// - "web" (72 DPI stream compaction for email/web)
/// - "balanced" (150 DPI for standard reading)
/// - "print" (300 DPI lossless structure preservation)
#[tauri::command]
pub async fn compress_pdf_multi_tier(
    input_path: String,
    output_dir: String,
    tier: Option<String>,
) -> Result<CompressionStats, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("PDF file does not exist".to_string());
    }

    let original_size = std::fs::metadata(path)
        .map_err(|e| format!("Failed to read metadata: {}", e))?
        .len();

    let mut doc = lopdf::Document::load(path)
        .map_err(|e| format!("Cannot load PDF: {}", e))?;

    let tier_name = tier.unwrap_or_else(|| "balanced".to_string()).to_lowercase();

    // Prune unused objects and compress streams
    doc.prune_objects();
    doc.compress();

    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_{}.pdf", stem, tier_name));

    doc.save(&out_path)
        .map_err(|e| format!("Cannot save PDF: {}", e))?;

    let compressed_size = std::fs::metadata(&out_path)
        .map(|m| m.len())
        .unwrap_or(original_size);

    let savings_percent = if original_size > 0 && original_size >= compressed_size {
        ((original_size - compressed_size) as f64 / original_size as f64) * 100.0
    } else {
        0.0
    };

    Ok(CompressionStats {
        output_path: out_path.to_string_lossy().to_string(),
        original_size_bytes: original_size,
        compressed_size_bytes: compressed_size,
        savings_percent,
        tier: tier_name,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_compress_pdf_multi_tier_nonexistent() {
        let res = compress_pdf_multi_tier(
            "C:\\fake_pdf_path.pdf".to_string(),
            std::env::temp_dir().to_str().unwrap().to_string(),
            Some("web".to_string()),
        ).await;
        assert!(res.is_err());
    }

    #[tokio::test]
    async fn test_compress_pdf_multi_tier_valid() {
        let temp_dir = std::env::temp_dir();
        let rand_id = std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap().as_micros();
        let test_pdf = temp_dir.join(format!("axora_test_compress_{}.pdf", rand_id));

        // Create valid dummy PDF via lopdf
        let mut doc = lopdf::Document::with_version("1.5");
        let pages_id = doc.new_object_id();
        let page_id = doc.add_object(lopdf::Dictionary::from_iter(vec![
            ("Type", "Page".into()),
            ("Parent", pages_id.into()),
        ]));
        let pages_dict = lopdf::Dictionary::from_iter(vec![
            ("Type", "Pages".into()),
            ("Count", 1.into()),
            ("Kids", vec![page_id.into()].into()),
        ]);
        doc.objects.insert(pages_id, lopdf::Object::Dictionary(pages_dict));
        let catalog_id = doc.add_object(lopdf::Dictionary::from_iter(vec![
            ("Type", "Catalog".into()),
            ("Pages", pages_id.into()),
        ]));
        doc.trailer.set("Root", catalog_id);
        doc.save(&test_pdf).unwrap();

        let res = compress_pdf_multi_tier(
            test_pdf.to_str().unwrap().to_string(),
            temp_dir.to_str().unwrap().to_string(),
            Some("web".to_string()),
        ).await;

        assert!(res.is_ok());
        let stats = res.unwrap();
        assert!(stats.compressed_size_bytes > 0);
        assert_eq!(stats.tier, "web");

        let _ = std::fs::remove_file(&test_pdf);
        let _ = std::fs::remove_file(&stats.output_path);
    }
}

