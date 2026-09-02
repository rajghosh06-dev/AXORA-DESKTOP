import { useState, useEffect } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  Search, Brain, Shield, Moon, Sun, ArrowRight, X, Layers
} from "lucide-react";
import { useQuickDropStore } from "../store/useQuickDropStore";
import { useThemeStore } from "../store/themeStore";

interface CommandItem {
  id: string;
  title: string;
  subtitle: string;
  category: "Actions" | "Navigation" | "Theme";
  icon: any;
  action: () => void;
}

interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectPage?: (pageName: string) => void;
}

export function CommandPalette({ isOpen, onClose, onSelectPage }: CommandPaletteProps) {
  const [query, setQuery] = useState("");
  const [selectedIndex, setSelectedIndex] = useState(0);

  const { toggleOpen: toggleQuickDrop } = useQuickDropStore();
  const { theme, setTheme } = useThemeStore();

  const commands: CommandItem[] = [
    {
      id: "flashcard-studio",
      title: "Open Spaced Repetition Studio",
      subtitle: "Review SM-2 decks and export to Anki (.apkg)",
      category: "Navigation",
      icon: Brain,
      action: () => {
        onSelectPage?.("Spaced Repetition");
        onClose();
      },
    },
    {
      id: "quick-drop",
      title: "Toggle Quick Drop Drawer",
      subtitle: "View shared snippets, links, and mobile drops",
      category: "Actions",
      icon: Layers,
      action: () => {
        toggleQuickDrop();
        onClose();
      },
    },
    {
      id: "security-vault",
      title: "Open AxoraVault (Security)",
      subtitle: "Argon2id + AES-256-GCM file encryption",
      category: "Navigation",
      icon: Shield,
      action: () => {
        onSelectPage?.("AxoraVault");
        onClose();
      },
    },
    {
      id: "toggle-theme",
      title: theme === "dark" ? "Switch to Light Mode" : "Switch to Dark Mode",
      subtitle: "Toggle dynamic Material Design color theme",
      category: "Theme",
      icon: theme === "dark" ? Sun : Moon,
      action: () => {
        setTheme(theme === "dark" ? "light" : "dark");
        onClose();
      },
    },
  ];

  const filtered = commands.filter(
    (c) =>
      c.title.toLowerCase().includes(query.toLowerCase()) ||
      c.subtitle.toLowerCase().includes(query.toLowerCase())
  );

  useEffect(() => {
    setSelectedIndex(0);
  }, [query]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isOpen) return;

      if (e.key === "ArrowDown") {
        e.preventDefault();
        setSelectedIndex((prev) => (prev + 1) % Math.max(1, filtered.length));
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        setSelectedIndex((prev) => (prev - 1 + filtered.length) % Math.max(1, filtered.length));
      } else if (e.key === "Enter") {
        e.preventDefault();
        if (filtered[selectedIndex]) {
          filtered[selectedIndex].action();
        }
      } else if (e.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, selectedIndex, filtered, onClose]);

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            className="fixed inset-0 bg-black/65 backdrop-blur-sm z-50"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />

          {/* Dialog Container */}
          <motion.div
            className="fixed inset-0 z-50 flex items-start justify-center pt-24 px-4 pointer-events-none"
            initial={{ opacity: 0, scale: 0.96, y: -10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.96, y: -10 }}
            transition={{ type: "spring", stiffness: 400, damping: 30 }}
          >
            <div className="w-full max-w-xl bg-md-surface-low/95 backdrop-blur-2xl border border-md-outline-variant/40 rounded-2xl shadow-2xl overflow-hidden pointer-events-auto flex flex-col">
              {/* Search Bar */}
              <div className="flex items-center px-4 py-3 border-b border-md-outline-variant/20 gap-3">
                <Search className="text-md-primary" size={20} />
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Type a command or search (Ctrl+K)..."
                  className="flex-1 bg-transparent border-none outline-none text-base text-md-on-surface placeholder:text-md-on-surface-variant font-medium"
                  autoFocus
                />
                <button
                  onClick={onClose}
                  className="p-1 text-md-on-surface-variant hover:text-md-on-surface rounded-md transition-colors"
                >
                  <X size={16} />
                </button>
              </div>

              {/* Command List */}
              <div className="max-h-80 overflow-y-auto p-2 space-y-1">
                {filtered.length === 0 ? (
                  <div className="p-8 text-center text-sm text-md-on-surface-variant">
                    No commands matching "{query}"
                  </div>
                ) : (
                  filtered.map((item, idx) => {
                    const isSelected = idx === selectedIndex;
                    const IconComp = item.icon;
                    return (
                      <div
                        key={item.id}
                        onClick={item.action}
                        onMouseEnter={() => setSelectedIndex(idx)}
                        className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-all ${
                          isSelected
                            ? "bg-md-primary/15 border border-md-primary/30 text-md-on-surface"
                            : "hover:bg-md-surface-container text-md-on-surface-variant border border-transparent"
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          <div className={`p-2 rounded-lg ${isSelected ? "bg-md-primary text-md-on-primary" : "bg-md-surface-container-high text-md-primary"}`}>
                            <IconComp size={18} />
                          </div>
                          <div>
                            <p className="text-sm font-medium text-md-on-surface">{item.title}</p>
                            <p className="text-xs text-md-on-surface-variant">{item.subtitle}</p>
                          </div>
                        </div>
                        <ArrowRight size={14} className={isSelected ? "text-md-primary opacity-100" : "opacity-0"} />
                      </div>
                    );
                  })
                )}
              </div>

              {/* Footer */}
              <div className="px-4 py-2 bg-md-surface-container-high/50 border-t border-md-outline-variant/10 text-xs text-md-on-surface-variant flex items-center justify-between">
                <span>Use <kbd className="px-1.5 py-0.5 rounded bg-md-surface border border-md-outline-variant/30 font-mono text-[10px]">↑</kbd> <kbd className="px-1.5 py-0.5 rounded bg-md-surface border border-md-outline-variant/30 font-mono text-[10px]">↓</kbd> to navigate</span>
                <span><kbd className="px-1.5 py-0.5 rounded bg-md-surface border border-md-outline-variant/30 font-mono text-[10px]">Enter</kbd> to select</span>
              </div>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
