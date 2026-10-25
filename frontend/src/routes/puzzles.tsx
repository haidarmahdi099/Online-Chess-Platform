import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { Chess, type Square } from "chess.js";
import { AnimatePresence, motion } from "motion/react";
import { CheckCircle2, Eye, Flame, Loader2, Target, XCircle } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { ChessBoard } from "@/components/chess/ChessBoard";
import { ErrorState, LoadingBlock, PageHeader, Pill, SignInPrompt } from "@/components/common/bits";
import { PuzzleHistory } from "@/components/puzzles/PuzzleHistory";
import { PuzzleLibrary } from "@/components/puzzles/PuzzleLibrary";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { puzzlesApi } from "@/lib/api/endpoints";
import { usePlayerRatings } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import type { Puzzle } from "@/lib/api/types";
import { piecesFromChess } from "@/lib/game/board";
import { playSound } from "@/lib/sound";
import { useAuth } from "@/lib/state/auth";
import { usePreferences } from "@/lib/state/preferences";
import type { PieceColor } from "@/lib/types";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/puzzles")({
  head: () => ({ meta: [{ title: "Puzzles — Echelon Chess" }] }),
  component: Puzzles,
});

type Phase = "playing" | "checking" | "solved" | "failed";

const uciParts = (uci: string) => ({
  from: uci.slice(0, 2),
  to: uci.slice(2, 4),
  promotion: uci.length > 4 ? uci.slice(4, 5) : undefined,
});

function Puzzles() {
  const { status, user } = useAuth();
  const [puzzle, setPuzzle] = useState<Puzzle | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const lastId = useRef<string | undefined>(undefined);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const p = await puzzlesApi.next(lastId.current);
      lastId.current = p.id;
      setPuzzle(p);
    } catch (e) {
      setError(errorMessage(e, "Couldn't load a puzzle."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (status === "authenticated") void load();
  }, [status, load]);

  return (
    <div className="mx-auto max-w-[1300px] px-4 py-8 sm:px-6 sm:py-12">
      <PageHeader
        title="Puzzles"
        subtitle="Find the best move. Every solved puzzle moves your puzzle rating."
      />
      <Tabs defaultValue="train" className="mt-6">
        <TabsList className="grid w-full max-w-md grid-cols-3 font-mono text-[10px] uppercase tracking-[0.04em] sm:text-xs sm:tracking-[0.1em]">
          <TabsTrigger value="train">Train</TabsTrigger>
          <TabsTrigger value="library">Library</TabsTrigger>
          <TabsTrigger value="history">My history</TabsTrigger>
        </TabsList>

        {/* Kept mounted (just hidden) so switching tabs doesn't throw away a puzzle in progress. */}
        <TabsContent value="train" forceMount className="mt-5 data-[state=inactive]:hidden">
          {status === "anonymous" ? (
            <SignInPrompt
              text="Sign in to solve puzzles and track your puzzle rating."
              redirect="/puzzles"
            />
          ) : error ? (
            <ErrorState message={error} onRetry={() => void load()} />
          ) : !puzzle ? (
            <LoadingBlock />
          ) : (
            <PuzzleBoard
              key={puzzle.id}
              puzzle={puzzle}
              loadingNext={loading}
              onNext={() => void load()}
              userId={user?.userId}
            />
          )}
        </TabsContent>

        <TabsContent value="library" className="mt-5">
          <PuzzleLibrary />
        </TabsContent>

        <TabsContent value="history" className="mt-5">
          {status === "authenticated" ? (
            <PuzzleHistory />
          ) : (
            <SignInPrompt text="Sign in to see your puzzle attempts." redirect="/puzzles" />
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function PuzzleBoard({
  puzzle,
  onNext,
  loadingNext,
  userId,
}: {
  puzzle: Puzzle;
  onNext: () => void;
  loadingNext: boolean;
  userId: string | undefined;
}) {
  const queryClient = useQueryClient();
  const { prefs } = usePreferences();
  const initialLast = puzzle.initialMove ? uciParts(puzzle.initialMove) : null;

  const [fen, setFen] = useState(puzzle.fen);
  const [lastMove, setLastMove] = useState<{ from: string; to: string } | null>(
    initialLast ? { from: initialLast.from, to: initialLast.to } : null,
  );
  const [userMoves, setUserMoves] = useState<string[]>([]);
  const [phase, setPhase] = useState<Phase>("playing");
  const [message, setMessage] = useState("Your move.");
  const [solution, setSolution] = useState<string[] | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [promotion, setPromotion] = useState<{
    from: string;
    to: string;
    color: PieceColor;
  } | null>(null);
  const [showing, setShowing] = useState(false);
  const timers = useRef<number[]>([]);

  useEffect(() => () => timers.current.forEach((t) => window.clearTimeout(t)), []);

  const chess = useMemo(() => new Chess(fen), [fen]);
  const playerColor = useMemo(() => new Chess(puzzle.fen).turn() as PieceColor, [puzzle.fen]);
  const board = useMemo(() => piecesFromChess(chess), [chess]);
  const kingSquare = chess.inCheck()
    ? (board.find((p) => p.type === "k" && p.color === chess.turn())?.square ?? null)
    : null;
  const canMove = phase === "playing" && !showing && chess.turn() === playerColor;
  const legalTargets = useMemo(
    () =>
      selected && canMove
        ? chess.moves({ square: selected as Square, verbose: true }).map((m) => m.to as string)
        : [],
    [selected, canMove, chess],
  );

  const sound = (san: string) =>
    prefs.sounds &&
    playSound(
      san.includes("+") || san.includes("#") ? "check" : san.includes("x") ? "capture" : "move",
      prefs.volume,
    );

  const submit = async (from: string, to: string, promo?: string) => {
    const c = new Chess(fen);
    let move;
    try {
      move = c.move({ from, to, ...(promo ? { promotion: promo } : {}) });
    } catch {
      return;
    }
    const uci = `${from}${to}${promo ?? ""}`;
    const moves = [...userMoves, uci];
    setUserMoves(moves);
    setFen(c.fen());
    setLastMove({ from, to });
    setSelected(null);
    setPhase("checking");
    sound(move.san);

    try {
      const res = await puzzlesApi.step(puzzle.id, moves);
      if (res.status === "continue" && res.opponentReply) {
        const reply = uciParts(res.opponentReply);
        setMessage("Good — keep going.");
        timers.current.push(
          window.setTimeout(() => {
            const next = new Chess(c.fen());
            try {
              const m = next.move({
                from: reply.from,
                to: reply.to,
                ...(reply.promotion ? { promotion: reply.promotion } : {}),
              });
              sound(m.san);
            } catch {
              /* the server is authoritative; a mismatch just stops the animation */
            }
            setFen(next.fen());
            setLastMove({ from: reply.from, to: reply.to });
            setPhase("playing");
          }, 450),
        );
      } else if (res.status === "solved") {
        setPhase("solved");
        setMessage(res.message || "Solved!");
        refreshStats();
      } else {
        setPhase("failed");
        setSolution(res.solution);
        setMessage(res.message || "That's not the move.");
        refreshStats();
      }
    } catch (e) {
      // Roll the board back so the player can try again.
      setFen(fen);
      setUserMoves(userMoves);
      setPhase("playing");
      toast.error(errorMessage(e));
    }
  };

  function refreshStats() {
    [800, 3500].forEach((ms) =>
      window.setTimeout(() => {
        void queryClient.invalidateQueries({ queryKey: ["puzzle-stats"] });
        void queryClient.invalidateQueries({ queryKey: ["ratings"] });
      }, ms),
    );
  }

  const onSquareClick = (square: string) => {
    if (!canMove) return;
    const piece = board.find((p) => p.square === square);
    if (selected) {
      const options = chess
        .moves({ square: selected as Square, verbose: true })
        .filter((m) => m.to === square);
      if (options.length) {
        if (options.some((m) => m.promotion))
          return setPromotion({ from: selected, to: square, color: playerColor });
        return void submit(selected, square);
      }
    }
    setSelected(piece && piece.color === playerColor ? square : null);
  };

  // Replays the stored solution on the board from the starting position.
  const showSolution = () => {
    if (!solution?.length || showing) return;
    setShowing(true);
    const c = new Chess(puzzle.fen);
    setFen(puzzle.fen);
    solution.forEach((uci, i) => {
      timers.current.push(
        window.setTimeout(
          () => {
            const p = uciParts(uci);
            try {
              c.move({
                from: p.from,
                to: p.to,
                ...(p.promotion ? { promotion: p.promotion } : {}),
              });
              setFen(c.fen());
              setLastMove({ from: p.from, to: p.to });
            } catch {
              /* ignore a line that doesn't fit the position */
            }
            if (i === solution.length - 1) setShowing(false);
          },
          700 * (i + 1),
        ),
      );
    });
  };

  const ratings = usePlayerRatings(userId);
  const stats = useQuery({ queryKey: ["puzzle-stats"], queryFn: puzzlesApi.myStats });
  const done = phase === "solved" || phase === "failed";

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
      <div className="panel p-3 sm:p-4">
        <div className="relative">
          <ChessBoard
            board={board}
            orientation={playerColor}
            selected={selected}
            legalTargets={legalTargets}
            lastMove={lastMove}
            checkSquare={kingSquare}
            perspective={prefs.perspective}
            interactive={canMove}
            onSquareClick={onSquareClick}
            promotion={promotion}
            onPromote={(p) => {
              const pr = promotion;
              setPromotion(null);
              if (pr) void submit(pr.from, pr.to, p);
            }}
            onCancelPromotion={() => {
              setPromotion(null);
              setSelected(null);
            }}
          />
        </div>
      </div>

      <div className="space-y-4">
        <div className="panel p-5">
          <div className="flex items-center justify-between">
            <span className="label-mono">Puzzle rating</span>
            <span className="font-mono text-2xl font-bold tabular-nums">{puzzle.rating}</span>
          </div>
          <div className="mt-3 flex flex-wrap gap-1.5">
            {puzzle.themes.map((t) => (
              <Pill key={t}>{t}</Pill>
            ))}
          </div>
          <p className="label-mono mt-4">{playerColor === "w" ? "White" : "Black"} to move</p>

          <AnimatePresence mode="wait">
            <motion.div
              key={phase + message}
              initial={{ opacity: 0, y: 6 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
              className={cn(
                "mt-3 flex items-center gap-3 rounded-lg border px-4 py-3 text-sm font-semibold",
                phase === "solved" && "border-live/50 bg-live/10 text-live",
                phase === "failed" && "border-destructive/50 bg-destructive/10 text-destructive",
                (phase === "playing" || phase === "checking") && "border-border bg-secondary",
              )}
              role="status"
            >
              {phase === "solved" ? (
                <CheckCircle2 className="h-5 w-5 shrink-0" />
              ) : phase === "failed" ? (
                <XCircle className="h-5 w-5 shrink-0" />
              ) : phase === "checking" ? (
                <Loader2 className="h-5 w-5 shrink-0 animate-spin" />
              ) : (
                <Target className="h-5 w-5 shrink-0 text-primary" />
              )}
              {message}
            </motion.div>
          </AnimatePresence>

          <div className="mt-4 grid gap-2">
            {phase === "failed" && solution && solution.length > 0 && (
              <Button variant="secondary" onClick={showSolution} disabled={showing}>
                <Eye className="mr-2 h-4 w-4" /> Show solution
              </Button>
            )}
            <Button
              onClick={onNext}
              disabled={loadingNext || (!done && phase !== "playing")}
              variant={done ? "default" : "secondary"}
            >
              {loadingNext ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              {done ? "Next puzzle" : "Skip puzzle"}
            </Button>
          </div>
        </div>

        <div className="panel p-5">
          <h2 className="font-display text-base font-bold uppercase">Your training</h2>
          <div className="mt-3 grid grid-cols-2 gap-3">
            {[
              { l: "Rating", v: ratings.data?.puzzle.rating ?? "–" },
              { l: "Solved", v: stats.data?.distinctPuzzlesSolved ?? "–" },
              { l: "Accuracy", v: stats.data ? `${Math.round(stats.data.accuracy)}%` : "–" },
              { l: "Day streak", v: stats.data?.currentDailyStreak ?? "–", icon: Flame },
            ].map(({ l, v, icon: Icon }) => (
              <div key={l} className="rounded-lg bg-secondary px-3 py-3">
                <p className="label-mono flex items-center gap-1">
                  {Icon && <Icon className="h-3 w-3 text-primary" />}
                  {l}
                </p>
                <p className="mt-1 font-mono text-xl font-bold tabular-nums">{v}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
