"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { BookInput } from "@/lib/types";
import { ErrorNotice, Spinner, SuccessNotice } from "@/components/Feedback";

export default function Page() {
  return (
    <Suspense fallback={<div className="surface h-96 animate-pulse rounded-xl" />}>
      <BookForm />
    </Suspense>
  );
}

function BookForm() {
  const router = useRouter();
  const editId = useSearchParams().get("edit");
  const isEditing = Boolean(editId);

  const [form, setForm] = useState<BookInput>({
    title: "",
    author: "",
    isbn: "",
    synopsis: "",
    category: "",
    publishedYear: null,
    tags: [],
    initialCopies: 1,
  });

  const [aiEnabled, setAiEnabled] = useState(false);
  const [enriching, setEnriching] = useState(false);
  const [aiNotice, setAiNotice] = useState<string | null>(null);

  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(isEditing);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getAiStatus()
      .then((s) => setAiEnabled(s.enabled))
      .catch(() => setAiEnabled(false));
  }, []);

  useEffect(() => {
    if (!editId) return;
    api
      .getBook(editId)
      .then((book) =>
        setForm({
          title: book.title,
          author: book.author,
          isbn: book.isbn ?? "",
          synopsis: book.synopsis ?? "",
          category: book.category ?? "",
          publishedYear: book.publishedYear,
          tags: book.tags,
        }),
      )
      .catch((e) => setError(e instanceof ApiError ? e.message : "Could not load this book."))
      .finally(() => setLoading(false));
  }, [editId]);

  const set = <K extends keyof BookInput>(key: K, value: BookInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  /*
   * The model drafts; the librarian decides.
   *
   * The suggestion only fills the form - nothing is saved until the form is
   * submitted, and every field stays editable. That is deliberate: a model can be
   * confidently wrong about a publication year, and a catalogue is only worth having
   * if a human vouched for what is in it.
   */
  const enrich = async () => {
    if (!form.title.trim() || !form.author.trim()) {
      setError("Enter a title and an author first — those are what the suggestion is based on.");
      return;
    }

    setEnriching(true);
    setError(null);
    setAiNotice(null);

    try {
      const s = await api.enrichMetadata(form.title, form.author);

      setForm((f) => ({
        ...f,
        synopsis: s.synopsis ?? f.synopsis,
        category: s.category ?? f.category,
        publishedYear: s.publishedYear ?? f.publishedYear,
        tags: s.tags.length > 0 ? s.tags : f.tags,
      }));

      setAiNotice("Suggestion applied. Review and correct anything before saving.");
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not reach the suggestion service.");
    } finally {
      setEnriching(false);
    }
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);

    const payload: BookInput = {
      ...form,
      isbn: form.isbn?.trim() || null,
      synopsis: form.synopsis?.trim() || null,
      category: form.category?.trim() || null,
    };

    try {
      const saved = isEditing
        ? await api.updateBook(editId!, payload)
        : await api.createBook(payload);
      router.push(`/books/${saved.id}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not save this book.");
      setSaving(false);
    }
  };

  if (loading) return <div className="surface h-96 animate-pulse rounded-xl" />;

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <Link href={isEditing ? `/books/${editId}` : "/"} className="text-sm muted hover:underline">
        ← Cancel
      </Link>

      <h1 className="font-serif text-3xl font-semibold tracking-tight">
        {isEditing ? "Edit this title" : "Add a book"}
      </h1>

      {error && <ErrorNotice message={error} />}
      {aiNotice && <SuccessNotice message={aiNotice} />}

      <form onSubmit={submit} className="surface space-y-5 rounded-xl p-6">
        <Field label="Title" required>
          <input
            className="field"
            required
            value={form.title}
            onChange={(e) => set("title", e.target.value)}
          />
        </Field>

        <Field label="Author" required>
          <input
            className="field"
            required
            value={form.author}
            onChange={(e) => set("author", e.target.value)}
          />
        </Field>

        {aiEnabled && (
          <div
            className="rounded-lg p-4"
            style={{ background: "var(--color-brand-50)", border: "1px solid var(--border)" }}
          >
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-medium text-stone-900">Fill in the rest with AI</p>
                <p className="text-xs text-stone-600">
                  Drafts the synopsis, subject, year and keywords for you to review.
                </p>
              </div>
              <button
                type="button"
                className="btn-secondary shrink-0"
                onClick={enrich}
                disabled={enriching}
              >
                {enriching ? <Spinner label="Thinking…" /> : "Suggest details"}
              </button>
            </div>
          </div>
        )}

        <Field label="Synopsis">
          <textarea
            className="field min-h-28 resize-y"
            value={form.synopsis ?? ""}
            onChange={(e) => set("synopsis", e.target.value)}
          />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label="Subject">
            <input
              className="field"
              placeholder="Science Fiction"
              value={form.category ?? ""}
              onChange={(e) => set("category", e.target.value)}
            />
          </Field>

          <Field label="First published">
            <input
              className="field"
              type="number"
              min={1450}
              max={new Date().getFullYear() + 1}
              value={form.publishedYear ?? ""}
              onChange={(e) =>
                set("publishedYear", e.target.value ? Number(e.target.value) : null)
              }
            />
          </Field>
        </div>

        <Field label="ISBN" hint="Validated on save; hyphens are fine.">
          <input
            className="field font-mono text-sm"
            placeholder="978-0-441-01359-3"
            value={form.isbn ?? ""}
            onChange={(e) => set("isbn", e.target.value)}
          />
        </Field>

        <Field label="Keywords" hint="Comma separated.">
          <input
            className="field"
            value={form.tags?.join(", ") ?? ""}
            onChange={(e) =>
              set(
                "tags",
                e.target.value
                  .split(",")
                  .map((t) => t.trim())
                  .filter(Boolean),
              )
            }
          />
        </Field>

        {!isEditing && (
          <Field label="Copies to register" hint="How many physical items are going on the shelf.">
            <input
              className="field"
              type="number"
              min={0}
              max={50}
              value={form.initialCopies ?? 1}
              onChange={(e) => set("initialCopies", Number(e.target.value))}
            />
          </Field>
        )}

        <div className="flex gap-3 pt-2">
          <button type="submit" className="btn-primary" disabled={saving}>
            {saving ? <Spinner label="Saving…" /> : isEditing ? "Save changes" : "Add to catalogue"}
          </button>
          <Link href={isEditing ? `/books/${editId}` : "/"} className="btn-secondary">
            Cancel
          </Link>
        </div>
      </form>
    </div>
  );
}

function Field({
  label,
  hint,
  required,
  children,
}: {
  label: string;
  hint?: string;
  required?: boolean;
  children: React.ReactNode;
}) {
  return (
    <label className="block">
      <span className="text-sm font-medium">
        {label}
        {required && <span className="ml-0.5 text-red-600">*</span>}
      </span>
      {hint && <span className="ml-2 text-xs muted">{hint}</span>}
      <div className="mt-1.5">{children}</div>
    </label>
  );
}
