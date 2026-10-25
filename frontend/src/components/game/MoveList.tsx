import { ChevronFirst, ChevronLast, ChevronLeft, ChevronRight } from "lucide-react";
import { motion } from "motion/react";
import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";
import type { MoveHistoryItem } from "@/lib/api/types";
import { cn } from "@/lib/utils";

interface MoveListProps {
  moves: MoveHistoryItem[];
  /** null = live position, -1 = starting position, otherwise index into `moves`. */
  viewPly: number | null;
  onSelect: (ply: number | null) => void;
}

export function MoveList({ moves, viewPly, onSelect }: MoveListProps) {
  const activeRef = useRef<HTMLButtonElement>(null);
  const endRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (viewPly === null) endRef.current?.scrollIntoView({ block: "nearest" });
    else activeRef.current?.scrollIntoView({ block: "nearest" });
  }, [moves.length, viewPly]);

  const rows: { no: number; white?: MoveHistoryItem; black?: MoveHistoryItem }[] = [];
  moves.forEach((m, i) => {
    const r = Math.floor(i / 2);
    rows[r] ??= { no: r + 1 };
    if (i % 2 === 0) rows[r]!.white = m;
    else rows[r]!.black = m;
  });

  const current = viewPly === null ? moves.length - 1 : viewPly;
  const goto = (idx: number) => {
    const clamped = Math.min(moves.length - 1, Math.max(-1, idx));
    onSelect(clamped === moves.length - 1 ? null : clamped);
  };

  // Arrow keys walk the game while focus is anywhere on the page (except inside inputs).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement | null;
      if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA")) return;
      if (e.key === "ArrowLeft") goto(current - 1);
      else if (e.key === "ArrowRight") goto(current + 1);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-center justify-between border-b border-border px-3 py-2">
        <span className="label-mono">Moves</span>
        <span className="font-mono text-[11px] text-muted-foreground">{moves.length} ply</span>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-1 py-1">
        {moves.length === 0 && (
          <p className="px-3 py-6 text-center text-xs text-muted-foreground">No moves yet.</p>
        )}
        {rows.map((row) => (
          <div key={row.no} className="grid grid-cols-[2.2rem_1fr_1fr] items-center gap-1 px-2">
            <span className="font-mono text-[11px] text-muted-foreground">{row.no}.</span>
            {[row.white, row.black].map((m, i) =>
              m ? (
                <motion.button
                  key={i}
                  ref={current === m.ply - 1 ? activeRef : undefined}
                  // The newest move eases in; replaying older ones is instant.
                  initial={m.ply === moves.length ? { opacity: 0, x: -6 } : false}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.2 }}
                  onClick={() => goto(m.ply - 1)}
                  className={cn(
                    "min-h-10 rounded px-2 py-1 text-left font-mono text-[13px] transition-colors hover:bg-secondary sm:min-h-0",
                    current === m.ply - 1 && "bg-primary/20 text-foreground",
                  )}
                >
                  {m.san}
                </motion.button>
              ) : (
                <span key={i} />
              ),
            )}
          </div>
        ))}
        <div ref={endRef} />
      </div>

      <div className="pb-safe grid grid-cols-4 gap-1 border-t border-border p-2">
        <Button variant="ghost" size="sm" aria-label="First move" onClick={() => goto(-1)}>
          <ChevronFirst className="h-4 w-4" />
        </Button>
        <Button
          variant="ghost"
          size="sm"
          aria-label="Previous move"
          onClick={() => goto(current - 1)}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <Button variant="ghost" size="sm" aria-label="Next move" onClick={() => goto(current + 1)}>
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button variant="ghost" size="sm" aria-label="Latest move" onClick={() => onSelect(null)}>
          <ChevronLast className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}

/**
 * Phone/tablet move strip: one horizontally scrolling line under the board, so the latest
 * moves stay visible without scrolling the page down to the full list.
 */
export function MoveRibbon({
  moves,
  viewPly,
  onSelect,
}: {
  moves: MoveHistoryItem[];
  viewPly: number | null;
  onSelect: (ply: number | null) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const current = viewPly === null ? moves.length - 1 : viewPly;

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    // Keep the active move in view by scrolling the strip itself, never the page.
    const active = el.querySelector<HTMLElement>("[data-active='true']");
    if (active)
      el.scrollTo({
        left: active.offsetLeft - el.clientWidth / 2 + active.clientWidth / 2,
        behavior: "smooth",
      });
  }, [moves.length, current]);

  if (moves.length === 0) return null;
  return (
    <div
      ref={ref}
      className="flex gap-1 overflow-x-auto overscroll-x-contain rounded-lg border border-border bg-surface px-1.5 py-1.5 [scrollbar-width:none] xl:hidden"
      aria-label="Move history"
    >
      {moves.map((m) => (
        <button
          key={m.ply}
          type="button"
          data-active={current === m.ply - 1}
          onClick={() => onSelect(m.ply === moves.length ? null : m.ply - 1)}
          className={cn(
            "flex min-h-9 shrink-0 items-center gap-1 rounded-md px-2 font-mono text-[13px] transition-colors",
            current === m.ply - 1
              ? "bg-primary/20 text-foreground"
              : "text-muted-foreground active:bg-secondary",
          )}
        >
          {m.ply % 2 === 1 && <span className="text-[10px] opacity-60">{(m.ply + 1) / 2}.</span>}
          {m.san}
        </button>
      ))}
    </div>
  );
}
