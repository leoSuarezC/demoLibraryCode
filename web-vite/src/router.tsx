import { createBrowserRouter } from "react-router-dom";
import { AppShell } from "@/components/AppShell";
import CataloguePage from "@/pages/CataloguePage";
import SignInPage from "@/pages/SignInPage";
import BookDetailPage from "@/pages/BookDetailPage";
import BookFormPage from "@/pages/BookFormPage";
import LoansPage from "@/pages/LoansPage";
import MyLoansPage from "@/pages/MyLoansPage";

// AppShell is the layout route: it renders the header/nav and an <Outlet /> for the
// active page, and redirects to /sign-in when there is no session. React Router ranks
// the static /books/new above the dynamic /books/:id, so their order here is moot.
export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { path: "/", element: <CataloguePage /> },
      { path: "/sign-in", element: <SignInPage /> },
      { path: "/books/new", element: <BookFormPage /> },
      { path: "/books/:id", element: <BookDetailPage /> },
      { path: "/loans", element: <LoansPage /> },
      { path: "/my-loans", element: <MyLoansPage /> },
    ],
  },
]);
