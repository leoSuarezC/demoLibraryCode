/**
 * Mirrors the contracts in Library.Application.Contracts.
 *
 * Hand-written rather than generated: the surface is small, and a reviewer can read
 * these next to the C# records and see they agree. A larger API would earn its
 * generator from the OpenAPI document the backend already publishes.
 */

export type CopyStatus = "Available" | "OnLoan" | "Lost" | "Withdrawn";

export interface BookSummary {
  id: string;
  title: string;
  author: string;
  isbn: string | null;
  category: string | null;
  publishedYear: number | null;
  totalCopies: number;
  availableCopies: number;
  isAvailable: boolean;
}

export interface LoanSummary {
  id: string;
  bookCopyId: string;
  barcode: string;
  memberId: string;
  memberName: string;
  bookTitle: string;
  checkedOutAtUtc: string;
  dueAtUtc: string;
  returnedAtUtc: string | null;
  isOverdue: boolean;
  daysOverdue: number;
  overdueFee: number;
}

export interface CopyDetail {
  id: string;
  barcode: string;
  status: CopyStatus;
  acquiredAtUtc: string;
  currentLoan: LoanSummary | null;
}

export interface BookDetail {
  id: string;
  title: string;
  author: string;
  isbn: string | null;
  synopsis: string | null;
  category: string | null;
  publishedYear: number | null;
  tags: string[];
  copies: CopyDetail[];
  createdAtUtc: string;
  updatedAtUtc: string;
  totalCopies: number;
  availableCopies: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface MemberSummary {
  id: string;
  fullName: string;
  email: string;
  membershipCode: string;
  openLoans: number;
}

export interface AiMetadataSuggestion {
  synopsis: string | null;
  category: string | null;
  publishedYear: number | null;
  tags: string[];
}

export interface CheckoutResult {
  loan: LoanSummary;
  wasAlreadyProcessed: boolean;
}

export interface CheckinResult {
  loan: LoanSummary;
  feeCharged: number;
}

export interface CurrentUser {
  subjectId: string | null;
  name: string | null;
  email: string | null;
  roles: string[];
  memberId: string | null;
}

export interface DemoAccount {
  id: string;
  name: string;
  email: string;
  role: string;
  memberId: string | null;
  description: string;
}

export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  account: DemoAccount;
}

export interface AuthConfig {
  mode: "oidc" | "development";
  authority: string | null;
  audience: string;
  roles: string[];
}

export interface BookInput {
  title: string;
  author: string;
  isbn?: string | null;
  synopsis?: string | null;
  category?: string | null;
  publishedYear?: number | null;
  tags?: string[];
  initialCopies?: number;
}
