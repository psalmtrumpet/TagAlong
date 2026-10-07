/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      // TagAlong brand — same tokens as the app (app_colors.dart) and website
      colors: {
        brand: { 50: '#FDF5E4', 100: '#FBE8B8', 200: '#F7D57A', 500: '#F1B01C', 600: '#F1B01C', 700: '#D97706' },
        leaf: { 50: '#E8F5E9', 500: '#228B22', 600: '#228B22', 700: '#1B6F1B' },
        cream: '#F5F3EE',
      },
      fontFamily: {
        sans: ['Outfit', 'system-ui', 'sans-serif'],
        display: ['"Space Grotesk"', 'Outfit', 'system-ui', 'sans-serif'],
      },
    }
  },
  plugins: []
}
