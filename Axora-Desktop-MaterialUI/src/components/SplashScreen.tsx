import { useEffect, useState, useRef } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { invoke } from "@tauri-apps/api/core";
import { useThemeStore } from "../store/themeStore";

interface SplashScreenProps {
  onComplete: () => void;
}

// Individual animated letter for the title stagger effect
function AnimatedLetter({ char, delay }: { char: string; delay: number }) {
  return (
    <motion.span
      style={{ display: "inline-block", willChange: "transform, opacity" }}
      initial={{ opacity: 0, y: 20, scale: 0.8 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{
        delay,
        duration: 0.4,
        ease: [0.34, 1.4, 0.64, 1], // Spring-like — matches Google's letter animation
      }}
    >
      {char === " " ? "\u00a0" : char}
    </motion.span>
  );
}

/**
 * Premium macOS/Google-inspired startup splash.
 *
 * Animation phases:
 * 1. (0ms)    Ring expands from center — Gemini-style reveal
 * 2. (200ms)  Logo springs in with overshoot bounce
 * 3. (500ms)  "Axora" title appears letter by letter (staggered spring)
 * 4. (900ms)  Subtitle & MD3 linear progress indicator appear
 * 5. (exit)   Scale-down + fade-out — the whole splash contracts to center (macOS-style)
 */
export default function SplashScreen({ onComplete }: SplashScreenProps) {
  const [visible, setVisible] = useState(true);
  const [progress, setProgress] = useState(0);
  const [statusText, setStatusText] = useState("Starting…");
  const [logoVisible, setLogoVisible] = useState(false);
  const [textVisible, setTextVisible] = useState(false);
  const dismissedRef = useRef(false);

  const dismiss = () => {
    if (dismissedRef.current) return;
    dismissedRef.current = true;
    setProgress(100);
    setStatusText("Ready  ✓");
    setTimeout(() => {
      setVisible(false);
      setTimeout(onComplete, 380);
    }, 280);
  };

  useEffect(() => {
    const startTime = Date.now();
    let minDuration = 1800;

    // Staggered reveal sequence
    const t1 = setTimeout(() => setLogoVisible(true), 60);
    const t2 = setTimeout(() => setTextVisible(true), 360);
    const t3 = setTimeout(() => !dismissedRef.current && setProgress(35), 300);
    const t4 = setTimeout(() => {
      if (!dismissedRef.current) {
        setProgress(55);
        setStatusText("Loading modules…");
      }
    }, 600);
    const t5 = setTimeout(() => !dismissedRef.current && setProgress(78), 1000);

    // Load backend preferences and apply theme
    invoke("load_settings")
      .then((settings: any) => {
        if (settings.enable_splash === false) {
          minDuration = 0;
        } else if (settings.splash_duration !== undefined) {
          minDuration = Number(settings.splash_duration);
        }

        if (settings.theme_accent) {
          localStorage.setItem("axora-accent", settings.theme_accent);
        }
        if (settings.theme) {
          localStorage.setItem("axora-theme", settings.theme);
        }
        useThemeStore.getState().initializeTheme();
      })
      .catch((e) => console.warn("Failed to load settings on startup", e))
      .finally(() => {
        // If splash is disabled, dismiss immediately
        if (minDuration === 0) {
          dismiss();
          return;
        }

        // Ping backend IPC
        invoke("ping_backend")
          .then(() => {
            const elapsed = Date.now() - startTime;
            const remaining = Math.max(0, minDuration - elapsed);
            setTimeout(() => {
              setStatusText("Backend ready");
              dismiss();
            }, remaining);
          })
          .catch(() => {
            const elapsed = Date.now() - startTime;
            const remaining = Math.max(0, minDuration - elapsed);
            setTimeout(() => {
              setStatusText("Workspace ready");
              dismiss();
            }, remaining);
          });
      });

    // Hard cap timeout fallback (default 3.5s to accommodate relaxed durations)
    const hardTimeout = setTimeout(dismiss, 3500);

    return () => {
      [t1, t2, t3, t4, t5, hardTimeout].forEach(clearTimeout);
    };
  }, []);

  const titleChars = "Axora".split("");

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className="fixed inset-0 z-[99999] flex flex-col items-center justify-center overflow-hidden select-none"
          initial={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.96 }}
          transition={{ duration: 0.38, ease: [0.4, 0, 1, 1] }}
          style={{
            backgroundColor: "var(--md-sys-color-surface)",
            willChange: "opacity, transform",
          }}
        >
          {/* ── Very subtle radial surface toning — not flashy ──────────── */}
          <div
            className="absolute inset-0 pointer-events-none"
            style={{
              background:
                "radial-gradient(ellipse 70% 60% at 50% 50%, color-mix(in srgb, var(--md-sys-color-primary) 6%, transparent) 0%, transparent 100%)",
            }}
          />

          {/* ── Main content block ──────────────────────────────────────── */}
          <div className="relative z-10 flex flex-col items-center gap-6">

            {/* ── Phase 1+2: Expanding ring → Logo spring-in ─────────────── */}
            <div className="relative flex items-center justify-center">
              {/* Expanding ring — appears first */}
              <motion.div
                className="absolute rounded-full"
                initial={{ width: 24, height: 24, opacity: 0.8 }}
                animate={{ width: 160, height: 160, opacity: 0 }}
                transition={{ duration: 0.7, ease: [0.4, 0, 0.2, 1], delay: 0 }}
                style={{
                  border: "2px solid var(--md-sys-color-primary)",
                  willChange: "width, height, opacity",
                }}
              />

              {/* Second ring — delayed, slower */}
              <motion.div
                className="absolute rounded-full"
                initial={{ width: 24, height: 24, opacity: 0.5 }}
                animate={{ width: 200, height: 200, opacity: 0 }}
                transition={{ duration: 0.9, ease: [0.4, 0, 0.2, 1], delay: 0.12 }}
                style={{
                  border: "1.5px solid var(--md-sys-color-secondary)",
                  willChange: "width, height, opacity",
                }}
              />

              {/* Logo container — springs in after ring */}
              <AnimatePresence>
                {logoVisible && (
                  <motion.div
                    className="w-20 h-20 rounded-2xl flex items-center justify-center relative z-10"
                    initial={{ scale: 0.3, opacity: 0, rotate: -8 }}
                    animate={{ scale: 1, opacity: 1, rotate: 0 }}
                    transition={{
                      type: "spring",
                      stiffness: 280,
                      damping: 18,
                      delay: 0,
                    }}
                    style={{
                      backgroundColor: "var(--md-sys-color-surface-container-high)",
                      boxShadow:
                        "0 4px 20px color-mix(in srgb, var(--md-sys-color-primary) 25%, transparent), 0 1px 4px rgba(0,0,0,0.15)",
                      border: "1px solid color-mix(in srgb, var(--md-sys-color-primary) 18%, transparent)",
                      willChange: "transform, opacity",
                    }}
                  >
                    <img
                      src="/src/assets/logo-transparent.png"
                      className="w-14 h-14 drop-shadow-md"
                      alt="Axora"
                      style={{ imageRendering: "crisp-edges" }}
                    />
                  </motion.div>
                )}
              </AnimatePresence>
            </div>

            {/* ── Phase 3: Letter-by-letter title animation ───────────────── */}
            <AnimatePresence>
              {textVisible && (
                <motion.div
                  className="text-center"
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ duration: 0.01 }}
                >
                  {/* Title: each letter springs in with stagger */}
                  <h1
                    className="flex items-baseline justify-center font-semibold tracking-tight"
                    style={{
                      fontSize: "28px",
                      color: "var(--md-sys-color-on-surface)",
                      fontFamily: "'Google Sans', Roboto, sans-serif",
                      letterSpacing: "-0.3px",
                      lineHeight: 1.2,
                    }}
                  >
                    {titleChars.map((char, i) => (
                      <AnimatedLetter
                        key={i}
                        char={char}
                        delay={i * 0.04} // 40ms stagger per letter
                      />
                    ))}
                  </h1>

                  {/* Subtitle fades in after title completes */}
                  <motion.p
                    className="mt-1"
                    style={{
                      fontSize: "12px",
                      color: "var(--md-sys-color-on-surface-variant)",
                      fontFamily: "'Google Sans', Roboto, sans-serif",
                    }}
                    initial={{ opacity: 0, y: 6 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.45, duration: 0.3, ease: [0.2, 0, 0, 1] }}
                  >
                    Windows 11 Productivity Suite
                  </motion.p>
                </motion.div>
              )}
            </AnimatePresence>

            {/* ── Phase 4: Status + MD3 Linear Progress indicator ─────────── */}
            <AnimatePresence>
              {textVisible && (
                <motion.div
                  className="flex flex-col items-center gap-2"
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.55, duration: 0.3, ease: [0.2, 0, 0, 1] }}
                >
                  {/* Status label — animates on text change */}
                  <motion.p
                    key={statusText}
                    style={{
                      fontSize: "11px",
                      color: "var(--md-sys-color-primary)",
                      fontFamily: "'Google Sans', Roboto, sans-serif",
                      fontWeight: 500,
                    }}
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    transition={{ duration: 0.2 }}
                  >
                    {statusText}
                  </motion.p>

                  {/* MD3 Linear Determinate Progress Bar */}
                  <div
                    className="overflow-hidden"
                    style={{
                      width: "160px",
                      height: "3px",
                      borderRadius: "100px",
                      backgroundColor: "var(--md-sys-color-surface-container-high)",
                    }}
                  >
                    <motion.div
                      style={{
                        height: "100%",
                        borderRadius: "100px",
                        backgroundColor: "var(--md-sys-color-primary)",
                        willChange: "width",
                      }}
                      animate={{ width: `${progress}%` }}
                      transition={{ duration: 0.5, ease: [0.2, 0, 0, 1] }}
                    />
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
