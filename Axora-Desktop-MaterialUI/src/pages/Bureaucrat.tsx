import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import {
  FileText, Scissors, CreditCard, Upload,
  Download, CheckCircle2, Loader2,
} from "lucide-react";
import { MdRipple } from "../components/MdRipple";
import { useToast } from "../components/ToastNotification";

// ─────────────────────────────────────────────────────────────────────────────
// Tabs definition
// ─────────────────────────────────────────────────────────────────────────────
const TABS = [
  { id: "resizer", label: "Target Resizer", icon: FileText },
  { id: "signature", label: "Signature Extractor", icon: Scissors },
  { id: "idcard", label: "ID Card Stitcher", icon: CreditCard },
];

// ─────────────────────────────────────────────────────────────────────────────
// Helper: file drop zone component
// ─────────────────────────────────────────────────────────────────────────────
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
      filters: accept ? [{ name: "Files", extensions: accept.split(",").map((e) => e.trim().replace(".", "")) }] : [],
    });
    if (selected) onFile(selected as string);
  };

  return (
    <MdRipple
      onClick={handleClick}
      className="w-full border-2 border-dashed rounded-2xl p-6 flex flex-col items-center gap-3 cursor-pointer transition-colors"
      style={{
        borderColor: file ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)",
        backgroundColor: file ? "rgba(11,87,208,0.05)" : "var(--md-sys-color-surface-container)",
      }}
      color="var(--md-sys-color-primary)"
    >
      <Upload size={28} style={{ color: file ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface-variant)" }} />
      {file ? (
        <div className="text-center">
          <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-primary)" }}>
            {file.split("\\").pop()}
          </p>
          <p className="text-xs mt-0.5" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
            Click to change
          </p>
        </div>
      ) : (
        <div className="text-center">
          <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
            {label}
          </p>
          <p className="text-xs mt-0.5" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
            Click to browse
          </p>
        </div>
      )}
    </MdRipple>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 1: Strict Target KB Resizer
// ─────────────────────────────────────────────────────────────────────────────
function TargetResizer() {
  const { success, error } = useToast();
  const [inputFile, setInputFile] = useState("");
  const [targetKb, setTargetKb] = useState(48);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{
    output_path: string; achieved_kb: number; quality_used: number; target_kb: number;
  } | null>(null);

  const handleResize = async () => {
    if (!inputFile) return error("Please select an image file first.");
    setLoading(true);
    try {
      const outputDir = inputFile.substring(0, inputFile.lastIndexOf("\\"));
      const res = await invoke<any>("resize_to_target_kb", {
        inputPath: inputFile,
        outputDir,
        targetKb,
      });
      setResult(res);
      success(`Compressed to ${res.achieved_kb}KB (target: ${targetKb}KB) using quality ${res.quality_used}`);
    } catch (e: any) {
      error(`Resize failed: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-5">
      <div
        className="rounded-2xl p-5"
        style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}
      >
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          Binary Search Compression
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Iteratively adjusts JPEG quality in-memory to land precisely below your KB limit.
        </p>

        <DropZone label="Select Image (JPG, PNG, WEBP)" accept=".jpg,.jpeg,.png,.webp,.bmp" onFile={setInputFile} file={inputFile} />

        {/* Target KB input */}
        <div className="mt-4">
          <label className="text-sm font-medium block mb-2" style={{ color: "var(--md-sys-color-on-surface)" }}>
            Target Size: {targetKb} KB
          </label>
          <div className="flex items-center gap-3">
            <input
              type="range" min={5} max={500} step={1}
              value={targetKb}
              onChange={(e) => setTargetKb(parseInt(e.target.value))}
              className="flex-1 h-2 rounded-full appearance-none cursor-pointer"
              style={{ accentColor: "var(--md-sys-color-primary)" }}
            />
            <input
              type="number" min={5} max={5000}
              value={targetKb}
              onChange={(e) => setTargetKb(parseInt(e.target.value) || 48)}
              className="w-20 text-center text-sm font-medium rounded-xl px-2 py-2 border outline-none"
              style={{
                backgroundColor: "var(--md-sys-color-surface-container)",
                borderColor: "var(--md-sys-color-outline-variant)",
                color: "var(--md-sys-color-on-surface)",
              }}
            />
            <span className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>KB</span>
          </div>
        </div>

        <motion.button
          onClick={handleResize}
          disabled={loading || !inputFile}
          className="mt-4 w-full flex items-center justify-center gap-2 py-3 rounded-full font-medium text-sm disabled:opacity-50"
          style={{ backgroundColor: "var(--md-sys-color-primary)", color: "var(--md-sys-color-on-primary)" }}
          whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
        >
          {loading ? <Loader2 size={16} className="animate-spin" /> : <Scissors size={16} />}
          {loading ? "Compressing..." : "Resize to Target"}
        </motion.button>
      </div>

      {/* Result */}
      <AnimatePresence>
        {result && (
          <motion.div
            initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -8 }}
            className="rounded-2xl p-4 flex items-start gap-3"
            style={{ backgroundColor: "rgba(11,87,208,0.08)", border: "1px solid rgba(11,87,208,0.2)" }}
          >
            <CheckCircle2 size={20} style={{ color: "var(--md-sys-color-primary)", flexShrink: 0, marginTop: 2 }} />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-primary)" }}>
                Compressed to {result.achieved_kb} KB (Quality: {result.quality_used}%)
              </p>
              <p className="text-xs mt-0.5 truncate" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                {result.output_path}
              </p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 2: Signature Extractor
// ─────────────────────────────────────────────────────────────────────────────
function SignatureExtractor() {
  const { success, error } = useToast();
  const [inputFile, setInputFile] = useState("");
  const [threshold, setThreshold] = useState(140);
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleExtract = async () => {
    if (!inputFile) return error("Please select a signature image.");
    setLoading(true);
    try {
      const outputDir = inputFile.substring(0, inputFile.lastIndexOf("\\"));
      const path = await invoke<string>("extract_signature", {
        inputPath: inputFile,
        outputDir,
        threshold,
      });
      setOutputPath(path);
      success("Signature extracted with transparent background!");
    } catch (e: any) {
      error(`Extraction failed: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          Signature Background Removal
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Strips gray/yellow paper backgrounds, converts ink to pure black, outputs transparent PNG.
        </p>

        <DropZone label="Select Signature Image" accept=".jpg,.jpeg,.png,.webp,.bmp" onFile={setInputFile} file={inputFile} />

        <div className="mt-4">
          <label className="text-sm font-medium block mb-2" style={{ color: "var(--md-sys-color-on-surface)" }}>
            Ink Threshold: {threshold} (lower = more ink detected)
          </label>
          <input
            type="range" min={80} max={220} step={1}
            value={threshold}
            onChange={(e) => setThreshold(parseInt(e.target.value))}
            className="w-full h-2 rounded-full appearance-none cursor-pointer"
            style={{ accentColor: "var(--md-sys-color-primary)" }}
          />
          <div className="flex justify-between text-xs mt-1" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
            <span>Dark ink only</span>
            <span>Include light strokes</span>
          </div>
        </div>

        <motion.button
          onClick={handleExtract}
          disabled={loading || !inputFile}
          className="mt-4 w-full flex items-center justify-center gap-2 py-3 rounded-full font-medium text-sm disabled:opacity-50"
          style={{ backgroundColor: "var(--md-sys-color-secondary)", color: "var(--md-sys-color-on-secondary)" }}
          whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
        >
          {loading ? <Loader2 size={16} className="animate-spin" /> : <Scissors size={16} />}
          {loading ? "Extracting..." : "Extract Signature"}
        </motion.button>
      </div>

      <AnimatePresence>
        {outputPath && (
          <motion.div
            initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl p-4 flex items-center gap-3"
            style={{ backgroundColor: "rgba(0,99,155,0.08)", border: "1px solid rgba(0,99,155,0.2)" }}
          >
            <CheckCircle2 size={20} style={{ color: "var(--md-sys-color-secondary)", flexShrink: 0 }} />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-secondary)" }}>
                Transparent PNG saved
              </p>
              <p className="text-xs mt-0.5 truncate" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                {outputPath}
              </p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 3: ID Card Stitcher
// ─────────────────────────────────────────────────────────────────────────────
function IdCardStitcher() {
  const { success, error } = useToast();
  const [frontFile, setFrontFile] = useState("");
  const [backFile, setBackFile] = useState("");
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleStitch = async () => {
    if (!frontFile && !backFile) return error("Please select at least one card image.");
    setLoading(true);
    try {
      const outputDir = (frontFile || backFile).substring(0, (frontFile || backFile).lastIndexOf("\\"));
      const path = await invoke<string>("stitch_id_card_pdf", {
        frontPath: frontFile,
        backPath: backFile,
        outputDir,
      });
      setOutputPath(path);
      success("ID card PDF generated — print-ready A4 layout!");
    } catch (e: any) {
      error(`Stitching failed: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          A4 ID Card Layout (ISO ID-1)
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Places front and back of your ID card on a standard A4 canvas at 85.6×54mm each.
        </p>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <p className="text-xs font-medium mb-2" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>FRONT SIDE</p>
            <DropZone label="Front of ID" accept=".jpg,.jpeg,.png,.webp" onFile={setFrontFile} file={frontFile} />
          </div>
          <div>
            <p className="text-xs font-medium mb-2" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>BACK SIDE</p>
            <DropZone label="Back of ID" accept=".jpg,.jpeg,.png,.webp" onFile={setBackFile} file={backFile} />
          </div>
        </div>

        {/* A4 Preview diagram */}
        <div
          className="mt-4 rounded-xl p-4 flex flex-col items-center gap-2"
          style={{ backgroundColor: "var(--md-sys-color-surface-container)", border: "1px solid var(--md-sys-color-outline-variant)" }}
        >
          <p className="text-xs font-medium" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>A4 Preview Layout</p>
          <div className="w-24 h-32 rounded border-2 flex flex-col items-center justify-center gap-1.5"
            style={{ borderColor: "var(--md-sys-color-outline-variant)", backgroundColor: "var(--md-sys-color-surface)" }}>
            <div className="w-16 h-9 rounded border flex items-center justify-center text-[8px]"
              style={{ borderColor: "var(--md-sys-color-primary)", backgroundColor: "rgba(11,87,208,0.05)", color: "var(--md-sys-color-primary)" }}>
              Front
            </div>
            <div className="w-16 h-9 rounded border flex items-center justify-center text-[8px]"
              style={{ borderColor: "var(--md-sys-color-secondary)", backgroundColor: "rgba(0,99,155,0.05)", color: "var(--md-sys-color-secondary)" }}>
              Back
            </div>
          </div>
          <p className="text-[10px]" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>210 × 297 mm</p>
        </div>

        <motion.button
          onClick={handleStitch}
          disabled={loading || (!frontFile && !backFile)}
          className="mt-4 w-full flex items-center justify-center gap-2 py-3 rounded-full font-medium text-sm disabled:opacity-50"
          style={{ backgroundColor: "var(--md-sys-color-tertiary)", color: "var(--md-sys-color-on-tertiary)" }}
          whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
        >
          {loading ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />}
          {loading ? "Generating PDF..." : "Generate PDF"}
        </motion.button>
      </div>

      <AnimatePresence>
        {outputPath && (
          <motion.div
            initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl p-4 flex items-center gap-3"
            style={{ backgroundColor: "rgba(107,63,160,0.08)", border: "1px solid rgba(107,63,160,0.2)" }}
          >
            <CheckCircle2 size={20} style={{ color: "var(--md-sys-color-tertiary)", flexShrink: 0 }} />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-tertiary)" }}>PDF ready</p>
              <p className="text-xs mt-0.5 truncate" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>{outputPath}</p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Bureaucrat Page
// ─────────────────────────────────────────────────────────────────────────────
export default function Bureaucrat() {
  const [activeTab, setActiveTab] = useState("resizer");

  return (
    <div className="flex flex-col min-h-full">
      <motion.header className="mb-6" initial={{ opacity: 0, y: -12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }}>
        <h2 className="text-3xl font-medium mb-1.5 flex items-center gap-2.5" style={{ color: "var(--md-sys-color-on-surface)" }}>
          <FileText size={28} style={{ color: "var(--md-sys-color-primary)" }} />
          Bureaucrat Suite
        </h2>
        <p className="text-base" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Form-ready image tools — compress, clean, and compile documents for official submissions.
        </p>
      </motion.header>

      {/* MD3 Tab Bar */}
      <div
        className="flex gap-1 p-1 rounded-2xl mb-5 flex-shrink-0"
        style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
      >
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

      {/* Tab content */}
      <div className="flex-1 overflow-y-auto">
        <AnimatePresence mode="wait">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -8 }}
            transition={{ duration: 0.2, ease: [0.2, 0, 0, 1] }}
          >
            {activeTab === "resizer" && <TargetResizer />}
            {activeTab === "signature" && <SignatureExtractor />}
            {activeTab === "idcard" && <IdCardStitcher />}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
