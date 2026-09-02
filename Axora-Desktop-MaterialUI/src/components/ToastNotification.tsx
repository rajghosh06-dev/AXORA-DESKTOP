import { AnimatePresence, motion } from "framer-motion";
import { X, CheckCircle, AlertTriangle, XCircle } from "lucide-react";
import { useToastStore, Toast, ToastVariant } from "../store/toastStore";

// ─────────────────────────────────────────────────────────────────────────────
// MD3 Toast config by variant
// ─────────────────────────────────────────────────────────────────────────────
const TOAST_CONFIG: Record<
  ToastVariant,
  { icon: React.ElementType; bg: string; border: string; text: string; iconColor: string }
> = {
  info: {
    icon: CheckCircle,
    bg: "rgba(11, 87, 208, 0.12)",
    border: "rgba(11, 87, 208, 0.3)",
    text: "#0b57d0",
    iconColor: "#0b57d0",
  },
  warning: {
    icon: AlertTriangle,
    bg: "rgba(251, 188, 4, 0.12)",
    border: "rgba(251, 188, 4, 0.4)",
    text: "#8a6800",
    iconColor: "#f9ab00",
  },
  error: {
    icon: XCircle,
    bg: "rgba(179, 38, 30, 0.12)",
    border: "rgba(179, 38, 30, 0.3)",
    text: "#b3261e",
    iconColor: "#b3261e",
  },
};

// Dark-mode toast config
const TOAST_CONFIG_DARK: Record<
  ToastVariant,
  { bg: string; border: string; text: string; iconColor: string }
> = {
  info: {
    bg: "rgba(168, 199, 250, 0.12)",
    border: "rgba(168, 199, 250, 0.25)",
    text: "#a8c7fa",
    iconColor: "#a8c7fa",
  },
  warning: {
    bg: "rgba(255, 213, 79, 0.10)",
    border: "rgba(255, 213, 79, 0.3)",
    text: "#ffd54f",
    iconColor: "#ffd54f",
  },
  error: {
    bg: "rgba(242, 184, 181, 0.12)",
    border: "rgba(242, 184, 181, 0.3)",
    text: "#f2b8b5",
    iconColor: "#f2b8b5",
  },
};

// ─────────────────────────────────────────────────────────────────────────────
// Single Toast Item
// ─────────────────────────────────────────────────────────────────────────────
function ToastItem({ toast }: { toast: Toast }) {
  const removeToast = useToastStore((s) => s.removeToast);
  const config = TOAST_CONFIG[toast.variant];
  const Icon = config.icon;

  const isDark = document.documentElement.classList.contains("dark");
  const darkConfig = isDark ? TOAST_CONFIG_DARK[toast.variant] : null;

  const bg = darkConfig?.bg ?? config.bg;
  const border = darkConfig?.border ?? config.border;
  const text = darkConfig?.text ?? config.text;
  const iconColor = darkConfig?.iconColor ?? config.iconColor;

  return (
    <motion.div
      layout
      initial={{ opacity: 0, x: 60, scale: 0.9 }}
      animate={{ opacity: 1, x: 0, scale: 1 }}
      exit={{ opacity: 0, x: 60, scale: 0.85 }}
      transition={{ duration: 0.28, ease: [0.2, 0, 0, 1] }}
      className="flex items-start gap-3 px-4 py-3 rounded-2xl shadow-lg max-w-sm"
      style={{
        background: `${bg} / 0.8`,
        backdropFilter: "blur(16px)",
        WebkitBackdropFilter: "blur(16px)",
        backgroundColor: bg,
        border: `1px solid ${border}`,
        boxShadow: "0px 4px 12px rgba(0,0,0,0.15), 0px 1px 3px rgba(0,0,0,0.2)",
      }}
      role="alert"
      aria-live="polite"
    >
      {/* Icon */}
      <span className="flex-shrink-0 mt-0.5">
        <Icon size={18} style={{ color: iconColor }} />
      </span>

      {/* Message */}
      <p
        className="flex-1 text-sm font-medium leading-snug"
        style={{ color: text }}
      >
        {toast.message}
      </p>

      {/* Dismiss button */}
      <button
        onClick={() => removeToast(toast.id)}
        className="flex-shrink-0 rounded-full p-0.5 hover:bg-black/10 dark:hover:bg-white/10 transition-colors"
        aria-label="Dismiss notification"
      >
        <X size={14} style={{ color: text }} />
      </button>
    </motion.div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Toast Container — renders all active toasts in top-right corner
// ─────────────────────────────────────────────────────────────────────────────
export function ToastNotification() {
  const toasts = useToastStore((s) => s.toasts);

  return (
    <div
      className="fixed top-4 right-4 z-[9999] flex flex-col gap-2 pointer-events-none"
      aria-label="Notifications"
    >
      <AnimatePresence mode="popLayout">
        {toasts.map((toast) => (
          <div key={toast.id} className="pointer-events-auto">
            <ToastItem toast={toast} />
          </div>
        ))}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Convenience hooks for dispatching toasts
// ─────────────────────────────────────────────────────────────────────────────
export function useToast() {
  const addToast = useToastStore((s) => s.addToast);

  return {
    /** Blue banner — auto-dismissed after 2.5 seconds */
    success: (message: string) => addToast({ variant: "info", message }),

    /** Yellow banner — persistent until user dismisses */
    warning: (message: string) => addToast({ variant: "warning", message }),

    /** Red banner — persistent, requires explicit dismissal */
    error: (message: string) => addToast({ variant: "error", message }),
  };
}
