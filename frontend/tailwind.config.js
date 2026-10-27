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
          fondo: "#F8F8F0",
          bosque: "#123F36",
          pasto: "#2F7D5B",
          menta: "#E3F2DC",
          borde: "#DCE5EC"
        }
      },
      fontFamily: {
        heading: ["'Plus Jakarta Sans'", "Segoe UI", "sans-serif"],
        body: ["'Plus Jakarta Sans'", "Segoe UI", "sans-serif"]
      },
      boxShadow: {
        tarjeta: "0 8px 30px rgba(18, 63, 54, 0.08)"
      }
    },
  },
  plugins: [],
}
