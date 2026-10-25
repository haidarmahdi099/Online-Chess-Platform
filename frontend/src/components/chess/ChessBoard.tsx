import { lazy, Suspense, useEffect, useState } from "react";
import { ChessBoard2D, type ChessBoardProps } from "@/components/chess/ChessBoard2D";
import { PieceGlyph } from "@/components/chess/pieces";
import type { PieceColor } from "@/lib/types";
import { cn } from "@/lib/utils";

const ChessBoard3D = lazy(() =>
  import("@/components/chess/ChessBoard3D").then((m) => ({ default: m.ChessBoard3D })),
);

export type { ChessBoardProps };

interface Props extends ChessBoardProps {
  /** Set when a pawn move needs a promotion choice. */
  promotion?: { from: string; to: string; color: PieceColor } | null;
  onPromote?: (piece: "q" | "r" | "b" | "n") => void;
  onCancelPromotion?: () => void;
}

function hasWebGL() {
  try {
    const c = document.createElement("canvas");
    return !!(c.getContext("webgl2") || c.getContext("webgl"));
  } catch {
    return false;
  }
}

/**
 * Board entry point used by the routes. Renders the 3D board on the client and
 * falls back to the CSS board when WebGL is unavailable. Also owns the
 * promotion picker so both renderers get it.
 */
export function ChessBoard({ promotion, onPromote, onCancelPromotion, ...board }: Props) {
  const [mode, setMode] = useState<"pending" | "3d" | "2d">("pending");
  useEffect(() => setMode(hasWebGL() ? "3d" : "2d"), []);

  // On phones the 3D board is viewed straight down: the biggest, most tappable squares.
  const [phone, setPhone] = useState(false);
  useEffect(() => {
    const mq = window.matchMedia("(max-width: 639px)");
    const on = () => setPhone(mq.matches);
    on();
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, []);
  const flat = { ...board, perspective: (board.perspective ?? true) && !phone };

  const placeholder = (
    <div
      className={cn(
        "mx-auto aspect-square w-full max-w-[min(100%,760px,max(16rem,calc(100dvh-13rem)))]",
        board.className,
      )}
    />
  );

  return (
    <div className="relative">
      {mode === "pending" && placeholder}
      {mode === "2d" && <ChessBoard2D {...flat} />}
      {mode === "3d" && (
        <Suspense fallback={placeholder}>
          <ChessBoard3D {...flat} />
        </Suspense>
      )}

      {promotion && (
        <div
          className="absolute inset-0 z-20 grid place-items-center rounded-xl bg-background/60 backdrop-blur-sm"
          onClick={onCancelPromotion}
        >
          <div
            className="panel flex flex-col items-center gap-3 px-6 py-5"
            onClick={(e) => e.stopPropagation()}
          >
            <p className="label-mono">Promote pawn to</p>
            <div className="flex gap-2">
              {(["q", "r", "b", "n"] as const).map((t) => (
                <button
                  key={t}
                  type="button"
                  onClick={() => onPromote?.(t)}
                  aria-label={`Promote to ${{ q: "queen", r: "rook", b: "bishop", n: "knight" }[t]}`}
                  className="grid h-16 w-16 place-items-center rounded-md border border-border bg-secondary transition-colors hover:bg-primary/20 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
                >
                  <PieceGlyph type={t} color={promotion.color} className="text-5xl" />
                </button>
              ))}
            </div>
            <button
              type="button"
              onClick={onCancelPromotion}
              className="text-xs text-muted-foreground underline"
            >
              Cancel
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
