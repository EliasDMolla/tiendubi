/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  darkMode: 'class',
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        display: ['Fredoka', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        body: ['Nunito', 'ui-sans-serif', 'system-ui', 'sans-serif']
      },
      colors: {
        darkBg: '#080A10', darkCard: '#111524', darkBorder: '#1D243B',
        accentViolet: '#6366F1', accentPurple: '#A855F7', accentEmerald: '#10B981', accentPink: '#EC4899',
        background: 'oklch(0.975 0.012 75 / <alpha-value>)',
        foreground: 'oklch(0.34 0.025 290 / <alpha-value>)',
        card: 'oklch(0.995 0.004 75 / <alpha-value>)',
        'card-foreground': 'oklch(0.34 0.025 290 / <alpha-value>)',
        popover: 'oklch(0.995 0.004 75 / <alpha-value>)',
        'popover-foreground': 'oklch(0.34 0.025 290 / <alpha-value>)',
        primary: 'oklch(0.65 0.12 273 / <alpha-value>)',
        'primary-foreground': 'oklch(0.99 0.005 80 / <alpha-value>)',
        secondary: 'oklch(0.86 0.075 145 / <alpha-value>)',
        'secondary-foreground': 'oklch(0.29 0.025 290 / <alpha-value>)',
        muted: 'oklch(0.94 0.018 76 / <alpha-value>)',
        'muted-foreground': 'oklch(0.52 0.025 290 / <alpha-value>)',
        accent: 'oklch(0.79 0.11 12 / <alpha-value>)',
        'accent-foreground': 'oklch(0.29 0.025 290 / <alpha-value>)',
        'accent-strong': 'oklch(0.6 0.18 22 / <alpha-value>)',
        sage: 'oklch(0.78 0.085 145 / <alpha-value>)',
        butter: 'oklch(0.88 0.11 85 / <alpha-value>)',
        'butter-strong': 'oklch(0.61 0.13 74 / <alpha-value>)',
        border: 'oklch(0.84 0.025 290 / <alpha-value>)',
        input: 'oklch(0.9 0.02 76 / <alpha-value>)',
        ring: 'oklch(0.65 0.12 273 / <alpha-value>)'
      },
      animation: {
        'pulse-slow': 'pulse 4s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        float: 'float 6s ease-in-out infinite'
      }
    }
  },
  plugins: []
};
