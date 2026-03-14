/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          teal: {
            50: '#EDF8F5',
            100: '#C8EEE6',
            200: '#A3E4D7',
            300: '#6DD4C0',
            400: '#3BC4A9',
            500: '#1A9478',
            600: '#0F6652',
            700: '#0A4D3E',
            800: '#06342A',
          },
          amber: {
            50: '#FEF7EC',
            100: '#FCE8C0',
            200: '#F9D98A',
            300: '#F0C050',
            400: '#D4820F',
            500: '#A8620A',
            600: '#7C4808',
          },
          slate: {
            50: '#F5F7F7',
            100: '#E8ECEC',
            200: '#D1D8D8',
            300: '#A8B5B5',
            400: '#7F9292',
            500: '#5A6F6F',
            600: '#3E5252',
            700: '#2C3C3C',
            800: '#1E2A2A',
          },
          red: '#B91C1C',
        },
      },
      fontFamily: {
        serif: ['Lora', 'Georgia', 'serif'],
        sans: ['DM Sans', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        input: '4px',
        badge: '6px',
        button: '8px',
        card: '12px',
        modal: '16px',
      },
    },
  },
  plugins: [],
}
