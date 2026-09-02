import { useState, useEffect } from "react";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import {
  ScanLine,
  Printer,
  Settings2,
  Download,
  Trash2,
  RefreshCw,
  ChevronDown,
  ZoomIn,
} from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";

interface ScannerDevice {
  id: string;
  name: string;
}

interface ScannedPage {
  path: string;
  page: number;
  dataUrl?: string;
}

const DPI_OPTIONS = [75, 150, 200, 300, 600];
const COLOR_MODES = ["Color", "Grayscale", "BlackAndWhite"];

export default function Scanner() {
  const [devices, setDevices] = useState<ScannerDevice[]>([]);
  const [selectedDevice, setSelectedDevice] = useState<string>("");
  const [dpi, setDpi] = useState(200);
  const [colorMode, setColorMode] = useState("Color");
  const [scanning, setScanning] = useState(false);
  const [loadingDevices, setLoadingDevices] = useState(false);
  const [scannedPages, setScannedPages] = useState<ScannedPage[]>([]);
  const [outputDir, setOutputDir] = useState("");
  const [showSettings, setShowSettings] = useState(false);
  const [previewPage, setPreviewPage] = useState<ScannedPage | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    refreshDevices();
    invoke("get_download_dir").then((dir) => setOutputDir(dir as string));
  }, []);

  const refreshDevices = async () => {
    setLoadingDevices(true);
    setErrorMsg(null);
    try {
      const list = await invoke<ScannerDevice[]>("list_scanners");
      setDevices(list);
      if (list.length > 0 && !selectedDevice) {
        setSelectedDevice(list[0].id);
      }
      if (list.length === 0) {
        setErrorMsg("No scanner detected. Connect a WIA-compatible scanner and try again.");
      }
    } catch (e: any) {
      setErrorMsg(`Failed to enumerate scanners: ${e}`);
    }
    setLoadingDevices(false);
  };

  const handleScan = async () => {
    if (!selectedDevice) return;
    setScanning(true);
    setErrorMsg(null);
    try {
      const result = await invoke<{ path: string; page: number }>("scan_document", {
        deviceId: selectedDevice,
        outputDir,
        dpi,
        colorMode,
        pageNumber: scannedPages.length + 1,
      });
      setScannedPages((prev) => [...prev, { ...result }]);
    } catch (e: any) {
      setErrorMsg(`Scan failed: ${e}`);
    }
    setScanning(false);
  };

  const handleBrowseOutput = async () => {
    const selected = await openDialog({ directory: true, multiple: false, title: "Select Output Folder" });
    if (selected) setOutputDir(selected as string);
  };

  const removePage = (page: number) => {
    setScannedPages((prev) => prev.filter((p) => p.page !== page));
  };

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500 relative">
      {/* Header */}
      <header className="mb-8 flex items-end justify-between">
        <div>
          <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
            <ScanLine className="text-md-primary" size={28} />
            Document Scanner
          </h2>
          <p className="text-md-on-surface-variant text-lg">
            WIA hardware scanner — Windows 11 native integration.
          </p>
        </div>
        <motion.button
          onClick={() => setShowSettings(!showSettings)}
          className={`p-3 rounded-full shadow-sm transition-colors ${
            showSettings
              ? "bg-md-primary text-md-on-primary"
              : "bg-md-surface-container text-md-on-surface hover:bg-md-surface-high"
          }`}
          whileTap={{ scale: 0.9 }}
        >
          <Settings2 size={22} />
        </motion.button>
      </header>

      {/* Settings Panel */}
      <AnimatePresence>
        {showSettings && (
          <motion.div
            className="bg-md-surface-container/50 border border-md-outline-variant/30 rounded-[1.5rem] p-6 mb-6 backdrop-blur-sm"
            initial={{ opacity: 0, height: 0, y: -10 }}
            animate={{ opacity: 1, height: "auto", y: 0 }}
            exit={{ opacity: 0, height: 0, y: -10 }}
            transition={{ type: "spring", stiffness: 400, damping: 30 }}
          >
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Device Selector */}
              <div>
                <label className="block text-xs font-semibold text-md-on-surface-variant uppercase tracking-wider mb-2">
                  Scanner Device
                </label>
                <div className="relative">
                  <select
                    value={selectedDevice}
                    onChange={(e) => setSelectedDevice(e.target.value)}
                    className="w-full bg-md-surface-low border border-md-outline-variant/50 text-md-on-surface text-sm rounded-xl p-3 pr-8 outline-none appearance-none focus:border-md-primary transition-colors"
                    disabled={loadingDevices}
                  >
                    {devices.length === 0 && (
                      <option value="">No scanners found</option>
                    )}
                    {devices.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.name}
                      </option>
                    ))}
                  </select>
                  <ChevronDown size={16} className="absolute right-3 top-3.5 text-md-on-surface-variant pointer-events-none" />
                </div>
              </div>

              {/* DPI Selector */}
              <div>
                <label className="block text-xs font-semibold text-md-on-surface-variant uppercase tracking-wider mb-2">
                  Resolution (DPI)
                </label>
                <div className="flex flex-wrap gap-2">
                  {DPI_OPTIONS.map((d) => (
                    <button
                      key={d}
                      onClick={() => setDpi(d)}
                      className={`px-3 py-1.5 rounded-full text-sm font-medium transition-all ${
                        dpi === d
                          ? "bg-md-primary text-md-on-primary shadow-md"
                          : "bg-md-surface-low border border-md-outline-variant/30 text-md-on-surface-variant hover:bg-md-surface-container"
                      }`}
                    >
                      {d}
                    </button>
                  ))}
                </div>
              </div>

              {/* Color Mode */}
              <div>
                <label className="block text-xs font-semibold text-md-on-surface-variant uppercase tracking-wider mb-2">
                  Color Mode
                </label>
                <div className="flex gap-2 flex-wrap">
                  {COLOR_MODES.map((m) => (
                    <button
                      key={m}
                      onClick={() => setColorMode(m)}
                      className={`px-3 py-1.5 rounded-full text-sm font-medium transition-all ${
                        colorMode === m
                          ? "bg-md-primary-container text-md-on-primary-container border border-md-primary/20 shadow-sm"
                          : "bg-md-surface-low border border-md-outline-variant/30 text-md-on-surface-variant hover:bg-md-surface-container"
                      }`}
                    >
                      {m}
                    </button>
                  ))}
                </div>
              </div>

              {/* Output Directory */}
              <div className="md:col-span-3">
                <label className="block text-xs font-semibold text-md-on-surface-variant uppercase tracking-wider mb-2">
                  Output Directory
                </label>
                <div className="flex gap-3">
                  <input
                    type="text"
                    readOnly
                    value={outputDir}
                    className="flex-1 bg-md-surface-low border border-md-outline-variant/30 rounded-xl px-4 py-2.5 text-sm text-md-on-surface outline-none"
                  />
                  <button
                    onClick={handleBrowseOutput}
                    className="px-5 py-2.5 bg-md-surface-container hover:bg-md-surface-high border border-md-outline-variant/30 rounded-xl text-sm font-medium text-md-on-surface transition-colors"
                  >
                    Browse
                  </button>
                </div>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Error Banner */}
      <AnimatePresence>
        {errorMsg && (
          <motion.div
            className="bg-red-500/10 border border-red-500/20 text-red-500 text-sm rounded-xl p-4 mb-6 flex items-center gap-3"
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
          >
            <ScanLine size={18} />
            {errorMsg}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Scanned Pages Grid */}
      <div className="flex-1">
        {scannedPages.length === 0 ? (
          <div className="flex flex-col items-center justify-center min-h-[320px] border-2 border-dashed border-md-outline-variant/40 rounded-[2rem] text-center p-12">
            <div className="w-24 h-24 rounded-full bg-md-surface-container flex items-center justify-center mb-6 border border-md-outline-variant/30">
              <Printer size={44} className="text-md-on-surface-variant" />
            </div>
            <h3 className="text-xl font-medium text-md-on-surface mb-2">No pages scanned yet</h3>
            <p className="text-md-on-surface-variant mb-2">
              Select a scanner above and press the scan button.
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
            <AnimatePresence>
              {scannedPages.map((page, i) => (
                <motion.div
                  key={page.page}
                  className="bg-md-surface-low border border-md-outline-variant/30 rounded-2xl overflow-hidden group shadow-sm hover:shadow-md transition-shadow cursor-pointer"
                  initial={{ opacity: 0, scale: 0.8 }}
                  animate={{ opacity: 1, scale: 1 }}
                  exit={{ opacity: 0, scale: 0.8 }}
                  transition={{ delay: i * 0.05, type: "spring", stiffness: 400, damping: 25 }}
                  whileHover={{ y: -4 }}
                  onClick={() => setPreviewPage(page)}
                >
                  <div className="aspect-[3/4] bg-md-surface-container flex items-center justify-center relative overflow-hidden">
                    {page.dataUrl ? (
                      <img src={page.dataUrl} alt={`Page ${page.page}`} className="w-full h-full object-cover" />
                    ) : (
                      <div className="flex flex-col items-center text-md-on-surface-variant">
                        <ScanLine size={32} className="mb-2 text-md-primary" />
                        <span className="text-xs font-medium">Page {page.page}</span>
                      </div>
                    )}
                    {/* Hover overlay */}
                    <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors flex items-center justify-center">
                      <ZoomIn size={24} className="text-white opacity-0 group-hover:opacity-100 transition-opacity" />
                    </div>
                  </div>
                  <div className="p-3 flex items-center justify-between">
                    <span className="text-xs font-medium text-md-on-surface-variant">Page {page.page}</span>
                    <button
                      onClick={(e) => { e.stopPropagation(); removePage(page.page); }}
                      className="p-1 rounded-full hover:bg-red-500/10 hover:text-red-500 text-md-on-surface-variant transition-colors"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </motion.div>
              ))}
            </AnimatePresence>
          </div>
        )}
      </div>

      {/* MD3 FAB — Scan Button */}
      <motion.button
        onClick={handleScan}
        disabled={scanning || !selectedDevice || loadingDevices}
        className="fixed bottom-8 right-8 flex items-center gap-3 bg-md-primary text-md-on-primary px-6 py-4 rounded-[1rem] font-semibold shadow-2xl hover:brightness-110 transition-all disabled:opacity-60 z-40"
        whileHover={{ scale: 1.04, y: -2 }}
        whileTap={{ scale: 0.96 }}
        transition={{ type: "spring", stiffness: 400, damping: 25 }}
      >
        {scanning ? (
          <>
            <motion.div
              animate={{ rotate: 360 }}
              transition={{ duration: 1, repeat: Infinity, ease: "linear" }}
            >
              <RefreshCw size={22} />
            </motion.div>
            Scanning...
          </>
        ) : (
          <>
            <ScanLine size={22} />
            {scannedPages.length === 0 ? "Scan Page" : `Scan Page ${scannedPages.length + 1}`}
          </>
        )}
      </motion.button>

      {/* Refresh Devices FAB (secondary) */}
      <motion.button
        onClick={refreshDevices}
        disabled={loadingDevices}
        className="fixed bottom-8 right-52 p-4 bg-md-surface-container text-md-on-surface rounded-[1rem] shadow-lg hover:bg-md-surface-high transition-colors z-40 border border-md-outline-variant/30"
        whileHover={{ scale: 1.08 }}
        whileTap={{ scale: 0.92 }}
        title="Refresh Scanner List"
      >
        <motion.div
          animate={loadingDevices ? { rotate: 360 } : { rotate: 0 }}
          transition={loadingDevices ? { duration: 1, repeat: Infinity, ease: "linear" } : {}}
        >
          <RefreshCw size={20} />
        </motion.div>
      </motion.button>

      {/* Preview Modal */}
      <AnimatePresence>
        {previewPage && (
          <motion.div
            className="fixed inset-0 z-50 flex items-center justify-center p-8 bg-black/60 backdrop-blur-md"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setPreviewPage(null)}
          >
            <motion.div
              className="bg-md-surface-low rounded-[2rem] overflow-hidden shadow-2xl max-w-2xl max-h-full"
              initial={{ scale: 0.8, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.8, opacity: 0 }}
              transition={{ type: "spring", stiffness: 400, damping: 30 }}
              onClick={(e) => e.stopPropagation()}
            >
              <div className="p-4 border-b border-md-outline-variant/30 flex items-center justify-between">
                <span className="font-medium text-md-on-surface">Page {previewPage.page}</span>
                <div className="flex gap-2">
                  <a
                    href={`file://${previewPage.path}`}
                    download
                    className="p-2 rounded-full hover:bg-md-surface-container text-md-on-surface-variant transition-colors"
                  >
                    <Download size={18} />
                  </a>
                  <button
                    onClick={() => setPreviewPage(null)}
                    className="p-2 rounded-full hover:bg-md-surface-container text-md-on-surface-variant transition-colors"
                  >
                    ✕
                  </button>
                </div>
              </div>
              {previewPage.dataUrl ? (
                <img src={previewPage.dataUrl} alt="Preview" className="max-h-[75vh] object-contain" />
              ) : (
                <div className="p-12 text-center text-md-on-surface-variant">
                  <p className="mb-2">File saved to:</p>
                  <code className="text-sm bg-md-surface-container px-3 py-1 rounded-lg">{previewPage.path}</code>
                </div>
              )}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
