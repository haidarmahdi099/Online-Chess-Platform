import { useEffect, useRef } from "react";
import { createEngine, type Engine } from "@/lib/chess3d/engine";
import type { ChessBoardProps } from "@/components/chess/ChessBoard2D";
import { cn } from "@/lib/utils";

/** WebGL board. Same props as the old CSS board, so routes don't change. */
export function ChessBoard3D({
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
  const hostRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<Engine | null>(null);
  const clickRef = useRef(onSquareClick);
  clickRef.current = onSquareClick;

  useEffect(() => {
    const engine = createEngine(hostRef.current!);
    engine.setOnSquareClick((sq) => clickRef.current?.(sq));
    engineRef.current = engine;
    return () => {
      engine.dispose();
      engineRef.current = null;
    };
  }, []);

  useEffect(() => {
    engineRef.current?.update({
      board,
      orientation,
      selected,
      legalTargets,
      lastMove,
      checkSquare,
      perspective,
      interactive,
    });
  }, [board, orientation, selected, legalTargets, lastMove, checkSquare, perspective, interactive]);

  return (
    <div
      ref={hostRef}
      role="application"
      aria-label="Chess board. Drag to rotate, click a piece then a square to move."
      className={cn(
        "relative mx-auto aspect-square w-full max-w-[min(100%,760px,max(16rem,calc(100dvh-13rem)))]",
        className,
      )}
    />
  );
}
