import { useMemo } from "react";
import { PieceGlyph } from "@/components/chess/pieces";
import { piecesFromFen } from "@/lib/game/board";

const FILES = ["a", "b", "c", "d", "e", "f", "g", "h"];

/** Lightweight static board preview for lists (no WebGL, no interaction). */
export function MiniBoard({ fen }: { fen: string }) {
  const pieces = useMemo(() => new Map(piecesFromFen(fen).map((p) => [p.square, p])), [fen]);
  return (
    <div
      className="grid aspect-square w-full grid-cols-8 overflow-hidden rounded-md border border-border"
      aria-hidden
    >
      {Array.from({ length: 64 }, (_, i) => {
        const file = i % 8;
        const rank = 8 - Math.floor(i / 8);
        const p = pieces.get(`${FILES[file]}${rank}`);
        const light = (file + rank) % 2 === 1;
        return (
          <div
            key={i}
            className={`grid place-items-center ${light ? "bg-board-light" : "bg-board-dark"}`}
          >
            {p && (
              <PieceGlyph
                type={p.type}
                color={p.color}
                className="text-[clamp(10px,2.2vw,22px)] leading-none"
              />
            )}
          </div>
        );
      })}
    </div>
  );
}
