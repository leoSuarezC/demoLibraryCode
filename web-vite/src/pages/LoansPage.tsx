import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { LoanSummary } from "@/lib/types";
import { OverduePill, Pill } from "@/components/Badges";
import { EmptyState, ErrorNotice, SuccessNotice } from "@/components/Feedback";

export default function LoansPage() {
  const [loans, setLoans] = useState<LoanSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  // Refresh after an action. Called from event handlers, never from an effect.
  const reload = useCallback(async () => {
    try {
      setLoans(await api.getOpenLoans());
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not load outstanding loans.");
    }
  }, []);

  useEffect(() => {
    api
      .getOpenLoans()
      .then(setLoans)
      .catch((e) =>
        setError(e instanceof ApiError ? e.message : "Could not load outstanding loans."),
      );
  }, []);

  const checkin = async (loan: LoanSummary) => {
    setBusyId(loan.id);
    setError(null);
    setNotice(null);
    try {
      const result = await api.checkin(loan.bookCopyId);
      setNotice(
        result.feeCharged > 0
          ? `${loan.bookTitle} returned — ${result.feeCharged.toFixed(2)} overdue fee due.`
          : `${loan.bookTitle} returned. Nothing to pay.`,
      );
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Check-in failed.");
    } finally {
      setBusyId(null);
    }
  };

  const overdue = loans?.filter((l) => l.isOverdue) ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-serif text-3xl font-semibold tracking-tight">On loan</h1>
        <p className="mt-1 text-sm muted">
          Everything currently out, soonest due first.
          {overdue.length > 0 && ` ${overdue.length} overdue.`}
        </p>
      </div>

      {error && <ErrorNotice message={error} />}
      {notice && <SuccessNotice message={notice} />}

      {loans === null && <div className="surface h-64 animate-pulse rounded-xl" />}

      {loans?.length === 0 && (
        <EmptyState title="Nothing is out." hint="Every copy is on the shelf." />
      )}

      {loans && loans.length > 0 && (
        <div className="surface overflow-hidden rounded-xl">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr style={{ borderBottom: "1px solid var(--border)" }}>
                  <Th>Title</Th>
                  <Th>Copy</Th>
                  <Th>Member</Th>
                  <Th>Due</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {loans.map((loan) => (
                  <tr key={loan.id} style={{ borderBottom: "1px solid var(--border)" }}>
                    <td className="px-4 py-3 font-medium">{loan.bookTitle}</td>
                    <td className="px-4 py-3 font-mono text-xs muted">{loan.barcode}</td>
                    <td className="px-4 py-3">{loan.memberName}</td>
                    <td className="px-4 py-3">
                      {loan.isOverdue ? (
                        <OverduePill days={loan.daysOverdue} fee={loan.overdueFee} />
                      ) : (
                        <Pill tone="neutral">{formatDate(loan.dueAtUtc)}</Pill>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        className="btn-secondary !px-3 !py-1 text-xs"
                        onClick={() => checkin(loan)}
                        disabled={busyId !== null}
                      >
                        Check in
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

function Th({ children }: { children?: React.ReactNode }) {
  return (
    <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide muted">
      {children}
    </th>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
