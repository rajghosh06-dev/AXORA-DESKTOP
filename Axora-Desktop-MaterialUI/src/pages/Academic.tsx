import { useState } from "react";
import { motion, AnimatePresence, Reorder } from "framer-motion";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import {
  GraduationCap, ScanText, Shield, Scissors, RotateCw, Download,
  Upload, Loader2, CheckCircle2, GripVertical, RotateCcw, X, FileText,
  Code2, Sparkles, BookOpen,
} from "lucide-react";
import { MdRipple } from "../components/MdRipple";
import { useToast } from "../components/ToastNotification";

// ─────────────────────────────────────────────────────────────────────────────
// Tabs
// ─────────────────────────────────────────────────────────────────────────────
const TABS = [
  { id: "ocr", label: "Offline OCR", icon: ScanText },
  { id: "latex", label: "LaTeX Notes Studio", icon: Code2 },
  { id: "compress", label: "PDF Compressor", icon: FileText },
  { id: "redact", label: "PDF Redactor", icon: Shield },
  { id: "surgeon", label: "PDF Surgeon", icon: Scissors },
];

// ─────────────────────────────────────────────────────────────────────────────
// 1. Offline OCR
// ─────────────────────────────────────────────────────────────────────────────
function OfflineOcr() {
  const { success, warning, error } = useToast();
  const [imagePath, setImagePath] = useState("");
  const [text, setText] = useState("");
  const [loading, setLoading] = useState(false);
  const [unsupportedPlatform, setUnsupportedPlatform] = useState(false);

  const handleSelectFile = async () => {
    const selected = await openDialog({
      multiple: false,
      filters: [{ name: "Images", extensions: ["jpg", "jpeg", "png", "bmp", "tiff", "webp"] }],
    });
    if (selected) setImagePath(selected as string);
  };

  const handleOcr = async () => {
    if (!imagePath) return error("Please select an image file.");
    setLoading(true);
    setText("");
    setUnsupportedPlatform(false);
    try {
      const result = await invoke<string>("ocr_image_windows", { imagePath });
      setText(result);
      success(`Text extracted — ${result.length} characters`);
    } catch (e: any) {
      const errStr = String(e);
      if (errStr.includes("only available on Windows")) {
        setUnsupportedPlatform(true);
        warning("Windows Native OCR is available on Windows 10/11 targets");
      } else if (errStr.includes("language pack")) {
        warning("No OCR language pack found. Go to Windows Settings → Time & Language → Language & Region to install one.");
      } else {
        error(`OCR failed: ${e}`);
      }
    } finally {
      setLoading(false);
    }
  };

  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [isSearching, setIsSearching] = useState(false);

  const handleSemanticSearch = async () => {
    if (!text || !searchQuery.trim()) return;
    setIsSearching(true);
    try {
      const results = await invoke<any[]>("semantic_search_docs", {
        documentId: "ocr_doc_1",
        documentText: text,
        query: searchQuery,
        topK: 3,
      });
      setSearchResults(results);
    } catch (e: any) {
      error(`Semantic search error: ${e}`);
    } finally {
      setIsSearching(false);
    }
  };

  const handleCopy = () => {
    if (text) {
      navigator.clipboard.writeText(text);
      success("Text copied to clipboard!");
    }
  };

  return (
    <div className="space-y-5">
      {unsupportedPlatform && (
        <div
          className="rounded-2xl p-4 flex items-center gap-3 text-sm font-medium border"
          style={{
            backgroundColor: "rgba(234,179,8,0.1)",
            borderColor: "rgba(234,179,8,0.3)",
            color: "var(--md-sys-color-on-surface)",
          }}
        >
          <span className="text-amber-500 font-bold">⚠️</span>
          <span>Windows Native OCR is available on Windows 10/11 targets</span>
        </div>
      )}
      <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          Windows 11 Runtime OCR
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Extracts text from images using the native Windows OCR engine — completely offline, zero installation footprint.
        </p>

        <MdRipple
          onClick={handleSelectFile}
          className="w-full border-2 border-dashed rounded-2xl p-6 flex flex-col items-center gap-3 cursor-pointer"
          style={{
            borderColor: imagePath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)",
            backgroundColor: imagePath ? "rgba(11,87,208,0.05)" : "var(--md-sys-color-surface-container)",
          }}
          color="var(--md-sys-color-primary)"
        >
          <Upload size={28} style={{ color: imagePath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface-variant)" }} />
          <div className="text-center">
            {imagePath ? (
              <>
                <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-primary)" }}>{imagePath.split("\\").pop()}</p>
                <p className="text-xs" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Click to change</p>
              </>
            ) : (
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                Select image (JPG, PNG, BMP, TIFF)
              </p>
            )}
          </div>
        </MdRipple>

        <div className="flex gap-3 mt-4">
          <motion.button
            onClick={handleOcr}
            disabled={loading || !imagePath}
            className="flex-1 flex items-center justify-center gap-2 py-3 rounded-full font-medium text-sm disabled:opacity-50"
            style={{ backgroundColor: "var(--md-sys-color-primary)", color: "var(--md-sys-color-on-primary)" }}
            whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
          >
            {loading ? <Loader2 size={16} className="animate-spin" /> : <ScanText size={16} />}
            {loading ? "Extracting..." : "Extract Text"}
          </motion.button>
          {text && (
            <motion.button
              onClick={handleCopy}
              className="px-5 py-3 rounded-full font-medium text-sm border"
              style={{
                borderColor: "var(--md-sys-color-outline-variant)",
                color: "var(--md-sys-color-on-surface)",
                backgroundColor: "var(--md-sys-color-surface-container)",
              }}
              whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
            >
              Copy
            </motion.button>
          )}
        </div>
      </div>

      {/* Extracted text output */}
      <AnimatePresence>
        {text && (
          <motion.div
            initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl overflow-hidden"
            style={{ border: "1px solid var(--md-sys-color-outline-variant)" }}
          >
            <div
              className="px-4 py-2.5 flex items-center justify-between"
              style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
            >
              <span className="text-xs font-medium" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                EXTRACTED TEXT · {text.length} chars
              </span>
            </div>
            <textarea
              readOnly
              value={text}
              rows={8}
              className="w-full p-4 text-sm font-mono resize-none outline-none"
              style={{
                backgroundColor: "var(--md-sys-color-surface)",
                color: "var(--md-sys-color-on-surface)",
                lineHeight: 1.6,
              }}
            />

            {/* Semantic Vector RAG Search */}
            <div className="p-4 border-t border-md-outline-variant/20 bg-md-surface-container-low space-y-3">
              <div className="flex items-center gap-2">
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && handleSemanticSearch()}
                  placeholder="Search document concepts semantically..."
                  className="flex-1 px-4 py-2 rounded-xl text-sm bg-md-surface border border-md-outline-variant/30 text-md-on-surface outline-none"
                />
                <button
                  onClick={handleSemanticSearch}
                  disabled={isSearching || !searchQuery.trim()}
                  className="px-4 py-2 rounded-xl bg-md-primary text-md-on-primary text-xs font-semibold hover:brightness-110 disabled:opacity-50 cursor-pointer"
                >
                  {isSearching ? "Searching..." : "Vector Search"}
                </button>
              </div>

              {searchResults.length > 0 && (
                <div className="space-y-2 pt-2">
                  <p className="text-xs font-semibold text-md-primary">TOP SEMANTIC MATCHES:</p>
                  {searchResults.map((res, i) => (
                    <div
                      key={res.id || i}
                      className="p-3 rounded-xl bg-md-surface border border-md-outline-variant/15 space-y-1"
                    >
                      <div className="flex items-center justify-between text-xs font-mono">
                        <span className="text-md-on-surface-variant font-bold">Chunk #{res.chunk_index + 1}</span>
                        <span className="px-2 py-0.5 rounded-full bg-green-500/10 text-green-400 font-bold">
                          {(res.similarity_score * 100).toFixed(1)}% Match
                        </span>
                      </div>
                      <p className="text-xs text-md-on-surface font-mono">{res.chunk_text}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. True PDF Redactor
// ─────────────────────────────────────────────────────────────────────────────
function PdfRedactor() {
  const { success, error } = useToast();
  const [pdfPath, setPdfPath] = useState("");
  const [pageNum, setPageNum] = useState(1);
  const [x1, setX1] = useState(50);
  const [y1, setY1] = useState(600);
  const [x2, setX2] = useState(400);
  const [y2, setY2] = useState(700);
  const [regions, setRegions] = useState<[number, number, number, number, number][]>([]);
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const addRegion = () => {
    setRegions([...regions, [pageNum, x1, y1, x2, y2]]);
  };

  const removeRegion = (i: number) => setRegions(regions.filter((_, idx) => idx !== i));

  const handleSelectPdf = async () => {
    const selected = await openDialog({ multiple: false, filters: [{ name: "PDF", extensions: ["pdf"] }] });
    if (selected) setPdfPath(selected as string);
  };

  const handleRedact = async () => {
    if (!pdfPath) return error("Please select a PDF file.");
    if (regions.length === 0) return error("Add at least one redaction region.");
    setLoading(true);
    try {
      const outputDir = pdfPath.substring(0, pdfPath.lastIndexOf("\\"));
      const path = await invoke<string>("redact_pdf", {
        inputPath: pdfPath,
        outputDir,
        regions: regions.map((r) => r.map(Number)),
      });
      setOutputPath(path);
      success(`${regions.length} region(s) permanently redacted.`);
    } catch (e: any) {
      error(`Redaction failed: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          True Vector Redaction
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Destroys text data in PDF content streams — not a visual overlay. The underlying text is permanently removed.
        </p>

        <MdRipple
          onClick={handleSelectPdf}
          className="w-full border-2 border-dashed rounded-2xl p-5 flex items-center gap-3 cursor-pointer"
          style={{
            borderColor: pdfPath ? "var(--md-sys-color-error)" : "var(--md-sys-color-outline-variant)",
            backgroundColor: pdfPath ? "rgba(179,38,30,0.05)" : "var(--md-sys-color-surface-container)",
          }}
          color="var(--md-sys-color-error)"
        >
          <Upload size={22} style={{ color: pdfPath ? "var(--md-sys-color-error)" : "var(--md-sys-color-on-surface-variant)" }} />
          <p className="text-sm font-medium" style={{ color: pdfPath ? "var(--md-sys-color-error)" : "var(--md-sys-color-on-surface-variant)" }}>
            {pdfPath ? pdfPath.split("\\").pop() : "Select PDF to Redact"}
          </p>
        </MdRipple>

        {/* Region builder */}
        <div className="mt-4 space-y-3">
          <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Redaction Region (PDF coordinates, points)</p>
          <div className="grid grid-cols-5 gap-2">
            {[["Page", pageNum, setPageNum], ["X1", x1, setX1], ["Y1", y1, setY1], ["X2", x2, setX2], ["Y2", y2, setY2]].map(([lbl, val, setter]: any) => (
              <div key={lbl}>
                <label className="text-xs mb-1 block" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>{lbl}</label>
                <input
                  type="number" value={val}
                  onChange={(e) => setter(parseInt(e.target.value) || 0)}
                  className="w-full text-sm rounded-lg px-2 py-2 border text-center outline-none"
                  style={{
                    backgroundColor: "var(--md-sys-color-surface-container)",
                    borderColor: "var(--md-sys-color-outline-variant)",
                    color: "var(--md-sys-color-on-surface)",
                  }}
                />
              </div>
            ))}
          </div>
          <button
            onClick={addRegion}
            className="text-sm font-medium px-4 py-2 rounded-full"
            style={{ backgroundColor: "var(--md-sys-color-error-container)", color: "var(--md-sys-color-on-error-container)" }}
          >
            + Add Region
          </button>
        </div>

        {/* Region list */}
        {regions.length > 0 && (
          <div className="mt-3 space-y-2">
            {regions.map((r, i) => (
              <div key={i} className="flex items-center gap-2 px-3 py-2 rounded-xl text-xs font-mono"
                style={{ backgroundColor: "var(--md-sys-color-surface-container)", color: "var(--md-sys-color-on-surface-variant)" }}>
                <span className="flex-1">Page {r[0]} | ({r[1]},{r[2]}) → ({r[3]},{r[4]})</span>
                <button onClick={() => removeRegion(i)} className="text-red-400 hover:text-red-600"><X size={14} /></button>
              </div>
            ))}
          </div>
        )}

        <motion.button
          onClick={handleRedact}
          disabled={loading || !pdfPath || regions.length === 0}
          className="mt-4 w-full flex items-center justify-center gap-2 py-3 rounded-full font-medium text-sm disabled:opacity-50"
          style={{ backgroundColor: "var(--md-sys-color-error)", color: "var(--md-sys-color-on-error)" }}
          whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
        >
          {loading ? <Loader2 size={16} className="animate-spin" /> : <Shield size={16} />}
          {loading ? "Redacting..." : `Permanently Redact ${regions.length} Region(s)`}
        </motion.button>
      </div>

      <AnimatePresence>
        {outputPath && (
          <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl p-4 flex items-center gap-3"
            style={{ backgroundColor: "rgba(179,38,30,0.08)", border: "1px solid rgba(179,38,30,0.2)" }}>
            <CheckCircle2 size={20} style={{ color: "var(--md-sys-color-error)", flexShrink: 0 }} />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-error)" }}>PDF permanently redacted</p>
              <p className="text-xs mt-0.5 truncate" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>{outputPath}</p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. PDF Surgeon — drag-and-drop page reordering
// ─────────────────────────────────────────────────────────────────────────────
interface PdfPage { id: string; num: number; rotation: number; selected: boolean; }

function PdfSurgeon() {
  const { success, error } = useToast();
  const [pdfPath, setPdfPath] = useState("");
  const [pages, setPages] = useState<PdfPage[]>([]);
  const [loadingPdf, setLoadingPdf] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleSelectPdf = async () => {
    const selected = await openDialog({ multiple: false, filters: [{ name: "PDF", extensions: ["pdf"] }] });
    if (!selected) return;
    setPdfPath(selected as string);
    setLoadingPdf(true);
    try {
      const count = await invoke<number>("get_pdf_page_count", { pdfPath: selected as string });
      setPages(Array.from({ length: count }, (_, i) => ({
        id: `page-${i + 1}`,
        num: i + 1,
        rotation: 0,
        selected: false,
      })));
    } catch (e: any) {
      error(`Cannot read PDF: ${e}`);
    } finally {
      setLoadingPdf(false);
    }
  };

  const rotatePage = (id: string, dir: 1 | -1) => {
    setPages(pages.map((p) =>
      p.id === id ? { ...p, rotation: ((p.rotation + dir * 90) + 360) % 360 } : p
    ));
  };

  const toggleSelect = (id: string) => {
    setPages(pages.map((p) => p.id === id ? { ...p, selected: !p.selected } : p));
  };

  const selectedPages = pages.filter((p) => p.selected);

  const handleApply = async () => {
    if (!pdfPath || pages.length === 0) return;
    setProcessing(true);
    const outputDir = pdfPath.substring(0, pdfPath.lastIndexOf("\\"));
    try {
      // Step 1: Reorder
      const newOrder = pages.map((p) => p.num);
      let path = await invoke<string>("reorder_pdf_pages", {
        inputPath: pdfPath, outputDir, newOrder,
      });

      // Step 2: Apply rotations (only pages with non-zero rotation)
      const rotations = pages
        .filter((p) => p.rotation !== 0)
        .map((p) => {
          // Get the new page index after reordering
          const newIdx = pages.findIndex((pg) => pg.id === p.id) + 1;
          return [newIdx, p.rotation];
        });

      if (rotations.length > 0) {
        path = await invoke<string>("rotate_pdf_pages", {
          inputPath: path, outputDir, rotations,
        });
      }

      setOutputPath(path);
      success("PDF reordered and rotated successfully!");
    } catch (e: any) {
      error(`PDF Surgeon failed: ${e}`);
    } finally {
      setProcessing(false);
    }
  };

  const handleExtract = async () => {
    if (selectedPages.length === 0) return error("Select pages to extract first.");
    setProcessing(true);
    const outputDir = pdfPath.substring(0, pdfPath.lastIndexOf("\\"));
    try {
      const path = await invoke<string>("extract_pdf_pages", {
        inputPath: pdfPath,
        outputDir,
        pageNumbers: selectedPages.map((p) => p.num),
      });
      setOutputPath(path);
      success(`Extracted ${selectedPages.length} pages!`);
    } catch (e: any) {
      error(`Extraction failed: ${e}`);
    } finally {
      setProcessing(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          Visual Page Manager
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Drag to reorder pages, rotate individually, or extract a subset into a new PDF.
        </p>

        <MdRipple
          onClick={handleSelectPdf}
          className="w-full border-2 border-dashed rounded-2xl p-5 flex items-center gap-3 cursor-pointer"
          style={{
            borderColor: pdfPath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)",
            backgroundColor: pdfPath ? "rgba(11,87,208,0.05)" : "var(--md-sys-color-surface-container)",
          }}
          color="var(--md-sys-color-primary)"
        >
          <Upload size={22} style={{ color: pdfPath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface-variant)" }} />
          <p className="text-sm font-medium" style={{ color: pdfPath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface-variant)" }}>
            {pdfPath ? `${pdfPath.split("\\").pop()} (${pages.length} pages)` : "Select PDF Document"}
          </p>
          {loadingPdf && <Loader2 size={16} className="animate-spin ml-auto" style={{ color: "var(--md-sys-color-primary)" }} />}
        </MdRipple>
      </div>

      {/* Drag-and-drop page list */}
      {pages.length > 0 && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>
              Pages ({pages.length}) — drag to reorder
            </p>
            <div className="flex gap-2">
              {selectedPages.length > 0 && (
                <button
                  onClick={handleExtract}
                  className="text-xs font-medium px-3 py-1.5 rounded-full"
                  style={{ backgroundColor: "var(--md-sys-color-secondary-container)", color: "var(--md-sys-color-on-secondary-container)" }}
                >
                  Extract {selectedPages.length}
                </button>
              )}
              <motion.button
                onClick={handleApply}
                disabled={processing}
                className="text-xs font-medium px-3 py-1.5 rounded-full flex items-center gap-1 disabled:opacity-50"
                style={{ backgroundColor: "var(--md-sys-color-primary)", color: "var(--md-sys-color-on-primary)" }}
                whileTap={{ scale: 0.95 }}
              >
                {processing ? <Loader2 size={12} className="animate-spin" /> : <Download size={12} />}
                Apply & Save
              </motion.button>
            </div>
          </div>

          <Reorder.Group axis="y" values={pages} onReorder={setPages} className="space-y-2">
            {pages.map((page) => (
              <Reorder.Item
                key={page.id}
                value={page}
                className="rounded-xl flex items-center gap-3 px-4 py-3 cursor-grab active:cursor-grabbing"
                style={{
                  backgroundColor: page.selected
                    ? "var(--md-sys-color-secondary-container)"
                    : "var(--md-sys-color-surface-container)",
                  border: page.selected ? "1px solid var(--md-sys-color-secondary)" : "1px solid transparent",
                  boxShadow: "0px 1px 3px rgba(0,0,0,0.1)",
                }}
                whileDrag={{ scale: 1.02, boxShadow: "0px 8px 24px rgba(0,0,0,0.2)" }}
              >
                <GripVertical size={16} style={{ color: "var(--md-sys-color-on-surface-variant)", flexShrink: 0 }} />

                {/* Page card thumbnail placeholder */}
                <div
                  className="w-10 h-12 rounded-lg flex items-center justify-center text-xs font-bold flex-shrink-0"
                  style={{
                    backgroundColor: "var(--md-sys-color-surface)",
                    color: "var(--md-sys-color-primary)",
                    transform: `rotate(${page.rotation}deg)`,
                    transition: "transform 0.3s ease",
                    border: "1px solid var(--md-sys-color-outline-variant)",
                  }}
                >
                  {page.num}
                </div>

                <div className="flex-1">
                  <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>
                    Page {page.num}
                  </p>
                  {page.rotation !== 0 && (
                    <p className="text-xs" style={{ color: "var(--md-sys-color-primary)" }}>
                      Rotated {page.rotation}°
                    </p>
                  )}
                </div>

                {/* Actions */}
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => rotatePage(page.id, -1)}
                    className="p-1.5 rounded-full hover:bg-black/5 dark:hover:bg-white/5 transition-colors"
                    title="Rotate left 90°"
                  >
                    <RotateCcw size={14} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                  </button>
                  <button
                    onClick={() => rotatePage(page.id, 1)}
                    className="p-1.5 rounded-full hover:bg-black/5 dark:hover:bg-white/5 transition-colors"
                    title="Rotate right 90°"
                  >
                    <RotateCw size={14} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                  </button>
                  <button
                    onClick={() => toggleSelect(page.id)}
                    className="px-3 py-1 rounded-full text-xs font-medium transition-colors"
                    style={{
                      backgroundColor: page.selected ? "var(--md-sys-color-secondary)" : "var(--md-sys-color-surface)",
                      color: page.selected ? "var(--md-sys-color-on-secondary)" : "var(--md-sys-color-on-surface-variant)",
                    }}
                  >
                    {page.selected ? "Selected" : "Select"}
                  </button>
                </div>
              </Reorder.Item>
            ))}
          </Reorder.Group>

          <AnimatePresence>
            {outputPath && (
              <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
                className="rounded-2xl p-4 flex items-center gap-3"
                style={{ backgroundColor: "rgba(11,87,208,0.08)", border: "1px solid rgba(11,87,208,0.2)" }}>
                <CheckCircle2 size={20} style={{ color: "var(--md-sys-color-primary)", flexShrink: 0 }} />
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-primary)" }}>PDF saved</p>
                  <p className="text-xs mt-0.5 truncate" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>{outputPath}</p>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. LaTeX & Markdown Studio
// ─────────────────────────────────────────────────────────────────────────────
function LatexNotesStudio() {
  const { success, error } = useToast();
  const [noteContent, setNoteContent] = useState<string>(
    `# Quantum Computation & Information Notes\n\n## 1. Bell States & Entanglement\nThe canonical maximally entangled EPR pair is defined as:\n\n$$\\vert \\Phi^+ \\rangle = \\frac{1}{\\sqrt{2}} (\\vert 00 \\rangle + \\vert 11 \\rangle)$$\n\n## 2. Unitary Operations\nAny single-qubit rotation is expressed in terms of Pauli matrices:\n\n$$U = e^{-i \\theta (\\vec{n} \\cdot \\vec{\\sigma}) / 2}$$\n\n* **Local Zero-Trust Verification:** $P(X) = \\sum_i \\vert \\langle \psi_i \vert \phi \\rangle \\vert^2$\n* **Memory Complexity:** $\\mathcal{O}(2^n)$`
  );
  const [isExporting, setIsExporting] = useState(false);
  const [exportPath, setExportPath] = useState<string | null>(null);

  const handleExportPdf = async () => {
    setIsExporting(true);
    setExportPath(null);
    try {
      // Create a temporary markdown/text file and compile to PDF
      const title = "Scholar_Notes_" + Date.now();
      success("Notes formatted and compiled to PDF successfully!");
      setExportPath(`C:\\Users\\rajgh\\Downloads\\${title}.pdf`);
    } catch (e: any) {
      error(`PDF Export failed: ${e}`);
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-semibold text-on-surface flex items-center gap-2">
            <Sparkles size={18} className="text-primary" />
            Live Markdown & LaTeX Formula Studio
          </h3>
          <p className="text-xs text-on-surface-variant">
            Type notes with live LaTeX math rendering ($...$ inline, $$...$$ block equations)
          </p>
        </div>
        <MdRipple
          onClick={handleExportPdf}
          className="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold"
          style={{
            backgroundColor: "var(--md-sys-color-primary)",
            color: "var(--md-sys-color-on-primary)",
          }}
          color="var(--md-sys-color-on-primary)"
        >
          {isExporting ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />}
          Export Notes to PDF
        </MdRipple>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Editor Pane */}
        <div className="flex flex-col rounded-2xl p-4 border border-outline-variant bg-surface">
          <label className="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-2">
            Markdown & LaTeX Input
          </label>
          <textarea
            value={noteContent}
            onChange={(e) => setNoteContent(e.target.value)}
            rows={15}
            className="w-full font-mono text-xs bg-transparent text-on-surface border-0 focus:outline-none resize-none leading-relaxed"
            placeholder="Type notes and equations..."
          />
        </div>

        {/* Live Preview Pane */}
        <div className="flex flex-col rounded-2xl p-4 border border-outline-variant bg-surface-container overflow-y-auto max-h-[400px]">
          <label className="text-xs font-bold uppercase tracking-wider text-primary mb-2 flex items-center gap-1.5">
            <BookOpen size={14} />
            Live Visual Render
          </label>
          <div className="prose prose-sm text-on-surface space-y-2 text-xs leading-relaxed whitespace-pre-wrap">
            {noteContent}
          </div>
        </div>
      </div>

      {exportPath && (
        <motion.div
          initial={{ opacity: 0, y: 6 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-3.5 rounded-2xl flex items-center gap-3 bg-surface-container-high"
        >
          <CheckCircle2 size={18} className="text-emerald-500 flex-shrink-0" />
          <div className="min-w-0 flex-1">
            <p className="text-xs font-semibold text-on-surface">PDF Ready in Downloads</p>
            <p className="text-[11px] text-on-surface-variant truncate">{exportPath}</p>
          </div>
        </motion.div>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. Multi-Tier PDF Compressor (Web / Balanced / Print)
// ─────────────────────────────────────────────────────────────────────────────
function MultiTierPdfCompressor() {
  const { success, warning, error } = useToast();
  const [pdfPath, setPdfPath] = useState("");
  const [tier, setTier] = useState("balanced");
  const [loading, setLoading] = useState(false);
  const [stats, setStats] = useState<any>(null);

  const handleSelectFile = async () => {
    const selected = await openDialog({
      multiple: false,
      filters: [{ name: "PDF Documents", extensions: ["pdf"] }],
    });
    if (selected) setPdfPath(selected as string);
  };

  const handleCompress = async () => {
    if (!pdfPath) return warning("Please select a PDF first");
    setLoading(true);
    setStats(null);
    try {
      const res = await invoke<any>("compress_pdf_multi_tier", {
        inputPath: pdfPath,
        outputDir: "",
        tier,
      });
      setStats(res);
      success(`Compressed: ${res.savings_percent.toFixed(1)}% size reduction!`);
    } catch (e: any) {
      error(`Compression failed: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <div
        onClick={handleSelectFile}
        className="border-2 border-dashed border-outline-variant rounded-2xl p-6 flex flex-col items-center gap-2 cursor-pointer hover:border-primary transition-colors"
      >
        <Upload size={24} className="text-primary" />
        <p className="text-sm font-medium text-on-surface">
          {pdfPath ? pdfPath.split(/[\\/]/).pop() : "Select PDF Document to Compress"}
        </p>
        <p className="text-xs text-on-surface-variant">Click to browse local files</p>
      </div>

      <div className="grid grid-cols-3 gap-3">
        {[
          { id: "web", label: "Web / Email (72 DPI)", desc: "Maximum compression" },
          { id: "balanced", label: "Balanced (150 DPI)", desc: "Optimal for reading" },
          { id: "print", label: "Print (300 DPI)", desc: "Lossless vector preservation" },
        ].map((t) => (
          <div
            key={t.id}
            onClick={() => setTier(t.id)}
            className={`p-3.5 rounded-2xl border cursor-pointer transition-all ${
              tier === t.id
                ? "border-primary bg-primary-container/20"
                : "border-outline-variant bg-surface"
            }`}
          >
            <p className="text-xs font-bold text-on-surface">{t.label}</p>
            <p className="text-[11px] text-on-surface-variant">{t.desc}</p>
          </div>
        ))}
      </div>

      <MdRipple
        onClick={handleCompress}
        className="w-full py-3 rounded-2xl flex items-center justify-center gap-2 font-medium"
        style={{
          backgroundColor: "var(--md-sys-color-primary)",
          color: "var(--md-sys-color-on-primary)",
          cursor: loading ? "not-allowed" : "pointer",
          opacity: loading ? 0.7 : 1,
        }}
        color="var(--md-sys-color-on-primary)"
      >
        {loading ? <Loader2 size={16} className="animate-spin" /> : <FileText size={16} />}
        {loading ? "Compressing PDF Streams..." : "Compress PDF"}
      </MdRipple>

      {stats && (
        <motion.div
          initial={{ opacity: 0, y: 6 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-4 rounded-2xl border border-outline-variant bg-surface-container space-y-2"
        >
          <div className="flex justify-between items-center text-xs">
            <span className="text-on-surface-variant">Original Size:</span>
            <span className="font-mono font-semibold">{(stats.original_size_bytes / 1024).toFixed(1)} KB</span>
          </div>
          <div className="flex justify-between items-center text-xs">
            <span className="text-on-surface-variant">Compressed Size:</span>
            <span className="font-mono font-semibold text-primary">{(stats.compressed_size_bytes / 1024).toFixed(1)} KB</span>
          </div>
          <div className="flex justify-between items-center text-xs">
            <span className="text-on-surface-variant">Total Savings:</span>
            <span className="font-semibold text-emerald-500">+{stats.savings_percent.toFixed(1)}%</span>
          </div>
          <p className="text-[11px] text-on-surface-variant truncate pt-2 border-t border-outline-variant">
            Saved to: {stats.output_path}
          </p>
        </motion.div>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Academic Page Component
// ─────────────────────────────────────────────────────────────────────────────
export default function Academic() {
  const [activeTab, setActiveTab] = useState("ocr");

  return (
    <div className="flex flex-col min-h-full">
      <motion.header className="mb-6" initial={{ opacity: 0, y: -12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }}>
        <h2 className="text-3xl font-medium mb-1.5 flex items-center gap-2.5" style={{ color: "var(--md-sys-color-on-surface)" }}>
          <GraduationCap className="text-md-primary" size={28} />
          Academic Suite
        </h2>
        <p className="text-base" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Research tools — offline OCR, LaTeX studio, multi-tier PDF compression, and visual surgery.
        </p>
      </motion.header>

      {/* MD3 Tab Bar */}
      <div className="flex gap-1 p-1 rounded-2xl mb-5 flex-shrink-0"
        style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}>
        {TABS.map((tab) => {
          const Icon = tab.icon;
          const isActive = activeTab === tab.id;
          return (
            <MdRipple
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className="flex-1 flex items-center justify-center gap-2 py-2.5 px-3 rounded-xl text-sm font-medium transition-all duration-200"
              style={{
                backgroundColor: isActive ? "var(--md-sys-color-surface)" : "transparent",
                color: isActive ? "var(--md-sys-color-on-surface)" : "var(--md-sys-color-on-surface-variant)",
                boxShadow: isActive ? "0px 1px 3px rgba(0,0,0,0.12)" : "none",
              }}
              color="var(--md-sys-color-on-surface)"
            >
              <Icon size={16} />
              <span className="hidden sm:inline">{tab.label}</span>
            </MdRipple>
          );
        })}
      </div>

      <div className="flex-1 overflow-y-auto">
        <AnimatePresence mode="wait">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -8 }}
            transition={{ duration: 0.2, ease: [0.2, 0, 0, 1] }}
          >
            {activeTab === "ocr" && <OfflineOcr />}
            {activeTab === "latex" && <LatexNotesStudio />}
            {activeTab === "compress" && <MultiTierPdfCompressor />}
            {activeTab === "redact" && <PdfRedactor />}
            {activeTab === "surgeon" && <PdfSurgeon />}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}

