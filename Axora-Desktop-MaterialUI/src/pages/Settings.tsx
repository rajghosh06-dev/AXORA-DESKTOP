import {
  Palette, Cpu, FolderOpen, Save, RotateCcw,
  MonitorDown, Rocket, ShieldCheck, Image, FileText, Settings as SettingsIcon,
} from "lucide-react";
import { useThemeStore } from "../store/themeStore";
import { useState, useEffect } from "react";
import { invoke } from "@tauri-apps/api/core";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import { motion } from "framer-motion";
import { useToast } from "../components/ToastNotification";

type AccentColor = 'blue' | 'purple' | 'green' | 'red' | 'orange';

export default function Settings() {
  const { theme, setTheme, accent, setAccent } = useThemeStore();
  const { success, error } = useToast();
  const [outputDir, setOutputDir] = useState("");
  const [concurrency, setConcurrency] = useState(8);
  const [saving, setSaving] = useState(false);
  const [minimizeToTray, setMinimizeToTray] = useState(true);
  const [autostartEnabled, setAutostartEnabled] = useState(false);

  // New configuration options
  const [enableSplash, setEnableSplash] = useState(true);
  const [splashDuration, setSplashDuration] = useState(1800);
  const [defaultOcrLang, setDefaultOcrLang] = useState("en");
  const [clearMetadata, setClearMetadata] = useState(true);
  const [imageQuality, setImageQuality] = useState(85);
  const [argonMemory, setArgonMemory] = useState(65536);
  const [argonIterations, setArgonIterations] = useState(3);
  const [autoLockVault, setAutoLockVault] = useState(15);

  const [originalSettings, setOriginalSettings] = useState<{
    theme: "light" | "dark" | "system";
    themeAccent: AccentColor;
    concurrency: number;
    outputDir: string;
    minimizeToTray: boolean;
    enableSplash: boolean;
    splashDuration: number;
    defaultOcrLang: string;
    clearMetadata: boolean;
    imageQuality: number;
    argonMemory: number;
    argonIterations: number;
    autoLockVault: number;
  }>({
    theme: "system",
    themeAccent: "blue",
    concurrency: 8,
    outputDir: "",
    minimizeToTray: true,
    enableSplash: true,
    splashDuration: 1800,
    defaultOcrLang: "en",
    clearMetadata: true,
    imageQuality: 85,
    argonMemory: 65536,
    argonIterations: 3,
    autoLockVault: 15,
  });

  const isDirty =
    theme !== originalSettings.theme ||
    accent !== originalSettings.themeAccent ||
    concurrency !== originalSettings.concurrency ||
    outputDir !== originalSettings.outputDir ||
    minimizeToTray !== originalSettings.minimizeToTray ||
    enableSplash !== originalSettings.enableSplash ||
    splashDuration !== originalSettings.splashDuration ||
    defaultOcrLang !== originalSettings.defaultOcrLang ||
    clearMetadata !== originalSettings.clearMetadata ||
    imageQuality !== originalSettings.imageQuality ||
    argonMemory !== originalSettings.argonMemory ||
    argonIterations !== originalSettings.argonIterations ||
    autoLockVault !== originalSettings.autoLockVault;

  useEffect(() => {
    invoke("load_settings").then((settings: any) => {
      if (settings.theme) setTheme(settings.theme as any);
      if (settings.theme_accent) setAccent(settings.theme_accent as any);
      if (settings.hardware_concurrency) setConcurrency(settings.hardware_concurrency);
      if (settings.minimize_to_tray !== undefined) setMinimizeToTray(settings.minimize_to_tray);
      if (settings.enable_splash !== undefined) setEnableSplash(settings.enable_splash);
      if (settings.splash_duration !== undefined) setSplashDuration(settings.splash_duration);
      if (settings.default_ocr_lang !== undefined) setDefaultOcrLang(settings.default_ocr_lang);
      if (settings.clear_metadata !== undefined) setClearMetadata(settings.clear_metadata);
      if (settings.image_quality !== undefined) setImageQuality(settings.image_quality);
      if (settings.argon_memory !== undefined) setArgonMemory(settings.argon_memory);
      if (settings.argon_iterations !== undefined) setArgonIterations(settings.argon_iterations);
      if (settings.auto_lock_vault !== undefined) setAutoLockVault(settings.auto_lock_vault);

      const getDir = async () => {
        let dir = settings.output_directory;
        if (!dir || dir === "") {
          dir = await invoke("get_download_dir");
        }
        setOutputDir(dir);
        setOriginalSettings({
          theme: settings.theme || "system",
          themeAccent: settings.theme_accent || "blue",
          concurrency: settings.hardware_concurrency || 8,
          outputDir: dir,
          minimizeToTray: settings.minimize_to_tray ?? true,
          enableSplash: settings.enable_splash ?? true,
          splashDuration: settings.splash_duration ?? 1800,
          defaultOcrLang: settings.default_ocr_lang || "en",
          clearMetadata: settings.clear_metadata ?? true,
          imageQuality: settings.image_quality ?? 85,
          argonMemory: settings.argon_memory ?? 65536,
          argonIterations: settings.argon_iterations ?? 3,
          autoLockVault: settings.auto_lock_vault ?? 15,
        });
      };
      getDir();
    }).catch(() => {
      invoke("get_download_dir").then((dir) => {
        setOutputDir(dir as string);
        setOriginalSettings((prev) => ({ ...prev, outputDir: dir as string }));
      });
    });

    // Check autostart status
    invoke("get_autostart_enabled")
      .then((enabled) => setAutostartEnabled(enabled as boolean))
      .catch(() => {}); // Non-critical
  }, [setTheme, setAccent]);

  const handleBrowse = async () => {
    try {
      const selected = await openDialog({
        directory: true,
        multiple: false,
        title: "Select Output Directory",
      });
      if (selected) setOutputDir(selected as string);
    } catch (e) {
      console.error(e);
    }
  };

  const revertChanges = () => {
    setTheme(originalSettings.theme);
    setAccent(originalSettings.themeAccent);
    setConcurrency(originalSettings.concurrency);
    setOutputDir(originalSettings.outputDir);
    setMinimizeToTray(originalSettings.minimizeToTray);
    setEnableSplash(originalSettings.enableSplash);
    setSplashDuration(originalSettings.splashDuration);
    setDefaultOcrLang(originalSettings.defaultOcrLang);
    setClearMetadata(originalSettings.clearMetadata);
    setImageQuality(originalSettings.imageQuality);
    setArgonMemory(originalSettings.argonMemory);
    setArgonIterations(originalSettings.argonIterations);
    setAutoLockVault(originalSettings.autoLockVault);
  };

  const saveChanges = async () => {
    setSaving(true);
    try {
      await invoke("save_settings", {
        settings: {
          output_directory: outputDir,
          hardware_concurrency: concurrency,
          theme: theme,
          theme_accent: accent,
          minimize_to_tray: minimizeToTray,
          enable_splash: enableSplash,
          splash_duration: splashDuration,
          default_ocr_lang: defaultOcrLang,
          clear_metadata: clearMetadata,
          image_quality: imageQuality,
          argon_memory: argonMemory,
          argon_iterations: argonIterations,
          auto_lock_vault: autoLockVault,
        },
      });
      setOriginalSettings({
        theme,
        themeAccent: accent,
        concurrency,
        outputDir,
        minimizeToTray,
        enableSplash,
        splashDuration,
        defaultOcrLang,
        clearMetadata,
        imageQuality,
        argonMemory,
        argonIterations,
        autoLockVault,
      });
      success("Settings saved successfully!");
      setTimeout(() => setSaving(false), 500);
    } catch (e: any) {
      error(`Failed to save settings: ${e}`);
      setSaving(false);
    }
  };

  const handleAutostartToggle = async (enabled: boolean) => {
    try {
      await invoke("set_autostart_enabled", { enabled });
      setAutostartEnabled(enabled);
      success(enabled ? "Axora added to startup apps!" : "Removed from startup apps.");
    } catch (e: any) {
      error(`Autostart toggle failed: ${e}`);
    }
  };

  const Toggle = ({
    enabled,
    onChange,
    disabled = false,
  }: {
    enabled: boolean;
    onChange: (v: boolean) => void;
    disabled?: boolean;
  }) => (
    <button
      onClick={() => onChange(!enabled)}
      disabled={disabled}
      className="relative w-12 h-6 rounded-full transition-colors duration-200 disabled:opacity-50"
      style={{ backgroundColor: enabled ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)" }}
    >
      <motion.div
        className="absolute top-1 w-4 h-4 rounded-full shadow-sm"
        style={{ backgroundColor: enabled ? "var(--md-sys-color-on-primary)" : "var(--md-sys-color-surface)" }}
        animate={{ left: enabled ? "calc(100% - 20px)" : "4px" }}
        transition={{ duration: 0.2, ease: [0.2, 0, 0, 1] }}
      />
    </button>
  );

  const accentPills: { name: AccentColor; color: string }[] = [
    { name: "blue", color: "#0b57d0" },
    { name: "purple", color: "#8a3ffc" },
    { name: "green", color: "#386a20" },
    { name: "red", color: "#bc1e33" },
    { name: "orange", color: "#8b5000" },
  ];

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500 relative pb-28">
      <header className="mb-8">
        <h2 className="text-3xl font-medium mb-2 flex items-center gap-3" style={{ color: "var(--md-sys-color-on-surface)" }}>
          <SettingsIcon style={{ color: "var(--md-sys-color-primary)" }} size={28} />
          Settings
        </h2>
        <p className="text-lg" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
          Manage application preferences and hardware configuration.
        </p>
      </header>

      <div className="space-y-6 max-w-3xl flex-1">

        {/* ── Appearance ──────────────────────────────────────────────── */}
        <div
          className="border rounded-[2rem] p-6 shadow-sm hover:shadow-md transition-shadow duration-300"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container-low)",
            borderColor: "var(--md-sys-color-outline-variant)",
          }}
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="p-3 rounded-2xl shadow-sm" style={{ backgroundColor: "var(--md-sys-color-primary-container)" }}>
              <Palette size={24} style={{ color: "var(--md-sys-color-on-primary-container)" }} />
            </div>
            <div>
              <h3 className="text-lg font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Appearance</h3>
              <p style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Customize theme mode and accent colors</p>
            </div>
          </div>

          <div className="pl-16 space-y-6">
            {/* Dark/Light mode buttons */}
            <div>
              <p className="text-sm font-medium mb-2" style={{ color: "var(--md-sys-color-on-surface)" }}>Theme Mode</p>
              <div className="flex items-center gap-3">
                {(["dark", "light", "system"] as const).map((t) => (
                  <button
                    key={t}
                    onClick={() => setTheme(t)}
                    className="px-6 py-2.5 rounded-full font-medium transition-all capitalize"
                    style={{
                      backgroundColor: theme === t ? "var(--md-sys-color-primary-container)" : "var(--md-sys-color-surface-container)",
                      color: theme === t ? "var(--md-sys-color-on-primary-container)" : "var(--md-sys-color-on-surface-variant)",
                      border: `1px solid ${theme === t ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)"}`,
                    }}
                  >
                    {t}
                  </button>
                ))}
              </div>
            </div>

            {/* Accent Color Picker */}
            <div>
              <p className="text-sm font-medium mb-2" style={{ color: "var(--md-sys-color-on-surface)" }}>Accent Palette</p>
              <div className="flex items-center gap-3.5">
                {accentPills.map((pill) => (
                  <button
                    key={pill.name}
                    onClick={() => setAccent(pill.name)}
                    className="w-10 h-10 rounded-full flex items-center justify-center relative border border-black/10 dark:border-white/10"
                    style={{ backgroundColor: pill.color }}
                  >
                    {accent === pill.name && (
                      <motion.div
                        className="absolute inset-0.5 rounded-full border-2 border-white dark:border-black"
                        layoutId="activeAccentOutline"
                      />
                    )}
                  </button>
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* ── Output Directory ────────────────────────────────────────── */}
        <div
          className="border rounded-[2rem] p-6 shadow-sm hover:shadow-md transition-shadow duration-300"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container-low)",
            borderColor: "var(--md-sys-color-outline-variant)",
          }}
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="p-3 rounded-2xl shadow-sm" style={{ backgroundColor: "rgba(56,142,60,0.15)" }}>
              <FolderOpen size={24} style={{ color: "#388e3c" }} />
            </div>
            <div>
              <h3 className="text-lg font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Output Directory</h3>
              <p style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Default save location for processed files</p>
            </div>
          </div>
          <div className="pl-16 flex gap-3">
            <input
              type="text" readOnly value={outputDir}
              className="flex-1 rounded-xl px-4 py-3 outline-none text-sm font-medium shadow-sm border"
              style={{
                backgroundColor: "var(--md-sys-color-surface-container)",
                borderColor: "var(--md-sys-color-outline-variant)",
                color: "var(--md-sys-color-on-surface)",
              }}
            />
            <button
              onClick={handleBrowse}
              className="px-6 py-3 rounded-xl font-medium text-sm border hover:brightness-105 transition-all active:scale-95 shadow-sm"
              style={{
                backgroundColor: "var(--md-sys-color-surface-container)",
                borderColor: "var(--md-sys-color-outline-variant)",
                color: "var(--md-sys-color-on-surface)",
              }}
            >
              Browse
            </button>
          </div>
        </div>

        {/* ── Hardware Configuration ───────────────────────────────────── */}
        <div
          className="border rounded-[2rem] p-6 shadow-sm hover:shadow-md transition-shadow duration-300"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container-low)",
            borderColor: "var(--md-sys-color-outline-variant)",
          }}
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="p-3 rounded-2xl shadow-sm" style={{ backgroundColor: "var(--md-sys-color-tertiary-container)" }}>
              <Cpu size={24} style={{ color: "var(--md-sys-color-on-tertiary-container)" }} />
            </div>
            <div>
              <h3 className="text-lg font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Hardware & System</h3>
              <p style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Adjust background threads and startup behaviors</p>
            </div>
          </div>

          <div className="pl-16 space-y-6">
            {/* Thread slider */}
            <div>
              <p className="text-sm font-medium mb-1.5" style={{ color: "var(--md-sys-color-on-surface)" }}>Hardware Concurrency</p>
              <input
                type="range" min="1" max="16" value={concurrency}
                onChange={(e) => setConcurrency(parseInt(e.target.value))}
                className="w-full h-2 rounded-lg appearance-none cursor-pointer bg-md-surface-container"
                style={{ accentColor: "var(--md-sys-color-primary)" }}
              />
              <div className="flex justify-between text-xs mt-2" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>
                <span>1 Core</span>
                <span className="font-semibold" style={{ color: "var(--md-sys-color-on-surface)" }}>{concurrency} Cores</span>
                <span>Max</span>
              </div>
            </div>

            <div className="h-px bg-md-outline-variant/20" />

            {/* Minimize to tray & autostart switches */}
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <MonitorDown size={18} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                    <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Minimize to Tray on Close</p>
                  </div>
                  <p className="text-[11px] mt-0.5 pl-7" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Hide to tray instead of exiting</p>
                </div>
                <Toggle enabled={minimizeToTray} onChange={setMinimizeToTray} />
              </div>

              <div className="flex items-center justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <Rocket size={18} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                    <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Launch at Startup</p>
                  </div>
                  <p className="text-[11px] mt-0.5 pl-7" style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Add app registry entry to boot on system startup</p>
                </div>
                <Toggle enabled={autostartEnabled} onChange={handleAutostartToggle} />
              </div>
            </div>
          </div>
        </div>

        {/* ── Splash Screen settings ────────────────────────────────────── */}
        <div
          className="border rounded-[2rem] p-6 shadow-sm hover:shadow-md transition-shadow duration-300"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container-low)",
            borderColor: "var(--md-sys-color-outline-variant)",
          }}
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="p-3 rounded-2xl shadow-sm" style={{ backgroundColor: "rgba(255,213,79,0.15)" }}>
              <Palette size={24} style={{ color: "#fbc02d" }} />
            </div>
            <div>
              <h3 className="text-lg font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Startup Splash Preferences</h3>
              <p style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Configure splash screen behavior</p>
            </div>
          </div>

          <div className="pl-16 space-y-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Enable Startup Animation</p>
                <p className="text-[11px] mt-0.5 text-md-on-surface-variant">Play the animated splash screen on app boot</p>
              </div>
              <Toggle enabled={enableSplash} onChange={setEnableSplash} />
            </div>

            {enableSplash && (
              <div>
                <p className="text-sm font-medium mb-2" style={{ color: "var(--md-sys-color-on-surface)" }}>Splash Animation Speed</p>
                <div className="flex gap-2">
                  {[
                    { label: "Fast (1.0s)", value: 1000 },
                    { label: "Standard (1.8s)", value: 1800 },
                    { label: "Relaxed (3.0s)", value: 3000 }
                  ].map((item) => (
                    <button
                      key={item.value}
                      onClick={() => setSplashDuration(item.value)}
                      className="px-4 py-2 rounded-xl text-xs font-semibold border transition-all"
                      style={{
                        backgroundColor: splashDuration === item.value ? "var(--md-sys-color-primary-container)" : "var(--md-sys-color-surface-container)",
                        color: splashDuration === item.value ? "var(--md-sys-color-on-primary-container)" : "var(--md-sys-color-on-surface-variant)",
                        borderColor: splashDuration === item.value ? "var(--md-sys-color-primary)" : "var(--md-sys-color-outline-variant)",
                      }}
                    >
                      {item.label}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* ── Advanced Modules (AxoraVault & Scholar Kit) ─────────────── */}
        <div
          className="border rounded-[2rem] p-6 shadow-sm hover:shadow-md transition-shadow duration-300"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container-low)",
            borderColor: "var(--md-sys-color-outline-variant)",
          }}
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="p-3 rounded-2xl shadow-sm" style={{ backgroundColor: "rgba(107,63,160,0.12)" }}>
              <ShieldCheck size={24} style={{ color: "var(--md-sys-color-tertiary)" }} />
            </div>
            <div>
              <h3 className="text-lg font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Security & Cryptography</h3>
              <p style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Configure encryption parameters for AxoraVault</p>
            </div>
          </div>

          <div className="pl-16 space-y-6">
            {/* Auto-lock timer */}
            <div>
              <p className="text-sm font-medium mb-1.5" style={{ color: "var(--md-sys-color-on-surface)" }}>Vault Auto-Lock Timer</p>
              <select
                value={autoLockVault}
                onChange={(e) => setAutoLockVault(Number(e.target.value))}
                className="w-full max-w-xs rounded-xl px-4 py-2 outline-none text-xs border bg-md-surface-container border-md-outline-variant/30 text-md-on-surface"
              >
                <option value={0}>Never Lock Automatically</option>
                <option value={1}>1 Minute</option>
                <option value={5}>5 Minutes</option>
                <option value={15}>15 Minutes</option>
                <option value={30}>30 Minutes</option>
              </select>
            </div>

            <div className="h-px bg-md-outline-variant/20" />

            {/* Argon2 parameters */}
            <div className="space-y-4">
              <div>
                <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Argon2id Key Derivation Tuning</p>
                <p className="text-[11px] text-md-on-surface-variant mt-0.5">Tune CPU complexity parameters for password hashing.</p>
              </div>

              <div>
                <div className="flex justify-between text-xs mb-1">
                  <span style={{ color: "var(--md-sys-color-on-surface)" }}>Memory Cost:</span>
                  <span className="font-bold text-md-primary">{(argonMemory / 1024).toFixed(0)} MB</span>
                </div>
                <input
                  type="range" min={16384} max={262144} step={16384} value={argonMemory}
                  onChange={(e) => setArgonMemory(Number(e.target.value))}
                  className="w-full h-1.5 rounded-lg appearance-none cursor-pointer bg-md-surface-container"
                  style={{ accentColor: "var(--md-sys-color-primary)" }}
                />
              </div>

              <div>
                <div className="flex justify-between text-xs mb-1">
                  <span style={{ color: "var(--md-sys-color-on-surface)" }}>Time Cost (Iterations):</span>
                  <span className="font-bold text-md-primary">{argonIterations} Passes</span>
                </div>
                <input
                  type="range" min={1} max={10} value={argonIterations}
                  onChange={(e) => setArgonIterations(Number(e.target.value))}
                  className="w-full h-1.5 rounded-lg appearance-none cursor-pointer bg-md-surface-container"
                  style={{ accentColor: "var(--md-sys-color-primary)" }}
                />
              </div>
            </div>
          </div>
        </div>

        {/* ── Bulk Canvas & Scholar Kit Defaults ───────────────────────── */}
        <div
          className="border rounded-[2rem] p-6 shadow-sm hover:shadow-md transition-shadow duration-300"
          style={{
            backgroundColor: "var(--md-sys-color-surface-container-low)",
            borderColor: "var(--md-sys-color-outline-variant)",
          }}
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="p-3 rounded-2xl shadow-sm" style={{ backgroundColor: "rgba(255,110,64,0.12)" }}>
              <Image size={24} style={{ color: "#ff6e40" }} />
            </div>
            <div>
              <h3 className="text-lg font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Image & Documents Defaults</h3>
              <p style={{ color: "var(--md-sys-color-on-surface-variant)" }}>Configure default quality and language settings</p>
            </div>
          </div>

          <div className="pl-16 space-y-6">
            {/* Image compression */}
            <div>
              <div className="flex justify-between text-sm mb-1.5">
                <span className="font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Default Image Compression Quality</span>
                <span className="font-semibold text-md-primary">{imageQuality}%</span>
              </div>
              <input
                type="range" min={50} max={100} value={imageQuality}
                onChange={(e) => setImageQuality(Number(e.target.value))}
                className="w-full h-2 rounded-lg appearance-none cursor-pointer bg-md-surface-container"
                style={{ accentColor: "var(--md-sys-color-primary)" }}
              />
            </div>

            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Remove Metadata (EXIF) on Save</p>
                <p className="text-[11px] mt-0.5 text-md-on-surface-variant">Strip camera model, GPS coordinates, and capture details for privacy</p>
              </div>
              <Toggle enabled={clearMetadata} onChange={setClearMetadata} />
            </div>

            <div className="h-px bg-md-outline-variant/20" />

            {/* OCR Language */}
            <div>
              <div className="flex items-center gap-2 mb-2">
                <FileText size={18} style={{ color: "var(--md-sys-color-on-surface-variant)" }} />
                <p className="text-sm font-medium" style={{ color: "var(--md-sys-color-on-surface)" }}>Default OCR Recognition Language</p>
              </div>
              <select
                value={defaultOcrLang}
                onChange={(e) => setDefaultOcrLang(e.target.value)}
                className="w-full max-w-xs rounded-xl px-4 py-2 outline-none text-xs border bg-md-surface-container border-md-outline-variant/30 text-md-on-surface"
              >
                <option value="en">English (default)</option>
                <option value="es">Spanish (Español)</option>
                <option value="de">German (Deutsch)</option>
                <option value="fr">French (Français)</option>
              </select>
            </div>
          </div>
        </div>

      </div>

      {/* ── Floating Save/Revert Pill ─────────────────────────────────── */}
      <motion.div
        className="absolute bottom-8 left-1/2 -translate-x-1/2 z-50"
        initial={{ opacity: 0, y: 20, scale: 0.9 }}
        animate={{
          opacity: isDirty ? 1 : 0,
          y: isDirty ? 0 : 20,
          scale: isDirty ? 1 : 0.9,
        }}
        transition={{ duration: 0.25, ease: [0.34, 1.56, 0.64, 1] }}
        style={{ pointerEvents: isDirty ? "auto" : "none" }}
      >
        <div
          className="px-6 py-4 rounded-full shadow-2xl flex items-center gap-4 border"
          style={{
            backgroundColor: "var(--md-sys-color-inverse-surface)",
            borderColor: "rgba(255,255,255,0.1)",
          }}
        >
          <span className="text-sm flex items-center gap-2" style={{ color: "var(--md-sys-color-inverse-on-surface)" }}>
            <Palette size={16} style={{ color: "#ffd54f" }} />
            Unsaved changes
          </span>
          <button
            onClick={revertChanges}
            disabled={saving}
            className="text-sm font-medium flex items-center gap-1 hover:opacity-80 transition-opacity"
            style={{ color: "var(--md-sys-color-inverse-on-surface)" }}
          >
            <RotateCcw size={14} />
            Revert
          </button>
          <button
            onClick={saveChanges}
            disabled={saving}
            className="px-6 py-2 rounded-full font-medium text-sm flex items-center gap-2 hover:brightness-110 transition-all shadow-md active:scale-95 disabled:opacity-70"
            style={{
              backgroundColor: "var(--md-sys-color-primary)",
              color: "var(--md-sys-color-on-primary)",
            }}
          >
            <Save size={14} />
            {saving ? "Saving..." : "Save"}
          </button>
        </div>
      </motion.div>
    </div>
  );
}
