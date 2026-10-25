import { Link, useNavigate, useRouterState } from "@tanstack/react-router";
import { AnimatePresence, motion } from "motion/react";
import { LogOut, Menu, Moon, Settings, Sun, User, Users, X, History } from "lucide-react";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { toast } from "sonner";
import { PageTransition } from "@/components/common/motion";
import { Notifications } from "@/components/layout/Notifications";
import { Button } from "@/components/ui/button";
import { hub } from "@/lib/realtime/hub";
import { useAuth } from "@/lib/state/auth";
import { useTheme } from "@/lib/state/theme";
import { cn } from "@/lib/utils";

const NAV = [
  { to: "/play", label: "Play" },
  { to: "/puzzles", label: "Puzzles" },
  { to: "/watch", label: "Watch", live: true },
  { to: "/tournaments", label: "Tournaments" },
  { to: "/leaderboard", label: "Leaderboard" },
] as const;

export function AppShell({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const [menu, setMenu] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const { theme, toggle } = useTheme();
  const { realtimeStatus, user, status, logout } = useAuth();
  const path = useRouterState({ select: (s) => s.location.pathname });
  const navigate = useNavigate();

  // Close menus on navigation.
  useEffect(() => {
    setOpen(false);
    setMenu(false);
  }, [path]);

  useEffect(() => {
    if (!menu) return;
    const onDown = (e: MouseEvent) =>
      !menuRef.current?.contains(e.target as Node) && setMenu(false);
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [menu]);

  // Matchmaking pairs you wherever you are in the app: jump straight into the game.
  useEffect(() => {
    if (status !== "authenticated") return;
    return hub.on("game:matched", (e) => {
      toast.success("Opponent found — good luck!");
      void navigate({ to: "/game/$gameId", params: { gameId: e.gameId } });
    });
  }, [status, navigate]);

  const mobileLinks = [
    ...NAV,
    ...(user
      ? ([
          { to: "/friends", label: "Friends" },
          { to: "/games", label: "My games" },
          { to: "/profile", label: "Profile" },
          { to: "/settings", label: "Settings" },
        ] as const)
      : ([] as const)),
  ];

  const dot =
    realtimeStatus === "connected"
      ? "bg-live"
      : realtimeStatus === "disconnected"
        ? "bg-muted-foreground"
        : "animate-pulse bg-primary";

  return (
    <div className="flex min-h-dvh flex-col bg-background">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-[200] focus:rounded-md focus:bg-primary focus:px-3 focus:py-2 focus:text-primary-foreground"
      >
        Skip to content
      </a>
      <header className="sticky top-0 z-50 border-b border-border/70 bg-background/80 backdrop-blur-xl">
        <div className="mx-auto grid max-w-[1800px] grid-cols-[auto_1fr_auto] items-center gap-2 px-3 py-2 sm:gap-4 sm:px-6 sm:py-3">
          <Link to="/" className="flex min-w-0 items-center gap-2">
            <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-gradient-to-br from-primary to-rose text-lg text-primary-foreground shadow-[var(--shadow-glow)]">
              ♞
            </span>
            <span className="hidden font-display text-lg font-bold tracking-[0.18em] min-[380px]:inline">
              ECHELON
            </span>
          </Link>

          <nav className="hidden items-center justify-center gap-1 lg:flex" aria-label="Main">
            {NAV.map((item) => {
              const active = path.startsWith(item.to);
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  className={cn(
                    "relative rounded-md px-3 py-2 font-mono text-xs uppercase tracking-[0.14em] transition-colors",
                    active ? "text-foreground" : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {item.label}
                  {"live" in item && item.live && (
                    <span className="ml-2 rounded-sm bg-live px-1.5 py-0.5 text-[9px] font-bold text-live-foreground">
                      LIVE
                    </span>
                  )}
                  {active && (
                    <motion.span
                      layoutId="nav-underline"
                      className="absolute inset-x-2 -bottom-0.5 h-0.5 rounded-full bg-primary"
                    />
                  )}
                </Link>
              );
            })}
          </nav>

          <div className="flex items-center justify-end gap-0.5 sm:gap-1.5">
            {status === "authenticated" && (
              <span
                className="mr-1 hidden items-center gap-2 font-mono text-[10px] uppercase tracking-[0.14em] text-muted-foreground md:flex"
                title="Realtime connection"
              >
                <span className={cn("h-1.5 w-1.5 rounded-full", dot)} />
                {realtimeStatus}
              </span>
            )}
            <Button variant="ghost" size="icon" onClick={toggle} aria-label="Toggle theme">
              {theme === "dark" ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </Button>

            {user ? (
              <>
                <Notifications />
                <div ref={menuRef} className="relative hidden sm:block">
                  <Button
                    variant="outline"
                    size="sm"
                    className="max-w-[170px] font-mono text-xs uppercase tracking-[0.14em]"
                    onClick={() => setMenu((m) => !m)}
                    aria-expanded={menu}
                  >
                    <span className="truncate">{user.username}</span>
                  </Button>
                  <AnimatePresence>
                    {menu && (
                      <motion.div
                        initial={{ opacity: 0, y: -6 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, y: -6 }}
                        className="panel absolute right-0 mt-2 w-56 overflow-hidden p-1"
                      >
                        {[
                          { to: "/profile", label: "Profile", icon: User },
                          { to: "/friends", label: "Friends", icon: Users },
                          { to: "/games", label: "My games", icon: History },
                          { to: "/settings", label: "Settings", icon: Settings },
                        ].map(({ to, label, icon: Icon }) => (
                          <Link
                            key={to}
                            to={to}
                            className="flex items-center gap-3 rounded-md px-3 py-2.5 text-sm hover:bg-secondary"
                          >
                            <Icon className="h-4 w-4 text-muted-foreground" /> {label}
                          </Link>
                        ))}
                        <button
                          type="button"
                          onClick={() => void logout().then(() => navigate({ to: "/" }))}
                          className="flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-left text-sm text-destructive hover:bg-secondary"
                        >
                          <LogOut className="h-4 w-4" /> Sign out
                        </button>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>
              </>
            ) : status === "anonymous" ? (
              <>
                <Button
                  asChild
                  variant="ghost"
                  size="sm"
                  className="hidden font-mono text-xs uppercase tracking-[0.14em] sm:inline-flex"
                >
                  <Link to="/login" search={{ redirect: undefined }}>
                    Sign in
                  </Link>
                </Button>
                <Button
                  asChild
                  size="sm"
                  className="hidden font-mono text-xs uppercase tracking-[0.14em] sm:inline-flex"
                >
                  <Link to="/register">Join free</Link>
                </Button>
              </>
            ) : null}

            <Button
              variant="ghost"
              size="icon"
              className="lg:hidden"
              onClick={() => setOpen((o) => !o)}
              aria-label="Menu"
              aria-expanded={open}
            >
              {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </Button>
          </div>
        </div>

        <AnimatePresence>
          {open && (
            <motion.nav
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: "auto", opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              className="overflow-hidden border-t border-border lg:hidden"
              aria-label="Mobile"
            >
              <motion.div
                className="flex max-h-[calc(100dvh-4rem)] flex-col overflow-y-auto p-2 pb-safe"
                initial="hidden"
                animate="show"
                variants={{ hidden: {}, show: { transition: { staggerChildren: 0.04 } } }}
              >
                {mobileLinks.map((item) => {
                  const active = path.startsWith(item.to);
                  return (
                    <motion.div
                      key={item.to}
                      variants={{ hidden: { opacity: 0, x: -10 }, show: { opacity: 1, x: 0 } }}
                    >
                      <Link
                        to={item.to}
                        className={cn(
                          "flex min-h-12 items-center rounded-md px-3 font-mono text-xs uppercase tracking-[0.14em] transition-colors active:bg-secondary",
                          active
                            ? "bg-primary/10 text-foreground"
                            : "text-muted-foreground hover:bg-secondary hover:text-foreground",
                        )}
                      >
                        {item.label}
                        {"live" in item && item.live && (
                          <span className="ml-2 rounded-sm bg-live px-1.5 py-0.5 text-[9px] font-bold text-live-foreground">
                            LIVE
                          </span>
                        )}
                      </Link>
                    </motion.div>
                  );
                })}
                {user ? (
                  <button
                    type="button"
                    onClick={() => void logout().then(() => navigate({ to: "/" }))}
                    className="flex min-h-12 items-center rounded-md px-3 text-left font-mono text-xs uppercase tracking-[0.14em] text-destructive hover:bg-secondary"
                  >
                    Sign out
                  </button>
                ) : (
                  <div className="mt-2 grid grid-cols-2 gap-2">
                    <Button asChild variant="secondary">
                      <Link to="/login" search={{ redirect: undefined }}>
                        Sign in
                      </Link>
                    </Button>
                    <Button asChild>
                      <Link to="/register">Join free</Link>
                    </Button>
                  </div>
                )}
              </motion.div>
            </motion.nav>
          )}
        </AnimatePresence>
      </header>

      <main id="main" className="flex-1">
        <PageTransition id={path}>{children}</PageTransition>
      </main>

      <footer className="border-t border-border/70 px-4 py-8 sm:px-6">
        <div className="mx-auto flex max-w-[1800px] flex-wrap items-center justify-between gap-4">
          <p className="label-mono">Echelon Chess · built for serious play</p>
          <div className="flex flex-wrap gap-4">
            {[
              { to: "/leaderboard", label: "Leaderboard" },
              { to: "/tournaments", label: "Tournaments" },
              { to: "/settings", label: "Settings" },
            ].map((l) => (
              <Link key={l.to} to={l.to} className="label-mono hover:text-foreground">
                {l.label}
              </Link>
            ))}
          </div>
        </div>
      </footer>
    </div>
  );
}
