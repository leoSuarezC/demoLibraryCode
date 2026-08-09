import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useEffect } from "react";
import { useAuth } from "@/lib/auth";
import { Pill } from "./Badges";

/** The only page reachable without a session. */
const PUBLIC_PATHS = ["/sign-in"];

export function AppShell() {
  const { user, loading, isSignedIn, isStaff, isAdmin, signOut } = useAuth();
  const pathname = useLocation().pathname;
  const navigate = useNavigate();

  const isPublic = PUBLIC_PATHS.includes(pathname);

  useEffect(() => {
    if (!loading && !isSignedIn && !isPublic) navigate("/sign-in", { replace: true });
  }, [loading, isSignedIn, isPublic, navigate]);

  // Navigation mirrors the API's policies. Hiding an action the caller cannot perform
  // is a courtesy, not a control: every one of these is enforced server-side, and the
  // UI would be no less safe if it showed them all.
  const navigation = [
    { href: "/", label: "Catalogue", show: true },
    { href: "/loans", label: "On loan", show: isStaff },
    { href: "/my-loans", label: "My loans", show: isSignedIn && !isStaff },
    { href: "/books/new", label: "Add a book", show: isStaff },
  ].filter((item) => item.show);

  if (loading) {
    return (
      <div className="grid min-h-screen place-items-center">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-current border-t-transparent" />
      </div>
    );
  }

  if (!isSignedIn && !isPublic) return null; // redirecting

  return (
    <>
      <header
        className="sticky top-0 z-20 backdrop-blur"
        style={{
          background: "color-mix(in srgb, var(--bg) 85%, transparent)",
          borderBottom: "1px solid var(--border)",
        }}
      >
        <div className="mx-auto flex max-w-6xl items-center gap-4 px-4 py-3">
          <Link to="/" className="flex shrink-0 items-center gap-2.5">
            <span
              className="grid h-8 w-8 place-items-center rounded-lg text-sm font-bold text-white"
              style={{ background: "var(--color-brand-600)" }}
              aria-hidden
            >
              A
            </span>
            <span className="font-serif text-lg font-semibold tracking-tight">Athenaeum</span>
          </Link>

          <nav className="flex flex-1 items-center gap-1 text-sm">
            {navigation.map((item) => (
              <Link
                key={item.href}
                to={item.href}
                aria-current={pathname === item.href ? "page" : undefined}
                className={`rounded-lg px-3 py-1.5 transition hover:bg-black/5 dark:hover:bg-white/5 ${
                  pathname === item.href ? "font-medium" : ""
                }`}
              >
                {item.label}
              </Link>
            ))}
          </nav>

          {user && (
            <div className="flex items-center gap-3">
              <div className="hidden text-right sm:block">
                <p className="text-sm leading-tight">{user.name}</p>
                <p className="text-xs muted leading-tight">{user.email}</p>
              </div>
              <Pill tone={isAdmin ? "red" : isStaff ? "amber" : "neutral"}>
                {user.roles[0] ?? "No role"}
              </Pill>
              <button
                onClick={() => {
                  signOut();
                  navigate("/sign-in", { replace: true });
                }}
                className="rounded-lg px-2 py-1 text-sm muted transition hover:bg-black/5 dark:hover:bg-white/5"
              >
                Sign out
              </button>
            </div>
          )}
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">
        <Outlet />
      </main>

      <footer className="mx-auto w-full max-w-6xl px-4 pb-10 pt-4 text-xs muted">
        Athenaeum · a small library management system
      </footer>
    </>
  );
}
