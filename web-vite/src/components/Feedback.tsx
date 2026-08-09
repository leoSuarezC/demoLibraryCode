export function ErrorNotice({ message }: { message: string }) {
  return (
    <div
      role="alert"
      className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800
                 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300"
    >
      {message}
    </div>
  );
}

export function SuccessNotice({ message }: { message: string }) {
  return (
    <div
      role="status"
      className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800
                 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300"
    >
      {message}
    </div>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="surface rounded-xl px-6 py-16 text-center">
      <p className="font-medium">{title}</p>
      {hint && <p className="mt-1 text-sm muted">{hint}</p>}
    </div>
  );
}

/** Placeholder rows that match the real card's height, so the layout does not jump. */
export function CardSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="surface h-40 animate-pulse rounded-xl" />
      ))}
    </div>
  );
}

export function Spinner({ label }: { label?: string }) {
  return (
    <span className="inline-flex items-center gap-2">
      <span
        className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-current border-t-transparent"
        aria-hidden
      />
      {label}
    </span>
  );
}
