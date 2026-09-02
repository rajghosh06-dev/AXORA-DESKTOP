import { create } from "zustand";

export type ToastVariant = "info" | "warning" | "error";

export interface Toast {
  id: string;
  variant: ToastVariant;
  message: string;
  autoDismiss?: boolean; // true for info (2s), false for warning/error
}

interface ToastState {
  toasts: Toast[];
  addToast: (toast: Omit<Toast, "id">) => string;
  removeToast: (id: string) => void;
  clearAll: () => void;
}

let idCounter = 0;

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],

  addToast: (toast) => {
    const id = `toast-${++idCounter}-${Date.now()}`;
    const newToast: Toast = { ...toast, id };
    set((state) => ({ toasts: [...state.toasts, newToast] }));

    // Auto-dismiss info toasts after 2 seconds
    if (toast.variant === "info" || toast.autoDismiss === true) {
      setTimeout(() => {
        get().removeToast(id);
      }, 2500);
    }

    return id;
  },

  removeToast: (id) => {
    set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) }));
  },

  clearAll: () => set({ toasts: [] }),
}));
