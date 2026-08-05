"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { BookSummary, PagedResult } from "@/lib/types";
import { AvailabilityBadge } from "@/components/Badges";
import { CardSkeleton, EmptyState, ErrorNotice } from "@/components/Feedback";

export default function CataloguePage() {
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("");
  const [availableOnly, setAvailableOnly] = useState(false);
  const [page, setPage] = useState(1);

  const [categories, setCategories] = useState<string[]>([]);
  const [results, setResults] = useState<PagedResult<BookSummary> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getCategories()
      .then(setCategories)
      .catch(() => setCategories([]));
  }, []);

  const load = useCallback(
    async (params: {
      query: string;
      category: string;
      availableOnly: boolean;
      page: number;
    }) => {
      setLoading(true);
      setError(null);
      try {
        setResults(
          await api.searchBooks({
            query: params.query || undefined,
            category: params.category || undefined,
            availableOnly: params.availableOnly,
            page: params.page,
            pageSize: 12,
          }),
        );
      } catch (e) {
        setError(e instanceof ApiError ? e.message : "Something went wrong.");
        setResults(null);
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  const isFirstRun = useRef(true);

  useEffect(() => {
    // Typing should not fire a request per keystroke. 300ms is short enough to feel
    // immediate and long enough to collapse a burst of typing into one query.
    const delay = isFirstRun.current ? 0 : 300;
    isFirstRun.current = false;

    const timer = setTimeout(() => load({ query, category, availableOnly, page }), delay);
    return () => clearTimeout(timer);
  }, [query, category, availableOnly, page, load]);

  // Any change to the filters invalidates the current page number.
  const onFilterChange = (apply: () => void) => {
    apply();
    setPage(1);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-serif text-3xl font-semibold tracking-tight">Catalogue</h1>
          <p className="mt-1 text-sm muted">
            Search by title, author, ISBN, subject or keyword.
          </p>
        </div>
        <Link href="/books/new" className="btn-primary">
          Add a book
        </Link>
      </div>

      <div className="surface rounded-xl p-4">
        <div className="flex flex-col gap-3 sm:flex-row">
          <div className="flex-1">
            <label htmlFor="search" className="sr-only">
              Search the catalogue
            </label>
            <input
              id="search"
              type="search"
              className="field"
              placeholder="Try &ldquo;le guin&rdquo;, &ldquo;cyberpunk&rdquo;, or an ISBN…"
              value={query}
              onChange={(e) => onFilterChange(() => setQuery(e.target.value))}
            />
          </div>

          <div className="sm:w-56">
            <label htmlFor="category" className="sr-only">
              Subject
            </label>
            <select
              id="category"
              className="field"
              value={category}
              onChange={(e) => onFilterChange(() => setCategory(e.target.value))}
            >
              <option value="">All subjects</option>
              {categories.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>

          <label className="flex select-none items-center gap-2 text-sm sm:px-1">
            <input
              type="checkbox"
              className="h-4 w-4 rounded accent-amber-700"
              checked={availableOnly}
              onChange={(e) => onFilterChange(() => setAvailableOnly(e.target.checked))}
            />
            On shelf only
          </label>
        </div>
      </div>

      {error && <ErrorNotice message={error} />}

      {loading && !results && <CardSkeleton />}

      {results && results.items.length === 0 && !loading && (
        <EmptyState
          title="Nothing matched that search."
          hint="Try a broader term, or clear the filters."
        />
      )}

      {results && results.items.length > 0 && (
        <>
          <p className="text-sm muted">
            {results.totalCount} title{results.totalCount === 1 ? "" : "s"}
            {results.totalPages > 1 &&
              ` · page ${results.page} of ${results.totalPages}`}
          </p>

          <div
            className={`grid gap-4 transition-opacity sm:grid-cols-2 lg:grid-cols-3 ${
              loading ? "opacity-50" : ""
            }`}
          >
            {results.items.map((book) => (
              <BookCard key={book.id} book={book} />
            ))}
          </div>

          {results.totalPages > 1 && (
            <div className="flex items-center justify-center gap-3 pt-2">
              <button
                className="btn-secondary"
                disabled={!results.hasPrevious || loading}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous
              </button>
              <span className="text-sm muted">
                {results.page} / {results.totalPages}
              </span>
              <button
                className="btn-secondary"
                disabled={!results.hasNext || loading}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function BookCard({ book }: { book: BookSummary }) {
  return (
    <Link
      href={`/books/${book.id}`}
      className="surface flex flex-col rounded-xl p-4 transition hover:shadow-md"
    >
      <div className="flex-1">
        <h2 className="font-serif text-lg font-semibold leading-snug tracking-tight">
          {book.title}
        </h2>
        <p className="mt-0.5 text-sm muted">{book.author}</p>

        <div className="mt-3 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs muted">
          {book.category && <span>{book.category}</span>}
          {book.category && book.publishedYear && <span aria-hidden>·</span>}
          {book.publishedYear && <span>{book.publishedYear}</span>}
        </div>
      </div>

      <div className="mt-4">
        <AvailabilityBadge available={book.availableCopies} total={book.totalCopies} />
      </div>
    </Link>
  );
}
