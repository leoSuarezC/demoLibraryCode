import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "Athenaeum — Library Management",
  description: "Catalogue, lending and returns for a small library.",
};

const navigation = [
  { href: "/", label: "Catalogue" },
  { href: "/loans", label: "On loan" },
  { href: "/books/new", label: "Add a book" },
];

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="h-full">
      <body className="flex min-h-full flex-col">
        <header
          className="sticky top-0 z-20 backdrop-blur"
          style={{
            background: "color-mix(in srgb, var(--bg) 85%, transparent)",
            borderBottom: "1px solid var(--border)",
          }}
        >
          <div className="mx-auto flex max-w-6xl items-center gap-6 px-4 py-3">
            <Link href="/" className="flex items-center gap-2.5">
              <span
                className="grid h-8 w-8 place-items-center rounded-lg text-sm font-bold text-white"
                style={{ background: "var(--color-brand-600)" }}
                aria-hidden
              >
                A
              </span>
              <span className="font-serif text-lg font-semibold tracking-tight">
                Athenaeum
              </span>
            </Link>

            <nav className="flex items-center gap-1 text-sm">
              {navigation.map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  className="rounded-lg px-3 py-1.5 transition hover:bg-black/5 dark:hover:bg-white/5"
                >
                  {item.label}
                </Link>
              ))}
            </nav>
          </div>
        </header>

        <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">{children}</main>

        <footer className="mx-auto w-full max-w-6xl px-4 pb-10 pt-4 text-xs muted">
          Athenaeum · a small library management system
        </footer>
      </body>
    </html>
  );
}
