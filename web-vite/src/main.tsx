import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router-dom";
import { AuthProvider } from "@/lib/auth";
import { router } from "./router";
import "./globals.css";

// AuthProvider wraps the router so every route sees the session. It uses no router
// hooks itself (only the API and sessionStorage), so it is free to sit outside.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  </StrictMode>,
);
