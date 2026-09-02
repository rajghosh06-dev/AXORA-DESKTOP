/**
 * FormStudio — Official Documents & PDF Suite
 * (Renamed from "Bureaucrat" to "Form Studio" in the MD3 taxonomy)
 *
 * Tabs:
 *   1. Target Resizer   — Binary-search JPEG compression to exact KB target
 *   2. Signature Extractor — Strip paper, isolate ink, export transparent PNG
 *   3. ID Card Stitcher — Front + back on A4 PDF canvas
 *   4. PDF Builder      — Ordered image-to-PDF with per-page index badges
 */

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import {
  FileText,
  Scissors,
  CreditCard,
  BookImage,
  Upload,
  Download,
  CheckCircle2,
  Loader2,
  Trash2,
  Plus,
  FileStack,
} from "lucide-react";
import { MdRipple } from "../components/MdRipple";
import { useToast } from "../components/ToastNotification";

// ─── Tab definitions ──────────────────────────────────────────────────────────
const TABS = [
  { id: "resizer",     label: "Target Resizer",       icon: FileText },
  { id: "signature",   label: "Signature Extractor",   icon: Scissors },
  { id: "bgremoval",   label: "AI Background Remover", icon: Scissors },
  { id: "stamp",       label: "Official Stamp Isolator", icon: CheckCircle2 },
  { id: "idcard",      label: "ID Card Stitcher",      icon: CreditCard },
  { id: "pdfbuilder",  label: "PDF Builder",           icon: BookImage },
];

// ─── Shared DropZone ──────────────────────────────────────────────────────────
function DropZone({
  label,
  accept,
  onFile,
  file,
}: {
  label: string;
  accept?: string;
  onFile: (path: string) => void;
  file?: string;
}) {
  const handleClick = async () => {
    const selected = await openDialog({
      multiple: false,
      title: label,
      filters: accept
        ? [{ name: "Files", extensions: accept.split(",").map((e) => e.trim().replace(".", "")) }]
        : [],
    });
    if (selected) onFile(selected as string);
  };

  return (
    <MdRipple
      onClick={handleClick}
      className="w-full border-2 border-dashed rounded-2xl p-6 flex flex-col items-center gap-3 cursor-pointer transition-colors"
      style={{
        borderColor: file
          ? "var(--md-sys-color-primary)"
          : "var(--md-sys-color-outline-variant)",
        backgroundColor: file
          ? "color-mix(in srgb, var(--md-sys-color-primary) 5%, transparent)"
          : "var(--md-sys-color-surface-container)",
      }}
      color="var(--md-sys-color-primary)"
    >
      <Upload
        size={22}
        style={{
          color: file
            ? "var(--md-sys-color-primary)"
            : "var(--md-sys-color-on-surface-variant)",
        }}
      />
      <div className="text-center">
        <p
          style={{
            fontSize: "13px",
            fontWeight: 500,
            color: file
              ? "var(--md-sys-color-primary)"
              : "var(--md-sys-color-on-surface)",
          }}
        >
          {file ? file.split(/[\\/]/).pop() : label}
        </p>
        {!file && (
          <p
            style={{
              fontSize: "11px",
              color: "var(--md-sys-color-on-surface-variant)",
              marginTop: "2px",
            }}
          >
            Click to browse
          </p>
        )}
      </div>
    </MdRipple>
  );
}

// ─── Tab 1: Target Resizer ────────────────────────────────────────────────────
function TargetResizer() {
  const toast = useToast();
  const [inputPath, setInputPath] = useState("");
  const [targetKb, setTargetKb] = useState("150");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{ path: string; sizeBytes: number } | null>(null);

  const handleRun = async () => {
    if (!inputPath) return toast.warning("Select an image first");
    const kb = parseInt(targetKb, 10);
    if (isNaN(kb) || kb < 1) return toast.warning("Enter a valid KB target");

    setLoading(true);
    setResult(null);
    try {
      const [outPath, sizeBytes] = await invoke<[string, number]>("resize_to_target_kb", {
        inputPath,
        targetKb: kb,
      });
      setResult({ path: outPath, sizeBytes });
      toast.success(`Saved: ${(sizeBytes / 1024).toFixed(1)} KB`);
    } catch (e) {
      toast.error(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <DropZone label="Select Image (JPEG/PNG)" accept=".jpg,.jpeg,.png,.webp" onFile={setInputPath} file={inputPath} />
      <div className="flex gap-3">
        <div className="flex-1">
          <label style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase" }}>
            Target Size (KB)
          </label>
          <input
            type="number"
            value={targetKb}
            onChange={(e) => setTargetKb(e.target.value)}
            className="w-full mt-1 px-4 py-2.5 rounded-xl outline-none"
            style={{
              backgroundColor: "var(--md-sys-color-surface-container-high)",
              color: "var(--md-sys-color-on-surface)",
              border: "1px solid var(--md-sys-color-outline-variant)",
              fontSize: "14px",
            }}
            min="10"
            max="5000"
          />
        </div>
        <div className="flex items-end">
          <MdRipple
            onClick={handleRun}
            className="px-6 py-2.5 rounded-xl flex items-center gap-2"
            style={{
              backgroundColor: "var(--md-sys-color-primary)",
              color: "var(--md-sys-color-on-primary)",
              border: "none",
              cursor: loading ? "not-allowed" : "pointer",
              opacity: loading ? 0.7 : 1,
              fontSize: "13px",
              fontWeight: 500,
            }}
            color="var(--md-sys-color-on-primary)"
          >
            {loading ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />}
            {loading ? "Processing…" : "Compress"}
          </MdRipple>
        </div>
      </div>
      {result && (
        <motion.div
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          className="flex items-center gap-3 p-4 rounded-2xl"
          style={{ backgroundColor: "var(--md-sys-color-primary-container)" }}
        >
          <CheckCircle2 size={18} style={{ color: "var(--md-sys-color-primary)" }} />
          <div>
            <p style={{ fontSize: "13px", fontWeight: 600, color: "var(--md-sys-color-on-primary-container)" }}>
              {(result.sizeBytes / 1024).toFixed(1)} KB — {result.path.split(/[\\/]/).pop()}
            </p>
            <p style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", wordBreak: "break-all" }}>
              {result.path}
            </p>
          </div>
        </motion.div>
      )}
    </div>
  );
}

// ─── Tab 2: Signature Extractor ───────────────────────────────────────────────
function SignatureExtractor() {
  const toast = useToast();
  const [inputPath, setInputPath] = useState("");
  const [threshold, setThreshold] = useState("200");
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleRun = async () => {
    if (!inputPath) return toast.warning("Select an image first");
    setLoading(true);
    setOutputPath("");
    try {
      const out = await invoke<string>("extract_signature", {
        inputPath,
        bgThreshold: parseInt(threshold, 10),
      });
      setOutputPath(out);
      toast.success("Signature extracted successfully");
    } catch (e) {
      toast.error(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <DropZone label="Select Signature Image" accept=".jpg,.jpeg,.png,.webp,.bmp" onFile={setInputPath} file={inputPath} />
      <div className="flex gap-3">
        <div className="flex-1">
          <label style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase" }}>
            Background Threshold (0–255)
          </label>
          <input
            type="range"
            min="100"
            max="254"
            value={threshold}
            onChange={(e) => setThreshold(e.target.value)}
            className="w-full mt-2"
            style={{ accentColor: "var(--md-sys-color-primary)" }}
          />
          <p style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)" }}>
            Pixels brighter than {threshold} are stripped as background
          </p>
        </div>
        <div className="flex items-end">
          <MdRipple
            onClick={handleRun}
            className="px-6 py-2.5 rounded-xl flex items-center gap-2"
            style={{
              backgroundColor: "var(--md-sys-color-primary)",
              color: "var(--md-sys-color-on-primary)",
              border: "none",
              cursor: "pointer",
              fontSize: "13px",
              fontWeight: 500,
            }}
            color="var(--md-sys-color-on-primary)"
          >
            {loading ? <Loader2 size={16} className="animate-spin" /> : <Scissors size={16} />}
            Extract
          </MdRipple>
        </div>
      </div>
      {outputPath && (
        <motion.div
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-4 rounded-2xl"
          style={{ backgroundColor: "var(--md-sys-color-primary-container)" }}
        >
          <p style={{ fontSize: "12px", color: "var(--md-sys-color-on-primary-container)" }}>
            ✓ Saved to: {outputPath}
          </p>
        </motion.div>
      )}
    </div>
  );
}

// ─── Tab 3: ID Card Stitcher ──────────────────────────────────────────────────
function IDCardStitcher() {
  const toast = useToast();
  const [frontPath, setFrontPath] = useState("");
  const [backPath, setBackPath] = useState("");
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleRun = async () => {
    if (!frontPath || !backPath) return toast.warning("Select both front and back images");
    setLoading(true);
    setOutputPath("");
    try {
      const downloadDir = await invoke<string>("get_download_dir");
      const out = await invoke<string>("stitch_id_card_pdf", {
        frontPath,
        backPath,
        outputDir: downloadDir,
      });
      setOutputPath(out);
      toast.success("ID card PDF created successfully");
    } catch (e) {
      toast.error(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase", marginBottom: "6px", display: "block" }}>Front Side</label>
          <DropZone label="Select Front" accept=".jpg,.jpeg,.png" onFile={setFrontPath} file={frontPath} />
        </div>
        <div>
          <label style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase", marginBottom: "6px", display: "block" }}>Back Side</label>
          <DropZone label="Select Back" accept=".jpg,.jpeg,.png" onFile={setBackPath} file={backPath} />
        </div>
      </div>
      <MdRipple
        onClick={handleRun}
        className="w-full py-3 rounded-xl flex items-center justify-center gap-2"
        style={{
          backgroundColor: "var(--md-sys-color-primary)",
          color: "var(--md-sys-color-on-primary)",
          border: "none",
          cursor: "pointer",
          fontSize: "13px",
          fontWeight: 500,
        }}
        color="var(--md-sys-color-on-primary)"
      >
        {loading ? <Loader2 size={16} className="animate-spin" /> : <CreditCard size={16} />}
        {loading ? "Stitching…" : "Generate ID Card PDF"}
      </MdRipple>
      {outputPath && (
        <motion.div
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-4 rounded-2xl"
          style={{ backgroundColor: "var(--md-sys-color-primary-container)" }}
        >
          <p style={{ fontSize: "12px", color: "var(--md-sys-color-on-primary-container)" }}>
            ✓ Saved: {outputPath}
          </p>
        </motion.div>
      )}
    </div>
  );
}

// ─── Tab 4: Ordered PDF Builder ───────────────────────────────────────────────
interface PageItem {
  path: string;
  name: string;
  pageIndex: number; // 1-based, in selection order
}

function OrderedPdfBuilder() {
  const toast = useToast();
  const [pages, setPages] = useState<PageItem[]>([]);
  const [docName, setDocName] = useState("My Document");
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleAddImages = async () => {
    const selected = await openDialog({
      multiple: true,
      title: "Select Images (in page order)",
      filters: [{ name: "Images", extensions: ["jpg", "jpeg", "png", "webp", "bmp"] }],
    });

    if (!selected) return;
    const paths = Array.isArray(selected) ? selected : [selected];
    const startIndex = pages.length + 1;

    const newItems: PageItem[] = paths.map((p, i) => ({
      path: p,
      name: p.split(/[\\/]/).pop() || p,
      pageIndex: startIndex + i,
    }));

    setPages((prev) => [...prev, ...newItems]);
    setOutputPath("");
  };

  const removePage = (index: number) => {
    setPages((prev) => {
      const updated = prev.filter((_, i) => i !== index);
      // Re-number pages sequentially
      return updated.map((item, i) => ({ ...item, pageIndex: i + 1 }));
    });
  };

  const clearAll = () => {
    setPages([]);
    setOutputPath("");
  };

  const handleCompile = async () => {
    if (pages.length === 0) return toast.warning("Add at least one image");
    setLoading(true);
    setOutputPath("");
    try {
      const downloadDir = await invoke<string>("get_download_dir");
      const orderedPaths = pages.map((p) => p.path);
      const out = await invoke<string>("compile_ordered_pdf", {
        orderedPaths,
        outputName: docName || "Axora PDF",
        outputDir: downloadDir,
      });
      setOutputPath(out);
      toast.success(`PDF compiled: ${pages.length} pages`);
    } catch (e) {
      toast.error(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      {/* Controls row */}
      <div className="flex gap-3 items-end">
        <div className="flex-1">
          <label
            style={{
              fontSize: "11px",
              color: "var(--md-sys-color-on-surface-variant)",
              fontWeight: 600,
              letterSpacing: "0.08em",
              textTransform: "uppercase",
              marginBottom: "6px",
              display: "block",
            }}
          >
            Document Name
          </label>
          <input
            type="text"
            value={docName}
            onChange={(e) => setDocName(e.target.value)}
            placeholder="My Document"
            className="w-full px-4 py-2.5 rounded-xl outline-none"
            style={{
              backgroundColor: "var(--md-sys-color-surface-container-high)",
              color: "var(--md-sys-color-on-surface)",
              border: "1px solid var(--md-sys-color-outline-variant)",
              fontSize: "14px",
            }}
          />
        </div>
        <MdRipple
          onClick={handleAddImages}
          className="px-4 py-2.5 rounded-xl flex items-center gap-2"
          style={{
            backgroundColor: "var(--md-sys-color-secondary-container)",
            color: "var(--md-sys-color-on-secondary-container)",
            border: "none",
            cursor: "pointer",
            fontSize: "13px",
            fontWeight: 500,
            whiteSpace: "nowrap",
          }}
          color="var(--md-sys-color-on-secondary-container)"
        >
          <Plus size={16} />
          Add Images
        </MdRipple>
        {pages.length > 0 && (
          <MdRipple
            onClick={clearAll}
            className="px-4 py-2.5 rounded-xl flex items-center gap-2"
            style={{
              backgroundColor: "var(--md-sys-color-error-container)",
              color: "var(--md-sys-color-on-error-container)",
              border: "none",
              cursor: "pointer",
              fontSize: "13px",
              fontWeight: 500,
            }}
            color="var(--md-sys-color-on-error-container)"
          >
            <Trash2 size={16} />
            Clear
          </MdRipple>
        )}
      </div>

      {/* Page grid */}
      {pages.length === 0 ? (
        <div
          className="rounded-2xl p-8 flex flex-col items-center gap-3 text-center"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container)",
            border: "2px dashed color-mix(in srgb, var(--md-sys-color-outline-variant) 40%, transparent)",
            minHeight: "180px",
            justifyContent: "center",
          }}
        >
          <FileStack size={32} style={{ color: "var(--md-sys-color-on-surface-variant)", opacity: 0.5 }} />
          <div>
            <p style={{ fontSize: "14px", fontWeight: 500, color: "var(--md-sys-color-on-surface)" }}>
              No images added yet
            </p>
            <p style={{ fontSize: "12px", color: "var(--md-sys-color-on-surface-variant)", marginTop: "4px" }}>
              The order you select images is the page order in the PDF
            </p>
          </div>
        </div>
      ) : (
        <div className="grid gap-2" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))" }}>
          <AnimatePresence>
            {pages.map((page, i) => (
              <motion.div
                key={`${page.path}-${i}`}
                layout
                initial={{ opacity: 0, scale: 0.85 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.85 }}
                transition={{ type: "spring", stiffness: 400, damping: 25 }}
                className="relative rounded-2xl overflow-hidden"
                style={{
                  backgroundColor: "var(--md-sys-color-surface-container)",
                  border: "1px solid color-mix(in srgb, var(--md-sys-color-outline-variant) 30%, transparent)",
                }}
              >
                {/* Page index badge */}
                <div
                  className="absolute top-2 left-2 z-10 px-2 py-0.5 rounded-full"
                  style={{
                    backgroundColor: "var(--md-sys-color-primary)",
                    color: "var(--md-sys-color-on-primary)",
                    fontSize: "10px",
                    fontWeight: 700,
                  }}
                >
                  Page {page.pageIndex}
                </div>

                {/* Remove button */}
                <button
                  onClick={() => removePage(i)}
                  className="absolute top-2 right-2 z-10 w-6 h-6 rounded-full flex items-center justify-center"
                  style={{
                    backgroundColor: "var(--md-sys-color-error)",
                    color: "var(--md-sys-color-on-error)",
                    border: "none",
                    cursor: "pointer",
                  }}
                >
                  <Trash2 size={10} />
                </button>

                {/* Image preview */}
                <div
                  className="flex items-center justify-center"
                  style={{
                    height: "100px",
                    backgroundColor: "var(--md-sys-color-surface-container-high)",
                    marginTop: "28px",
                  }}
                >
                  <img
                    src={`https://tauri-localhost-asset/${page.path}`}
                    alt={page.name}
                    style={{ maxWidth: "100%", maxHeight: "100%", objectFit: "contain" }}
                    onError={(e) => {
                      (e.target as HTMLImageElement).style.display = "none";
                    }}
                  />
                </div>

                {/* File name */}
                <div
                  className="px-2 py-2"
                  style={{
                    fontSize: "10px",
                    color: "var(--md-sys-color-on-surface-variant)",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                  title={page.name}
                >
                  {page.name}
                </div>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>
      )}

      {/* Summary + compile button */}
      {pages.length > 0 && (
        <div className="flex items-center gap-3">
          <div style={{ fontSize: "12px", color: "var(--md-sys-color-on-surface-variant)", flex: 1 }}>
            {pages.length} page{pages.length !== 1 ? "s" : ""} ready to compile
          </div>
          <MdRipple
            onClick={handleCompile}
            className="px-6 py-2.5 rounded-xl flex items-center gap-2"
            style={{
              backgroundColor: "var(--md-sys-color-primary)",
              color: "var(--md-sys-color-on-primary)",
              border: "none",
              cursor: loading ? "not-allowed" : "pointer",
              opacity: loading ? 0.7 : 1,
              fontSize: "13px",
              fontWeight: 500,
            }}
            color="var(--md-sys-color-on-primary)"
          >
            {loading ? <Loader2 size={16} className="animate-spin" /> : <BookImage size={16} />}
            {loading ? `Compiling ${pages.length} pages…` : "Compile PDF"}
          </MdRipple>
        </div>
      )}

      {/* Output result */}
      {outputPath && (
        <motion.div
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          className="flex items-center gap-3 p-4 rounded-2xl"
          style={{ backgroundColor: "var(--md-sys-color-primary-container)" }}
        >
          <CheckCircle2 size={18} style={{ color: "var(--md-sys-color-primary)" }} />
          <div>
            <p style={{ fontSize: "13px", fontWeight: 600, color: "var(--md-sys-color-on-primary-container)" }}>
              PDF compiled successfully!
            </p>
            <p style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", wordBreak: "break-all" }}>
              {outputPath}
            </p>
          </div>
        </motion.div>
      )}
    </div>
  );
}

// ─── Tab 5: AI Background Remover ──────────────────────────────────────────────
function BackgroundRemover() {
  const toast = useToast();
  const [inputPath, setInputPath] = useState("");
  const [sensitivity, setSensitivity] = useState(40);
  const [loading, setLoading] = useState(false);
  const [resultPath, setResultPath] = useState<string | null>(null);

  const handleRun = async () => {
    if (!inputPath) return toast.warning("Select a portrait photo or scan first");

    setLoading(true);
    setResultPath(null);
    try {
      const outPath = await invoke<string>("remove_photo_background", {
        inputPath,
        outputDir: "",
        sensitivity: Number(sensitivity),
      });
      setResultPath(outPath);
      toast.success("Background stripped successfully!");
    } catch (e) {
      toast.error(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <DropZone
        label="Select Portrait or Photo"
        accept=".jpg,.jpeg,.png,.webp,.bmp"
        onFile={setInputPath}
        file={inputPath}
      />
      <div>
        <div className="flex justify-between mb-1">
          <label style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase" }}>
            Removal Sensitivity ({sensitivity}%)
          </label>
        </div>
        <input
          type="range"
          min="10"
          max="90"
          value={sensitivity}
          onChange={(e) => setSensitivity(Number(e.target.value))}
          className="w-full accent-indigo-600"
        />
      </div>

      <MdRipple
        onClick={handleRun}
        className="w-full py-3 rounded-2xl flex items-center justify-center gap-2 font-medium transition-opacity"
        style={{
          backgroundColor: "var(--md-sys-color-primary)",
          color: "var(--md-sys-color-on-primary)",
          cursor: loading ? "not-allowed" : "pointer",
          opacity: loading ? 0.7 : 1,
        }}
        color="var(--md-sys-color-on-primary)"
      >
        {loading ? <Loader2 size={16} className="animate-spin" /> : <Scissors size={16} />}
        {loading ? "Stripping Background..." : "Remove Background"}
      </MdRipple>

      {resultPath && (
        <motion.div
          initial={{ opacity: 0, y: 6 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-4 rounded-2xl flex items-center gap-3"
          style={{ backgroundColor: "var(--md-sys-color-surface-container-high)" }}
        >
          <CheckCircle2 size={20} className="text-emerald-500 flex-shrink-0" />
          <div className="min-w-0 flex-1">
            <p style={{ fontSize: "13px", fontWeight: 600, color: "var(--md-sys-color-on-surface)" }}>
              Transparent PNG Saved
            </p>
            <p className="truncate" style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)" }}>
              {resultPath}
            </p>
          </div>
        </motion.div>
      )}
    </div>
  );
}

// ─── Tab 6: Official Stamp Isolator ────────────────────────────────────────────
function StampExtractor() {
  const toast = useToast();
  const [inputPath, setInputPath] = useState("");
  const [stampColor, setStampColor] = useState("all");
  const [loading, setLoading] = useState(false);
  const [resultPath, setResultPath] = useState<string | null>(null);

  const handleRun = async () => {
    if (!inputPath) return toast.warning("Select a scanned document first");

    setLoading(true);
    setResultPath(null);
    try {
      const outPath = await invoke<string>("extract_official_stamp", {
        inputPath,
        outputDir: "",
        stampColor,
      });
      setResultPath(outPath);
      toast.success("Official seal isolated!");
    } catch (e) {
      toast.error(String(e));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <DropZone
        label="Select Document Scan with Stamp"
        accept=".jpg,.jpeg,.png,.webp,.bmp"
        onFile={setInputPath}
        file={inputPath}
      />
      <div>
        <label style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)", fontWeight: 600, letterSpacing: "0.08em", textTransform: "uppercase" }}>
          Stamp Ink Color
        </label>
        <select
          value={stampColor}
          onChange={(e) => setStampColor(e.target.value)}
          className="w-full mt-1 p-2.5 rounded-xl border border-outline-variant bg-surface text-on-surface text-sm"
        >
          <option value="all">All Inks (Red, Blue, Purple, Green)</option>
          <option value="red">Red Ink Stamp</option>
          <option value="blue">Blue Ink Stamp</option>
          <option value="purple">Purple / Violet Stamp</option>
          <option value="green">Green Stamp</option>
        </select>
      </div>

      <MdRipple
        onClick={handleRun}
        className="w-full py-3 rounded-2xl flex items-center justify-center gap-2 font-medium transition-opacity"
        style={{
          backgroundColor: "var(--md-sys-color-primary)",
          color: "var(--md-sys-color-on-primary)",
          cursor: loading ? "not-allowed" : "pointer",
          opacity: loading ? 0.7 : 1,
        }}
        color="var(--md-sys-color-on-primary)"
      >
        {loading ? <Loader2 size={16} className="animate-spin" /> : <CheckCircle2 size={16} />}
        {loading ? "Isolating Stamp..." : "Isolate Official Seal"}
      </MdRipple>

      {resultPath && (
        <motion.div
          initial={{ opacity: 0, y: 6 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-4 rounded-2xl flex items-center gap-3"
          style={{ backgroundColor: "var(--md-sys-color-surface-container-high)" }}
        >
          <CheckCircle2 size={20} className="text-emerald-500 flex-shrink-0" />
          <div className="min-w-0 flex-1">
            <p style={{ fontSize: "13px", fontWeight: 600, color: "var(--md-sys-color-on-surface)" }}>
              Stamp Isolated (Transparent PNG)
            </p>
            <p className="truncate" style={{ fontSize: "11px", color: "var(--md-sys-color-on-surface-variant)" }}>
              {resultPath}
            </p>
          </div>
        </motion.div>
      )}
    </div>
  );
}

// ─── Main FormStudio export ───────────────────────────────────────────────────
export default function FormStudio() {
  const [activeTab, setActiveTab] = useState("resizer");

  const renderTab = () => {
    switch (activeTab) {
      case "resizer":    return <TargetResizer />;
      case "signature":  return <SignatureExtractor />;
      case "bgremoval":  return <BackgroundRemover />;
      case "stamp":      return <StampExtractor />;
      case "idcard":     return <IDCardStitcher />;
      case "pdfbuilder": return <OrderedPdfBuilder />;
      default:           return <TargetResizer />;
    }
  };

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500 relative pb-4">
      {/* ── Page Header ──────────────────────────────────────────── */}
      <header className="mb-8">
        <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
          <FileStack className="text-md-primary" size={28} />
          Form Studio
        </h2>
        <p className="text-md-on-surface-variant text-lg">
          Official document tools — compress, extract, stitch, and compile.
        </p>
      </header>

      {/* ── MD3 Tab Bar ──────────────────────────────────────────── */}
      <div
        className="flex gap-1 p-1 rounded-2xl overflow-x-auto scrollbar-hide"
        style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
      >
        {TABS.map((tab) => {
          const Icon = tab.icon;
          const isActive = activeTab === tab.id;
          return (
            <MdRipple
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl flex-shrink-0 transition-colors duration-150"
              style={{
                backgroundColor: isActive
                  ? "var(--md-sys-color-surface)"
                  : "transparent",
                color: isActive
                  ? "var(--md-sys-color-primary)"
                  : "var(--md-sys-color-on-surface-variant)",
                boxShadow: isActive
                  ? "0 1px 4px rgba(0,0,0,0.12)"
                  : "none",
                fontSize: "13px",
                fontWeight: isActive ? 600 : 400,
                cursor: "pointer",
                border: "none",
              }}
              color={isActive ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface)"}
            >
              <Icon size={15} />
              {tab.label}
            </MdRipple>
          );
        })}
      </div>

      {/* ── Tab Content ──────────────────────────────────────────── */}
      <div
        className="rounded-3xl p-5"
        style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
      >
        <AnimatePresence mode="wait">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -6 }}
            transition={{ duration: 0.18, ease: [0.2, 0, 0, 1] }}
          >
            {renderTab()}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
