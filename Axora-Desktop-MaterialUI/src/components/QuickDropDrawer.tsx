import { motion, AnimatePresence } from "framer-motion";
import { Copy, Trash2, ExternalLink, FileText, Link as LinkIcon, Sparkles, X } from "lucide-react";
import { useQuickDropStore } from "../store/useQuickDropStore";
import { useToast } from "./ToastNotification";

export function QuickDropDrawer() {
  const { items, isOpen, setOpen, removeItem, clearItems } = useQuickDropStore();
  const { success } = useToast();

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    success("Copied to clipboard!");
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            className="fixed inset-0 bg-black/60 backdrop-blur-md z-40"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setOpen(false)}
          />

          {/* Drawer Panel */}
          <motion.div
            className="fixed right-0 top-0 bottom-0 w-96 bg-md-surface-low/90 backdrop-blur-xl border-l border-md-outline-variant/40 shadow-2xl z-50 flex flex-col p-6 overflow-hidden"
            initial={{ x: "100%" }}
            animate={{ x: 0 }}
            exit={{ x: "100%" }}
            transition={{ type: "spring", stiffness: 300, damping: 25 }}
          >
            {/* Header */}
            <div className="flex items-center justify-between pb-4 border-b border-md-outline-variant/30 mb-4">
              <div className="flex items-center gap-2">
                <Sparkles className="text-md-primary" size={22} />
                <h3 className="text-lg font-semibold text-md-on-surface">Quick Drop Drawer</h3>
              </div>
              <div className="flex items-center gap-1">
                {items.length > 0 && (
                  <button
                    onClick={clearItems}
                    className="text-xs text-red-400 hover:text-red-500 px-2 py-1 rounded-md transition-colors"
                  >
                    Clear All
                  </button>
                )}
                <button
                  onClick={() => setOpen(false)}
                  className="p-1 rounded-full text-md-on-surface-variant hover:bg-md-surface-high transition-colors"
                >
                  <X size={18} />
                </button>
              </div>
            </div>

            {/* List */}
            <div className="flex-1 overflow-y-auto space-y-3 pr-1">
              {items.length === 0 ? (
                <div className="h-full flex flex-col items-center justify-center text-center text-md-on-surface-variant p-6">
                  <Sparkles size={40} className="opacity-30 mb-3" />
                  <p className="text-sm font-medium">No Quick Drop Items</p>
                  <p className="text-xs opacity-75 mt-1">
                    Share text, links, or files from Axora Mobile to see them appear here instantly.
                  </p>
                </div>
              ) : (
                items.map((item) => (
                  <motion.div
                    key={item.id}
                    layout
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, x: 20 }}
                    className="bg-md-surface-container rounded-2xl p-4 border border-md-outline-variant/20 space-y-2 relative group"
                  >
                    <div className="flex items-center justify-between text-xs text-md-on-surface-variant">
                      <span className="flex items-center gap-1.5 font-medium text-md-primary">
                        {item.type === "link" && <LinkIcon size={14} />}
                        {item.type === "file" && <FileText size={14} />}
                        {item.type === "text" && <FileText size={14} />}
                        {item.filename || item.type.toUpperCase()}
                      </span>
                      <span>{new Date(item.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                    </div>

                    <div className="text-sm font-mono bg-md-surface p-3 rounded-xl border border-md-outline-variant/10 max-h-36 overflow-y-auto break-all text-md-on-surface">
                      {item.content}
                    </div>

                    <div className="flex items-center justify-end gap-2 pt-1">
                      <button
                        onClick={() => handleCopy(item.content)}
                        className="flex items-center gap-1 text-xs font-medium px-3 py-1.5 rounded-full bg-md-primary/10 text-md-primary hover:bg-md-primary/20 transition-colors"
                      >
                        <Copy size={12} />
                        Copy
                      </button>

                      {item.type === "link" && (
                        <a
                          href={item.content}
                          target="_blank"
                          rel="noreferrer"
                          className="flex items-center gap-1 text-xs font-medium px-3 py-1.5 rounded-full bg-md-surface-high text-md-on-surface hover:bg-md-surface-highest transition-colors"
                        >
                          <ExternalLink size={12} />
                          Open
                        </a>
                      )}

                      <button
                        onClick={() => removeItem(item.id)}
                        className="p-1.5 rounded-full text-md-on-surface-variant hover:text-red-500 hover:bg-red-500/10 transition-colors"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </motion.div>
                ))
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
