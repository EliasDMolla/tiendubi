/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        display: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif']
      },
      colors: {
        darkBg: '#080A10', darkCard: '#111524', darkBorder: '#1D243B',
        accentViolet: '#6366F1', accentPurple: '#A855F7', accentEmerald: '#10B981', accentPink: '#EC4899'
      },
      animation: {
        'pulse-slow': 'pulse 4s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        float: 'float 6s ease-in-out infinite'
      }
    }
  },
  plugins: []
};
