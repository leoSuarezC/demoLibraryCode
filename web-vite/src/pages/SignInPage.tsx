import { useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { AuthConfig, DemoAccount } from "@/lib/types";
import { Pill } from "@/components/Badges";
import { ErrorNotice, Spinner } from "@/components/Feedback";

export default function SignInPage() {
  const navigate = useNavigate();
  const { signIn, isSignedIn } = useAuth();

  const [config, setConfig] = useState<AuthConfig | null>(null);
  const [accounts, setAccounts] = useState<DemoAccount[]>([]);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getAuthConfig()
      .then(setConfig)
      .catch(() => setConfig(null));

    api
      .getDemoAccounts()
      .then(setAccounts)
      .catch(() => setAccounts([]));
  }, []);

  useEffect(() => {
    if (isSignedIn) navigate("/", { replace: true });
  }, [isSignedIn, navigate]);

  const choose = async (account: DemoAccount) => {
    setBusyId(account.id);
    setError(null);
    try {
      await signIn(account.id);
      navigate("/", { replace: true });
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Sign-in failed.");
      setBusyId(null);
    }
  };

  const staff = accounts.filter((a) => a.role !== "Member");
  const borrowers = accounts.filter((a) => a.role === "Member");

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <div>
        <h1 className="font-serif text-3xl font-semibold tracking-tight">Sign in</h1>
        <p className="mt-2 text-sm muted">
          Choose an account to see the library from that person&rsquo;s point of view.
        </p>
      </div>

      {error && <ErrorNotice message={error} />}

      {config?.mode === "oidc" && (
        <div className="surface rounded-xl p-6">
          <p className="text-sm">
            This deployment authenticates against{" "}
            <span className="font-mono text-xs">{config.authority}</span>. Sign in through
            your identity provider; the local account picker is disabled.
          </p>
        </div>
      )}

      {config?.mode === "development" && (
        <>
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide muted">Staff</h2>
            <div className="grid gap-3">
              {staff.map((account) => (
                <AccountButton
                  key={account.id}
                  account={account}
                  busy={busyId === account.id}
                  disabled={busyId !== null}
                  onSelect={choose}
                />
              ))}
            </div>
          </section>

          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide muted">
              Borrowers
            </h2>
            <p className="text-sm muted">
              A borrower can search and see their own loans, but cannot catalogue, lend,
              or read the member roster.
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              {borrowers.map((account) => (
                <AccountButton
                  key={account.id}
                  account={account}
                  busy={busyId === account.id}
                  disabled={busyId !== null}
                  onSelect={choose}
                />
              ))}
            </div>
          </section>

          <p className="rounded-lg px-4 py-3 text-xs muted" style={{ border: "1px solid var(--border)" }}>
            This account picker exists so the system can be reviewed without registering
            with an identity provider. The API is an ordinary OAuth2 resource server:
            setting <span className="font-mono">Authentication:Authority</span> to an OIDC
            issuer switches it to Auth0, Entra ID, Clerk or Keycloak and disables this
            page entirely.
          </p>
        </>
      )}

      {config === null && <div className="surface h-48 animate-pulse rounded-xl" />}
    </div>
  );
}

function AccountButton({
  account,
  busy,
  disabled,
  onSelect,
}: {
  account: DemoAccount;
  busy: boolean;
  disabled: boolean;
  onSelect: (account: DemoAccount) => void;
}) {
  const tone = account.role === "Admin" ? "red" : account.role === "Librarian" ? "amber" : "neutral";

  return (
    <button
      onClick={() => onSelect(account)}
      disabled={disabled}
      className="surface flex items-center justify-between gap-4 rounded-xl px-4 py-3 text-left
                 transition hover:shadow-md disabled:opacity-50"
    >
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{account.name}</span>
          <Pill tone={tone}>{account.role}</Pill>
        </div>
        <p className="mt-0.5 truncate text-xs muted">{account.description}</p>
      </div>
      {busy && <Spinner />}
    </button>
  );
}
