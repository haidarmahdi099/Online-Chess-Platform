import { Chess } from "chess.js";
import type { PieceColor } from "@/lib/types";

export interface BoardPiece {
  square: string;
  type: string;
  color: PieceColor;
  id: string;
}

const FILES = ["a", "b", "c", "d", "e", "f", "g", "h"];

/** Flat piece list for the board renderers (2D and 3D). */
export function piecesFromChess(chess: Chess): BoardPiece[] {
  const out: BoardPiece[] = [];
  chess.board().forEach((row, r) =>
    row.forEach((cell, c) => {
      if (!cell) return;
      const square = `${FILES[c]}${8 - r}`;
      out.push({ square, type: cell.type, color: cell.color as PieceColor, id: square });
    }),
  );
  return out;
}

export function piecesFromFen(fen: string): BoardPiece[] {
  try {
    return piecesFromChess(new Chess(fen));
  } catch {
    return [];
  }
}

export const START_FEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

/** Pieces captured so far, per side, derived from what is missing on the board. */
export function capturedFromBoard(board: BoardPiece[]) {
  const start: Record<string, number> = { p: 8, n: 2, b: 2, r: 2, q: 1 };
  const lost = (color: PieceColor) => {
    const left: Record<string, number> = { p: 0, n: 0, b: 0, r: 0, q: 0 };
    board
      .filter((p) => p.color === color && p.type !== "k")
      .forEach((p) => {
        left[p.type] = (left[p.type] ?? 0) + 1;
      });
    // promotions can make "left" exceed the start count, so clamp at zero
    return (["q", "r", "b", "n", "p"] as const).flatMap((t) =>
      Array.from({ length: Math.max(0, (start[t] ?? 0) - (left[t] ?? 0)) }, () => t as string),
    );
  };
  return { whiteLost: lost("w"), blackLost: lost("b") };
}
