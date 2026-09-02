use rayon::prelude::*;
use std::path::Path;
use std::fs::File;
use std::sync::atomic::{AtomicI32, Ordering};
use tauri::{AppHandle, Emitter};

#[derive(Clone, serde::Serialize)]
struct ProgressPayload {
    processed: i32,
    total: i32,
}

// ─────────────────────────────────────────────────────────────────────────────
// Helper: detect if Microsoft Office (Word) is available on this Windows 11 machine
// ─────────────────────────────────────────────────────────────────────────────
fn has_microsoft_office() -> bool {
    let check_script = r#"
try {
    $word = New-Object -ComObject Word.Application
    $word.Quit()
    Write-Output "YES"
} catch {
    Write-Output "NO"
}
"#;
    let temp_dir = std::env::temp_dir();
    let script_path = temp_dir.join("axora_office_check.ps1");
    if std::fs::write(&script_path, check_script).is_err() {
        return false;
    }
    let output = std::process::Command::new("powershell")
        .args(&["-ExecutionPolicy", "Bypass", "-File", script_path.to_str().unwrap()])
        .output();
    let _ = std::fs::remove_file(&script_path);
    if let Ok(out) = output {
        return String::from_utf8_lossy(&out.stdout).trim() == "YES";
    }
    false
}

// ─────────────────────────────────────────────────────────────────────────────
// Helper: detect if LibreOffice is installed (fallback for non-Office machines)
// Checks common Windows 11 install paths.
// ─────────────────────────────────────────────────────────────────────────────
fn find_libreoffice_soffice() -> Option<String> {
    let candidates = [
        r"C:\Program Files\LibreOffice\program\soffice.exe",
        r"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
    ];
    for path in &candidates {
        if Path::new(path).exists() {
            return Some(path.to_string());
        }
    }
    None
}

// ─────────────────────────────────────────────────────────────────────────────
// LibreOffice conversion: convert any supported document to a target format
// ─────────────────────────────────────────────────────────────────────────────
fn convert_via_libreoffice(soffice: &str, input: &str, output_dir: &str, filter: &str) -> bool {
    let result = std::process::Command::new(soffice)
        .args(&[
            "--headless",
            "--convert-to",
            filter,
            "--outdir",
            output_dir,
            input,
        ])
        .output();
    if let Ok(out) = result {
        return out.status.success();
    }
    false
}

// ─────────────────────────────────────────────────────────────────────────────
// PowerShell COM conversion (Microsoft Office path)
// ─────────────────────────────────────────────────────────────────────────────
fn convert_via_office_com(ps_script: &str) -> bool {
    let temp_dir = std::env::temp_dir();
    let rand_id = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_micros();
    let script_path = temp_dir.join(format!("axora_convert_{}.ps1", rand_id));
    if std::fs::write(&script_path, ps_script).is_err() {
        return false;
    }
    let output = std::process::Command::new("powershell")
        .args(&["-ExecutionPolicy", "Bypass", "-File", script_path.to_str().unwrap()])
        .output();
    let _ = std::fs::remove_file(&script_path);
    if let Ok(out) = output {
        return out.status.success();
    }
    false
}

// ─────────────────────────────────────────────────────────────────────────────
// Sanitize path: strip Windows extended-length UNC prefix (\\?\)
// so that PowerShell COM and LibreOffice accept the path.
// ─────────────────────────────────────────────────────────────────────────────
fn clean_path(p: &Path) -> String {
    p.canonicalize()
        .unwrap_or(p.to_path_buf())
        .to_string_lossy()
        .replace(r"\\?\", "")
        .to_string()
}

// ─────────────────────────────────────────────────────────────────────────────
// Batch Image Processor
// ─────────────────────────────────────────────────────────────────────────────
#[tauri::command]
pub async fn batch_process_images(
    app_handle: AppHandle,
    files: Vec<String>,
    output_dir: String,
    _max_size_value: u32,
    _max_size_unit: String,
    target_ext: String,
) -> Result<String, String> {
    let out_dir = Path::new(&output_dir);
    if !out_dir.exists() {
        std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    }

    // Load dynamic settings
    let settings = crate::settings::load_settings().unwrap_or_else(|_| {
        crate::settings::Settings {
            theme: None,
            theme_accent: None,
            hardware_concurrency: None,
            output_directory: None,
            minimize_to_tray: None,
            enable_splash: None,
            splash_duration: None,
            default_ocr_lang: None,
            clear_metadata: Some(true),
            image_quality: Some(85),
            argon_memory: None,
            argon_iterations: None,
            auto_lock_vault: None,
        }
    });
    let quality = settings.image_quality.unwrap_or(85) as u8;

    let total = files.len() as i32;
    let processed_counter = AtomicI32::new(0);

    let success_count = files
        .par_iter()
        .map(|file_path| {
            let path = Path::new(file_path);
            let mut success = 0;

            if path.is_file() {
                if let Ok(img) = image::open(path) {
                    let stem = path.file_stem().unwrap_or_default().to_string_lossy();
                    let out_path = out_dir.join(format!("{}{}", stem, target_ext));

                    if target_ext == ".jpg" {
                        if let Ok(mut file) = File::create(&out_path) {
                            let mut encoder =
                                image::codecs::jpeg::JpegEncoder::new_with_quality(&mut file, quality);
                            if encoder.encode_image(&img).is_ok() {
                                success = 1;
                            }
                        }
                    } else if img.save(&out_path).is_ok() {
                        success = 1;
                    }
                }
            }

            let current = processed_counter.fetch_add(1, Ordering::Relaxed) + 1;
            let _ = app_handle.emit("batch-progress", ProgressPayload { processed: current, total });
            success
        })
        .sum::<i32>();

    Ok(format!("Processed {} images successfully", success_count))
}

// ─────────────────────────────────────────────────────────────────────────────
// Universal Document Converter
// Priority: MS Office COM (if installed) → LibreOffice (fallback) → image crate
// ─────────────────────────────────────────────────────────────────────────────
#[tauri::command]
pub async fn convert_files(
    files: Vec<String>,
    output_dir: String,
    target_ext: String,
) -> Result<String, String> {
    let out_dir = Path::new(&output_dir);
    if !out_dir.exists() {
        std::fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;
    }

    let has_office = has_microsoft_office();
    let libreoffice_path = find_libreoffice_soffice();
    let out_dir_str = clean_path(out_dir);

    // Load dynamic settings
    let settings = crate::settings::load_settings().unwrap_or_else(|_| {
        crate::settings::Settings {
            theme: None,
            theme_accent: None,
            hardware_concurrency: None,
            output_directory: None,
            minimize_to_tray: None,
            enable_splash: None,
            splash_duration: None,
            default_ocr_lang: None,
            clear_metadata: Some(true),
            image_quality: Some(85),
            argon_memory: None,
            argon_iterations: None,
            auto_lock_vault: None,
        }
    });
    let quality = settings.image_quality.unwrap_or(85) as u8;

    let success_count = files
        .par_iter()
        .map(|file_path| {
            let path = Path::new(file_path);
            if !path.is_file() {
                return 0;
            }

            let stem = path.file_stem().unwrap_or_default().to_string_lossy();
            let ext = path.extension().unwrap_or_default().to_ascii_lowercase();
            let ext_str = ext.to_string_lossy();
            let out_path = out_dir.join(format!("{}{}", stem, target_ext));

            // ── 1. Pure image conversions (always available) ────────────────
            let image_exts = ["jpg", "jpeg", "png", "webp", "bmp", "gif", "tiff"];
            let target_is_image = [".jpg", ".png", ".webp", ".bmp", ".tiff"]
                .contains(&target_ext.as_str());

            if image_exts.contains(&ext_str.as_ref()) && target_is_image {
                if let Ok(img) = image::open(path) {
                    if target_ext == ".jpg" {
                        if let Ok(mut file) = File::create(&out_path) {
                            let mut encoder =
                                image::codecs::jpeg::JpegEncoder::new_with_quality(&mut file, quality);
                            if encoder.encode_image(&img).is_ok() {
                                return 1;
                            }
                        }
                    } else if img.save(&out_path).is_ok() {
                        return 1;
                    }
                }
                return 0;
            }

            let in_abs = clean_path(path);
            let out_abs = format!("{}{}", out_dir_str.trim_end_matches('\\'), format!("\\{}{}", stem, target_ext));

            // ── 2. Native PDF -> DOCX converter via lopdf + docx-rs (Zero External Dependencies) ──
            if ext_str == "pdf" && target_ext == ".docx" {
                if convert_pdf_to_docx_native(path, &out_path) {
                    return 1;
                }
            }

            // ── 3. Microsoft Office COM (Word / PowerPoint) ─────────────────
            if has_office {
                let ps_script = build_office_com_script(&ext_str, &target_ext, &in_abs, &out_abs);
                if let Some(script) = ps_script {
                    if convert_via_office_com(&script) {
                        return 1;
                    }
                }
            }

            // ── 4. LibreOffice fallback ─────────────────────────────────────
            if let Some(ref soffice) = libreoffice_path {
                let lo_filter = libreoffice_filter(&target_ext);
                if let Some(filter) = lo_filter {
                    if convert_via_libreoffice(soffice, &in_abs, &out_dir_str, filter) {
                        return 1;
                    }
                }
            }

            0
        })
        .sum::<i32>();

    Ok(format!("Converted {} files to {}", success_count, target_ext))
}

// ─────────────────────────────────────────────────────────────────────────────
// Native standalone PDF to DOCX conversion via lopdf and docx-rs
// ─────────────────────────────────────────────────────────────────────────────
pub fn convert_pdf_to_docx_native(pdf_path: &Path, docx_path: &Path) -> bool {
    let doc = match lopdf::Document::load(pdf_path) {
        Ok(d) => d,
        Err(_) => return false,
    };

    let mut docx = docx_rs::Docx::new();
    let pages = doc.get_pages();
    let page_numbers: Vec<u32> = pages.keys().cloned().collect();

    for page_num in page_numbers {
        if let Ok(text) = doc.extract_text(&[page_num]) {
            for line in text.lines() {
                let trimmed = line.trim();
                if !trimmed.is_empty() {
                    let paragraph = docx_rs::Paragraph::new().add_run(
                        docx_rs::Run::new().add_text(trimmed)
                    );
                    docx = docx.add_paragraph(paragraph);
                }
            }
        }
    }

    if let Ok(file) = File::create(docx_path) {
        return docx.build().pack(file).is_ok();
    }
    false
}

// ─────────────────────────────────────────────────────────────────────────────
// Build PowerShell COM automation script for MS Office conversions
// ─────────────────────────────────────────────────────────────────────────────
fn build_office_com_script(
    ext_str: &str,
    target_ext: &str,
    in_abs: &str,
    out_abs: &str,
) -> Option<String> {
    let i = in_abs.replace('\'', "''");
    let o = out_abs.replace('\'', "''");

    match (ext_str, target_ext) {
        // PDF → DOCX (Word opens PDF natively in Office 2013+)
        ("pdf", ".docx") => Some(format!(
            r#"
$regPath = 'HKCU:\Software\Microsoft\Office\16.0\Word\Options'
if (!(Test-Path $regPath)) {{ New-Item -Path $regPath -Force | Out-Null }}
Set-ItemProperty -Path $regPath -Name 'DisableConvertPdfWarning' -Value 1 -Type DWord -Force
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {{
    $doc = $word.Documents.Open('{i}')
    $doc.SaveAs([ref] '{o}', [ref] 16)
    $doc.Close()
}} finally {{ $word.Quit() }}
"#,
            i = i,
            o = o
        )),

        // DOCX / DOC → PDF
        ("docx", ".pdf") | ("doc", ".pdf") => Some(format!(
            r#"
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {{
    $doc = $word.Documents.Open('{i}')
    $doc.SaveAs([ref] '{o}', [ref] 17)
    $doc.Close()
}} finally {{ $word.Quit() }}
"#,
            i = i,
            o = o
        )),

        // PPTX / PPT → PDF
        ("pptx", ".pdf") | ("ppt", ".pdf") => Some(format!(
            r#"
$ppt = New-Object -ComObject PowerPoint.Application
try {{
    $presentation = $ppt.Presentations.Open('{i}', $null, $null, $false)
    $presentation.SaveAs('{o}', 32)
    $presentation.Close()
}} finally {{ $ppt.Quit() }}
"#,
            i = i,
            o = o
        )),

        // PDF → PPTX: Use Word to open PDF, save as PPTX indirectly via PowerPoint COM
        // Strategy: Word → DOCX (temp) → PowerPoint opens DOCX and saves as PPTX
        ("pdf", ".pptx") => Some(format!(
            r#"
$regPath = 'HKCU:\Software\Microsoft\Office\16.0\Word\Options'
if (!(Test-Path $regPath)) {{ New-Item -Path $regPath -Force | Out-Null }}
Set-ItemProperty -Path $regPath -Name 'DisableConvertPdfWarning' -Value 1 -Type DWord -Force

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$tempDocx = [System.IO.Path]::GetTempFileName() + '.docx'
try {{
    $doc = $word.Documents.Open('{i}')
    $doc.SaveAs([ref] $tempDocx, [ref] 16)
    $doc.Close()
}} finally {{ $word.Quit() }}

$ppt = New-Object -ComObject PowerPoint.Application
try {{
    $presentation = $ppt.Presentations.Open($tempDocx, $null, $null, $false)
    $presentation.SaveAs('{o}', 24)
    $presentation.Close()
}} finally {{
    $ppt.Quit()
    Remove-Item $tempDocx -ErrorAction SilentlyContinue
}}
"#,
            i = i,
            o = o
        )),

        // XLSX → PDF
        ("xlsx", ".pdf") | ("xls", ".pdf") => Some(format!(
            r#"
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
try {{
    $wb = $excel.Workbooks.Open('{i}')
    $wb.ExportAsFixedFormat(0, '{o}')
    $wb.Close($false)
}} finally {{ $excel.Quit() }}
"#,
            i = i,
            o = o
        )),

        _ => None,
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Map target extension to LibreOffice --convert-to filter name
// ─────────────────────────────────────────────────────────────────────────────
fn libreoffice_filter(target_ext: &str) -> Option<&'static str> {
    match target_ext {
        ".pdf" => Some("pdf"),
        ".docx" => Some("docx"),
        ".doc" => Some("doc"),
        ".pptx" => Some("pptx"),
        ".xlsx" => Some("xlsx"),
        ".odt" => Some("odt"),
        ".txt" => Some("txt"),
        _ => None,
    }
}
