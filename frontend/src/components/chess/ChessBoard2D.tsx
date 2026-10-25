import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useEffect, useMemo, useState } from "react";
import { PieceGlyph } from "@/components/chess/pieces";
import { useTrackedPieces } from "@/lib/game/pieceTracks";
import type { BoardPiece } from "@/lib/game/board";
import type { PieceColor } from "@/lib/types";
import { cn } from "@/lib/utils";

/** True below the `sm` breakpoint. */
function useSmallScreen() {
  const [small, setSmall] = useState(
    () => typeof window !== "undefined" && window.matchMedia("(max-width: 639px)").matches,
  );
  useEffect(() => {
    const mq = window.matchMedia("(max-width: 639px)");
    const on = () => setSmall(mq.matches);
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, []);
  return small;
}

const FILES = ["a", "b", "c", "d", "e", "f", "g", "h"];
const RANKS = ["8", "7", "6", "5", "4", "3", "2", "1"];

export interface ChessBoardProps {
  board: BoardPiece[];
  orientation: PieceColor;
  selected: string | null;
  legalTargets: string[];
  lastMove: { from: string; to: string } | null;
  checkSquare: string | null;
  perspective?: boolean;
  interactive?: boolean;
  onSquareClick?: (square: string) => void;
  className?: string;
}

export function ChessBoard2D({
  board,
  orientation,
  selected,
  legalTargets,
  lastMove,
  checkSquare,
  perspective = true,
  interactive = true,
  onSquareClick,
  className,
}: ChessBoardProps) {
  const files = orientation === "w" ? FILES : [...FILES].reverse();
  const ranks = orientation === "w" ? RANKS : [...RANKS].reverse();

  const pieceMap = useMemo(() => new Map(board.map((p) => [p.square, p])), [board]);
  const tracked = useTrackedPieces(board, lastMove);
  const reduce = useReducedMotion();
  // Phones: a flat top-down board is bigger and easier to tap than a tilted one.
  const small = useSmallScreen();
  const tilt = perspective && !small;

  const coords = (square: string) => {
    const col = files.indexOf(square[0]!);
    const row = ranks.indexOf(square[1]!);
    return { col, row };
  };

  return (
    <div
      className={cn("w-full", className)}
      style={tilt ? { perspective: "1600px", perspectiveOrigin: "50% 18%" } : undefined}
    >
      <motion.div
        initial={false}
        animate={{ rotateX: tilt ? 17 : 0, scale: tilt ? 0.98 : 1 }}
        transition={{ type: "spring", stiffness: 90, damping: 18 }}
        style={{ transformStyle: "preserve-3d" }}
        className="relative mx-auto aspect-square w-full max-w-[min(100%,760px,max(16rem,calc(100dvh-13rem)))]"
      >
        {/* board slab edge */}
        <div
          className="absolute inset-0 rounded-[14px]"
          style={{
            transform: "translateZ(-18px)",
            background: "linear-gradient(180deg, oklch(0.26 0.02 264), oklch(0.13 0.02 264))",
            boxShadow: "0 60px 80px -40px oklch(0 0 0 / 0.9)",
          }}
        />
        <div className="relative grid h-full w-full select-none grid-cols-8 grid-rows-8 overflow-hidden rounded-[12px] ring-1 ring-border [-webkit-touch-callout:none]">
          {ranks.map((rank, r) =>
            files.map((file, c) => {
              const square = `${file}${rank}`;
              const dark = (r + c) % 2 === 1;
              const isTarget = legalTargets.includes(square);
              const occupied = pieceMap.has(square);
              return (
                <button
                  key={square}
                  type="button"
                  disabled={!interactive}
                  onClick={() => onSquareClick?.(square)}
                  aria-label={square}
                  className="relative flex items-center justify-center"
                  style={{
                    background: dark ? "var(--color-board-dark)" : "var(--color-board-light)",
                  }}
                >
                  {(lastMove?.from === square || lastMove?.to === square) && (
                    <motion.span
                      layoutId={`last-${lastMove.from === square ? "from" : "to"}`}
                      className="absolute inset-0 bg-primary/25"
                    />
                  )}
                  {selected === square && (
                    <span className="absolute inset-0 bg-primary/35 ring-2 ring-inset ring-primary" />
                  )}
                  {checkSquare === square && (
                    <motion.span
                      className="absolute inset-0"
                      animate={{ opacity: [0.35, 0.9, 0.35] }}
                      transition={{ duration: 1.1, repeat: Infinity }}
                      style={{
                        background:
                          "radial-gradient(circle, var(--color-destructive) 0%, transparent 72%)",
                      }}
                    />
                  )}
                  {isTarget && !occupied && (
                    <motion.span
                      initial={{ scale: 0.4, opacity: 0 }}
                      animate={{ scale: 1, opacity: 1 }}
                      className="h-[22%] w-[22%] rounded-full bg-foreground/35"
                    />
                  )}
                  {isTarget && occupied && (
                    <span className="absolute inset-[6%] rounded-full ring-[4px] ring-destructive/70" />
                  )}
                  {c === 0 && (
                    <span
                      className="pointer-events-none absolute left-[3px] top-[2px] font-mono text-[9px] opacity-45 sm:text-[10px]"
                      style={{
                        color: dark ? "var(--color-board-light)" : "var(--color-board-dark)",
                      }}
                    >
                      {rank}
                    </span>
                  )}
                  {r === 7 && (
                    <span
                      className="pointer-events-none absolute bottom-[1px] right-[3px] font-mono text-[9px] opacity-45 sm:text-[10px]"
                      style={{
                        color: dark ? "var(--color-board-light)" : "var(--color-board-dark)",
                      }}
                    >
                      {file}
                    </span>
                  )}
                </button>
              );
            }),
          )}

          {/* pieces layer — animated between squares */}
          <div className="pointer-events-none absolute inset-0">
            <AnimatePresence>
              {tracked.map((piece) => {
                const { col, row } = coords(piece.square);
                if (col < 0 || row < 0) return null;
                return (
                  <motion.div
                    key={piece.id}
                    initial={{ opacity: 0, scale: 0.7, x: `${col * 100}%`, y: `${row * 100}%` }}
                    animate={{ opacity: 1, scale: 1, x: `${col * 100}%`, y: `${row * 100}%` }}
                    exit={{ opacity: 0, scale: 0.5 }}
                    // Slide on transforms (GPU). A quick spring that settles in ~250ms, so a
                    // fast move or premove never waits on the previous animation.
                    transition={
                      reduce
                        ? { duration: 0 }
                        : { type: "spring", stiffness: 520, damping: 38, mass: 0.7 }
                    }
                    className="absolute left-0 top-0 flex h-[12.5%] w-[12.5%] items-center justify-center will-change-transform"
                  >
                    <PieceGlyph
                      type={piece.type}
                      color={piece.color}
                      className="text-[clamp(1.5rem,9vw,3.4rem)] sm:text-[clamp(1.5rem,6.2vw,3.4rem)]"
                    />
                  </motion.div>
                );
              })}
            </AnimatePresence>
          </div>
        </div>
      </motion.div>
    </div>
  );
}
