import { useState, useEffect, useCallback } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import { listen } from "@tauri-apps/api/event";
import {
  Clapperboard, Code2, FileVideo, Plus, Trash2, Edit3,
  Copy, Search, Loader2, CheckCircle2, X, Music,
} from "lucide-react";
import { MdRipple } from "../components/MdRipple";
import { useToast } from "../components/ToastNotification";

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────
interface CodeSnippet {
  id: string;
  title: string;
  language: string;
  content: string;
  tags: string[];
  created_at: number;
  updated_at: number;
}

const LANGUAGES = ["cpp", "c", "java", "python", "javascript", "typescript", "rust", "go", "bash", "sql", "other"];
const LANG_COLORS: Record<string, string> = {
  cpp: "#9c27b0", c: "#7b1fa2", java: "#f57c00", python: "#1976d2",
  javascript: "#f9a825", typescript: "#0288d1", rust: "#bf360c",
  go: "#00897b", bash: "#388e3c", sql: "#5c6bc0", other: "#546e7a",
};

const TABS = [
  { id: "vault", label: "Snippet Vault", icon: Code2 },
  { id: "media", label: "Media Stripper", icon: FileVideo },
];

// ─────────────────────────────────────────────────────────────────────────────
// Snippet Vault Component
// ─────────────────────────────────────────────────────────────────────────────
function SnippetVault({ isOverlay = false }: { isOverlay?: boolean }) {
  const { success, error } = useToast();
  const [snippets, setSnippets] = useState<CodeSnippet[]>([]);
  const [search, setSearch] = useState("");
  const [filterLang, setFilterLang] = useState("all");
  const [showModal, setShowModal] = useState(false);
  const [editingSnippet, setEditingSnippet] = useState<Partial<CodeSnippet>>({});
  const [loading, setLoading] = useState(false);

  const loadSnippets = useCallback(async () => {
    try {
      const data = await invoke<CodeSnippet[]>("load_snippets");
      setSnippets(data);
    } catch (e: any) {
      error(`Failed to load snippets: ${e}`);
    }
  }, []);

  useEffect(() => {
    loadSnippets();
  }, [loadSnippets]);

  // Listen for global hotkey event from backend
  useEffect(() => {
    const unlisten = listen("snippet-vault-open", () => {
      // This event is only relevant when used as overlay
    });
    return () => { unlisten.then((f) => f()); };
  }, []);

  const filteredSnippets = snippets.filter((s) => {
    const matchSearch = !search || s.title.toLowerCase().includes(search.toLowerCase()) ||
      s.content.toLowerCase().includes(search.toLowerCase()) ||
      s.tags.some((t) => t.toLowerCase().includes(search.toLowerCase()));
    const matchLang = filterLang === "all" || s.language === filterLang;
    return matchSearch && matchLang;
  });

  const handleSave = async () => {
    if (!editingSnippet.title || !editingSnippet.content) {
      return error("Title and content are required.");
    }
    setLoading(true);
    try {
      const now = Date.now();
      const snippet: CodeSnippet = {
        id: editingSnippet.id || `snip-${now}`,
        title: editingSnippet.title || "",
        language: editingSnippet.language || "other",
        content: editingSnippet.content || "",
        tags: editingSnippet.tags || [],
        created_at: editingSnippet.created_at || now,
        updated_at: now,
      };
      await invoke("save_snippet", { snippet });
      success("Snippet saved to encrypted vault!");
      await loadSnippets();
      setShowModal(false);
      setEditingSnippet({});
    } catch (e: any) {
      error(`Save failed: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await invoke("delete_snippet", { id });
      success("Snippet deleted.");
      await loadSnippets();
    } catch (e: any) {
      error(`Delete failed: ${e}`);
    }
  };

  const copySnippet = (content: string) => {
    navigator.clipboard.writeText(content);
    success("Snippet copied to clipboard!");
  };

  return (
    <div className="space-y-4">
      {!isOverlay && (
        <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
          <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
            Encrypted Code Snippets
          </h3>
          <p className="text-sm" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
            Snippets are stored in an AES-256-GCM encrypted vault. Use Alt+Shift+V to open the floating picker.
          </p>
        </div>
      )}

      {/* Search + Filter bar */}
      <div className="flex gap-2">
        <div className="flex-1 flex items-center gap-2 px-3 py-2 rounded-xl border"
          style={{ backgroundColor: "var(--md-sys-color-surface-container)", borderColor: "var(--md-sys-color-outline-variant)" }}>
          <Search size={16} style={{ color: "var(--md-sys-color-on-surface-variant)", flexShrink: 0 }} />
          <input
            type="text" placeholder="Search snippets..."
            value={search} onChange={(e) => setSearch(e.target.value)}
            className="flex-1 text-sm bg-transparent outline-none"
            style={{ color: "var(--md-sys-color-on-surface)" }}
          />
        </div>
        <select
          value={filterLang} onChange={(e) => setFilterLang(e.target.value)}
          className="text-sm rounded-xl px-3 py-2 border outline-none cursor-pointer"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container)",
            borderColor: "var(--md-sys-color-outline-variant)",
            color: "var(--md-sys-color-on-surface)",
          }}
        >
          <option value="all">All Languages</option>
          {LANGUAGES.map((l) => <option key={l} value={l}>{l.toUpperCase()}</option>)}
        </select>
        <MdRipple
          onClick={() => { setEditingSnippet({}); setShowModal(true); }}
          className="flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium"
          style={{ backgroundColor: "var(--md-sys-color-primary)", color: "var(--md-sys-color-on-primary)" }}
          color="var(--md-sys-color-on-primary)"
        >
          <Plus size={16} />
          New
        </MdRipple>
      </div>

      {/* Snippet list */}
      <div className="space-y-2 max-h-[400px] overflow-y-auto">
        <AnimatePresence>
          {filteredSnippets.length === 0 ? (
            <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }}
              className="py-12 text-center" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
              <Code2 size={32} className="mx-auto mb-3 opacity-40" />
              <p className="text-sm">No snippets yet. Create your first one!</p>
            </motion.div>
          ) : (
            filteredSnippets.map((snippet) => (
              <motion.div
                key={snippet.id}
                initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}
                className="rounded-xl overflow-hidden"
                style={{ border: "1px solid var(--md-sys-color-outline-variant)" }}
              >
                {/* Header */}
                <div className="flex items-center gap-3 px-4 py-3"
                  style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}>
                  <div className="w-2 h-2 rounded-full flex-shrink-0"
                    style={{ backgroundColor: LANG_COLORS[snippet.language] || "#546e7a" }} />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium truncate" style={{ color: "var(--md-sys-color-on-surface)" }}>
                      {snippet.title}
                    </p>
                    <p className="text-xs" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                      {snippet.language.toUpperCase()} · {snippet.content.split("\n").length} lines
                    </p>
                  </div>
                  <div className="flex items-center gap-1">
                    <button onClick={() => copySnippet(snippet.content)}
                      className="p-1.5 rounded-full hover:bg-black/5 dark:hover:bg-white/5 transition-colors" title="Copy">
                      <Copy size={14} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                    </button>
                    <button onClick={() => { setEditingSnippet(snippet); setShowModal(true); }}
                      className="p-1.5 rounded-full hover:bg-black/5 dark:hover:bg-white/5 transition-colors" title="Edit">
                      <Edit3 size={14} style={{ color: "var(--md-sys-color-primary)" }} />
                    </button>
                    <button onClick={() => handleDelete(snippet.id)}
                      className="p-1.5 rounded-full hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors" title="Delete">
                      <Trash2 size={14} style={{ color: "var(--md-sys-color-error)" }} />
                    </button>
                  </div>
                </div>
                {/* Content preview */}
                <pre className="px-4 py-3 text-xs font-mono overflow-x-auto max-h-24"
                  style={{
                    backgroundColor: "var(--md-sys-color-surface)",
                    color: "var(--md-sys-color-on-surface-variant)",
                    lineHeight: 1.5,
                  }}>
                  {snippet.content.slice(0, 200)}{snippet.content.length > 200 ? "..." : ""}
                </pre>
              </motion.div>
            ))
          )}
        </AnimatePresence>
      </div>

      {/* Add/Edit Modal */}
      <AnimatePresence>
        {showModal && (
          <motion.div
            className="fixed inset-0 z-[9998] flex items-center justify-center p-4"
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          >
            <motion.div className="absolute inset-0" style={{ backgroundColor: "rgba(0,0,0,0.4)", backdropFilter: "blur(4px)" }}
              onClick={() => { setShowModal(false); setEditingSnippet({}); }} />
            <motion.div
              className="relative w-full max-w-lg rounded-3xl overflow-hidden z-10"
              style={{
                backgroundColor: "var(--md-sys-color-surface)",
                boxShadow: "0px 8px 32px rgba(0,0,0,0.25), 0px 4px 8px rgba(0,0,0,0.15)",
              }}
              initial={{ scale: 0.9, opacity: 0, y: 20 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.9, opacity: 0, y: 20 }}
              transition={{ duration: 0.25, ease: [0.34, 1.56, 0.64, 1] }}
            >
              <div className="px-6 py-5 flex items-center justify-between border-b"
                style={{ borderColor: "var(--md-sys-color-outline-variant)" }}>
                <h3 className="text-base font-semibold" style={{ color: "var(--md-sys-color-on-surface)" }}>
                  {editingSnippet.id ? "Edit Snippet" : "New Snippet"}
                </h3>
                <button onClick={() => { setShowModal(false); setEditingSnippet({}); }}
                  className="p-2 rounded-full hover:bg-black/5 dark:hover:bg-white/5 transition-colors">
                  <X size={18} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                </button>
              </div>

              <div className="p-6 space-y-4">
                <input
                  type="text" placeholder="Snippet title..."
                  value={editingSnippet.title || ""}
                  onChange={(e) => setEditingSnippet({ ...editingSnippet, title: e.target.value })}
                  className="w-full px-4 py-3 rounded-xl text-sm border outline-none"
                  style={{
                    backgroundColor: "var(--md-sys-color-surface-container)",
                    borderColor: "var(--md-sys-color-outline-variant)",
                    color: "var(--md-sys-color-on-surface)",
                  }}
                />
                <select
                  value={editingSnippet.language || "other"}
                  onChange={(e) => setEditingSnippet({ ...editingSnippet, language: e.target.value })}
                  className="w-full px-4 py-3 rounded-xl text-sm border outline-none cursor-pointer"
                  style={{
                    backgroundColor: "var(--md-sys-color-surface-container)",
                    borderColor: "var(--md-sys-color-outline-variant)",
                    color: "var(--md-sys-color-on-surface)",
                  }}
                >
                  {LANGUAGES.map((l) => <option key={l} value={l}>{l.toUpperCase()}</option>)}
                </select>
                <textarea
                  rows={8} placeholder="Paste your code here..."
                  value={editingSnippet.content || ""}
                  onChange={(e) => setEditingSnippet({ ...editingSnippet, content: e.target.value })}
                  className="w-full px-4 py-3 rounded-xl text-sm font-mono border outline-none resize-none"
                  style={{
                    backgroundColor: "var(--md-sys-color-surface-container)",
                    borderColor: "var(--md-sys-color-outline-variant)",
                    color: "var(--md-sys-color-on-surface)",
                    lineHeight: 1.6,
                  }}
                />
              </div>

              <div className="px-6 pb-5 flex justify-end gap-3">
                <button onClick={() => { setShowModal(false); setEditingSnippet({}); }}
                  className="px-5 py-2.5 rounded-full text-sm font-medium border"
                  style={{ borderColor: "var(--md-sys-color-outline-variant)", color: "var(--md-sys-color-on-surface-variant)" }}>
                  Cancel
                </button>
                <motion.button onClick={handleSave} disabled={loading}
                  className="px-5 py-2.5 rounded-full text-sm font-medium flex items-center gap-2 disabled:opacity-60"
                  style={{ backgroundColor: "var(--md-sys-color-primary)", color: "var(--md-sys-color-on-primary)" }}
                  whileHover={{ scale: 1.03 }} whileTap={{ scale: 0.97 }}>
                  {loading ? <Loader2 size={14} className="animate-spin" /> : null}
                  Save Snippet
                </motion.button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Media Stripper Component
// ─────────────────────────────────────────────────────────────────────────────
function MediaStripper() {
  const { success, error, warning } = useToast();
  const [videoPath, setVideoPath] = useState("");
  const [format, setFormat] = useState<"mp3" | "wav">("mp3");
  const [loading, setLoading] = useState(false);
  const [outputPath, setOutputPath] = useState("");

  const handleSelectVideo = async () => {
    const selected = await openDialog({
      multiple: false,
      filters: [{ name: "Video Files", extensions: ["mp4", "mkv", "avi", "mov", "webm"] }],
    });
    if (selected) setVideoPath(selected as string);
  };

  const handleExtract = async () => {
    if (!videoPath) return error("Please select a video file first.");
    setLoading(true);
    setOutputPath("");
    try {
      const outputDir = videoPath.substring(0, videoPath.lastIndexOf("\\"));
      const path = await invoke<string>("extract_audio", {
        inputPath: videoPath,
        outputDir,
        format,
      });
      setOutputPath(path);
      success(`Audio extracted as ${format.toUpperCase()}!`);
    } catch (e: any) {
      const msg = String(e);
      if (msg.includes("ffmpeg")) {
        warning("ffmpeg not found in PATH. Install ffmpeg from ffmpeg.org and add it to your system PATH for best quality.");
      } else {
        error(`Extraction failed: ${e}`);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-5">
      <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--md-sys-color-surface-container-low)" }}>
        <h3 className="text-base font-medium mb-1" style={{ color: "var(--md-sys-color-on-surface)" }}>
          Audio Extractor
        </h3>
        <p className="text-sm mb-4" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Extract high-quality audio tracks from video files. Uses ffmpeg if available, Windows Media Foundation as fallback.
        </p>

        <MdRipple
          onClick={handleSelectVideo}
          className="w-full border-2 border-dashed rounded-2xl p-8 flex flex-col items-center gap-3 cursor-pointer"
          style={{
            borderColor: videoPath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)",
            backgroundColor: videoPath ? "rgba(11,87,208,0.05)" : "var(--md-sys-color-surface-container)",
          }}
          color="var(--md-sys-color-primary)"
        >
          <motion.div
            animate={videoPath ? { scale: [1, 1.1, 1] } : {}}
            transition={{ duration: 0.4 }}
          >
            <FileVideo size={40} style={{ color: videoPath ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface-variant)" }} />
          </motion.div>
          {videoPath ? (
            <div className="text-center">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-primary)" }}>
                {videoPath.split("\\").pop()}
              </p>
              <p className="text-xs mt-0.5" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Click to change</p>
            </div>
          ) : (
            <div className="text-center">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                Drop video here or click to browse
              </p>
              <p className="text-xs mt-0.5" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                MP4, MKV, AVI, MOV, WEBM
              </p>
            </div>
          )}
        </MdRipple>

        {/* Format selector */}
        <div className="mt-4">
          <p className="text-sm font-medium mb-2" style={{ color: "var(--md-sys-color-on-surface)" }}>Output Format</p>
          <div className="flex gap-2">
            {(["mp3", "wav"] as const).map((fmt) => (
              <MdRipple
                key={fmt}
                onClick={() => setFormat(fmt)}
                className="flex-1 py-2.5 rounded-full text-sm font-medium text-center"
                style={{
                  backgroundColor: format === fmt ? "var(--md-sys-color-primary-container)" : "var(--md-sys-color-surface-container)",
                  color: format === fmt ? "var(--md-sys-color-on-primary-container)" : "var(--md-sys-color-on-surface-variant)",
                  border: format === fmt ? "1px solid var(--md-sys-color-primary)" : "1px solid transparent",
                }}
                color={format === fmt ? "var(--md-sys-color-primary)" : "var(--md-sys-color-on-surface)"}
              >
                {fmt.toUpperCase()}
                <span className="block text-xs opacity-70">{fmt === "mp3" ? "Compressed" : "Lossless"}</span>
              </MdRipple>
            ))}
          </div>
        </div>

        <motion.button
          onClick={handleExtract}
          disabled={loading || !videoPath}
          className="mt-4 w-full flex items-center justify-center gap-2 py-3 rounded-full font-medium text-sm disabled:opacity-50"
          style={{ backgroundColor: "var(--md-sys-color-primary)", color: "var(--md-sys-color-on-primary)" }}
          whileHover={{ scale: 1.02 }} whileTap={{ scale: 0.97 }}
        >
          {loading ? (
            <>
              <Loader2 size={16} className="animate-spin" />
              Extracting...
              <motion.span className="text-xs opacity-70 ml-1" animate={{ opacity: [1, 0.5, 1] }} transition={{ duration: 1.5, repeat: Infinity }}>
                (this may take a moment)
              </motion.span>
            </>
          ) : (
            <>
              <Music size={16} />
              Extract {format.toUpperCase()} Audio
            </>
          )}
        </motion.button>
      </div>

      <AnimatePresence>
        {outputPath && (
          <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl p-4 flex items-center gap-3"
            style={{ backgroundColor: "rgba(11,87,208,0.08)", border: "1px solid rgba(11,87,208,0.2)" }}>
            <CheckCircle2 size={20} style={{ color: "var(--md-sys-color-primary)", flexShrink: 0 }} />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-primary)" }}>
                {format.toUpperCase()} extracted successfully
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
// Floating Snippet Picker Overlay (triggered by Alt+Shift+V)
// ─────────────────────────────────────────────────────────────────────────────
export function SnippetOverlay({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className="fixed inset-0 z-[99997] flex items-center justify-center"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          onClick={onClose}
        >
          <motion.div
            className="w-full max-w-md mx-4 rounded-3xl overflow-hidden"
            style={{
              backgroundColor: "var(--md-sys-color-surface)",
              backdropFilter: "blur(20px)",
              WebkitBackdropFilter: "blur(20px)",
              boxShadow: "0px 12px 48px rgba(0,0,0,0.3), 0px 4px 12px rgba(0,0,0,0.2)",
              border: "1px solid var(--md-sys-color-outline-variant)",
            }}
            initial={{ scale: 0.9, opacity: 0, y: -20 }}
            animate={{ scale: 1, opacity: 1, y: 0 }}
            exit={{ scale: 0.9, opacity: 0, y: -20 }}
            transition={{ duration: 0.2, ease: [0.34, 1.56, 0.64, 1] }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="px-5 py-4 flex items-center justify-between border-b"
              style={{ borderColor: "var(--md-sys-color-outline-variant)" }}>
              <div className="flex items-center gap-2">
                <Code2 size={18} style={{ color: "var(--md-sys-color-primary)" }} />
                <span className="text-sm font-semibold" style={{ color: "var(--md-sys-color-on-surface)" }}>Quick Snippet Picker</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs px-2 py-1 rounded-full" style={{ backgroundColor: "var(--md-sys-color-surface-container)", color: "var(--md-sys-color-on-surface-variant)" }}>
                  Alt+Shift+V
                </span>
                <button onClick={onClose} className="p-1 rounded-full hover:bg-black/5 dark:hover:bg-white/5">
                  <X size={16} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                </button>
              </div>
            </div>
            <div className="p-4 max-h-80 overflow-y-auto">
              <SnippetVault isOverlay />
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Media Page
// ─────────────────────────────────────────────────────────────────────────────
export default function Media() {
  const [activeTab, setActiveTab] = useState("vault");

  return (
    <div className="flex flex-col min-h-full">
      <motion.header className="mb-6" initial={{ opacity: 0, y: -12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }}>
        <h2 className="text-3xl font-medium mb-1.5 flex items-center gap-2.5" style={{ color: "var(--md-sys-color-on-surface)" }}>
          <Clapperboard className="text-md-primary" size={28} />
          Media & Dev Suite
        </h2>
        <p className="text-base" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Developer tools — encrypted snippet vault with hotkey access and video audio extraction.
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
              <span>{tab.label}</span>
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
            {activeTab === "vault" && <SnippetVault />}
            {activeTab === "media" && <MediaStripper />}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
