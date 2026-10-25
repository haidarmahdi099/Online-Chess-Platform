import { motion } from "motion/react";
import { PieceGlyph, PIECE_VALUES } from "@/components/chess/pieces";
import { Avatar } from "@/components/common/bits";
import { formatClock } from "@/lib/chess-utils";
import type { PieceColor } from "@/lib/types";
import { cn } from "@/lib/utils";

interface PlayerCardProps {
  name: string;
  rating?: number | undefined;
  color: PieceColor;
  clockMs: number | null;
  active: boolean;
  online?: boolean | undefined;
  capturedTypes: string[];
  className?: string;
}

export function PlayerCard({
  name,
  rating,
  color,
  clockMs,
  active,
  capturedTypes,
  className,
}: PlayerCardProps) {
  const material = capturedTypes.reduce((sum, t) => sum + (PIECE_VALUES[t] ?? 0), 0);
  const low = clockMs !== null && clockMs < 20_000;

  return (
    <div
      className={cn(
        "grid grid-cols-[minmax(0,1fr)_auto] items-center gap-2 rounded-lg border border-border bg-surface px-2.5 py-2 transition-colors sm:gap-3 sm:px-3 sm:py-2.5",
        active && "border-primary/60 bg-surface-2 shadow-[0_0_24px_-10px_var(--primary)]",
        className,
      )}
    >
      <div className="flex min-w-0 items-center gap-2 sm:gap-3">
        <div className="relative">
          <Avatar name={name} className="h-8 w-8 text-[11px] sm:h-10 sm:w-10 sm:text-sm" />
          <span
            className={cn(
              "absolute -bottom-1 -right-1 h-3.5 w-3.5 rounded-full border-2 border-surface",
              color === "w" ? "bg-board-light" : "bg-board-dark",
            )}
            title={color === "w" ? "White" : "Black"}
          />
        </div>
        <div className="min-w-0">
          <div className="flex min-w-0 items-center gap-2">
            <span className="truncate text-sm font-semibold">{name}</span>
            {rating !== undefined && (
              <span className="shrink-0 font-mono text-xs text-muted-foreground">{rating}</span>
            )}
          </div>
          <div className="mt-1 flex min-h-[18px] items-center gap-0.5">
            {capturedTypes.map((t, i) => (
              <PieceGlyph
                key={`${t}-${i}`}
                type={t}
                color={color === "w" ? "b" : "w"}
                className="text-sm opacity-80 sm:text-base"
              />
            ))}
            {material > 0 && (
              <span className="ml-1 font-mono text-[11px] text-muted-foreground">+{material}</span>
            )}
          </div>
        </div>
      </div>

      <motion.div
        animate={active ? { scale: [1, 1.02, 1] } : { scale: 1 }}
        transition={{ duration: 1, repeat: active ? Infinity : 0 }}
        className={cn(
          "shrink-0 rounded-md border px-2.5 py-1.5 font-mono text-lg tabular-nums min-[400px]:text-xl sm:px-3 sm:py-2 sm:text-2xl",
          active
            ? "border-primary bg-primary/10 text-foreground"
            : "border-border bg-background text-muted-foreground",
          low && active && "border-destructive bg-destructive/15 text-destructive",
        )}
        aria-label="Clock"
      >
        {clockMs === null ? "–:––" : formatClock(clockMs)}
      </motion.div>
    </div>
  );
}
