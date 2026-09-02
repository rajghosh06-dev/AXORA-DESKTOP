import { useState, useCallback } from "react";
import { invoke } from "@tauri-apps/api/core";
import { motion, AnimatePresence } from "framer-motion";
import { Smartphone, Wifi, WifiOff, Copy, Check } from "lucide-react";
import QRCode from "react-qr-code";

interface ServerInfo {
  ip: string;
  port: number;
  auth_token: string;
  server_pubkey_b64: string;
}

/**
 * Mobile Link — Wi-Fi network pairing module.
 *
 * Features:
 * - MD3 Switch toggle to start/stop the Axum server (CancellationToken-backed)
 * - Client-side SVG QR code generation (react-qr-code)
 * - Live status badge: Server Active / Server Inactive
 * - Copy pairing URL to clipboard
 * - Clean animated state transitions
 */
export default function MobileLink() {
  const [serverActive, setServerActive] = useState(false);
  const [serverInfo, setServerInfo] = useState<ServerInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  // Build the QR payload from server info
  const qrPayload = serverInfo
    ? JSON.stringify({
        service: "axora",
        ip: serverInfo.ip,
        port: serverInfo.port,
        token: serverInfo.auth_token,
        pubkey: serverInfo.server_pubkey_b64,
      })
    : "";

  const pairingUrl = serverInfo
    ? `http://${serverInfo.ip}:${serverInfo.port}`
    : "";

  const handleToggle = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const newState = !serverActive;
      const result: ServerInfo | null = await invoke("toggle_sync_server", {
        enabled: newState,
      });

      setServerActive(newState);
      setServerInfo(result);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  }, [serverActive]);

  const copyToClipboard = async () => {
    if (!pairingUrl) return;
    await navigator.clipboard.writeText(pairingUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500 relative pb-4">
      {/* ── Page Header ──────────────────────────────────────────────────── */}
      <header className="mb-8 flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
            <Smartphone className="text-md-primary" size={28} />
            Mobile Link
          </h2>
          <p className="text-md-on-surface-variant text-lg">
            Connect your Android device over local Wi-Fi. No cloud. No tracking.
          </p>
        </div>

        {/* Status badge */}
        <motion.div
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-full"
          style={{
            backgroundColor: serverActive
              ? "color-mix(in srgb, var(--md-sys-color-primary) 15%, transparent)"
              : "var(--md-sys-color-surface-container)",
          }}
          animate={{ scale: [1, 1.02, 1] }}
          transition={{ duration: 0.3 }}
          key={String(serverActive)}
        >
          <motion.div
            className="w-2 h-2 rounded-full"
            style={{
              backgroundColor: serverActive
                ? "var(--md-sys-color-primary)"
                : "var(--md-sys-color-outline)",
            }}
            animate={serverActive ? { opacity: [1, 0.4, 1] } : { opacity: 1 }}
            transition={{ duration: 1.5, repeat: serverActive ? Infinity : 0 }}
          />
          <span
            style={{
              fontSize: "12px",
              fontWeight: 600,
              color: serverActive
                ? "var(--md-sys-color-primary)"
                : "var(--md-sys-color-on-surface-variant)",
            }}
          >
            {serverActive ? "Server Active" : "Server Inactive"}
          </span>
        </motion.div>
      </header>

      {/* ── Main Toggle Card ─────────────────────────────────────────────── */}
      <div
        className="rounded-3xl p-6"
        style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <motion.div
              className="w-14 h-14 rounded-2xl flex items-center justify-center"
              style={{
                backgroundColor: serverActive
                  ? "var(--md-sys-color-primary-container)"
                  : "var(--md-sys-color-surface-container-high)",
              }}
              animate={{ scale: serverActive ? 1.05 : 1 }}
              transition={{ type: "spring", stiffness: 300, damping: 20 }}
            >
              <motion.div
                animate={{ rotate: loading ? 360 : 0 }}
                transition={{ duration: 1, repeat: loading ? Infinity : 0, ease: "linear" }}
              >
                {serverActive ? (
                  <Wifi
                    size={24}
                    style={{ color: "var(--md-sys-color-on-primary-container)" }}
                  />
                ) : (
                  <WifiOff
                    size={24}
                    style={{ color: "var(--md-sys-color-on-surface-variant)" }}
                  />
                )}
              </motion.div>
            </motion.div>

            <div>
              <div
                className="font-semibold"
                style={{
                  fontSize: "15px",
                  color: "var(--md-sys-color-on-surface)",
                  fontFamily: "'Google Sans', Roboto, sans-serif",
                }}
              >
                {serverActive ? "AxoraLink Server Running" : "Start AxoraLink Server"}
              </div>
              <div
                style={{
                  fontSize: "12px",
                  color: "var(--md-sys-color-on-surface-variant)",
                  marginTop: "2px",
                }}
              >
                {serverActive && serverInfo
                  ? `${serverInfo.ip}:${serverInfo.port}`
                  : "Tap the toggle to begin pairing"}
              </div>
            </div>
          </div>

          {/* ── MD3 Switch toggle ─────────────────────────────────────────── */}
          <button
            onClick={handleToggle}
            disabled={loading}
            className="relative flex-shrink-0 focus:outline-none"
            style={{ padding: 0, background: "none", border: "none", cursor: loading ? "not-allowed" : "pointer" }}
            aria-label={serverActive ? "Stop server" : "Start server"}
            aria-checked={serverActive}
            role="switch"
          >
            <motion.div
              className="w-14 h-8 rounded-full flex items-center transition-colors duration-200"
              style={{
                backgroundColor: serverActive
                  ? "var(--md-sys-color-primary)"
                  : "var(--md-sys-color-surface-container-highest)",
                border: "2px solid",
                borderColor: serverActive
                  ? "var(--md-sys-color-primary)"
                  : "var(--md-sys-color-outline)",
                opacity: loading ? 0.6 : 1,
              }}
              whileTap={{ scale: 0.95 }}
            >
              <motion.div
                className="w-5 h-5 rounded-full shadow-md"
                style={{
                  backgroundColor: serverActive
                    ? "var(--md-sys-color-on-primary)"
                    : "var(--md-sys-color-outline)",
                }}
                animate={{ x: serverActive ? 26 : 4 }}
                transition={{ type: "spring", stiffness: 500, damping: 30 }}
              />
            </motion.div>
          </button>
        </div>

        {/* Error display */}
        {error && (
          <motion.div
            className="mt-4 p-3 rounded-xl"
            style={{
              backgroundColor: "var(--md-sys-color-error-container)",
              color: "var(--md-sys-color-on-error-container)",
              fontSize: "12px",
            }}
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
          >
            {error}
          </motion.div>
        )}
      </div>

      {/* ── QR Code + Connection Info ─────────────────────────────────────── */}
      <AnimatePresence mode="wait">
        {serverActive && serverInfo ? (
          <motion.div
            key="qr-panel"
            initial={{ opacity: 0, y: 16, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -8, scale: 0.97 }}
            transition={{ duration: 0.25, ease: [0.2, 0, 0, 1] }}
            className="grid grid-cols-1 gap-4"
            style={{ gridTemplateColumns: "auto 1fr" }}
          >
            {/* QR Code Card */}
            <div
              className="rounded-3xl p-6 flex flex-col items-center gap-3"
              style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
            >
              <div
                className="p-3 rounded-2xl"
                style={{ backgroundColor: "#ffffff" }}
              >
                <QRCode
                  value={qrPayload}
                  size={160}
                  bgColor="#ffffff"
                  fgColor="#000000"
                  level="M"
                />
              </div>
              <p
                style={{
                  fontSize: "11px",
                  color: "var(--md-sys-color-on-surface-variant)",
                  textAlign: "center",
                }}
              >
                Scan with Axora Mobile
              </p>
            </div>

            {/* Connection Details Card */}
            <div
              className="rounded-3xl p-6 flex flex-col gap-4"
              style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
            >
              <div
                className="font-semibold"
                style={{
                  fontSize: "14px",
                  color: "var(--md-sys-color-on-surface)",
                  fontFamily: "'Google Sans', Roboto, sans-serif",
                }}
              >
                Connection Details
              </div>

              {[
                { label: "Local IP", value: serverInfo.ip },
                { label: "Port", value: String(serverInfo.port) },
                { label: "Auth Token", value: `${serverInfo.auth_token.slice(0, 8)}…` },
              ].map(({ label, value }) => (
                <div key={label}>
                  <div
                    style={{
                      fontSize: "10px",
                      color: "var(--md-sys-color-on-surface-variant)",
                      textTransform: "uppercase",
                      letterSpacing: "0.08em",
                      fontWeight: 600,
                      marginBottom: "2px",
                    }}
                  >
                    {label}
                  </div>
                  <div
                    className="font-mono px-3 py-1.5 rounded-xl"
                    style={{
                      fontSize: "13px",
                      backgroundColor: "var(--md-sys-color-surface-container-high)",
                      color: "var(--md-sys-color-on-surface)",
                    }}
                  >
                    {value}
                  </div>
                </div>
              ))}

              {/* Copy URL button */}
              <button
                onClick={copyToClipboard}
                className="flex items-center gap-2 mt-auto px-4 py-2.5 rounded-xl w-full justify-center transition-colors duration-150"
                style={{
                  backgroundColor: copied
                    ? "var(--md-sys-color-primary-container)"
                    : "var(--md-sys-color-secondary-container)",
                  color: copied
                    ? "var(--md-sys-color-on-primary-container)"
                    : "var(--md-sys-color-on-secondary-container)",
                  border: "none",
                  cursor: "pointer",
                  fontSize: "13px",
                  fontWeight: 500,
                }}
              >
                <AnimatePresence mode="wait">
                  {copied ? (
                    <motion.span
                      key="check"
                      className="flex items-center gap-2"
                      initial={{ opacity: 0, scale: 0.8 }}
                      animate={{ opacity: 1, scale: 1 }}
                      exit={{ opacity: 0, scale: 0.8 }}
                    >
                      <Check size={14} /> Copied!
                    </motion.span>
                  ) : (
                    <motion.span
                      key="copy"
                      className="flex items-center gap-2"
                      initial={{ opacity: 0, scale: 0.8 }}
                      animate={{ opacity: 1, scale: 1 }}
                      exit={{ opacity: 0, scale: 0.8 }}
                    >
                      <Copy size={14} /> Copy Address
                    </motion.span>
                  )}
                </AnimatePresence>
              </button>
            </div>
          </motion.div>
        ) : (
          <motion.div
            key="inactive-panel"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="rounded-3xl p-8 flex flex-col items-center justify-center gap-4 text-center"
            style={{
              backgroundColor: "var(--md-sys-color-surface-container)",
              minHeight: "220px",
              border: "2px dashed color-mix(in srgb, var(--md-sys-color-outline-variant) 40%, transparent)",
            }}
          >
            <div
              className="w-16 h-16 rounded-full flex items-center justify-center"
              style={{ backgroundColor: "var(--md-sys-color-surface-container-high)" }}
            >
              <Smartphone
                size={28}
                style={{ color: "var(--md-sys-color-on-surface-variant)" }}
              />
            </div>
            <div>
              <div
                className="font-medium"
                style={{
                  fontSize: "15px",
                  color: "var(--md-sys-color-on-surface)",
                  fontFamily: "'Google Sans', Roboto, sans-serif",
                }}
              >
                No Active Connection
              </div>
              <p
                style={{
                  fontSize: "12px",
                  color: "var(--md-sys-color-on-surface-variant)",
                  marginTop: "4px",
                  maxWidth: "260px",
                }}
              >
                Enable the server above to generate a QR pairing code for your Android device.
              </p>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Instructions ─────────────────────────────────────────────────── */}
      <div
        className="rounded-3xl p-5"
        style={{ backgroundColor: "var(--md-sys-color-surface-container)" }}
      >
        <div
          className="font-semibold mb-3"
          style={{
            fontSize: "13px",
            color: "var(--md-sys-color-on-surface)",
            fontFamily: "'Google Sans', Roboto, sans-serif",
          }}
        >
          How to pair
        </div>
        {[
          "Enable the server toggle above",
          "Open Axora Mobile on your Android device",
          "Tap 'Scan QR Code' and point at the code",
          "File transfer begins immediately over local Wi-Fi",
        ].map((step, i) => (
          <div
            key={i}
            className="flex items-start gap-3 py-1.5"
          >
            <div
              className="w-5 h-5 rounded-full flex items-center justify-center flex-shrink-0 mt-0.5"
              style={{
                backgroundColor: "var(--md-sys-color-secondary-container)",
                color: "var(--md-sys-color-on-secondary-container)",
                fontSize: "10px",
                fontWeight: 700,
              }}
            >
              {i + 1}
            </div>
            <p
              style={{
                fontSize: "12px",
                color: "var(--md-sys-color-on-surface-variant)",
              }}
            >
              {step}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}
