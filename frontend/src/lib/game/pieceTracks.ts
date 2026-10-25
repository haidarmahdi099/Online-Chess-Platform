import { useRef } from "react";
import type { BoardPiece } from "@/lib/game/board";

export interface TrackedPiece extends BoardPiece {
  /** Stable for the life of the piece, so it can slide instead of vanishing and reappearing. */
  id: string;
}

const dist = (a: string, b: string) =>
  Math.hypot(a.charCodeAt(0) - b.charCodeAt(0), a.charCodeAt(1) - b.charCodeAt(1));

/**
 * `piecesFromChess` gives every piece an id equal to its square, which changes on each move.
 * This hook keeps identities across positions so a moved piece keeps its key and animates:
 *  - a piece that did not move keeps its id;
 *  - a piece that did is matched to the nearest unmatched piece of the same kind (the move's
 *    from-square wins ties, which also handles castling and en passant);
 *  - a promotion or a brand-new position simply gets fresh ids.
 */
export function useTrackedPieces(
  board: BoardPiece[],
  lastMove: { from: string; to: string } | null,
): TrackedPiece[] {
  const prev = useRef<TrackedPiece[]>([]);
  const counter = useRef(0);
  const memo = useRef<{ board: BoardPiece[]; result: TrackedPiece[] } | null>(null);

  // Same input array → same output (render can run many times per position).
  if (memo.current && memo.current.board === board) return memo.current.result;

  const old = prev.current;
  const taken = new Set<string>();
  const result: TrackedPiece[] = new Array(board.length);
  const pending: number[] = [];

  board.forEach((p, i) => {
    const same = old.find((o) => o.square === p.square && o.type === p.type && o.color === p.color);
    if (same) {
      taken.add(same.id);
      result[i] = { ...p, id: same.id };
    } else pending.push(i);
  });

  // Pieces that landed on the move's target first, so they claim the from-square piece.
  pending.sort(
    (a, b) => Number(board[b]!.square === lastMove?.to) - Number(board[a]!.square === lastMove?.to),
  );

  for (const i of pending) {
    const p = board[i]!;
    let best: TrackedPiece | undefined;
    let bestScore = Infinity;
    for (const o of old) {
      if (taken.has(o.id) || o.type !== p.type || o.color !== p.color) continue;
      const score =
        o.square === lastMove?.from && p.square === lastMove.to ? -1 : dist(o.square, p.square);
      if (score < bestScore) {
        bestScore = score;
        best = o;
      }
    }
    // Far-away matches are really "different piece appeared" (e.g. jumping to another position).
    if (best && bestScore <= 8) {
      taken.add(best.id);
      result[i] = { ...p, id: best.id };
    } else {
      result[i] = { ...p, id: `p${counter.current++}` };
    }
  }

  prev.current = result;
  memo.current = { board, result };
  return result;
}
