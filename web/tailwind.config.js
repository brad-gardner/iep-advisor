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
          // Full error/destructive scale shaped like the slate/teal ramps.
          // Anchored so #B91C1C lands at 700 (the legacy flat `red`); 50/100
          // are light backgrounds. Not yet consumed — the raw `red-*`/`brand-red`
          // error surfaces (Notice/Badge/Button) migrate onto this scale in a
          // later phase. Verified WCAG pairs for the intended error treatment:
          //   text danger-700 (#B91C1C) on bg danger-50 (#FEF2F2) = 5.91:1 (AA body)
          //   text danger-600 (#C42A26) on bg danger-50            = 5.18:1 (AA body)
          //   text danger-700 on white                             = 6.47:1
          // border danger-200 (#F7C4C4) is a light, non-text hairline.
          danger: {
            50: '#FEF2F2',
            100: '#FCE0E0',
            200: '#F7C4C4',
            300: '#EF9A9A',
            400: '#E26A6A',
            500: '#D2413F',
            600: '#C42A26',
            700: '#B91C1C',
            800: '#8E1515',
          },
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
