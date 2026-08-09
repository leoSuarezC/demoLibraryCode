import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath, URL } from "node:url";

// A plain React SPA (no framework), built with Vite. It talks to the same
// Library.Api backend as the Next.js app in ../web, and shares its API client,
// types and styling verbatim. Run one or the other; they are interchangeable
// clients over the same API.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    // Mirrors the "@/*" -> src alias the shared files already use, so the copied
    // lib/ and components/ import paths need no rewriting.
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    port: 5173,
  },
});
