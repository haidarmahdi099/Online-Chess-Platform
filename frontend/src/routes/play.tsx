import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { motion } from "motion/react";
import { Loader2, Swords, Users, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { PageHeader, SignInPrompt } from "@/components/common/bits";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { gamesApi } from "@/lib/api/endpoints";
import { ApiError, errorMessage } from "@/lib/api/http";
import type { SeekResponse, TimeCategory, TimeControlBody } from "@/lib/api/types";
import { categoryFor, timeControlLabel } from "@/lib/chess-utils";
import { useAuth } from "@/lib/state/auth";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/play")({
  head: () => ({ meta: [{ title: "Play — Echelon Chess" }] }),
  component: Play,
});

const POOLS: { label: string; tc: TimeControlBody }[] = [
  { label: "1+0", tc: { initialSeconds: 60, incrementSeconds: 0, category: "bullet" } },
  { label: "2+1", tc: { initialSeconds: 120, incrementSeconds: 1, category: "bullet" } },
  { label: "3+0", tc: { initialSeconds: 180, incrementSeconds: 0, category: "blitz" } },
  { label: "5+3", tc: { initialSeconds: 300, incrementSeconds: 3, category: "blitz" } },
  { label: "10+0", tc: { initialSeconds: 600, incrementSeconds: 0, category: "rapid" } },
  { label: "15+10", tc: { initialSeconds: 900, incrementSeconds: 10, category: "rapid" } },
  { label: "30+0", tc: { initialSeconds: 1800, incrementSeconds: 0, category: "classical" } },
  { label: "60+0", tc: { initialSeconds: 3600, incrementSeconds: 0, category: "classical" } },
];

const CATEGORY_COLORS: Record<TimeCategory, string> = {
  bullet: "text-chart-5",
  blitz: "text-primary",
  rapid: "text-chart-2",
  classical: "text-chart-4",
};

function Play() {
  const { user, status } = useAuth();
  const navigate = useNavigate();
  const [rated, setRated] = useState(true);
  const [seek, setSeek] = useState<SeekResponse | null>(null);
  const [starting, setStarting] = useState<string | null>(null);
  const [elapsed, setElapsed] = useState(0);
  const [customMin, setCustomMin] = useState("7");
  const [customInc, setCustomInc] = useState("2");
  const sawSearching = useRef(false);
  const handledMatchId = useRef<string | null>(null);

  const enterGame = useCallback(
    (gameId: string) => void navigate({ to: "/game/$gameId", params: { gameId } }),
    [navigate],
  );

  const applySeek = useCallback(
    async (s: SeekResponse) => {
      if (s.status === "Matched" && s.gameId) {
        // A matched seek remains in history after its game ends. Only resume it
        // while its game is still waiting or live; otherwise /play would send
        // players straight back to the completed game.
        if (handledMatchId.current === s.gameId) return;
        handledMatchId.current = s.gameId;
        try {
          const game = await gamesApi.get(s.gameId);
          if (game.status === "Waiting" || game.status === "Live") {
            enterGame(s.gameId);
          } else {
            sawSearching.current = false;
            setSeek(null);
          }
        } catch {
          handledMatchId.current = null;
          sawSearching.current = false;
          setSeek(null);
        }
        return;
      }
      handledMatchId.current = null;
      if (s.status === "Searching") {
        sawSearching.current = true;
        setSeek(s);
      } else {
        if (sawSearching.current)
          toast.info("No opponent found in time. Try again or pick another pool.");
        sawSearching.current = false;
        setSeek(null);
      }
    },
    [enterGame],
  );

  // Resume a search that was started earlier (another tab, or before a reload).
  useEffect(() => {
    if (status !== "authenticated") return;
    gamesApi
      .seekStatus()
      .then(applySeek)
      .catch(() => undefined);
  }, [status, applySeek]);

  // The hub announces a match instantly (handled in the app shell); polling is the safety net.
  useEffect(() => {
    if (!seek) return;
    const poll = window.setInterval(
      () =>
        void gamesApi
          .seekStatus()
          .then(applySeek)
          .catch(() => undefined),
      3000,
    );
    const tick = window.setInterval(
      () => setElapsed(Math.floor((Date.now() - new Date(seek.createdAt).getTime()) / 1000)),
      500,
    );
    return () => {
      window.clearInterval(poll);
      window.clearInterval(tick);
    };
  }, [seek, applySeek]);

  const start = async (label: string, tc: TimeControlBody) => {
    if (!user) return;
    setStarting(label);
    try {
      applySeek(await gamesApi.seek(tc, rated));
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        // Already searching: pick that search up instead of failing.
        await gamesApi
          .seekStatus()
          .then(applySeek)
          .catch(() => toast.error(errorMessage(e)));
      } else toast.error(errorMessage(e));
    } finally {
      setStarting(null);
    }
  };

  const cancel = async () => {
    try {
      await gamesApi.cancelSeek();
    } catch (e) {
      toast.error(errorMessage(e));
    }
    sawSearching.current = false;
    setSeek(null);
  };

  const customInitial = Math.round(Number(customMin) * 60);
  const customIncrement = Math.round(Number(customInc));
  const customValid =
    customInitial >= 30 &&
    customInitial <= 10_800 &&
    customIncrement >= 0 &&
    customIncrement <= 180;

  if (status === "anonymous") {
    return (
      <div className="mx-auto max-w-[1200px] px-4 py-10 sm:px-6 sm:py-16">
        <PageHeader
          title="Play"
          subtitle="Pick a pool and we'll pair you with an opponent in your rating band."
        />
        <div className="mt-8">
          <SignInPrompt
            text="Create a free account to play rated and casual games."
            redirect="/play"
          />
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1200px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="Play"
        subtitle="Pick a pool and we'll pair you with an opponent in your rating band."
      />

      {seek ? (
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="panel mt-8 grid place-items-center gap-5 px-4 py-12 text-center sm:px-6 sm:py-14"
        >
          <div className="relative grid h-28 w-28 place-items-center" aria-hidden>
            {[0, 1, 2].map((i) => (
              <motion.span
                key={i}
                className="absolute inset-0 rounded-full border border-primary/50"
                initial={{ scale: 0.5, opacity: 0.7 }}
                animate={{ scale: 1.5, opacity: 0 }}
                transition={{ duration: 2.4, repeat: Infinity, delay: i * 0.8, ease: "easeOut" }}
              />
            ))}
            <motion.span
              className="relative grid h-16 w-16 place-items-center rounded-full bg-gradient-to-br from-primary to-rose text-3xl text-primary-foreground shadow-[var(--shadow-glow)]"
              animate={{ rotate: [0, -8, 8, 0] }}
              transition={{ duration: 3, repeat: Infinity, ease: "easeInOut" }}
            >
              ♞
            </motion.span>
          </div>
          <div>
            <p className="font-display text-2xl font-bold uppercase">Finding an opponent…</p>
            <p className="label-mono mt-2">
              {timeControlLabel(seek.initialSeconds, seek.incrementSeconds)} {seek.category} ·{" "}
              {seek.rated ? "rated" : "casual"} · {elapsed}s
            </p>
          </div>
          <Button variant="outline" className="w-full sm:w-auto" onClick={() => void cancel()}>
            <X className="mr-2 h-4 w-4" /> Cancel search
          </Button>
        </motion.div>
      ) : (
        <>
          <div className="mt-8 flex items-center gap-3 rounded-lg border border-border bg-surface px-4 py-3">
            <Switch checked={rated} onCheckedChange={setRated} id="rated" />
            <label htmlFor="rated" className="font-mono text-xs uppercase tracking-[0.14em]">
              {rated ? "Rated — affects your rating" : "Casual — no rating change"}
            </label>
          </div>

          <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {POOLS.map((p, i) => (
              <motion.button
                key={p.label}
                initial={{ opacity: 0, y: 12 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.03 }}
                whileHover={{ y: -3 }}
                whileTap={{ scale: 0.98 }}
                onClick={() => void start(p.label, p.tc)}
                disabled={starting !== null}
                className={cn(
                  "panel flex min-h-24 flex-col items-start justify-between gap-1 p-4 text-left transition-colors hover:border-primary/60 disabled:opacity-60 sm:p-5",
                  starting === p.label && "border-primary",
                )}
              >
                <span className="font-mono text-2xl font-bold">
                  {starting === p.label ? <Loader2 className="h-6 w-6 animate-spin" /> : p.label}
                </span>
                <span
                  className={cn(
                    "font-mono text-[11px] uppercase tracking-[0.14em]",
                    CATEGORY_COLORS[p.tc.category],
                  )}
                >
                  {p.tc.category}
                </span>
              </motion.button>
            ))}
          </div>

          <div className="mt-6 grid gap-4 md:grid-cols-2">
            <div className="panel p-5">
              <h2 className="flex items-center gap-2 font-display text-base font-bold uppercase">
                <Swords className="h-5 w-5 text-primary" /> Custom time control
              </h2>
              <div className="mt-4 grid grid-cols-2 gap-3">
                <label className="space-y-1.5">
                  <span className="label-mono block">Minutes</span>
                  <Input
                    inputMode="decimal"
                    value={customMin}
                    onChange={(e) => setCustomMin(e.target.value)}
                  />
                </label>
                <label className="space-y-1.5">
                  <span className="label-mono block">Increment (s)</span>
                  <Input
                    inputMode="numeric"
                    value={customInc}
                    onChange={(e) => setCustomInc(e.target.value)}
                  />
                </label>
              </div>
              <div className="mt-4 flex items-center justify-between gap-3">
                <span className="label-mono">
                  {customValid
                    ? `${timeControlLabel(customInitial, customIncrement)} · ${categoryFor(customInitial, customIncrement)}`
                    : "30s – 180min, 0 – 180s"}
                </span>
                <Button
                  disabled={!customValid || starting !== null}
                  onClick={() =>
                    void start("custom", {
                      initialSeconds: customInitial,
                      incrementSeconds: customIncrement,
                      category: categoryFor(customInitial, customIncrement),
                    })
                  }
                >
                  Find game
                </Button>
              </div>
            </div>

            <div className="panel p-5">
              <h2 className="flex items-center gap-2 font-display text-base font-bold uppercase">
                <Users className="h-5 w-5 text-primary" /> Challenge a friend
              </h2>
              <p className="mt-3 text-sm text-muted-foreground">
                Send a direct challenge with any time control. Your friend gets a notification and
                can accept right away.
              </p>
              <Button asChild variant="secondary" className="mt-4">
                <Link to="/friends">Open friends</Link>
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
