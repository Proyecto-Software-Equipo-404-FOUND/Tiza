/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        tiza: {
          pizarra: "#12181B",
          lima: "#C8F051",
          violeta: "#6C5CE7",
          coral: "#FF6B57",
          fondo: "#F4F4EF"
        }
      },
      fontFamily: {
        heading: ["Arial", "sans-serif"],
        body: ["Calibri", "sans-serif"]
      }
    },
  },
  plugins: [],
}
