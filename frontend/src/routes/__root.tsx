import { QueryClientProvider, type QueryClient } from "@tanstack/react-query";
import {
  HeadContent,
  Link,
  Outlet,
  createRootRouteWithContext,
  useRouter,
  type ErrorComponentProps,
} from "@tanstack/react-router";
import { MotionConfig } from "motion/react";
import { AppShell } from "@/components/layout/AppShell";
import { Toaster } from "@/components/ui/sonner";
import { AuthProvider } from "@/lib/state/auth";
import { PreferencesProvider } from "@/lib/state/preferences";
import { ThemeProvider } from "@/lib/state/theme";

function NotFound() {
  return (
    <div className="flex min-h-[70vh] items-center justify-center px-4">
      <div className="max-w-md text-center">
        <p className="font-mono text-7xl font-bold text-primary">404</p>
        <h1 className="mt-4 font-display text-2xl font-bold uppercase">Page not found</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          That square is empty. The page you wanted doesn't exist or has moved.
        </p>
        <Link
          to="/"
          className="mt-6 inline-flex rounded-md bg-primary px-5 py-2.5 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          Back to the lobby
        </Link>
      </div>
    </div>
  );
}

function RouteError({ error, reset }: ErrorComponentProps) {
  const router = useRouter();
  console.error(error);
  return (
    <div className="flex min-h-[70vh] items-center justify-center px-4">
      <div className="max-w-md text-center">
        <h1 className="font-display text-2xl font-bold uppercase">This page didn't load</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Something went wrong. Try again, or head back to the lobby.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-2">
          <button
            onClick={() => {
              void router.invalidate();
              reset();
            }}
            className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
          >
            Try again
          </button>
          <Link
            to="/"
            className="rounded-md border border-input px-4 py-2 text-sm font-medium hover:bg-accent"
          >
            Go home
          </Link>
        </div>
      </div>
    </div>
  );
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  head: () => ({
    meta: [
      { title: "Echelon Chess" },
      {
        name: "description",
        content: "Play rated chess, solve puzzles, join tournaments and watch live games.",
      },
    ],
  }),
  component: Root,
  notFoundComponent: NotFound,
  errorComponent: RouteError,
});

function Root() {
  const { queryClient } = Route.useRouteContext();
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <PreferencesProvider>
          <AuthProvider>
            <HeadContent />
            {/* "user" = obey the OS "reduce motion" setting for every animation in the app. */}
            <MotionConfig reducedMotion="user">
              <AppShell>
                <Outlet />
              </AppShell>
            </MotionConfig>
            <Toaster />
          </AuthProvider>
        </PreferencesProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}
