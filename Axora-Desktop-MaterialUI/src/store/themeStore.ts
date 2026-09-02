import { create } from 'zustand';
import { invoke } from '@tauri-apps/api/core';

type ThemeMode = 'light' | 'dark' | 'system';
type AccentColor = 'blue' | 'purple' | 'green' | 'red' | 'orange';

interface ThemeState {
  theme: ThemeMode;
  accent: AccentColor;
  setTheme: (theme: ThemeMode) => void;
  setAccent: (accent: AccentColor) => void;
  initializeTheme: () => void;
}

export const useThemeStore = create<ThemeState>((set, get) => ({
  theme: 'system',
  accent: 'blue',
  
  setTheme: (theme: ThemeMode) => {
    set({ theme });
    localStorage.setItem('axora-theme', theme);
    get().initializeTheme();
    invoke('update_theme_settings', { theme, accent: get().accent }).catch(console.error);
  },

  setAccent: (accent: AccentColor) => {
    set({ accent });
    localStorage.setItem('axora-accent', accent);
    get().initializeTheme();
    invoke('update_theme_settings', { theme: get().theme, accent }).catch(console.error);
  },
  
  initializeTheme: () => {
    const savedTheme = localStorage.getItem('axora-theme') as ThemeMode | null;
    const savedAccent = localStorage.getItem('axora-accent') as AccentColor | null;
    
    if (savedTheme) {
      set({ theme: savedTheme });
    }
    if (savedAccent) {
      set({ accent: savedAccent });
    }

    const { theme, accent } = get();
    const isDark = 
      theme === 'dark' || 
      (theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
      
    if (isDark) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }

    // Clean up old accent classes
    Array.from(document.documentElement.classList).forEach((cls) => {
      if (cls.startsWith('accent-')) {
        document.documentElement.classList.remove(cls);
      }
    });

    // Add new accent class
    document.documentElement.classList.add(`accent-${accent}`);
  }
}));
