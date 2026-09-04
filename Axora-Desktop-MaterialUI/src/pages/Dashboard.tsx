import { useState, useEffect } from "react";
import { invoke } from "@tauri-apps/api/core";
import logoImg from "../assets/logo-transparent.png";
import {
  Sparkles, ArrowRight, Zap, ShieldCheck, FileType,
  ScanLine, Smartphone, Activity,
} from "lucide-react";
import { motion } from "framer-motion";
import { MdRipple } from "../components/MdRipple";

const staggerContainer = {
  hidden: {},
  show: {
    transition: { staggerChildren: 0.06 },
  },
};

const fadeUp = {
  hidden: { opacity: 0, y: 16 },
  show: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.3, ease: [0.2, 0, 0, 1] },
  },
};

export default function Dashboard({ setCurrentPage }: { setCurrentPage: (page: string) => void }) {
  const [status, setStatus] = useState<string>("Checking...");

  useEffect(() => {
    invoke("ping_backend")
      .then((res) => setStatus(res as string))
      .catch(() => setStatus("Backend Disconnected"));
  }, []);

  const quickActions = [
    {
      title: "Convert Document",
      icon: <FileType size={22} />,
      desc: "PDF, DOCX, PPTX — locally",
      page: "Universal Engine",
      color: "from-purple-500/20 to-purple-600/10",
      iconColor: "text-purple-500 dark:text-purple-400",
      borderColor: "border-purple-500/20",
    },
    {
      title: "Secure File",
      icon: <ShieldCheck size={22} />,
      desc: "AES-256 + Argon2id",
      page: "AxoraVault",
      color: "from-blue-500/20 to-blue-600/10",
      iconColor: "text-blue-500 dark:text-blue-400",
      borderColor: "border-blue-500/20",
    },
    {
      title: "Batch Process",
      icon: <Zap size={22} />,
      desc: "Up to 3000 images at once",
      page: "Bulk Canvas",
      color: "from-yellow-500/20 to-orange-500/10",
      iconColor: "text-yellow-500",
      borderColor: "border-yellow-500/20",
    },
    {
      title: "Scan Document",
      icon: <ScanLine size={22} />,
      desc: "WIA hardware scanner",
      page: "Hardware Capture",
      color: "from-teal-500/20 to-teal-600/10",
      iconColor: "text-teal-500",
      borderColor: "border-teal-500/20",
    },
    {
      title: "Pair Mobile",
      icon: <Smartphone size={22} />,
      desc: "Android 16+ · mDNS · ECDH",
      page: "Mobile Link",
      color: "from-green-500/20 to-emerald-500/10",
      iconColor: "text-green-500",
      borderColor: "border-green-500/20",
    },
    {
      title: "Analytics",
      icon: <Activity size={22} />,
      desc: "View system stats",
      page: "CompatibilityModal",
      color: "from-red-500/20 to-pink-500/10",
      iconColor: "text-red-500",
      borderColor: "border-red-500/20",
    },
  ];

  return (
    <div className="flex flex-col min-h-full">
      {/* ── Header ───────────────────────────────────────────────── */}
      <motion.header
        className="mb-8"
        initial={{ opacity: 0, y: -12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3, ease: [0.2, 0, 0, 1] }}
      >
        <h2 className="text-headline-md font-medium mb-2 flex items-center gap-3 text-md-on-surface group w-max cursor-default">
          <div className="animate-sparkles flex items-center justify-center">
            <Sparkles size={28} />
          </div>
          <span>Welcome back</span>
        </h2>

        <div className="flex items-center gap-2 text-label-md text-md-on-surface-variant bg-md-surface-container w-max px-4 py-2 rounded-full border border-md-outline-variant/30 shadow-sm">
          <motion.span
            className={`w-2 h-2 rounded-full ${(status && typeof status === 'string' && status.includes("Online")) ? "bg-green-400" : "bg-red-400"}`}
            animate={{ scale: (status && typeof status === 'string' && status.includes("Online")) ? [1, 1.3, 1] : 1 }}
            transition={{ duration: 1.5, repeat: Infinity }}
          />
          {status}
        </div>
      </motion.header>

      {/* ── Quick Action Grid ─────────────────────────────────────── */}
      <motion.div
        className="grid grid-cols-2 md:grid-cols-3 gap-4 mb-8"
        variants={staggerContainer}
        initial="hidden"
        animate="show"
      >
        {quickActions.map((action) => (
          <motion.div key={action.title} variants={fadeUp} className="h-full">
            <MdRipple
              onClick={() => {
                if (action.page === "CompatibilityModal") {
                  window.dispatchEvent(new CustomEvent("open-compatibility-modal"));
                } else {
                  setCurrentPage(action.page);
                }
              }}
              className={`w-full h-full flex flex-col items-start text-left bg-gradient-to-br ${action.color} border ${action.borderColor} rounded-[1.5rem] p-5 group shadow-sm`}
              color="currentColor"
            >
              <div className={`w-11 h-11 rounded-full bg-md-surface-container flex items-center justify-center mb-4 group-hover:scale-110 transition-transform duration-200 shadow-sm border ${action.borderColor} flex-shrink-0`}>
                <span className={action.iconColor}>{action.icon}</span>
              </div>
              <h4 className="text-title-sm font-medium mb-1 text-md-on-surface flex-shrink-0">{action.title}</h4>
              <p className="text-label-md text-md-on-surface-variant min-h-[2.5rem] flex-1">{action.desc}</p>
            </MdRipple>
          </motion.div>
        ))}
      </motion.div>

      {/* ── Hero Section ──────────────────────────────────────────── */}
      <motion.div
        className="flex-1 bg-md-surface-low/60 backdrop-blur-lg rounded-[2rem] border border-md-outline-variant/20 flex flex-col items-center justify-center text-center p-10 shadow-sm"
        initial={{ opacity: 0, scale: 0.96 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ duration: 0.4, delay: 0.15, ease: [0.2, 0, 0, 1] }}
      >
        <div className="relative group cursor-default mb-8">
          {/* Tonal glow */}
          <div className="absolute inset-0 bg-gradient-to-br from-md-primary/30 to-md-tertiary/20 blur-3xl rounded-full scale-50 group-hover:scale-150 transition-transform duration-700" />
          <motion.div
            className="w-24 h-24 bg-md-surface-container rounded-full flex items-center justify-center shadow-md border border-md-outline-variant/20 relative z-10"
            whileHover={{ rotate: 15, scale: 1.1 }}
            transition={{ type: "spring", stiffness: 400, damping: 20 }}
          >
            <img
              src={logoImg}
              className="w-16 h-16 drop-shadow-md"
              alt="Axora"
            />
          </motion.div>
        </div>

        <h3 className="text-headline-md font-medium mb-3 text-md-on-surface">Ready to work</h3>
        <p className="text-body-lg text-md-on-surface-variant max-w-md mb-8">
          Axora is deeply integrated with your Windows 11 native filesystem, hardware scanners,
          and Android 16 ecosystem. All processing happens locally — no cloud, no tracking.
        </p>

        <motion.div whileHover={{ scale: 1.04 }} whileTap={{ scale: 0.97 }}>
          <MdRipple
            onClick={() => setCurrentPage("Universal Engine")}
            className="flex items-center gap-2 bg-md-primary text-md-on-primary px-8 py-4 rounded-full font-semibold shadow-md"
            color="var(--md-sys-color-on-primary)"
          >
            Get Started
            <motion.div
              animate={{ x: [0, 4, 0] }}
              transition={{ duration: 1.2, repeat: Infinity, repeatDelay: 2 }}
            >
              <ArrowRight size={18} />
            </motion.div>
          </MdRipple>
        </motion.div>
      </motion.div>
    </div>
  );
}
