import type { CopyStatus } from "@/lib/types";

/**
 * Availability is the single most-scanned value on the catalogue page, so it is
 * stated in words and colour rather than left to a count the reader has to compare.
 */
export function AvailabilityBadge({
  available,
  total,
}: {
  available: number;
  total: number;
}) {
  if (total === 0) {
    return <Pill tone="neutral">No copies yet</Pill>;
  }
  if (available === 0) {
    return <Pill tone="amber">All {total} on loan</Pill>;
  }
  return (
    <Pill tone="green">
      {available} of {total} on shelf
    </Pill>
  );
}

export function CopyStatusBadge({ status }: { status: CopyStatus }) {
  const tone = status === "Available" ? "green" : status === "OnLoan" ? "amber" : "neutral";
  const label =
    status === "OnLoan" ? "On loan" : status === "Available" ? "On shelf" : status;

  return <Pill tone={tone}>{label}</Pill>;
}

export function OverduePill({ days, fee }: { days: number; fee: number }) {
  return (
    <Pill tone="red">
      {days} day{days === 1 ? "" : "s"} overdue
      {fee > 0 && ` · ${fee.toFixed(2)} due`}
    </Pill>
  );
}

type Tone = "green" | "amber" | "red" | "neutral";

const tones: Record<Tone, string> = {
  green:
    "bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-500/10 dark:text-emerald-400 dark:ring-emerald-400/20",
  amber:
    "bg-amber-50 text-amber-700 ring-amber-600/20 dark:bg-amber-500/10 dark:text-amber-400 dark:ring-amber-400/20",
  red: "bg-red-50 text-red-700 ring-red-600/20 dark:bg-red-500/10 dark:text-red-400 dark:ring-red-400/20",
  neutral:
    "bg-stone-100 text-stone-600 ring-stone-500/20 dark:bg-stone-500/10 dark:text-stone-400 dark:ring-stone-400/20",
};

export function Pill({ tone, children }: { tone: Tone; children: React.ReactNode }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${tones[tone]}`}
    >
      {children}
    </span>
  );
}
