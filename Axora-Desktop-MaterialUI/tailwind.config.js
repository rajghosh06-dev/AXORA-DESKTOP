/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  safelist: [
    'accent-blue',
    'accent-purple',
    'accent-green',
    'accent-red',
    'accent-orange',
  ],
  theme: {
    extend: {
      colors: {
        // ── MD3 Primary ──────────────────────────────────────────────
        'md-primary':                'var(--md-sys-color-primary)',
        'md-on-primary':             'var(--md-sys-color-on-primary)',
        'md-primary-container':      'var(--md-sys-color-primary-container)',
        'md-on-primary-container':   'var(--md-sys-color-on-primary-container)',
        'md-inverse-primary':        'var(--md-sys-color-inverse-primary)',

        // ── MD3 Secondary ─────────────────────────────────────────────
        'md-secondary':              'var(--md-sys-color-secondary)',
        'md-on-secondary':           'var(--md-sys-color-on-secondary)',
        'md-secondary-container':    'var(--md-sys-color-secondary-container)',
        'md-on-secondary-container': 'var(--md-sys-color-on-secondary-container)',

        // ── MD3 Tertiary ──────────────────────────────────────────────
        'md-tertiary':               'var(--md-sys-color-tertiary)',
        'md-on-tertiary':            'var(--md-sys-color-on-tertiary)',
        'md-tertiary-container':     'var(--md-sys-color-tertiary-container)',
        'md-on-tertiary-container':  'var(--md-sys-color-on-tertiary-container)',

        // ── MD3 Error ─────────────────────────────────────────────────
        'md-error':                  'var(--md-sys-color-error)',
        'md-on-error':               'var(--md-sys-color-on-error)',
        'md-error-container':        'var(--md-sys-color-error-container)',
        'md-on-error-container':     'var(--md-sys-color-on-error-container)',

        // ── MD3 Surface Tones (5 levels) ──────────────────────────────
        'md-surface':                'var(--md-sys-color-surface)',
        'md-surface-dim':            'var(--md-sys-color-surface-dim)',
        'md-surface-bright':         'var(--md-sys-color-surface-bright)',
        'md-surface-lowest':         'var(--md-sys-color-surface-container-lowest)',
        'md-surface-low':            'var(--md-sys-color-surface-container-low)',
        'md-surface-container':      'var(--md-sys-color-surface-container)',
        'md-surface-high':           'var(--md-sys-color-surface-container-high)',
        'md-surface-highest':        'var(--md-sys-color-surface-container-highest)',
        'md-inverse-surface':        'var(--md-sys-color-inverse-surface)',
        'md-inverse-on-surface':     'var(--md-sys-color-inverse-on-surface)',

        // ── MD3 On-Surface ────────────────────────────────────────────
        'md-on-surface':             'var(--md-sys-color-on-surface)',
        'md-on-surface-variant':     'var(--md-sys-color-on-surface-variant)',

        // ── MD3 Outline ───────────────────────────────────────────────
        'md-outline':                'var(--md-sys-color-outline)',
        'md-outline-variant':        'var(--md-sys-color-outline-variant)',

        // ── Legacy aliases (backward compatibility) ────────────────────
        'google-primary':            'var(--md-sys-color-primary)',
        'google-on-primary':         'var(--md-sys-color-on-primary)',
        'google-bg':                 'var(--md-sys-color-surface)',
        'google-surface':            'var(--md-sys-color-surface-container)',
        'google-surface-hover':      'var(--md-sys-color-surface-container-high)',
        'google-active-bg':          'var(--md-sys-color-primary-container)',
        'google-text':               'var(--md-sys-color-on-surface)',
        'google-text-secondary':     'var(--md-sys-color-on-surface-variant)',
        'google-border':             'var(--md-sys-color-outline-variant)',
      },

      fontFamily: {
        sans: ['Google Sans', 'Roboto', 'Inter', 'system-ui', 'sans-serif'],
        mono: ['Roboto Mono', 'JetBrains Mono', 'Menlo', 'monospace'],
      },

      fontSize: {
        'display-lg': ['57px', { lineHeight: '64px', letterSpacing: '-0.25px', fontWeight: '400' }],
        'headline-lg': ['32px', { lineHeight: '40px', letterSpacing: '0', fontWeight: '400' }],
        'headline-md': ['28px', { lineHeight: '36px', letterSpacing: '0', fontWeight: '400' }],
        'title-lg': ['22px', { lineHeight: '28px', letterSpacing: '0', fontWeight: '400' }],
        'title-md': ['16px', { lineHeight: '24px', letterSpacing: '0.15px', fontWeight: '500' }],
        'title-sm': ['14px', { lineHeight: '20px', letterSpacing: '0.1px', fontWeight: '500' }],
        'body-lg': ['16px', { lineHeight: '24px', letterSpacing: '0.5px', fontWeight: '400' }],
        'body-md': ['14px', { lineHeight: '20px', letterSpacing: '0.25px', fontWeight: '400' }],
        'label-lg': ['14px', { lineHeight: '20px', letterSpacing: '0.1px', fontWeight: '500' }],
        'label-md': ['12px', { lineHeight: '16px', letterSpacing: '0.5px', fontWeight: '500' }],
        'label-sm': ['11px', { lineHeight: '16px', letterSpacing: '0.5px', fontWeight: '500' }],
      },

      borderRadius: {
        // MD3 shape tokens
        'none': '0',
        'xs': '4px',
        'sm': '8px',
        'md': '12px',
        'lg': '16px',
        'xl': '20px',
        '2xl': '24px',
        '3xl': '28px',
        'full': '9999px',
      },

      // MD3 spacing — 4dp baseline grid
      spacing: {
        '0': '0',
        '1': '4px', '2': '8px', '3': '12px', '4': '16px',
        '5': '20px', '6': '24px', '7': '28px', '8': '32px',
        '9': '36px', '10': '40px', '12': '48px', '14': '56px',
        '16': '64px', '20': '80px', '24': '96px', '28': '112px',
        '32': '128px', '36': '144px', '40': '160px', '48': '192px',
        '56': '224px', '64': '256px', '72': '288px', '80': '320px',
        '96': '384px',
      },

      transitionTimingFunction: {
        'md-standard':   'cubic-bezier(0.2, 0, 0, 1)',
        'md-decelerate': 'cubic-bezier(0, 0, 0, 1)',
        'md-accelerate': 'cubic-bezier(0.3, 0, 1, 1)',
        'md-spring':     'cubic-bezier(0.34, 1.56, 0.64, 1)',
      },

      transitionDuration: {
        'md-short1': '50ms',
        'md-short2': '100ms',
        'md-short3': '150ms',
        'md-short4': '200ms',
        'md-medium1': '250ms',
        'md-medium2': '300ms',
        'md-long1': '350ms',
        'md-long2': '400ms',
      },

      animation: {
        'ripple': 'md-ripple 600ms cubic-bezier(0.4, 0, 0.2, 1) forwards',
        'fade-in': 'fadeIn 200ms cubic-bezier(0.2, 0, 0, 1)',
        'slide-up': 'slideUp 300ms cubic-bezier(0, 0, 0, 1)',
        'scale-in': 'scaleIn 200ms cubic-bezier(0.34, 1.56, 0.64, 1)',
      },

      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%': { opacity: '0', transform: 'translateY(16px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        scaleIn: {
          '0%': { opacity: '0', transform: 'scale(0.85)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
      },

      boxShadow: {
        'md-1': '0px 1px 2px rgba(0,0,0,0.3), 0px 1px 3px 1px rgba(0,0,0,0.15)',
        'md-2': '0px 1px 2px rgba(0,0,0,0.3), 0px 2px 6px 2px rgba(0,0,0,0.15)',
        'md-3': '0px 4px 8px 3px rgba(0,0,0,0.15), 0px 1px 3px rgba(0,0,0,0.3)',
        'md-4': '0px 6px 10px 4px rgba(0,0,0,0.15), 0px 2px 3px rgba(0,0,0,0.3)',
        'md-5': '0px 8px 12px 6px rgba(0,0,0,0.15), 0px 4px 4px rgba(0,0,0,0.3)',
      },
    },
  },
  plugins: [],
}
