import { useState, useEffect } from "react";
import { AnimatePresence, motion } from "framer-motion";
import { listen } from "@tauri-apps/api/event";
import { register } from "@tauri-apps/plugin-global-shortcut";
import Sidebar from "./components/Sidebar";
import Dashboard from "./pages/Dashboard";
import Converter from "./pages/Converter";
import Security from "./pages/Security";
import BatchProcessor from "./pages/BatchProcessor";
import Settings from "./pages/Settings";
import Scanner from "./pages/Scanner";
import MobileLink from "./pages/MobileLink";
import FormStudio from "./pages/FormStudio";
import Academic from "./pages/Academic";
import Media, { SnippetOverlay } from "./pages/Media";
import { useThemeStore } from "./store/themeStore";
import ThemeToggle from "./components/ThemeToggle";
import SplashScreen from "./components/SplashScreen";
import { ToastNotification } from "./components/ToastNotification";

import FlashcardStudio from "./pages/FlashcardStudio";
import { CommandPalette } from "./components/CommandPalette";
import FileDropZoneOverlay from "./components/FileDropZoneOverlay";

// MD3 page transition — forward nav: slight up + fade, faster timing for snappiness
const PAGE_VARIANTS = {
  initial:  { opacity: 0, y: 10, scale: 0.985 },
  animate:  { opacity: 1, y: 0,  scale: 1 },
  exit:     { opacity: 0, y: -6, scale: 0.99 },
};

const PAGE_TRANSITION = {
  duration: 0.2,
  ease: [0.2, 0, 0, 1], // MD3 standard easing
};

function App() {
  // Default page: "Workspace Hub" (renamed from "Dashboard")
  const [currentPage, setCurrentPage] = useState<string>("Workspace Hub");
  const [splashDone, setSplashDone] = useState(false);
  const [snippetOverlayOpen, setSnippetOverlayOpen] = useState(false);
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false);
  const initializeTheme = useThemeStore((state) => state.initializeTheme);

  useEffect(() => {
    initializeTheme();

    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setCommandPaletteOpen((prev) => !prev);
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [initializeTheme]);

  useEffect(() => {
    // Scroll listener for overlay scrollbars
    let scrollTimeout: ReturnType<typeof setTimeout>;
    const handleScroll = () => {
      document.body.classList.add("is-scrolling");
      clearTimeout(scrollTimeout);
      scrollTimeout = setTimeout(() => {
        document.body.classList.remove("is-scrolling");
      }, 600);
    };

    window.addEventListener("scroll", handleScroll, true);
    return () => {
      window.removeEventListener("scroll", handleScroll, true);
      clearTimeout(scrollTimeout);
    };
  }, []);

  // Register global hotkeys
  useEffect(() => {
    register("Alt+Shift+V", () => {
      setSnippetOverlayOpen((prev) => !prev);
    }).catch(console.warn);

    const unlisten = listen("snippet-vault-open", () => {
      setSnippetOverlayOpen((prev) => !prev);
    });

    const unlistenSync = listen("toggle-clipboard-sync", () => {
      console.info("Clipboard sync toggled from tray");
    });

    return () => {
      unlisten.then((f) => f());
      unlistenSync.then((f) => f());
    };
  }, []);

  // ── Route table — new MD3 taxonomy ────────────────────────────────────────
  const renderPage = (page: string) => {
    switch (page) {
      case "Workspace Hub":      return <Dashboard setCurrentPage={setCurrentPage} />;
      case "Universal Engine":   return <Converter />;
      case "AxoraVault":        return <Security />;
      case "Bulk Canvas":        return <BatchProcessor />;
      case "Hardware Capture":   return <Scanner />;
      case "Mobile Link":        return <MobileLink />;
      case "Form Studio":        return <FormStudio />;
      case "Scholar Kit":        return <Academic />;
      case "Media Forge":        return <Media />;
      case "Spaced Repetition":  return <FlashcardStudio />;
      case "Settings":           return <Settings />;
      default:                   return <Dashboard setCurrentPage={setCurrentPage} />;
    }
  };

  return (
    <>
      {/* ── Splash Screen ─────────────────────────────────────────── */}
      {!splashDone && (
        <SplashScreen onComplete={() => setSplashDone(true)} />
      )}

      {/* ── Global Toast Notification System ──────────────────────── */}
      <ToastNotification />

      {/* ── Snippet Vault Overlay (Alt+Shift+V) ───────────────────── */}
      <SnippetOverlay
        visible={snippetOverlayOpen}
        onClose={() => setSnippetOverlayOpen(false)}
      />

      {/* ── Global Command Palette (Ctrl+K) ────────────────────────── */}
      <CommandPalette
        isOpen={commandPaletteOpen}
        onClose={() => setCommandPaletteOpen(false)}
        onSelectPage={setCurrentPage}
      />

      {/* ── Native File Drag-and-Drop Dropzone Overlay ────────────── */}
      <FileDropZoneOverlay />

      {/* ── Main App Shell ────────────────────────────────────────── */}
      <div
        className="flex h-screen overflow-hidden"
        style={{
          backgroundColor: "var(--md-sys-color-surface)",
          color: "var(--md-sys-color-on-surface)",
        }}
      >
        {/* MD3 Navigation Rail */}
        <Sidebar currentPage={currentPage} setCurrentPage={setCurrentPage} />

        {/* Main content area */}
        <main className="flex-1 overflow-hidden relative" style={{ padding: "10px 10px 10px 0" }}>
          {/* Ambient chromatic glow — subtle, non-distracting */}
          <div
            className="absolute top-0 left-0 w-64 h-64 pointer-events-none"
            style={{
              background: "radial-gradient(circle, var(--md-sys-color-primary) 0%, transparent 70%)",
              opacity: 0.04,
              filter: "blur(60px)",
              transform: "translate(-30%, -30%)",
            }}
          />
          <div
            className="absolute bottom-0 right-0 w-56 h-56 pointer-events-none"
            style={{
              background: "radial-gradient(circle, var(--md-sys-color-tertiary) 0%, transparent 70%)",
              opacity: 0.04,
              filter: "blur(60px)",
              transform: "translate(30%, 30%)",
            }}
          />

          {/* MD3 Surface Container — elevated card that holds all content */}
          <div
            className="h-full flex flex-col overflow-hidden relative"
            style={{
              backgroundColor: "var(--md-sys-color-surface-container-low)",
              borderRadius: "20px",
              boxShadow: "0 1px 3px rgba(0,0,0,0.18), 0 1px 2px rgba(0,0,0,0.12)",
            }}
          >
            {/* Universal header bar — Theme toggle */}
            <div className="flex justify-end px-5 pt-3.5 pb-2 flex-shrink-0">
              <ThemeToggle />
            </div>

            {/* Page content with AnimatePresence transitions */}
            <div className="flex-1 overflow-y-auto" style={{ padding: "0 20px 20px 20px" }}>
              <AnimatePresence mode="wait">
                <motion.div
                  key={currentPage}
                  variants={PAGE_VARIANTS}
                  initial="initial"
                  animate="animate"
                  exit="exit"
                  transition={PAGE_TRANSITION}
                  className="h-full"
                >
                  {renderPage(currentPage)}
                </motion.div>
              </AnimatePresence>
            </div>
          </div>
        </main>
      </div>
      <ToastNotification />
    </>
  );
}

export default App;
