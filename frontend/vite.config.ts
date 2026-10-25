import tailwindcss from "@tailwindcss/vite";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";

// Plain client-side SPA: deployable on any static host, independent of the backend.
// Port 3000 matches the gateway's default CORS origin and the Frontend:BaseUrl used in emails.
export default defineConfig({
  plugins: [tanstackRouter({ target: "react", autoCodeSplitting: true }), react(), tailwindcss()],
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  server: { port: 3000, strictPort: true },
  preview: { port: 3000 },
  build: { chunkSizeWarningLimit: 900 },
});
