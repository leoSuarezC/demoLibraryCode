import { Link, useNavigate, useParams } from "react-router-dom";
import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { BookDetail, MemberSummary } from "@/lib/types";
import { AvailabilityBadge, CopyStatusBadge, OverduePill } from "@/components/Badges";
import { ErrorNotice, Spinner, SuccessNotice } from "@/components/Feedback";

export default function BookDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { isStaff, isAdmin } = useAuth();

  const [book, setBook] = useState<BookDetail | null>(null);
  const [members, setMembers] = useState<MemberSummary[]>([]);
  const [memberId, setMemberId] = useState("");

  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // Refresh after an action. Called from event handlers, never from an effect.
  const reload = useCallback(async () => {
    try {
      setBook(await api.getBook(id));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not load this book.");
    }
  }, [id]);

  useEffect(() => {
    api
      .getBook(id)
      .then(setBook)
      .catch((e) =>
        setError(e instanceof ApiError ? e.message : "Could not load this book."),
      )
      .finally(() => setLoading(false));

    // The roster is staff-only. A borrower viewing this page simply does not ask for
    // it, rather than asking and swallowing a 403.
    if (isStaff) {
      api
        .getMembers()
        .then((m) => {
          setMembers(m);
          if (m.length > 0) setMemberId(m[0].id);
        })
        .catch(() => setMembers([]));
    }
  }, [id, isStaff]);

  /*
   * One key per intended borrowing, held until that borrowing succeeds.
   *
   * This is what makes the retry safe: if the response is lost and the librarian
   * clicks again, the same key goes back to the server, which recognises it and
   * returns the original loan instead of lending a second copy. A new key is minted
   * only once a check-out has actually completed.
   */
  const idempotencyKey = useRef<string>(crypto.randomUUID());

  const checkout = async () => {
    if (!memberId) return;
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const result = await api.checkout(id, memberId, idempotencyKey.current);

      setNotice(
        result.wasAlreadyProcessed
          ? `Already checked out on this request — ${result.loan.barcode} is with ${result.loan.memberName}. No second copy was lent.`
          : `${result.loan.barcode} checked out to ${result.loan.memberName}, due ${formatDate(result.loan.dueAtUtc)}.`,
      );

      idempotencyKey.current = crypto.randomUUID();
      await reload();
    } catch (e) {
      // The key is deliberately kept, so retrying this same action reuses it.
      setError(e instanceof ApiError ? e.message : "Check-out failed.");
    } finally {
      setBusy(false);
    }
  };

  const checkin = async (copyId: string) => {
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const result = await api.checkin(copyId);
      setNotice(
        result.feeCharged > 0
          ? `${result.loan.barcode} returned — ${result.feeCharged.toFixed(2)} overdue fee due.`
          : `${result.loan.barcode} returned. Nothing to pay.`,
      );
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Check-in failed.");
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.deleteBook(id);
      navigate("/");
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not remove this title.");
      setBusy(false);
    }
  };

  const addCopy = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.addCopy(id);
      setNotice("A new copy was registered.");
      await reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not add a copy.");
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return <div className="surface h-64 animate-pulse rounded-xl" />;
  }

  if (!book) {
    return <ErrorNotice message={error ?? "This book could not be found."} />;
  }

  return (
    <div className="space-y-6">
      <Link to="/" className="text-sm muted hover:underline">
        ← Back to the catalogue
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-serif text-3xl font-semibold tracking-tight">{book.title}</h1>
          <p className="mt-1 text-lg muted">{book.author}</p>
        </div>
        <div className="flex gap-2">
          {isStaff && (
            <Link to={`/books/new?edit=${book.id}`} className="btn-secondary">
              Edit
            </Link>
          )}
          {/* Removing a title destroys its loan history; administrators only. */}
          {isAdmin && (
            <button className="btn-secondary" onClick={remove} disabled={busy}>
              Remove
            </button>
          )}
        </div>
      </div>

      {error && <ErrorNotice message={error} />}
      {notice && <SuccessNotice message={notice} />}

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {book.synopsis && (
            <section className="surface rounded-xl p-5">
              <h2 className="text-sm font-semibold uppercase tracking-wide muted">Synopsis</h2>
              <p className="mt-2 leading-relaxed">{book.synopsis}</p>
            </section>
          )}

          <section className="surface rounded-xl p-5">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold uppercase tracking-wide muted">
                Copies
              </h2>
              {isStaff && (
                <button
                  className="btn-secondary !px-3 !py-1 text-xs"
                  onClick={addCopy}
                  disabled={busy}
                >
                  Add a copy
                </button>
              )}
            </div>

            {book.copies.length === 0 ? (
              <p className="mt-4 text-sm muted">
                No physical copies are registered for this title yet.
              </p>
            ) : (
              <ul className="mt-4 space-y-2">
                {book.copies.map((copy) => (
                  <li
                    key={copy.id}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-lg px-3 py-2.5"
                    style={{ border: "1px solid var(--border)" }}
                  >
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-sm">{copy.barcode}</span>
                        <CopyStatusBadge status={copy.status} />
                        {copy.currentLoan?.isOverdue && (
                          <OverduePill
                            days={copy.currentLoan.daysOverdue}
                            fee={copy.currentLoan.overdueFee}
                          />
                        )}
                      </div>
                      {copy.currentLoan && (
                        <p className="mt-1 text-xs muted">
                          {copy.currentLoan.memberName} · due{" "}
                          {formatDate(copy.currentLoan.dueAtUtc)}
                        </p>
                      )}
                    </div>

                    {isStaff && copy.status === "OnLoan" && (
                      <button
                        className="btn-secondary !px-3 !py-1 text-xs"
                        onClick={() => checkin(copy.id)}
                        disabled={busy}
                      >
                        Check in
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        <div className="space-y-6">
          <section className="surface rounded-xl p-5">
            <h2 className="text-sm font-semibold uppercase tracking-wide muted">
              {isStaff ? "Lend a copy" : "Availability"}
            </h2>

            <div className="mt-3">
              <AvailabilityBadge
                available={book.availableCopies}
                total={book.totalCopies}
              />
            </div>

            {isStaff ? (
              <>
                <label htmlFor="member" className="mt-4 block text-sm font-medium">
                  Member
                </label>
                <select
                  id="member"
                  className="field mt-1"
                  value={memberId}
                  onChange={(e) => setMemberId(e.target.value)}
                  disabled={members.length === 0}
                >
                  {members.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.fullName} ({m.openLoans} out)
                    </option>
                  ))}
                </select>

                <button
                  className="btn-primary mt-4 w-full"
                  onClick={checkout}
                  disabled={busy || book.availableCopies === 0 || !memberId}
                >
                  {busy ? <Spinner label="Working…" /> : "Check out"}
                </button>

                {book.availableCopies === 0 && book.totalCopies > 0 && (
                  <p className="mt-2 text-xs muted">
                    Every copy is out. Check one in to lend it again.
                  </p>
                )}
              </>
            ) : (
              <p className="mt-3 text-sm muted">
                {book.availableCopies > 0
                  ? "Ask at the desk to borrow this title."
                  : "Every copy is currently out."}
              </p>
            )}
          </section>

          <section className="surface rounded-xl p-5">
            <h2 className="text-sm font-semibold uppercase tracking-wide muted">Details</h2>
            <dl className="mt-3 space-y-2 text-sm">
              <Detail label="ISBN" value={book.isbn ?? "—"} mono />
              <Detail label="Subject" value={book.category ?? "—"} />
              <Detail label="First published" value={book.publishedYear?.toString() ?? "—"} />
            </dl>

            {book.tags.length > 0 && (
              <div className="mt-4 flex flex-wrap gap-1.5">
                {book.tags.map((tag) => (
                  <span
                    key={tag}
                    className="rounded-full px-2 py-0.5 text-xs muted"
                    style={{ border: "1px solid var(--border)" }}
                  >
                    {tag}
                  </span>
                ))}
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}

function Detail({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="muted">{label}</dt>
      <dd className={mono ? "font-mono text-xs" : ""}>{value}</dd>
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
