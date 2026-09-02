import { Lock, Unlock, Eye, EyeOff, KeyRound, ShieldAlert, CheckCircle2, ShieldCheck } from "lucide-react";
import { open as openDialog, message } from "@tauri-apps/plugin-dialog";
import { invoke } from "@tauri-apps/api/core";
import { useState, useRef } from "react";
import { motion, AnimatePresence } from "framer-motion";

function MdTextField({
  label,
  type = "text",
  value,
  onChange,
  placeholder = "",
  leadingIcon,
  trailingIcon,
  error,
}: {
  label: string;
  type?: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  leadingIcon?: React.ReactNode;
  trailingIcon?: React.ReactNode;
  error?: string;
}) {
  const [focused, setFocused] = useState(false);
  const hasValue = value.length > 0;
  const isFloating = focused || hasValue;

  return (
    <div className="relative">
      <div
        className={`relative flex items-center bg-md-surface-low border rounded-xl transition-all duration-200 ${
          focused ? "border-md-primary ring-2 ring-md-primary/20" : error ? "border-red-500" : "border-md-outline-variant/50"
        }`}
      >
        {leadingIcon && (
          <span className={`ml-4 flex-shrink-0 transition-colors ${focused ? "text-md-primary" : "text-md-on-surface-variant"}`}>
            {leadingIcon}
          </span>
        )}
        <div className="flex-1 relative pt-4 pb-2 px-4">
          <label
            className={`absolute left-0 transition-all duration-200 pointer-events-none font-medium ${
              isFloating
                ? "text-xs top-1 " + (focused ? "text-md-primary" : error ? "text-red-500" : "text-md-on-surface-variant")
                : "text-sm top-3 text-md-on-surface-variant"
            }`}
          >
            {label}
          </label>
          <input
            type={type}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onFocus={() => setFocused(true)}
            onBlur={() => setFocused(false)}
            placeholder={isFloating ? placeholder : ""}
            className="w-full bg-transparent outline-none text-sm text-md-on-surface font-medium mt-1"
          />
        </div>
        {trailingIcon && (
          <span className="mr-3 flex-shrink-0 text-md-on-surface-variant cursor-pointer">
            {trailingIcon}
          </span>
        )}
      </div>
      {error && (
        <p className="text-xs text-red-500 mt-1 ml-4 flex items-center gap-1">
          <ShieldAlert size={12} />
          {error}
        </p>
      )}
    </div>
  );
}

function PasswordDialog({
  mode,
  onSubmit,
  onCancel,
}: {
  mode: "encrypt" | "decrypt";
  onSubmit: (password: string) => void;
  onCancel: () => void;
}) {
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = () => {
    if (!password) { setError("Password is required"); return; }
    if (mode === "encrypt") {
      if (password.length < 6) { setError("Password must be at least 6 characters"); return; }
      if (password !== confirm) { setError("Passwords do not match"); return; }
    }
    setError("");
    onSubmit(password);
  };

  return (
    <motion.div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
    >
      {/* Scrim */}
      <motion.div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm"
        onClick={onCancel}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
      />

      {/* Dialog */}
      <motion.div
        className="relative w-full max-w-sm bg-md-surface-low rounded-[2rem] p-8 md-elevation-3 border border-md-outline-variant/20 z-10"
        initial={{ opacity: 0, scale: 0.9, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.9, y: 20 }}
        transition={{ type: "spring", stiffness: 400, damping: 30 }}
      >
        <div className="flex flex-col items-center mb-6">
          <div className={`w-16 h-16 rounded-full flex items-center justify-center mb-4 ${
            mode === "encrypt" ? "bg-red-500/10 text-red-500" : "bg-green-500/10 text-green-500"
          }`}>
            {mode === "encrypt" ? <Lock size={30} /> : <Unlock size={30} />}
          </div>
          <h3 className="text-xl font-semibold text-md-on-surface">
            {mode === "encrypt" ? "Set Encryption Password" : "Enter Password"}
          </h3>
          <p className="text-sm text-md-on-surface-variant text-center mt-2">
            {mode === "encrypt"
              ? "Your password will be used to derive an AES-256 key via Argon2id. Remember it — there is no recovery."
              : "Enter the password you used when encrypting this file."}
          </p>
        </div>

        <div className="space-y-4">
          <MdTextField
            label={mode === "encrypt" ? "New Password" : "Password"}
            type={showPassword ? "text" : "password"}
            value={password}
            onChange={setPassword}
            leadingIcon={<KeyRound size={18} />}
            trailingIcon={
              <button onClick={() => setShowPassword(!showPassword)} type="button">
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            }
            error={error && !confirm ? error : undefined}
          />

          {mode === "encrypt" && (
            <MdTextField
              label="Confirm Password"
              type={showPassword ? "text" : "password"}
              value={confirm}
              onChange={setConfirm}
              leadingIcon={<ShieldAlert size={18} />}
              error={error && confirm ? error : undefined}
            />
          )}

          {error && <p className="text-xs text-red-500 flex items-center gap-1 mt-1"><ShieldAlert size={12}/>{error}</p>}
        </div>

        <div className="flex gap-3 mt-8">
          <button
            onClick={onCancel}
            className="flex-1 py-3 rounded-full font-medium text-md-on-surface-variant bg-md-surface-container hover:bg-md-surface-high transition-colors active:scale-95"
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            className={`flex-1 py-3 rounded-full font-medium text-white transition-all active:scale-95 shadow-md hover:brightness-110 ${
              mode === "encrypt" ? "bg-red-500 hover:bg-red-600" : "bg-green-600 hover:bg-green-700"
            }`}
          >
            {mode === "encrypt" ? "Encrypt" : "Decrypt"}
          </button>
        </div>
      </motion.div>
    </motion.div>
  );
}

export default function Security() {
  const [activeAction, setActiveAction] = useState<string | null>(null);
  const [dialogMode, setDialogMode] = useState<"encrypt" | "decrypt" | null>(null);
  const pendingFilePath = useRef<string | null>(null);

  const handleEncrypt = async () => {
    try {
      const selected = await openDialog({
        multiple: false,
        title: "Select File to Encrypt",
      });
      if (selected) {
        if ((selected as string).toLowerCase().endsWith(".axora")) {
          await message("This file is already secured with Axora Encryption!", {
            title: "Invalid Selection",
            kind: "warning",
          });
          return;
        }
        pendingFilePath.current = selected as string;
        setDialogMode("encrypt");
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleDecrypt = async () => {
    try {
      const selected = await openDialog({
        multiple: false,
        title: "Select File to Decrypt",
        filters: [{ name: "Axora Encrypted", extensions: ["axora"] }],
      });
      if (selected) {
        pendingFilePath.current = selected as string;
        setDialogMode("decrypt");
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handlePasswordSubmit = async (password: string) => {
    const filePath = pendingFilePath.current;
    const mode = dialogMode;
    setDialogMode(null);
    pendingFilePath.current = null;

    if (!filePath || !mode) return;

    setActiveAction(mode === "encrypt" ? "Encrypting" : "Decrypting");
    try {
      const outPath = await invoke(mode === "encrypt" ? "encrypt_file" : "decrypt_file", {
        path: filePath,
        password,
      });
      await message(
        `File successfully ${mode === "encrypt" ? "encrypted" : "decrypted"} and saved to: ${outPath}`,
        { title: "Success", kind: "info" }
      );
    } catch (e: any) {
      await message(`${mode === "encrypt" ? "Encryption" : "Decryption"} failed: ${e}`, {
        title: "Error",
        kind: "error",
      });
    }
    setActiveAction(null);
  };

  return (
    <>
      <AnimatePresence>
        {dialogMode && (
          <PasswordDialog
            mode={dialogMode}
            onSubmit={handlePasswordSubmit}
            onCancel={() => { setDialogMode(null); pendingFilePath.current = null; }}
          />
        )}
      </AnimatePresence>
      <div className="flex flex-col min-h-full animate-in fade-in duration-500 relative">
        <header className="mb-8">
          <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
            <ShieldCheck className="text-md-primary" size={28} />
            File Vault
          </h2>
          <p className="text-md-on-surface-variant text-lg">
            AES-256-GCM + Argon2id — Military Grade Encryption.
          </p>
        </header>

        <AnimatePresence>
          {activeAction && (
            <motion.div
              className="absolute inset-0 z-10 bg-md-surface/90 backdrop-blur-md flex flex-col items-center justify-center rounded-[2rem]"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
            >
              <motion.div
                initial={{ scale: 0.5, opacity: 0 }}
                animate={{ scale: 1, opacity: 1 }}
                transition={{ type: "spring", stiffness: 400, damping: 25 }}
              >
                <CheckCircle2 size={72} className="text-md-primary mb-4" />
              </motion.div>
              <h3 className="text-2xl font-medium text-md-on-surface">{activeAction} file...</h3>
              <p className="text-md-on-surface-variant mt-2">Securing your data locally.</p>
              <div className="mt-6 flex gap-1">
                {[0, 1, 2].map((i) => (
                  <motion.div
                    key={i}
                    className="w-2 h-2 rounded-full bg-md-primary"
                    animate={{ scale: [1, 1.5, 1], opacity: [0.5, 1, 0.5] }}
                    transition={{ duration: 1, repeat: Infinity, delay: i * 0.2 }}
                  />
                ))}
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 flex-1">
          {/* Encrypt Card */}
          <motion.div
            onClick={handleEncrypt}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              const file = e.dataTransfer.files[0];
              if (file) {
                const path = (file as any).path || file.name;
                if (path.toLowerCase().endsWith(".axora")) {
                  message("This file is already secured with Axora Encryption!", {
                    title: "Invalid Selection",
                    kind: "warning",
                  });
                  return;
                }
                pendingFilePath.current = path;
                setDialogMode("encrypt");
              }
            }}
            className="bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-10 flex flex-col items-center justify-center text-center cursor-pointer group shadow-sm backdrop-blur-sm overflow-hidden relative"
            whileHover={{ scale: 1.02, y: -2 }}
            whileTap={{ scale: 0.98 }}
            transition={{ type: "spring", stiffness: 400, damping: 25 }}
          >
            {/* Ripple glow */}
            <div className="absolute inset-0 bg-red-500/0 group-hover:bg-red-500/5 transition-colors duration-300 rounded-[2rem]" />
            <motion.div
              className="w-24 h-24 rounded-full bg-red-500/10 text-red-500 flex items-center justify-center mb-8 shadow-sm border border-red-500/20"
              whileHover={{ rotate: [-5, 5, 0] }}
              transition={{ duration: 0.4 }}
            >
              <Lock size={44} />
            </motion.div>
            <h3 className="text-2xl font-medium mb-3 text-md-on-surface relative z-10">Encrypt File</h3>
            <p className="text-md-on-surface-variant text-base mb-8 max-w-sm relative z-10">
              Drop any file or click to secure with Argon2id per-file salt + AES-256-GCM.
            </p>
            <button className="bg-red-500/10 text-red-500 border border-red-500/30 px-8 py-3 rounded-full font-medium hover:bg-red-500/20 transition-colors active:scale-95 shadow-sm relative z-10">
              Select File to Encrypt
            </button>
          </motion.div>

          {/* Decrypt Card */}
          <motion.div
            onClick={handleDecrypt}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              const file = e.dataTransfer.files[0];
              if (file) {
                const path = (file as any).path || file.name;
                pendingFilePath.current = path;
                setDialogMode("decrypt");
              }
            }}
            className="bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-10 flex flex-col items-center justify-center text-center cursor-pointer group shadow-sm backdrop-blur-sm overflow-hidden relative"
            whileHover={{ scale: 1.02, y: -2 }}
            whileTap={{ scale: 0.98 }}
            transition={{ type: "spring", stiffness: 400, damping: 25 }}
          >
            <div className="absolute inset-0 bg-green-500/0 group-hover:bg-green-500/5 transition-colors duration-300 rounded-[2rem]" />
            <motion.div
              className="w-24 h-24 rounded-full bg-green-500/10 text-green-500 flex items-center justify-center mb-8 shadow-sm border border-green-500/20"
              whileHover={{ rotate: [5, -5, 0] }}
              transition={{ duration: 0.4 }}
            >
              <Unlock size={44} />
            </motion.div>
            <h3 className="text-2xl font-medium mb-3 text-md-on-surface relative z-10">Decrypt File</h3>
            <p className="text-md-on-surface-variant text-base mb-8 max-w-sm relative z-10">
              Drop an .axora file or click to unlock using Argon2id salt header.
            </p>
            <button className="bg-green-500/10 text-green-500 border border-green-500/30 px-8 py-3 rounded-full font-medium hover:bg-green-500/20 transition-colors active:scale-95 shadow-sm relative z-10">
              Select .axora File
            </button>
          </motion.div>
        </div>
      </div>
    </>
  );
}
