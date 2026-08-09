import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./globals.css";

// Scaffolding placeholder. Routing, the app shell and the pages arrive in later
// phases; this only proves the Vite + React + Tailwind toolchain builds and renders.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <main className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-serif text-3xl font-semibold tracking-tight">Athenaeum</h1>
      <p className="mt-1 text-sm muted">React (Vite) client — setting up.</p>
    </main>
  </StrictMode>,
);
