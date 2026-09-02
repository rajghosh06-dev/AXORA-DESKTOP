import { useState, useEffect } from "react";
import { invoke } from "@tauri-apps/api/core";
import {
  Wifi,
  Smartphone,
  Shield,
  RefreshCw,
  CheckCircle2,
  Copy,
  QrCode,
  Zap,
  Radio,
} from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";

interface ServerInfo {
  ip: string;
  port: number;
  auth_token: string;
  server_pubkey_b64: string;
}

function CopyableField({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="bg-md-surface-container rounded-2xl p-4 flex items-center justify-between gap-4">
      <div className="min-w-0">
        <p className="text-xs font-semibold text-md-on-surface-variant uppercase tracking-wider mb-1">
          {label}
        </p>
        <p className="text-sm font-mono text-md-on-surface truncate">{value}</p>
      </div>
      <motion.button
        onClick={handleCopy}
        className="flex-shrink-0 p-2 rounded-full hover:bg-md-surface-high text-md-on-surface-variant transition-colors"
        whileTap={{ scale: 0.85 }}
      >
        <AnimatePresence mode="wait">
          {copied ? (
            <motion.div
              key="check"
              initial={{ scale: 0, rotate: -90 }}
              animate={{ scale: 1, rotate: 0 }}
              exit={{ scale: 0 }}
              transition={{ type: "spring", stiffness: 600, damping: 20 }}
            >
              <CheckCircle2 size={18} className="text-green-500" />
            </motion.div>
          ) : (
            <motion.div key="copy" initial={{ scale: 0 }} animate={{ scale: 1 }}>
              <Copy size={18} />
            </motion.div>
          )}
        </AnimatePresence>
      </motion.button>
    </div>
  );
}

function PulsingDot({ active }: { active: boolean }) {
  return (
    <span className="relative flex h-3 w-3">
      {active && (
        <span className="absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-75 animate-ping" />
      )}
      <span
        className={`relative inline-flex rounded-full h-3 w-3 ${active ? "bg-green-400" : "bg-md-outline-variant"}`}
      />
    </span>
  );
}

export default function EcosystemSync() {
  const [serverInfo, setServerInfo] = useState<ServerInfo | null>(null);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const [refreshingQr, setRefreshingQr] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<"qr" | "manual">("qr");

  // Auto-start server when page loads
  useEffect(() => {
    invoke<ServerInfo>("get_server_info").then((info) => {
      if (info) {
        setServerInfo(info);
        generateQr();
      }
    }).catch(() => {});
  }, []);

  const startServer = async () => {
    setStarting(true);
    setError(null);
    try {
      const info = await invoke<ServerInfo>("start_ecosystem_server");
      setServerInfo(info);
      await generateQr();
    } catch (e: any) {
      setError(`Failed to start server: ${e}`);
    }
    setStarting(false);
  };

  const generateQr = async () => {
    setRefreshingQr(true);
    try {
      const dataUrl = await invoke<string>("generate_pairing_qr");
      setQrDataUrl(dataUrl);
    } catch (e: any) {
      setError(`Failed to generate QR code: ${e}`);
    }
    setRefreshingQr(false);
  };

  const refreshPairing = async () => {
    setStarting(true);
    setQrDataUrl(null);
    setServerInfo(null);
    setError(null);
    try {
      const info = await invoke<ServerInfo>("start_ecosystem_server");
      setServerInfo(info);
      await generateQr();
    } catch (e: any) {
      setError(`Restart failed: ${e}`);
    }
    setStarting(false);
  };

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500">
      {/* Header */}
      <header className="mb-8">
        <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
          <motion.div
            animate={{ rotate: [0, 15, -15, 0] }}
            transition={{ duration: 2, repeat: Infinity, repeatDelay: 3 }}
          >
            <Smartphone className="text-md-primary" size={28} />
          </motion.div>
          Ecosystem Sync
        </h2>
        <p className="text-md-on-surface-variant text-lg">
          Connect your Android 16+ device over local Wi-Fi using mDNS discovery.
        </p>
      </header>

      {/* Status Banner */}
      <motion.div
        className={`rounded-2xl p-4 mb-6 border flex items-center gap-4 ${
          serverInfo
            ? "bg-green-500/10 border-green-500/20"
            : "bg-md-surface-low border-md-outline-variant/30"
        }`}
        layout
      >
        <PulsingDot active={!!serverInfo} />
        <div className="flex-1">
          <p className={`font-semibold text-sm ${serverInfo ? "text-green-600 dark:text-green-400" : "text-md-on-surface-variant"}`}>
            {serverInfo ? "Server Active — Discoverable on Wi-Fi" : "Server not started"}
          </p>
          {serverInfo && (
            <p className="text-xs text-md-on-surface-variant mt-0.5">
              mDNS: _axora._tcp.local · {serverInfo.ip}:{serverInfo.port}
            </p>
          )}
        </div>
        {serverInfo && (
          <motion.button
            onClick={refreshPairing}
            disabled={starting}
            className="p-2 rounded-full hover:bg-md-surface-container text-md-on-surface-variant transition-colors"
            whileTap={{ scale: 0.85 }}
            title="Refresh session token"
          >
            <motion.div
              animate={starting ? { rotate: 360 } : { rotate: 0 }}
              transition={starting ? { duration: 1, repeat: Infinity, ease: "linear" } : {}}
            >
              <RefreshCw size={18} />
            </motion.div>
          </motion.button>
        )}
      </motion.div>

      {/* Error */}
      <AnimatePresence>
        {error && (
          <motion.div
            className="bg-red-500/10 border border-red-500/20 text-red-500 rounded-xl p-4 mb-6 text-sm"
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
          >
            {error}
          </motion.div>
        )}
      </AnimatePresence>

      {!serverInfo ? (
        /* Not started state */
        <div className="flex-1 flex flex-col items-center justify-center min-h-[400px] bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-12 text-center">
          <motion.div
            className="w-28 h-28 rounded-full bg-md-primary/10 flex items-center justify-center mb-8 border border-md-primary/20"
            animate={{ scale: [1, 1.05, 1] }}
            transition={{ duration: 2, repeat: Infinity }}
          >
            <Radio size={56} className="text-md-primary" />
          </motion.div>
          <h3 className="text-2xl font-medium text-md-on-surface mb-3">
            Start the Pairing Server
          </h3>
          <p className="text-md-on-surface-variant mb-8 max-w-md">
            Axora will start a local HTTP + WebSocket server and advertise itself via mDNS
            so your Android 16+ device can discover it instantly on Wi-Fi.
          </p>
          <motion.button
            onClick={startServer}
            disabled={starting}
            className="flex items-center gap-3 bg-md-primary text-md-on-primary px-8 py-4 rounded-full font-semibold shadow-lg hover:brightness-110 transition-all disabled:opacity-60"
            whileHover={{ scale: 1.04 }}
            whileTap={{ scale: 0.96 }}
          >
            {starting ? (
              <>
                <motion.div animate={{ rotate: 360 }} transition={{ duration: 1, repeat: Infinity, ease: "linear" }}>
                  <RefreshCw size={20} />
                </motion.div>
                Starting...
              </>
            ) : (
              <>
                <Zap size={20} />
                Start Pairing Server
              </>
            )}
          </motion.button>
        </div>
      ) : (
        /* Active state — show QR / Manual tabs */
        <div className="flex-1 grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Left: QR Code */}
          <div className="bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-8 flex flex-col items-center">
            {/* Tab Switcher */}
            <div className="flex gap-2 bg-md-surface-container p-1 rounded-full mb-8 w-full max-w-xs">
              {(["qr", "manual"] as const).map((tab) => (
                <button
                  key={tab}
                  onClick={() => setActiveTab(tab)}
                  className={`flex-1 py-2 rounded-full text-sm font-medium transition-all ${
                    activeTab === tab
                      ? "bg-md-primary text-md-on-primary shadow-sm"
                      : "text-md-on-surface-variant hover:text-md-on-surface"
                  }`}
                >
                  {tab === "qr" ? "QR Code" : "Manual"}
                </button>
              ))}
            </div>

            <AnimatePresence mode="wait">
              {activeTab === "qr" ? (
                <motion.div
                  key="qr"
                  className="flex flex-col items-center w-full"
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ type: "spring", stiffness: 400, damping: 30 }}
                >
                  {refreshingQr ? (
                    <div className="w-64 h-64 bg-md-surface-container rounded-2xl flex items-center justify-center">
                      <motion.div animate={{ rotate: 360 }} transition={{ duration: 1, repeat: Infinity, ease: "linear" }}>
                        <RefreshCw size={32} className="text-md-primary" />
                      </motion.div>
                    </div>
                  ) : qrDataUrl ? (
                    <motion.div
                      className="p-4 bg-white rounded-2xl shadow-md"
                      initial={{ scale: 0.8, opacity: 0 }}
                      animate={{ scale: 1, opacity: 1 }}
                      transition={{ type: "spring", stiffness: 400, damping: 25 }}
                    >
                      <img
                        src={qrDataUrl}
                        alt="Pairing QR Code"
                        className="w-56 h-56"
                        style={{ imageRendering: "pixelated" }}
                      />
                    </motion.div>
                  ) : (
                    <div className="w-64 h-64 bg-md-surface-container rounded-2xl flex items-center justify-center">
                      <QrCode size={64} className="text-md-on-surface-variant" />
                    </div>
                  )}

                  <p className="text-md-on-surface-variant text-sm text-center mt-6 max-w-xs">
                    Scan this QR code with Axora Mobile to pair your device instantly.
                  </p>

                  <button
                    onClick={generateQr}
                    disabled={refreshingQr}
                    className="mt-4 flex items-center gap-2 text-sm text-md-primary hover:text-md-primary/80 transition-colors font-medium"
                  >
                    <RefreshCw size={14} />
                    Refresh QR Code
                  </button>
                </motion.div>
              ) : (
                <motion.div
                  key="manual"
                  className="flex flex-col gap-4 w-full"
                  initial={{ opacity: 0, x: 20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: -20 }}
                  transition={{ type: "spring", stiffness: 400, damping: 30 }}
                >
                  <CopyableField label="IP Address" value={serverInfo.ip} />
                  <CopyableField label="Port" value={String(serverInfo.port)} />
                  <CopyableField label="Auth Token" value={serverInfo.auth_token} />
                  <div className="bg-md-primary/10 border border-md-primary/20 rounded-2xl p-4 text-sm text-md-on-surface mt-2">
                    <p className="font-semibold text-md-primary mb-1 flex items-center gap-2">
                      <Shield size={16} />
                      ECDH Security
                    </p>
                    <p className="text-md-on-surface-variant">
                      After entering these details in Axora Mobile, the app will perform a P-256
                      ECDH handshake to establish an encrypted session. The auth token is single-use
                      and rotates on each pairing session.
                    </p>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          {/* Right: Connection Details + Instructions */}
          <div className="flex flex-col gap-4">
            {/* How it works */}
            <div className="bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-6">
              <h3 className="font-semibold text-md-on-surface mb-4 flex items-center gap-2">
                <Wifi size={18} className="text-md-primary" />
                How to Connect
              </h3>
              <ol className="space-y-3">
                {[
                  "Ensure both devices are on the same Wi-Fi network.",
                  "Open Axora Mobile on your Android 16+ device.",
                  'Tap "Scan to Connect" and point your camera at the QR code.',
                  "The app auto-discovers this desktop via mDNS — no IP needed.",
                  "Approve the ECDH pairing request when prompted.",
                ].map((step, i) => (
                  <motion.li
                    key={i}
                    className="flex items-start gap-3 text-sm text-md-on-surface-variant"
                    initial={{ opacity: 0, x: -10 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: i * 0.05 }}
                  >
                    <span className="flex-shrink-0 w-6 h-6 rounded-full bg-md-primary-container text-md-on-primary-container text-xs font-bold flex items-center justify-center mt-0.5">
                      {i + 1}
                    </span>
                    {step}
                  </motion.li>
                ))}
              </ol>
            </div>

            {/* Security info */}
            <div className="bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-6 flex-1">
              <h3 className="font-semibold text-md-on-surface mb-4 flex items-center gap-2">
                <Shield size={18} className="text-green-500" />
                Security Architecture
              </h3>
              <div className="space-y-3 text-sm text-md-on-surface-variant">
                {[
                  { icon: "🔑", text: "P-256 ECDH — ephemeral keypair per session" },
                  { icon: "🔐", text: "UUID v4 auth token — single-use, rotates per pairing" },
                  { icon: "📡", text: "mDNS _axora._tcp.local — zero-config discovery" },
                  { icon: "🌐", text: "WebSocket — low-latency real-time communication" },
                  { icon: "🏠", text: "LAN only — never routed through the internet" },
                ].map((item, i) => (
                  <motion.div
                    key={i}
                    className="flex items-center gap-3 bg-md-surface-container rounded-xl p-3"
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.1 + i * 0.05 }}
                  >
                    <span className="text-lg">{item.icon}</span>
                    <span>{item.text}</span>
                  </motion.div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
