/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: { 50: '#edfcf4', 500: '#0D7A62', 600: '#0A5C4A', 700: '#08483A' },
      }
    }
  },
  plugins: []
}
