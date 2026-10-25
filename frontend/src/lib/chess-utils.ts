import type { GameDetails, RatingCategory, TimeCategory } from "@/lib/api/types";

/** Same rule the backend validates: estimated duration = initial + 40 × increment. */
export function categoryFor(initialSeconds: number, incrementSeconds: number): TimeCategory {
  const est = initialSeconds + 40 * incrementSeconds;
  if (est < 180) return "bullet";
  if (est < 600) return "blitz";
  if (est < 1800) return "rapid";
  return "classical";
}

export function timeControlLabel(initialSeconds: number, incrementSeconds: number) {
  const mins = initialSeconds / 60;
  const base = Number.isInteger(mins) ? String(mins) : mins.toFixed(1);
  return `${base}+${incrementSeconds}`;
}

export const CATEGORY_LABEL: Record<RatingCategory, string> = {
  chess: "Overall",
  bullet: "Bullet",
  blitz: "Blitz",
  rapid: "Rapid",
  classical: "Classical",
  puzzle: "Puzzles",
};

export function gameCategory(
  g: Pick<GameDetails, "initialTimeSeconds" | "incrementSeconds">,
): TimeCategory {
  return categoryFor(g.initialTimeSeconds, g.incrementSeconds);
}

const END_TEXT: Record<string, string> = {
  Checkmate: "Checkmate",
  Resignation: "Resignation",
  DrawAgreement: "Draw by agreement",
  Timeout: "Out of time",
  Stalemate: "Stalemate",
  ThreefoldRepetition: "Threefold repetition",
  FiftyMoveRule: "Fifty-move rule",
  InsufficientMaterial: "Insufficient material",
  Abandoned: "Abandoned",
  WaitingTimeout: "Nobody confirmed the start in time",
  RatedGameTooShort: "Rated game ended too early to count",
};

export const endReasonText = (reason?: string | null) =>
  reason ? (END_TEXT[reason] ?? reason) : "";

export function formatClock(ms: number) {
  const clamped = Math.max(0, ms);
  const total = Math.floor(clamped / 1000);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  if (h > 0) return `${h}:${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
  if (total < 10) return `${m}:${String(s).padStart(2, "0")}.${Math.floor((clamped % 1000) / 100)}`;
  return `${m}:${String(s).padStart(2, "0")}`;
}

export function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime();
  const s = Math.max(0, Math.floor(diff / 1000));
  if (s < 60) return "just now";
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  if (d < 30) return `${d}d ago`;
  return new Date(iso).toLocaleDateString();
}

export const shortDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });

export const shortDateTime = (iso: string) =>
  new Date(iso).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });

export const tournamentStatusTone = (s: string) =>
  (s === "InProgress"
    ? "good"
    : s === "Upcoming"
      ? "primary"
      : s === "Cancelled"
        ? "bad"
        : "neutral") as "good" | "primary" | "bad" | "neutral";

export const tournamentStatusLabel = (s: string) => (s === "InProgress" ? "In progress" : s);
