import { Sun, Moon, Monitor } from "lucide-react";
import { useThemeStore } from "../store/themeStore";

export default function ThemeToggle() {
  const { theme, setTheme } = useThemeStore();

  return (
    <div className="bg-md-surface-container-high backdrop-blur-xl border border-md-outline-variant/30 rounded-full p-1 flex items-center shadow-sm hover:md-elevation-1 transition-shadow group">
      <button 
        onClick={() => setTheme('light')}
        className={`p-2 rounded-full transition-all ${theme === 'light' ? 'bg-md-primary-container text-md-on-primary-container scale-105 shadow-sm' : 'text-md-on-surface-variant hover:text-md-on-surface hover:bg-md-on-surface/5'}`}
        title="Light Mode"
      >
        <Sun size={18} />
      </button>
      <button 
        onClick={() => setTheme('dark')}
        className={`p-2 rounded-full transition-all ${theme === 'dark' ? 'bg-md-primary-container text-md-on-primary-container scale-105 shadow-sm' : 'text-md-on-surface-variant hover:text-md-on-surface hover:bg-md-on-surface/5'}`}
        title="Dark Mode"
      >
        <Moon size={18} />
      </button>
      <button 
        onClick={() => setTheme('system')}
        className={`p-2 rounded-full transition-all ${theme === 'system' ? 'bg-md-primary-container text-md-on-primary-container scale-105 shadow-sm' : 'text-md-on-surface-variant hover:text-md-on-surface hover:bg-md-on-surface/5'}`}
        title="System Default"
      >
        <Monitor size={18} />
      </button>
    </div>
  );
}
