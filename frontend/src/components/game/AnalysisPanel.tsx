import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { Chess } from "chess.js";
import { useEffect, useState } from "react";
import { analysisApi } from "@/lib/api/endpoints";
import { ApiError } from "@/lib/api/http";
import type { Evaluation } from "@/lib/api/types";
import { cn } from "@/lib/utils";

/** Converts an engine score into centipawns for the bar (mate = ±10000). */
export function scoreToCp(e: Pick<Evaluation, "scoreType" | "score"> | undefined) {
  if (!e) return 0;
  if (e.scoreType === "mate") return e.score > 0 ? 10_000 : -10_000;
  return e.score;
}

export function scoreLabel(e: Pick<Evaluation, "scoreType" | "score"> | undefined) {
  if (!e) return "–";
  if (e.scoreType === "mate") return `${e.score > 0 ? "" : "-"}M${Math.abs(e.score)}`;
  const v = e.score / 100;
  return `${v > 0 ? "+" : ""}${v.toFixed(2)}`;
}

export function EvalBar({
  scoreCp,
  flipped,
  className,
}: {
  scoreCp: number;
  flipped?: boolean;
  className?: string;
}) {
  const clamped = Math.max(-800, Math.min(800, scoreCp));
  const whitePct = 50 + (clamped / 800) * 46;
  return (
    <div
      className={cn("relative w-3 overflow-hidden rounded-full bg-board-dark sm:w-4", className)}
      aria-hidden
    >
      <motion.div
        className={cn("absolute inset-x-0 bg-board-light", flipped ? "top-0" : "bottom-0")}
        animate={{ height: `${whitePct}%` }}
        transition={{ type: "spring", stiffness: 120, damping: 20 }}
      />
    </div>
  );
}

/** Turns UCI moves ("e2e4") into SAN for display, starting from `fen`. */
function pvToSan(fen: string, pv: string[]) {
  try {
    const c = new Chess(fen);
    const out: string[] = [];
    for (const uci of pv.slice(0, 8)) {
      const m = c.move({
        from: uci.slice(0, 2),
        to: uci.slice(2, 4),
        ...(uci[4] ? { promotion: uci[4] } : {}),
      });
      out.push(m.san);
    }
    return out;
  } catch {
    return pv.slice(0, 8);
  }
}

/** Evaluation for a position. Debounced so scrubbing through moves doesn't flood the engine. */
export function useEvaluation(fen: string, enabled: boolean, depth = 14) {
  const [debounced, setDebounced] = useState(fen);
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(fen), 350);
    return () => window.clearTimeout(id);
  }, [fen]);
  return useQuery({
    queryKey: ["analysis", debounced, depth],
    queryFn: ({ signal }) => analysisApi.evaluate(debounced, depth, signal),
    enabled,
    staleTime: Infinity,
    retry: (count, err) =>
      !(err instanceof ApiError && [400, 401, 429, 503].includes(err.status)) && count < 1,
  });
}

export function AnalysisPanel({ fen, enabled = true }: { fen: string; enabled?: boolean }) {
  const { data, isFetching, error } = useEvaluation(fen, enabled);
  const cp = scoreToCp(data);
  const pv = data ? pvToSan(data.fen, data.principalVariation) : [];
  const err = error instanceof ApiError ? error : null;

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-center justify-between border-b border-border px-3 py-2">
        <span className="label-mono">Engine</span>
        <span className="font-mono text-[11px] text-muted-foreground">Stockfish</span>
      </div>
      <div className="flex min-h-0 flex-1 gap-3 p-3">
        <EvalBar scoreCp={cp} className="h-full min-h-[120px]" />
        <div className="min-w-0 flex-1">
          {err ? (
            <p className="text-sm text-muted-foreground">
              {err.status === 429
                ? "Slow down — the analysis limit is 30 requests a minute."
                : err.status === 503
                  ? "The analysis engine is unavailable right now."
                  : err.message}
            </p>
          ) : (
            <>
              <div className="font-mono text-3xl tabular-nums">
                {isFetching && !data ? "…" : scoreLabel(data)}
              </div>
              <p className="label-mono mt-1">
                depth {data?.depth ?? "–"} ·{" "}
                {cp === 0 ? "equal" : cp > 0 ? "white better" : "black better"}
              </p>
              <div className="mt-4">
                <p className="label-mono">Best line</p>
                <p className="mt-1 break-words font-mono text-[13px] text-foreground/90">
                  {pv.length ? pv.join(" ") : "—"}
                </p>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
