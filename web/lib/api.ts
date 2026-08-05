import type {
  AiMetadataSuggestion,
  AuthConfig,
  BookDetail,
  BookInput,
  BookSummary,
  CheckinResult,
  CheckoutResult,
  CurrentUser,
  DemoAccount,
  LoanSummary,
  MemberSummary,
  PagedResult,
  TokenResponse,
} from "./types";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

/**
 * Held in a module variable rather than read from storage on every call, so this
 * module stays unaware of where the session is kept. lib/auth.tsx owns that.
 */
let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

/**
 * Carries the server's ProblemDetails message so the UI can show what actually went
 * wrong - "every copy is on loan" rather than "request failed".
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

interface RequestOptions extends RequestInit {
  /** Sent as an Idempotency-Key header. */
  idempotencyKey?: string;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { idempotencyKey, ...init } = options;

  const headers = new Headers(init.headers);
  if (init.body) headers.set("Content-Type", "application/json");
  if (idempotencyKey) headers.set("Idempotency-Key", idempotencyKey);
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);

  let response: Response;
  try {
    response = await fetch(`${BASE_URL}${path}`, { ...init, headers });
  } catch {
    // A network-level failure, not an HTTP error. Worth distinguishing: on a free
    // hosting tier this is usually the backend still waking up.
    throw new ApiError(
      "Could not reach the library service. If it is hosted on a free tier it may be starting up - try again in a moment.",
      0,
    );
  }

  if (response.status === 204) return undefined as T;

  const text = await response.text();
  const payload = text ? JSON.parse(text) : null;

  if (!response.ok) {
    // 401 and 403 are different problems and deserve different words. "Sign in" is
    // useless advice to someone who is already signed in and simply lacks the role.
    const fallback =
      response.status === 401
        ? "Your session has expired. Please sign in again."
        : response.status === 403
          ? "Your role does not allow this action."
          : `Request failed (${response.status}).`;

    throw new ApiError(payload?.detail ?? payload?.title ?? fallback, response.status);
  }

  return payload as T;
}

export interface SearchParams {
  query?: string;
  category?: string;
  availableOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export const api = {
  searchBooks(params: SearchParams = {}): Promise<PagedResult<BookSummary>> {
    const search = new URLSearchParams();
    if (params.query) search.set("query", params.query);
    if (params.category) search.set("category", params.category);
    if (params.availableOnly) search.set("availableOnly", "true");
    if (params.page) search.set("page", String(params.page));
    if (params.pageSize) search.set("pageSize", String(params.pageSize));

    const qs = search.toString();
    return request(`/api/books${qs ? `?${qs}` : ""}`);
  },

  getBook(id: string): Promise<BookDetail> {
    return request(`/api/books/${id}`);
  },

  getCategories(): Promise<string[]> {
    return request("/api/books/categories");
  },

  createBook(input: BookInput): Promise<BookDetail> {
    return request("/api/books", { method: "POST", body: JSON.stringify(input) });
  },

  updateBook(id: string, input: BookInput): Promise<BookDetail> {
    return request(`/api/books/${id}`, { method: "PUT", body: JSON.stringify(input) });
  },

  deleteBook(id: string): Promise<void> {
    return request(`/api/books/${id}`, { method: "DELETE" });
  },

  addCopy(id: string, barcode?: string): Promise<BookDetail> {
    return request(`/api/books/${id}/copies`, {
      method: "POST",
      body: JSON.stringify({ barcode: barcode ?? null }),
    });
  },

  /**
   * The key is generated here, in the browser, and reused if the caller retries.
   * That is the point: a request the client never saw the answer to can be repeated
   * safely, because the server recognises the key rather than the request.
   */
  checkout(bookId: string, memberId: string, idempotencyKey: string): Promise<CheckoutResult> {
    return request("/api/loans/checkout", {
      method: "POST",
      body: JSON.stringify({ bookId, memberId }),
      idempotencyKey,
    });
  },

  checkin(bookCopyId: string): Promise<CheckinResult> {
    return request("/api/loans/checkin", {
      method: "POST",
      body: JSON.stringify({ bookCopyId }),
    });
  },

  getOpenLoans(): Promise<LoanSummary[]> {
    return request("/api/loans/open");
  },

  /** What the signed-in member is holding. The server reads the member from the token. */
  getMyLoans(): Promise<LoanSummary[]> {
    return request("/api/loans/mine");
  },

  getMembers(): Promise<MemberSummary[]> {
    return request("/api/members");
  },

  getAiStatus(): Promise<{ enabled: boolean }> {
    return request("/api/ai/status");
  },

  enrichMetadata(title: string, author: string): Promise<AiMetadataSuggestion> {
    return request("/api/ai/enrich-metadata", {
      method: "POST",
      body: JSON.stringify({ title, author }),
    });
  },

  // ---- Authentication ----

  getAuthConfig(): Promise<AuthConfig> {
    return request("/api/auth/config");
  },

  getCurrentUser(): Promise<CurrentUser> {
    return request("/api/auth/me");
  },

  /** Accounts offered by the local sign-in. Absent when a real provider is configured. */
  getDemoAccounts(): Promise<DemoAccount[]> {
    return request("/api/auth/accounts");
  },

  issueDemoToken(accountId: string): Promise<TokenResponse> {
    return request("/api/auth/token", {
      method: "POST",
      body: JSON.stringify({ accountId }),
    });
  },
};
