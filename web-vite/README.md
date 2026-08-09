# web-vite — plain-React (Vite) client

A second frontend for the Athenaeum API, built as a client-side SPA with **Vite +
React 19 + React Router + TypeScript**. It is interchangeable with the Next.js app in
[`../web`](../web): same API, same DTOs, same styling and components, so the two render
identically. The difference is the framework — this one is plain React with no SSR.

## Run it

```bash
# The API must be running first (see the root README, step 2).
cp .env.example .env      # VITE_API_URL defaults to http://localhost:5080
npm install
npm run dev               # http://localhost:5173
```

`http://localhost:5173` is already allow-listed in the API's `Cors:AllowedOrigins`.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server with HMR on :5173 |
| `npm run build` | `tsc` type-check, then a production build into `dist/` |
| `npm run preview` | Serve the production build locally |
| `npm run lint` | ESLint (flat config) |

## How it is structured

Most of the code is shared verbatim with `../web`; only the routing layer is
re-expressed for React Router.

```
src/
  main.tsx           mounts <AuthProvider><RouterProvider/></AuthProvider>
  router.tsx         createBrowserRouter: AppShell layout + the six page routes
  globals.css        design tokens + component classes (identical to web/)
  lib/
    api.ts           fetch client. Identical to web/ except the base URL reads
                     import.meta.env.VITE_API_URL instead of process.env.NEXT_PUBLIC_API_URL
    types.ts         domain DTOs mirroring Library.Application.Contracts (verbatim)
    auth.tsx         token/session context and role flags (verbatim)
  components/
    AppShell.tsx     header, policy-mirrored nav, session guard; renders <Outlet/>
    Badges.tsx       status pills (verbatim)
    Feedback.tsx     notices, skeletons, spinner (verbatim)
  pages/
    CataloguePage · BookDetailPage · BookFormPage · LoansPage · MyLoansPage · SignInPage
```

### Next.js → React Router mapping

The only changes when porting the pages and shell from `../web`:

| Next.js | React Router |
|---|---|
| `next/link` `<Link href>` | `react-router-dom` `<Link to>` |
| `useRouter().push/replace` | `useNavigate()` |
| `usePathname()` | `useLocation().pathname` |
| `useParams()` | `useParams()` (same shape) |
| `useSearchParams()` | `useSearchParams()` (no Suspense needed) |
| `"use client"` directive | removed (not needed without RSC) |

Everything else — data fetching, the browser-held `Idempotency-Key` on check-out,
role-based UI, error handling — is unchanged from the Next.js client.
