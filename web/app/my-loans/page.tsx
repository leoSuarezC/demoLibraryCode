"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { LoanSummary } from "@/lib/types";
import { OverduePill, Pill } from "@/components/Badges";
import { EmptyState, ErrorNotice } from "@/components/Feedback";

/**
 * A borrower's own shelf.
 *
 * There is no member selector here and no id in the URL: the server reads the member
 * off the token. Nothing on this page can be pointed at somebody else's loans.
 */
export default function MyLoansPage() {
  const { user } = useAuth();
  const [loans, setLoans] = useState<LoanSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getMyLoans()
      .then(setLoans)
      .catch((e) =>
        setError(e instanceof ApiError ? e.message : "Could not load your loans."),
      );
  }, []);

  const overdue = loans?.filter((l) => l.isOverdue) ?? [];
  const totalDue = overdue.reduce((sum, l) => sum + l.overdueFee, 0);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-serif text-3xl font-semibold tracking-tight">My loans</h1>
        <p className="mt-1 text-sm muted">
          {user?.name}
          {loans !== null && ` · ${loans.length} item${loans.length === 1 ? "" : "s"} out`}
          {totalDue > 0 && ` · ${totalDue.toFixed(2)} in fees`}
        </p>
      </div>

      {error && <ErrorNotice message={error} />}

      {loans === null && !error && <div className="surface h-48 animate-pulse rounded-xl" />}

      {loans?.length === 0 && (
        <EmptyState
          title="You have nothing out."
          hint="Anything you borrow will show up here with its due date."
        />
      )}

      {loans && loans.length > 0 && (
        <ul className="space-y-3">
          {loans.map((loan) => (
            <li key={loan.id} className="surface rounded-xl p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <h2 className="font-serif text-lg font-semibold tracking-tight">
                    {loan.bookTitle}
                  </h2>
                  <p className="mt-0.5 font-mono text-xs muted">{loan.barcode}</p>
                </div>
                {loan.isOverdue ? (
                  <OverduePill days={loan.daysOverdue} fee={loan.overdueFee} />
                ) : (
                  <Pill tone="green">Due {formatDate(loan.dueAtUtc)}</Pill>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      <p className="text-xs muted">
        Returns are handled at the desk — bring the book to a librarian.
      </p>
    </div>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
