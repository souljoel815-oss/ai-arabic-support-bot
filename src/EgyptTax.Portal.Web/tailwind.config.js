/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{cshtml,razor,html}",
    "./Portal/**/*.{cshtml,razor,html}",
    "./Shared/**/*.{cshtml,razor,html}",
    "./wwwroot/**/*.html",
  ],
  theme: {
    extend: {
      colors: {
        "daftarx-charcoal": "#1f2024",
        "daftarx-gold": "#c89b3c",
        "daftarx-gold-soft": "#e8c87a",
        "daftarx-cream": "#f7f4ec",
        "daftarx-ink": "#0f1014",
      },
      fontFamily: {
        ar: ["Cairo", "Noto Sans Arabic", "Segoe UI", "system-ui", "sans-serif"],
        en: ["Inter", "Segoe UI", "system-ui", "sans-serif"],
      },
    },
  },
  plugins: [],
};
