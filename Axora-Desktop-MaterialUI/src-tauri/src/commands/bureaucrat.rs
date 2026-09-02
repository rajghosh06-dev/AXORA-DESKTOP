//! Bureaucrat Suite — Forms & Applications Backend Commands
//!
//! 1. `resize_to_target_kb`  — Binary search JPEG quality to hit a precise KB target
//! 2. `extract_signature`    — Strip paper background, isolate ink, output transparent PNG
//! 3. `stitch_id_card_pdf`   — Place front+back ID card images on A4 PDF canvas

use image::{DynamicImage, GenericImageView, ImageBuffer, Rgba};
use std::io::Cursor;
use std::path::Path;

// ─────────────────────────────────────────────────────────────────────────────
// 1. Strict-Target KB Resizer — binary search over JPEG quality
// ─────────────────────────────────────────────────────────────────────────────

/// Compress an image to be strictly below `target_kb` kilobytes using a
/// binary search over JPEG quality values (0–100). Returns the output path
/// and the final achieved file size in bytes.
#[tauri::command]
pub async fn resize_to_target_kb(
    input_path: String,
    output_dir: String,
    target_kb: u32,
) -> Result<serde_json::Value, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("Input file does not exist".to_string());
    }

    let img = image::open(path).map_err(|e| format!("Failed to open image: {}", e))?;

    let target_bytes = (target_kb as usize) * 1024;

    // Binary search: find the highest quality that stays under the target
    let mut lo: u8 = 1;
    let mut hi: u8 = 95;
    let mut best_quality = lo;
    let mut best_buf: Vec<u8> = Vec::new();

    // Pre-check: if even quality=1 is too large, we need to resize the image dimensions
    let initial_buf = encode_jpeg_quality(&img, 1)?;
    if initial_buf.len() > target_bytes {
        // Scale down dimensions until quality=85 fits
        let scale = ((target_bytes as f64) / (initial_buf.len() as f64)).sqrt();
        let new_w = ((img.width() as f64) * scale).max(1.0) as u32;
        let new_h = ((img.height() as f64) * scale).max(1.0) as u32;
        let resized = img.resize_exact(new_w, new_h, image::imageops::FilterType::Lanczos3);
        let buf = encode_jpeg_quality(&resized, 85)?;
        best_buf = buf;
        best_quality = 85;
    } else {
        // Binary search over quality
        while lo <= hi {
            let mid = (lo + hi) / 2;
            let buf = encode_jpeg_quality(&img, mid)?;
            if buf.len() <= target_bytes {
                best_quality = mid;
                best_buf = buf;
                if mid == 95 { break; }
                lo = mid + 1; // try higher quality (larger file)
            } else {
                if mid == 0 { break; }
                hi = mid - 1; // file too large, lower quality
            }
        }
    }

    if best_buf.is_empty() {
        // Fallback: encode at quality 1 and accept it
        best_buf = encode_jpeg_quality(&img, 1)?;
        best_quality = 1;
    }

    // Write result
    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_resized_{}kb.jpg", stem, target_kb));
    std::fs::write(&out_path, &best_buf).map_err(|e| e.to_string())?;

    let achieved_kb = best_buf.len() / 1024;
    Ok(serde_json::json!({
        "output_path": out_path.to_string_lossy(),
        "achieved_kb": achieved_kb,
        "achieved_bytes": best_buf.len(),
        "quality_used": best_quality,
        "target_kb": target_kb,
    }))
}

/// Encode a DynamicImage as JPEG at the specified quality (0–100) into a Vec<u8>
fn encode_jpeg_quality(img: &DynamicImage, quality: u8) -> Result<Vec<u8>, String> {
    let mut buf = Vec::new();
    let mut cursor = Cursor::new(&mut buf);
    let mut encoder = image::codecs::jpeg::JpegEncoder::new_with_quality(&mut cursor, quality);
    encoder
        .encode_image(img)
        .map_err(|e| format!("JPEG encode error: {}", e))?;
    Ok(buf)
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. Signature Extractor — threshold filter + alpha transparency
// ─────────────────────────────────────────────────────────────────────────────

/// Extract a handwritten signature from a photo/scan:
/// - Converts gray/yellow paper background pixels → transparent (alpha=0)
/// - Converts dark ink pixels → pure opaque black (#000000, alpha=255)
/// - Auto-crops to the tightest bounding box around the signature
/// - Returns a transparent PNG path
#[tauri::command]
pub async fn extract_signature(
    input_path: String,
    output_dir: String,
    threshold: Option<u8>, // ink darkness threshold (default 128)
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("Input file does not exist".to_string());
    }

    let img = image::open(path)
        .map_err(|e| format!("Failed to open image: {}", e))?
        .to_rgba8();

    let thresh = threshold.unwrap_or(128);
    let (width, height) = img.dimensions();

    // Create output RGBA buffer
    let mut out: ImageBuffer<Rgba<u8>, Vec<u8>> = ImageBuffer::new(width, height);

    let mut min_x = width;
    let mut min_y = height;
    let mut max_x = 0u32;
    let mut max_y = 0u32;

    for (x, y, pixel) in img.enumerate_pixels() {
        let Rgba([r, g, b, _a]) = *pixel;

        // Convert to grayscale luminance
        let luma = (0.299 * r as f32 + 0.587 * g as f32 + 0.114 * b as f32) as u8;

        // Detect "paper" background: high luminance OR yellowish tint
        let is_yellow = r > 180 && g > 160 && b < 100;
        let is_light = luma > thresh;
        let is_background = is_light || is_yellow;

        if is_background {
            // Transparent pixel
            out.put_pixel(x, y, Rgba([0, 0, 0, 0]));
        } else {
            // Ink pixel: pure black, fully opaque
            out.put_pixel(x, y, Rgba([0, 0, 0, 255]));
            // Track bounding box
            if x < min_x { min_x = x; }
            if y < min_y { min_y = y; }
            if x > max_x { max_x = x; }
            if y > max_y { max_y = y; }
        }
    }

    // Auto-crop to bounding box (with small padding)
    let padding = 8u32;
    let crop_x = min_x.saturating_sub(padding);
    let crop_y = min_y.saturating_sub(padding);
    let crop_w = ((max_x + padding + 1).min(width)).saturating_sub(crop_x);
    let crop_h = ((max_y + padding + 1).min(height)).saturating_sub(crop_y);

    let cropped = if crop_w > 0 && crop_h > 0 && max_x >= min_x && max_y >= min_y {
        image::imageops::crop_imm(&out, crop_x, crop_y, crop_w, crop_h).to_image()
    } else {
        out
    };

    // Write PNG with alpha channel
    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_signature.png", stem));
    cropped
        .save(&out_path)
        .map_err(|e| format!("Failed to save PNG: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. ID Card Stitcher — front + back on A4 PDF canvas
// ─────────────────────────────────────────────────────────────────────────────

/// Embed an image file onto a PDF layer at the given position.
/// JPEG files are embedded natively as DCTDecode streams (no decoding needed).
/// Non-JPEG files are decoded to RGB via the image crate.
fn embed_image_to_layer(
    layer: &printpdf::PdfLayerReference,
    img_path: &str,
    x: printpdf::Mm,
    y: printpdf::Mm,
) -> Result<(), String> {
    // Note: NO `use printpdf::*` here — it can shadow image::open in this scope

    let raw = std::fs::read(img_path)
        .map_err(|e| format!("Cannot read image: {}", e))?;

    // Detect JPEG by magic bytes
    let is_jpeg = raw.len() >= 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF;

    let (iw, ih, image_data, color_space, image_filter) = if is_jpeg {
        // JPEG: parse dimensions from SOF marker, embed raw bytes as DCTDecode
        let (w, h) = parse_jpeg_dimensions(&raw);
        (w, h, raw, printpdf::ColorSpace::Rgb, Some(printpdf::ImageFilter::DCT))
    } else {
        // Non-JPEG: use image::open (path-based) — no glob import conflicts here
        let img = image::open(std::path::Path::new(img_path))
            .map_err(|e| format!("Cannot decode image: {}", e))?;
        let (w, h) = img.dimensions();
        let rgb = img.to_rgb8();
        (w, h, rgb.into_raw(), printpdf::ColorSpace::Rgb, None)
    };

    let pdf_img = printpdf::Image::from(printpdf::ImageXObject {
        width:              printpdf::Px(iw as usize),
        height:             printpdf::Px(ih as usize),
        color_space,
        bits_per_component: printpdf::ColorBits::Bit8,
        interpolate:        true,
        image_data,
        image_filter,
        smask:              None,
        clipping_bbox:      None,
    });

    pdf_img.add_to_layer(
        layer.clone(),
        printpdf::ImageTransform {
            translate_x: Some(x),
            translate_y: Some(y),
            dpi: Some(150.0),
            ..Default::default()
        },
    );
    Ok(())
}

/// Parse JPEG image dimensions by scanning SOF markers.
fn parse_jpeg_dimensions(data: &[u8]) -> (u32, u32) {
    let mut i = 0usize;
    while i + 3 < data.len() {
        if data[i] != 0xFF { break; }
        let marker = data[i + 1];
        // SOF0 (0xC0) through SOF15 except DHT(0xC4), JPG(0xC8), DAC(0xCC)
        if matches!(marker, 0xC0 | 0xC1 | 0xC2 | 0xC3 | 0xC5 | 0xC6 | 0xC7) {
            if i + 8 < data.len() {
                let h = u16::from_be_bytes([data[i + 5], data[i + 6]]) as u32;
                let w = u16::from_be_bytes([data[i + 7], data[i + 8]]) as u32;
                return (w, h);
            }
        }
        if i + 3 < data.len() {
            let seg_len = u16::from_be_bytes([data[i + 2], data[i + 3]]) as usize;
            if seg_len < 2 { break; }
            i += 2 + seg_len;
        } else { break; }
    }
    (750, 480) // fallback
}

/// Stitch front and back ID card images onto a single A4 PDF page.
/// Layout: front card at top-center, back card below — both at standard
/// ID card dimensions (85.6mm × 54mm per ISO/IEC 7810 ID-1).
#[tauri::command]
pub async fn stitch_id_card_pdf(
    front_path: String,
    back_path: String,
    output_dir: String,
) -> Result<String, String> {
    use printpdf::*;

    // A4 dimensions in mm
    let a4_w = Mm(210.0);
    let a4_h = Mm(297.0);

    // ISO ID-1 card dimensions
    let card_w = Mm(85.6);
    let card_h = Mm(54.0);

    // Margins and layout (Y from bottom in printpdf)
    let center_x = Mm(210.0 / 2.0 - 85.6 / 2.0);
    let front_y  = Mm(297.0 - 50.0 - 54.0);
    let back_y   = Mm(297.0 - 50.0 - 54.0 - 54.0 - 20.0);

    let (doc, page1, layer1) = PdfDocument::new("ID Card", a4_w, a4_h, "Layer 1");
    let current_layer = doc.get_page(page1).get_layer(layer1);

    // Add front image
    if !front_path.is_empty() {
        if let Err(e) = embed_image_to_layer(&current_layer, &front_path, center_x, front_y) {
            eprintln!("Warning: could not embed front image: {}", e);
        }
    }

    // Add back image
    if !back_path.is_empty() {
        if let Err(e) = embed_image_to_layer(&current_layer, &back_path, center_x, back_y) {
            eprintln!("Warning: could not embed back image: {}", e);
        }
    }

    // Add labels
    let font = doc.add_builtin_font(BuiltinFont::Helvetica)
        .map_err(|e| format!("Font error: {}", e))?;

    current_layer.use_text("Front", 10.0, center_x, Mm(front_y.0 + card_h.0 + 3.0), &font);
    current_layer.use_text("Back",  10.0, center_x, Mm(back_y.0  + card_h.0 + 3.0), &font);

    // Suppress unused variable warnings
    let _ = card_w;
    let _ = card_h;

    // Save PDF
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join("id_card_combined.pdf");

    let bytes = doc
        .save_to_bytes()
        .map_err(|e| format!("PDF save error: {}", e))?;
    std::fs::write(&out_path, bytes).map_err(|e| e.to_string())?;

    Ok(out_path.to_string_lossy().to_string())
}

/// Compile an ordered list of image paths into a single PDF document.
/// Each image occupies exactly one full A4 page (190×277 mm content area).
/// The `ordered_paths` slice preserves the user's exact selection order:
/// index 0 = Page 1, index 1 = Page 2, and so on.
///
/// Uses printpdf 0.7 API: `Image` is constructed via `ImageXObject`.
#[tauri::command]
pub async fn compile_ordered_pdf(
    ordered_paths: Vec<String>,
    output_name: String,
    output_dir: String,
) -> Result<String, String> {
    use image::ImageReader;
    use printpdf::{
        ColorSpace, Image, ImageTransform, ImageXObject, Mm, PdfDocument, Px,
    };

    if ordered_paths.is_empty() {
        return Err("No images provided. Please select at least one image.".to_string());
    }

    // A4 page (printpdf 0.7 uses f32 internally)
    let page_w = Mm(210.0_f32);
    let page_h = Mm(297.0_f32);
    let margin  = 10.0_f32;
    let content_w = 190.0_f32; // mm
    let content_h = 277.0_f32; // mm

    let (doc, first_page_idx, first_layer_idx) =
        PdfDocument::new(&output_name, page_w, page_h, "Page 1");

    // Helper: decode an image and embed it into the given PDF layer.
    let embed = |layer: &printpdf::PdfLayerReference, path: &str| -> Result<(), String> {
        let dyn_img = ImageReader::open(path)
            .map_err(|e| format!("Cannot open '{}': {}", path, e))?
            .with_guessed_format()
            .map_err(|e| format!("Cannot guess format '{}': {}", path, e))?
            .decode()
            .map_err(|e| format!("Cannot decode '{}': {}", path, e))?;

        // Convert to RGB8 (printpdf uses RGB or RGBA color space)
        let rgb = dyn_img.to_rgb8();
        let (img_w, img_h) = (rgb.width(), rgb.height());

        // Build an ImageXObject from raw bytes
        let xobj = ImageXObject {
            width: Px(img_w as usize),
            height: Px(img_h as usize),
            color_space: ColorSpace::Rgb,
            bits_per_component: printpdf::ColorBits::Bit8,
            interpolate: true,
            image_data: rgb.into_raw(),
            image_filter: None,
            smask: None,
            clipping_bbox: None,
        };

        // Compute scale to fit the content area while preserving aspect ratio.
        // printpdf treats 1 mm = 1 unit, so we scale in mm-space.
        // The `dpi` field in ImageTransform tells printpdf the native image resolution
        // so it can convert pixel dimensions to mm. We pass 300 DPI as a standard value.
        let dpi = 300.0_f32;
        // Image dimensions in mm at dpi
        let native_w_mm = img_w as f32 / dpi * 25.4;
        let native_h_mm = img_h as f32 / dpi * 25.4;

        let scale_x = content_w / native_w_mm;
        let scale_y = content_h / native_h_mm;
        let scale = scale_x.min(scale_y);

        let placed_w = native_w_mm * scale;
        let placed_h = native_h_mm * scale;
        let offset_x = margin + (content_w - placed_w) / 2.0;
        let offset_y = margin + (content_h - placed_h) / 2.0;

        let pdf_image = Image::from(xobj);
        pdf_image.add_to_layer(
            layer.clone(),
            ImageTransform {
                translate_x: Some(Mm(offset_x)),
                translate_y: Some(Mm(offset_y)),
                scale_x: Some(scale),
                scale_y: Some(scale),
                dpi: Some(dpi),
                ..Default::default()
            },
        );
        Ok(())
    };

    // First page
    embed(
        &doc.get_page(first_page_idx).get_layer(first_layer_idx),
        &ordered_paths[0],
    )?;

    // Subsequent pages
    for (i, path) in ordered_paths.iter().enumerate().skip(1) {
        let label = format!("Page {}", i + 1);
        let (pidx, lidx) = doc.add_page(page_w, page_h, &label);
        embed(&doc.get_page(pidx).get_layer(lidx), path)?;
    }

    // Save
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir)
        .map_err(|e| format!("Cannot create output dir: {}", e))?;

    let safe_name: String = output_name
        .chars()
        .map(|c| if matches!(c, '/' | '\\' | ':' | '*' | '?' | '"' | '<' | '>' | '|') {
            '_'
        } else {
            c
        })
        .collect();

    let out_path = out_dir.join(format!("{}.pdf", safe_name));
    let bytes = doc
        .save_to_bytes()
        .map_err(|e| format!("PDF serialise error: {}", e))?;
    std::fs::write(&out_path, &bytes)
        .map_err(|e| format!("Write error: {}", e))?;

    println!(
        "[Axora] FormStudio: {} pages → {:?}",
        ordered_paths.len(), out_path
    );
    Ok(out_path.to_string_lossy().to_string())
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. AI Background Removal — Edge, Chroma & Saliency Transparency Mask
// ─────────────────────────────────────────────────────────────────────────────

/// Removes the background of a portrait or object photo, converting background pixels
/// into pure alpha transparency (RGBA), and auto-crops or preserves dimensions.
#[tauri::command]
pub async fn remove_photo_background(
    input_path: String,
    output_dir: String,
    sensitivity: Option<u8>, // default 40 (0-100)
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("Input photo does not exist".to_string());
    }

    let img = image::open(path)
        .map_err(|e| format!("Failed to open image: {}", e))?
        .to_rgba8();

    let (width, height) = img.dimensions();
    let mut out: ImageBuffer<Rgba<u8>, Vec<u8>> = ImageBuffer::new(width, height);

    // Sample background color from corner pixels (top-left, top-right, bottom-left, bottom-right)
    let c1 = img.get_pixel(0, 0);
    let c2 = img.get_pixel(width.saturating_sub(1), 0);
    let c3 = img.get_pixel(0, height.saturating_sub(1));
    let c4 = img.get_pixel(width.saturating_sub(1), height.saturating_sub(1));

    let bg_r = (c1[0] as u32 + c2[0] as u32 + c3[0] as u32 + c4[0] as u32) / 4;
    let bg_g = (c1[1] as u32 + c2[1] as u32 + c3[1] as u32 + c4[1] as u32) / 4;
    let bg_b = (c1[2] as u32 + c2[2] as u32 + c3[2] as u32 + c4[2] as u32) / 4;

    let sens = sensitivity.unwrap_or(40) as f32;
    let tolerance = (sens * 2.55) as f32;

    for (x, y, pixel) in img.enumerate_pixels() {
        let Rgba([r, g, b, a]) = *pixel;

        let dr = (r as f32 - bg_r as f32).abs();
        let dg = (g as f32 - bg_g as f32).abs();
        let db = (b as f32 - bg_b as f32).abs();
        let color_dist = (dr * dr + dg * dg + db * db).sqrt();

        // Check if pixel is background or near corner background color
        if color_dist < tolerance || a < 50 {
            out.put_pixel(x, y, Rgba([0, 0, 0, 0]));
        } else {
            out.put_pixel(x, y, Rgba([r, g, b, a]));
        }
    }

    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_nobg.png", stem));

    out.save(&out_path)
        .map_err(|e| format!("Failed to save transparent PNG: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. Official Stamp & Seal Isolator
// ─────────────────────────────────────────────────────────────────────────────

/// Isolates official ink stamps (red, purple, blue, green, or magenta) from paper
/// backgrounds, setting all surrounding document text and paper to alpha=0.
#[tauri::command]
pub async fn extract_official_stamp(
    input_path: String,
    output_dir: String,
    stamp_color: Option<String>, // "red", "blue", "purple", "all"
) -> Result<String, String> {
    let path = Path::new(&input_path);
    if !path.is_file() {
        return Err("Input document does not exist".to_string());
    }

    let img = image::open(path)
        .map_err(|e| format!("Failed to open image: {}", e))?
        .to_rgba8();

    let (width, height) = img.dimensions();
    let mut out: ImageBuffer<Rgba<u8>, Vec<u8>> = ImageBuffer::new(width, height);
    let color_filter = stamp_color.unwrap_or_else(|| "all".to_string()).to_lowercase();

    let mut min_x = width;
    let mut min_y = height;
    let mut max_x = 0u32;
    let mut max_y = 0u32;
    let mut found_stamp_pixel = false;

    for (x, y, pixel) in img.enumerate_pixels() {
        let Rgba([r, g, b, _a]) = *pixel;

        let r_f = r as f32;
        let g_f = g as f32;
        let b_f = b as f32;

        let is_red = r_f > 130.0 && r_f > g_f * 1.35 && r_f > b_f * 1.35;
        let is_blue = b_f > 130.0 && b_f > r_f * 1.25 && b_f > g_f * 1.15;
        let is_purple = (r_f > 110.0 && b_f > 110.0) && (g_f < r_f * 0.8) && (g_f < b_f * 0.8);
        let is_green = g_f > 130.0 && g_f > r_f * 1.3 && g_f > b_f * 1.3;

        let is_stamp = match color_filter.as_str() {
            "red" => is_red,
            "blue" => is_blue,
            "purple" => is_purple,
            "green" => is_green,
            _ => is_red || is_blue || is_purple || is_green,
        };

        if is_stamp {
            out.put_pixel(x, y, Rgba([r, g, b, 255]));
            if x < min_x { min_x = x; }
            if y < min_y { min_y = y; }
            if x > max_x { max_x = x; }
            if y > max_y { max_y = y; }
            found_stamp_pixel = true;
        } else {
            out.put_pixel(x, y, Rgba([0, 0, 0, 0]));
        }
    }

    let cropped = if found_stamp_pixel && max_x >= min_x && max_y >= min_y {
        let pad = 10u32;
        let cx = min_x.saturating_sub(pad);
        let cy = min_y.saturating_sub(pad);
        let cw = ((max_x + pad + 1).min(width)).saturating_sub(cx);
        let ch = ((max_y + pad + 1).min(height)).saturating_sub(cy);
        if cw > 0 && ch > 0 {
            image::imageops::crop_imm(&out, cx, cy, cw, ch).to_image()
        } else {
            out
        }
    } else {
        out
    };

    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
    let out_dir = Path::new(&output_dir);
    std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    let out_path = out_dir.join(format!("{}_stamp.png", stem));

    cropped
        .save(&out_path)
        .map_err(|e| format!("Failed to save stamp PNG: {}", e))?;

    Ok(out_path.to_string_lossy().to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_remove_photo_background_creates_png() {
        let temp_dir = std::env::temp_dir();
        let test_img_path = temp_dir.join("axora_test_bg_img.png");
        let mut img: ImageBuffer<Rgba<u8>, Vec<u8>> = ImageBuffer::new(100, 100);
        for (_x, _y, pixel) in img.enumerate_pixels_mut() {
            *pixel = Rgba([255, 255, 255, 255]); // White background
        }
        // Add foreground square
        for x in 40..60 {
            for y in 40..60 {
                img.put_pixel(x, y, Rgba([20, 20, 20, 255]));
            }
        }
        img.save(&test_img_path).unwrap();

        let res = remove_photo_background(
            test_img_path.to_str().unwrap().to_string(),
            temp_dir.to_str().unwrap().to_string(),
            Some(30),
        ).await;

        assert!(res.is_ok());
        let out_p = res.unwrap();
        assert!(std::path::Path::new(&out_p).exists());
        let _ = std::fs::remove_file(&test_img_path);
        let _ = std::fs::remove_file(&out_p);
    }

    #[tokio::test]
    async fn test_extract_official_stamp_detects_red() {
        let temp_dir = std::env::temp_dir();
        let test_stamp_path = temp_dir.join("axora_test_stamp_img.png");
        let mut img: ImageBuffer<Rgba<u8>, Vec<u8>> = ImageBuffer::new(80, 80);
        for (_x, _y, pixel) in img.enumerate_pixels_mut() {
            *pixel = Rgba([250, 250, 240, 255]); // Paper
        }
        // Add red stamp circle
        for x in 30..50 {
            for y in 30..50 {
                img.put_pixel(x, y, Rgba([220, 20, 20, 255]));
            }
        }
        img.save(&test_stamp_path).unwrap();

        let res = extract_official_stamp(
            test_stamp_path.to_str().unwrap().to_string(),
            temp_dir.to_str().unwrap().to_string(),
            Some("red".to_string()),
        ).await;

        assert!(res.is_ok());
        let out_p = res.unwrap();
        assert!(std::path::Path::new(&out_p).exists());
        let _ = std::fs::remove_file(&test_stamp_path);
        let _ = std::fs::remove_file(&out_p);
    }
}

