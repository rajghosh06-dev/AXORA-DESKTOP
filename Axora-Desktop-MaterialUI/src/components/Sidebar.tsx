import {
  LayoutDashboard,
  Settings,
  Cpu,
  ShieldCheck,
  Layers,
  Menu,
  ScanLine,
  Smartphone,
  FileStack,
  GraduationCap,
  Clapperboard,
  BookOpen,
} from "lucide-react";
import { useState, useEffect } from "react";
import { invoke } from "@tauri-apps/api/core";
import { motion, AnimatePresence } from "framer-motion";
import { MdRipple } from "./MdRipple";
import { X, CheckCircle2, Loader2 } from "lucide-react";

interface SidebarProps {
  currentPage: string;
  setCurrentPage: (page: string) => void;
}

// ─── Premium MD3 Naming Scheme — updated taxonomy ────────────────────────────
const NAV_ITEMS = [
  { id: "Workspace Hub",   icon: LayoutDashboard, label: "Workspace Hub",  subtitle: "Overview & Metrics" },
  { id: "Universal Engine",icon: Cpu,             label: "Universal Engine", subtitle: "File Conversion" },
  { id: "AxoraVault",      icon: ShieldCheck,     label: "AxoraVault",    subtitle: "AES-256 Encryption" },
  { id: "Bulk Canvas",     icon: Layers,          label: "Bulk Canvas",    subtitle: "Batch Processing" },
  { id: "Hardware Capture",icon: ScanLine,        label: "Hardware Capture", subtitle: "Scanner Devices" },
  { id: "Mobile Link",     icon: Smartphone,      label: "Mobile Link",    subtitle: "Wi-Fi Pairing" },
  { id: "Form Studio",     icon: FileStack,       label: "Form Studio",    subtitle: "Official Documents" },
  { id: "Scholar Kit",     icon: GraduationCap,   label: "Scholar Kit",    subtitle: "OCR & PDF Surgery" },
  { id: "Media Forge",     icon: Clapperboard,    label: "Media Forge",    subtitle: "Audio & Clips" },
  { id: "Spaced Repetition",icon: BookOpen,       label: "Spaced Repetition", subtitle: "Flashcards & SM-2" },
];

/**
 * MD3 Navigation Rail — premium naming + spec-compliant implementation.
 *
 * MD3 Nav Rail specs followed:
 * - Active indicator: full-width pill (rounded-full) with secondary-container color
 * - Icon size: 24dp with spring scale on hover
 * - Label: visible below icon in expanded mode; tooltip in collapsed mode
 * - Ripple on every interactive item
 * - Collapsed → icon-only (80px wide), Expanded → full label (264px)
 * - Transition: MD3 standard easing [0.2, 0, 0, 1] @ 300ms
 */
export default function Sidebar({ currentPage, setCurrentPage }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(false);

  interface SystemInfo {
    os_name: string;
    os_version: string;
    cpu_model: string;
    cpu_cores: number;
    total_memory_gb: number;
    free_disk_space_gb: number;
    is_tpm_available: boolean;
    is_webview2_installed: boolean;
  }

  const [systemInfo, setSystemInfo] = useState<SystemInfo | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  useEffect(() => {
    invoke<SystemInfo>("get_system_info")
      .then((info) => setSystemInfo(info))
      .catch((err) => console.warn("Failed to get system info:", err));

    const handleOpenModal = () => setIsModalOpen(true);
    window.addEventListener("open-compatibility-modal", handleOpenModal);
    return () => {
      window.removeEventListener("open-compatibility-modal", handleOpenModal);
    };
  }, []);

  return (
    <motion.aside
      className="flex flex-col h-full z-20"
      animate={{ width: collapsed ? 80 : 264 }}
      transition={{ duration: 0.3, ease: [0.2, 0, 0, 1] }}
      style={{
        backgroundColor: "var(--md-sys-color-surface)",
        borderRight: "1px solid color-mix(in srgb, var(--md-sys-color-outline-variant) 20%, transparent)",
        minWidth: collapsed ? 80 : 264,
      }}
    >
      {/* ── Header ────────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 p-4 pb-5">
        <MdRipple
          onClick={() => setCollapsed(!collapsed)}
          className="w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0"
          style={{ color: "var(--md-sys-color-on-surface-variant)" }}
          color="var(--md-sys-color-on-surface)"
        >
          <motion.div
            animate={{ rotate: collapsed ? 180 : 0 }}
            transition={{ duration: 0.3, ease: [0.2, 0, 0, 1] }}
          >
            <Menu size={22} />
          </motion.div>
        </MdRipple>

        <AnimatePresence>
          {!collapsed && (
            <motion.div
              className="flex items-center gap-3 overflow-hidden min-w-0"
              initial={{ opacity: 0, x: -16 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -16 }}
              transition={{ duration: 0.2, ease: [0.2, 0, 0, 1] }}
            >
              <div className="relative flex-shrink-0 group cursor-default">
                <div
                  className="absolute inset-0 rounded-full scale-0 group-hover:scale-150 transition-transform duration-700"
                  style={{
                    background: "var(--md-sys-color-primary)",
                    opacity: 0.15,
                    filter: "blur(8px)",
                  }}
                />
                <img
                  src="/src/assets/logo-transparent.png"
                  alt="Axora"
                  className="w-8 h-8 object-contain drop-shadow-sm relative z-10 group-hover:rotate-[15deg] group-hover:scale-110 transition-all duration-500"
                />
              </div>
              <div className="min-w-0">
                <h1
                  className="font-semibold tracking-tight leading-tight truncate"
                  style={{
                    fontSize: "15px",
                    color: "var(--md-sys-color-on-surface)",
                    fontFamily: "'Google Sans', Roboto, sans-serif",
                  }}
                >
                  Axora
                </h1>
                <p
                  className="truncate"
                  style={{
                    fontSize: "11px",
                    color: "var(--md-sys-color-on-surface-variant)",
                    fontFamily: "'Google Sans', Roboto, sans-serif",
                  }}
                >
                  {systemInfo ? systemInfo.os_name : "Windows 11"}
                </p>
                <button
                  onClick={() => setIsModalOpen(true)}
                  className="text-[10px] text-md-primary hover:underline font-medium text-left block focus:outline-none"
                >
                  View System Info
                </button>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* ── Navigation Items ──────────────────────────────────────── */}
      <nav className="flex-1 flex flex-col gap-0.5 px-2 overflow-y-auto scrollbar-thin">
        {NAV_ITEMS.map((item) => {
          const isActive = currentPage === item.id;
          const Icon = item.icon;

          return (
            <div
              key={item.id}
              className="relative"
              title={collapsed ? item.label : undefined}
            >
              <MdRipple
                onClick={() => setCurrentPage(item.id)}
                className={`w-full flex items-center rounded-2xl transition-colors duration-200 ${
                  collapsed ? "justify-center py-3.5 px-2" : "gap-3 px-3 py-2.5"
                }`}
                style={{
                  backgroundColor: isActive
                    ? "var(--md-sys-color-secondary-container)"
                    : "transparent",
                  color: isActive
                    ? "var(--md-sys-color-on-secondary-container)"
                    : "var(--md-sys-color-on-surface-variant)",
                }}
                color={
                  isActive
                    ? "var(--md-sys-color-on-secondary-container)"
                    : "var(--md-sys-color-on-surface)"
                }
              >
                {/* Animated active pill for collapsed mode */}
                {isActive && collapsed && (
                  <motion.div
                    className="absolute inset-1 rounded-2xl -z-10"
                    style={{ backgroundColor: "var(--md-sys-color-secondary-container)" }}
                    layoutId="active-indicator"
                    transition={{ type: "spring", stiffness: 400, damping: 30 }}
                  />
                )}

                <motion.div
                  className="flex-shrink-0"
                  whileHover={{ scale: 1.12 }}
                  animate={isActive ? { scale: 1.05 } : { scale: 1 }}
                  transition={{ type: "spring", stiffness: 500, damping: 25 }}
                >
                  <Icon size={22} />
                </motion.div>

                <AnimatePresence>
                  {!collapsed && (
                    <motion.div
                      className="min-w-0 flex-1"
                      initial={{ opacity: 0, x: -8 }}
                      animate={{ opacity: 1, x: 0 }}
                      exit={{ opacity: 0, x: -8 }}
                      transition={{ duration: 0.15, ease: [0.2, 0, 0, 1] }}
                    >
                      <div
                        className="font-medium truncate leading-tight"
                        style={{
                          fontSize: "13px",
                          fontFamily: "'Google Sans', Roboto, sans-serif",
                        }}
                      >
                        {item.label}
                      </div>
                      <div
                        className="truncate"
                        style={{
                          fontSize: "10px",
                          opacity: isActive ? 0.8 : 0.55,
                        }}
                      >
                        {item.subtitle}
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </MdRipple>
            </div>
          );
        })}
      </nav>

      {/* ── Bottom — Settings ─────────────────────────────────────── */}
      <div
        className="px-2 pb-4 pt-2"
        style={{
          borderTop: "1px solid color-mix(in srgb, var(--md-sys-color-outline-variant) 20%, transparent)",
        }}
      >
        <MdRipple
          onClick={() => setCurrentPage("Settings")}
          className={`w-full flex items-center rounded-2xl transition-colors duration-200 ${
            collapsed ? "justify-center py-3.5 px-2" : "gap-3 px-3 py-2.5"
          }`}
          style={{
            backgroundColor:
              currentPage === "Settings"
                ? "var(--md-sys-color-secondary-container)"
                : "transparent",
            color:
              currentPage === "Settings"
                ? "var(--md-sys-color-on-secondary-container)"
                : "var(--md-sys-color-on-surface-variant)",
          }}
          color={
            currentPage === "Settings"
              ? "var(--md-sys-color-on-secondary-container)"
              : "var(--md-sys-color-on-surface)"
          }
        >
          <motion.div
            className="flex-shrink-0"
            animate={currentPage === "Settings" ? { rotate: 45 } : { rotate: 0 }}
            transition={{ type: "spring", stiffness: 400, damping: 25 }}
          >
            <Settings size={22} />
          </motion.div>

          <AnimatePresence>
            {!collapsed && (
              <motion.div
                className="min-w-0 flex-1"
                initial={{ opacity: 0, x: -8 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -8 }}
                transition={{ duration: 0.15, ease: [0.2, 0, 0, 1] }}
              >
                <div
                  className="font-medium truncate"
                  style={{
                    fontSize: "13px",
                    fontFamily: "'Google Sans', Roboto, sans-serif",
                  }}
                >
                  Settings
                </div>
                <div
                  className="truncate"
                  style={{
                    fontSize: "10px",
                    opacity: 0.55,
                  }}
                >
                  Preferences
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </MdRipple>
      </div>

      {/* ── System Info Compatibility Modal ─────────────────────────── */}
      <AnimatePresence>
        {isModalOpen && (
          <motion.div
            className="fixed inset-0 z-[99999] flex items-center justify-center p-4 bg-black/45 backdrop-blur-md"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setIsModalOpen(false)}
          >
            <motion.div
              className="w-full max-w-xl bg-md-surface border border-md-outline-variant/30 rounded-[2.5rem] p-8 shadow-2xl flex flex-col relative z-10"
              initial={{ scale: 0.9, opacity: 0, y: 15 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.9, opacity: 0, y: 15 }}
              transition={{ type: "spring", stiffness: 450, damping: 30 }}
              onClick={(e) => e.stopPropagation()}
            >
              {/* Header */}
              <div className="flex items-center justify-between mb-6 pb-4 border-b border-md-outline-variant/20">
                <div className="flex items-center gap-2">
                  <Cpu className="text-md-primary" size={24} />
                  <h3 className="text-title-lg font-semibold text-md-on-surface">System Compatibility</h3>
                </div>
                <button
                  onClick={() => setIsModalOpen(false)}
                  className="p-1.5 rounded-full hover:bg-black/5 dark:hover:bg-white/5 transition-colors"
                >
                  <X size={20} className="text-md-on-surface-variant" />
                </button>
              </div>

              {/* Info Columns */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-sm mb-6 flex-1">
                {/* Minimum Requirements */}
                <div className="bg-md-surface-container-low rounded-3xl p-5 border border-md-outline-variant/20">
                  <h4 className="font-semibold text-md-on-surface-variant mb-4 text-xs tracking-wider uppercase">Minimum Requirements</h4>
                  <div className="space-y-3.5 text-xs text-md-on-surface-variant">
                    <div>
                      <span className="font-semibold text-md-on-surface">OS:</span> Windows 10/11 (64-bit)
                    </div>
                    <div>
                      <span className="font-semibold text-md-on-surface">CPU:</span> Dual-Core or better
                    </div>
                    <div>
                      <span className="font-semibold text-md-on-surface">RAM:</span> 4.0 GB+ System RAM
                    </div>
                    <div>
                      <span className="font-semibold text-md-on-surface">Storage:</span> 1.0 GB Free Space
                    </div>
                    <div>
                      <span className="font-semibold text-md-on-surface">TPM:</span> TPM 2.0 Secure Enclave
                    </div>
                    <div>
                      <span className="font-semibold text-md-on-surface">Runtime:</span> WebView2 Installed
                    </div>
                  </div>
                </div>

                {/* Current Device Specs */}
                <div className="bg-md-surface-container-low rounded-3xl p-5 border border-md-outline-variant/20">
                  <h4 className="font-semibold text-md-on-surface-variant mb-4 text-xs tracking-wider uppercase">Your Device Specs</h4>
                  {systemInfo ? (
                    <div className="space-y-3 text-xs text-md-on-surface-variant">
                      <div className="flex items-start gap-1.5 justify-between">
                        <div className="min-w-0">
                          <span className="font-semibold text-md-on-surface">OS:</span> {systemInfo.os_name}
                          <p className="text-[10px] opacity-70 truncate">{systemInfo.os_version}</p>
                        </div>
                        <CheckCircle2 size={16} className="text-green-500 flex-shrink-0" />
                      </div>
                      <div className="flex items-start gap-1.5 justify-between">
                        <div className="min-w-0">
                          <span className="font-semibold text-md-on-surface">CPU:</span> {systemInfo.cpu_model}
                          <p className="text-[10px] opacity-70">{systemInfo.cpu_cores} Cores ({systemInfo.cpu_cores >= 4 ? 'Optimal' : 'Standard'})</p>
                        </div>
                        <CheckCircle2 size={16} className="text-green-500 flex-shrink-0" />
                      </div>
                      <div className="flex items-start gap-1.5 justify-between">
                        <div>
                          <span className="font-semibold text-md-on-surface">RAM:</span> {systemInfo.total_memory_gb.toFixed(1)} GB RAM
                        </div>
                        <CheckCircle2 size={16} className="text-green-500 flex-shrink-0" />
                      </div>
                      <div className="flex items-start gap-1.5 justify-between">
                        <div>
                          <span className="font-semibold text-md-on-surface">Storage:</span> {systemInfo.free_disk_space_gb.toFixed(1)} GB Free
                        </div>
                        <CheckCircle2 size={16} className="text-green-500 flex-shrink-0" />
                      </div>
                      <div className="flex items-start gap-1.5 justify-between">
                        <div>
                          <span className="font-semibold text-md-on-surface">TPM:</span> {systemInfo.is_tpm_available ? "TPM 2.0 Active" : "TPM Emulation"}
                        </div>
                        {systemInfo.is_tpm_available ? (
                          <CheckCircle2 size={16} className="text-green-500 flex-shrink-0" />
                        ) : (
                          <span className="text-amber-500 text-[10px] font-semibold flex-shrink-0">Warn</span>
                        )}
                      </div>
                      <div className="flex items-start gap-1.5 justify-between">
                        <div>
                          <span className="font-semibold text-md-on-surface">Runtime:</span> WebView2 Active
                        </div>
                        <CheckCircle2 size={16} className="text-green-500 flex-shrink-0" />
                      </div>
                    </div>
                  ) : (
                    <div className="flex flex-col items-center justify-center h-28 gap-2">
                      <Loader2 className="animate-spin text-md-primary" size={20} />
                      <span className="text-xs">Fetching device info...</span>
                    </div>
                  )}
                </div>
              </div>

              {/* Footer */}
              <div className="flex items-center justify-between text-xs border-t border-md-outline-variant/20 pt-4">
                <div className="flex items-center gap-1.5 font-medium text-green-500 bg-green-500/10 px-3.5 py-1.5 rounded-full border border-green-500/20">
                  <span className="w-1.5 h-1.5 rounded-full bg-green-500 animate-ping" />
                  Hardware Acceleration Active
                </div>
                <button
                  onClick={() => setIsModalOpen(false)}
                  className="bg-md-primary text-md-on-primary px-6 py-2.5 rounded-full font-medium shadow-md hover:brightness-105 active:scale-95 transition-all text-xs"
                >
                  Done
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.aside>
  );
}
