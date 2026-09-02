import { create } from "zustand";

export interface QuickDropItem {
  id: string;
  type: "text" | "link" | "file";
  content: string;
  filename?: string;
  mimeType?: string;
  timestamp: number;
}

interface QuickDropStore {
  items: QuickDropItem[];
  isOpen: boolean;
  setOpen: (open: boolean) => void;
  toggleOpen: () => void;
  addItem: (item: Omit<QuickDropItem, "id" | "timestamp">) => void;
  removeItem: (id: string) => void;
  clearItems: () => void;
}

export const useQuickDropStore = create<QuickDropStore>((set) => ({
  items: [
    {
      id: "demo-1",
      type: "link",
      content: "https://axora.app/docs/security-audit",
      filename: "Axora Security Audit Doc",
      timestamp: Date.now() - 3600000,
    },
    {
      id: "demo-2",
      type: "text",
      content: "const aesGcmKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);",
      filename: "Crypto Snippet",
      timestamp: Date.now() - 1800000,
    },
  ],
  isOpen: false,
  setOpen: (open) => set({ isOpen: open }),
  toggleOpen: () => set((state) => ({ isOpen: !state.isOpen })),
  addItem: (item) =>
    set((state) => ({
      items: [
        {
          ...item,
          id: Math.random().toString(36).substring(2, 9),
          timestamp: Date.now(),
        },
        ...state.items,
      ],
      isOpen: true,
    })),
  removeItem: (id) =>
    set((state) => ({
      items: state.items.filter((i) => i.id !== id),
    })),
  clearItems: () => set({ items: [] }),
}));
