import type { Metadata } from "next";
import { AppShell } from "@/components/AppShell";
import { AuthProvider } from "@/lib/auth";
import "./globals.css";

export const metadata: Metadata = {
  title: "Athenaeum — Library Management",
  description: "Catalogue, lending and returns for a small library.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="h-full">
      <body className="flex min-h-full flex-col">
        <AuthProvider>
          <AppShell>{children}</AppShell>
        </AuthProvider>
      </body>
    </html>
  );
}
