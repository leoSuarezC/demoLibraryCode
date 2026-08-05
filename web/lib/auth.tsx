"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import { api, setAccessToken } from "./api";
import type { CurrentUser, DemoAccount } from "./types";

/**
 * Where the token lives.
 *
 * sessionStorage rather than localStorage: the session ends with the tab, which is
 * the right default for a shared desk terminal. A production deployment against a
 * real provider would keep the access token in memory and the refresh token in an
 * HttpOnly cookie instead — the seam for that is this module and nothing else.
 */
const TOKEN_KEY = "athenaeum.token";

export const ROLE = {
  member: "Member",
  librarian: "Librarian",
  admin: "Admin",
} as const;

interface AuthState {
  user: CurrentUser | null;
  loading: boolean;
  signIn: (accountId: string) => Promise<void>;
  signOut: () => void;
  /** True while a session is established. */
  isSignedIn: boolean;
  /** May catalogue, lend and take returns. */
  isStaff: boolean;
  /** May remove titles outright. */
  isAdmin: boolean;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const stored = sessionStorage.getItem(TOKEN_KEY);
    setAccessToken(stored);

    // Always a promise, even when there is no token: it keeps the restore on one path
    // and avoids a synchronous setState in the effect body, which React Compiler's
    // lint rejects for causing cascading renders.
    //
    // A stored token may have expired while the tab was closed, and only the API can
    // say. That costs one request on load, which is the right price for not showing a
    // signed-in shell to someone whose session is gone.
    const restore = stored ? api.getCurrentUser() : Promise.resolve(null);

    restore
      .then(setUser)
      .catch(() => {
        sessionStorage.removeItem(TOKEN_KEY);
        setAccessToken(null);
      })
      .finally(() => setLoading(false));
  }, []);

  const signIn = useCallback(async (accountId: string) => {
    const { accessToken } = await api.issueDemoToken(accountId);

    sessionStorage.setItem(TOKEN_KEY, accessToken);
    setAccessToken(accessToken);

    // Read the identity back from the API rather than decoding the token here. The
    // server's view of who you are is the one that governs every later request.
    setUser(await api.getCurrentUser());
  }, []);

  const signOut = useCallback(() => {
    sessionStorage.removeItem(TOKEN_KEY);
    setAccessToken(null);
    setUser(null);
  }, []);

  const value = useMemo<AuthState>(() => {
    const roles = user?.roles ?? [];
    return {
      user,
      loading,
      signIn,
      signOut,
      isSignedIn: user !== null,
      isStaff: roles.includes(ROLE.librarian) || roles.includes(ROLE.admin),
      isAdmin: roles.includes(ROLE.admin),
    };
  }, [user, loading, signIn, signOut]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside an AuthProvider.");
  return context;
}

export type { DemoAccount };
