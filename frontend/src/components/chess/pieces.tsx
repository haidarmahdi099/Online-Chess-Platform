import type { PieceColor } from "@/lib/types";

const GLYPHS: Record<string, string> = {
  k: "♚",
  q: "♛",
  r: "♜",
  b: "♝",
  n: "♞",
  p: "♟",
};

export const PIECE_VALUES: Record<string, number> = { p: 1, n: 3, b: 3, r: 5, q: 9 };

export function PieceGlyph({
  type,
  color,
  className = "",
}: {
  type: string;
  color: PieceColor;
  className?: string;
}) {
  const isWhite = color === "w";
  return (
    <span
      aria-hidden
      className={`select-none leading-none ${className}`}
      style={{
        color: isWhite ? "oklch(0.97 0.012 90)" : "oklch(0.22 0.02 264)",
        textShadow: isWhite
          ? "0 1px 0 oklch(0.78 0.03 90), 0 2px 1px oklch(0.6 0.04 80), 0 6px 12px oklch(0 0 0 / 0.55)"
          : "0 1px 0 oklch(0.12 0.02 264), 0 2px 1px oklch(0.08 0.02 264), 0 6px 12px oklch(0 0 0 / 0.6)",
        filter: isWhite
          ? "drop-shadow(0 4px 6px oklch(0 0 0 / 0.45))"
          : "drop-shadow(0 4px 6px oklch(0 0 0 / 0.5))",
      }}
    >
      {GLYPHS[type]}
    </span>
  );
}
