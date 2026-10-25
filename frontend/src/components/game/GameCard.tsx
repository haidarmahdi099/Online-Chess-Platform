import { Link } from "@tanstack/react-router";
import { Pill } from "@/components/common/bits";
import type { GameDetails } from "@/lib/api/types";
import { endReasonText, gameCategory, shortDateTime, timeControlLabel } from "@/lib/chess-utils";
import { cn } from "@/lib/utils";

interface GameCardProps {
  game: GameDetails;
  name: (id: string) => string;
  myId?: string | null;
  className?: string;
}

/** Outcome from my point of view (when I played), otherwise a neutral label. */
export function outcomeFor(game: GameDetails, myId?: string | null) {
  if (game.status === "Aborted") return { label: "Aborted", tone: "neutral" as const };
  if (game.status !== "Finished")
    return { label: game.status === "Waiting" ? "Starting" : "Live", tone: "good" as const };
  if (!game.winnerId) return { label: "Draw", tone: "neutral" as const };
  if (myId && game.winnerId.toLowerCase() === myId.toLowerCase())
    return { label: "Won", tone: "good" as const };
  if (
    myId &&
    (game.whitePlayerId.toLowerCase() === myId.toLowerCase() ||
      game.blackPlayerId.toLowerCase() === myId.toLowerCase())
  )
    return { label: "Lost", tone: "bad" as const };
  return { label: game.winnerId === game.whitePlayerId ? "1-0" : "0-1", tone: "neutral" as const };
}

export function GameCard({ game, name, myId, className }: GameCardProps) {
  const outcome = outcomeFor(game, myId);
  const cat = gameCategory(game);
  return (
    <Link
      to="/game/$gameId"
      params={{ gameId: game.id }}
      className={cn(
        "panel grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 p-4 transition-colors hover:border-primary/60",
        className,
      )}
    >
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold">
          <span className="inline-block h-2.5 w-2.5 rounded-full border border-border bg-board-light align-middle" />{" "}
          {name(game.whitePlayerId)}
          <span className="mx-2 text-muted-foreground">vs</span>
          <span className="inline-block h-2.5 w-2.5 rounded-full border border-border bg-board-dark align-middle" />{" "}
          {name(game.blackPlayerId)}
        </p>
        <p className="label-mono mt-1.5 truncate">
          {timeControlLabel(game.initialTimeSeconds, game.incrementSeconds)} {cat} ·{" "}
          {game.rated ? "rated" : "casual"}
          {game.status === "Finished" && game.endReason
            ? ` · ${endReasonText(game.endReason)}`
            : ""}
          {game.status !== "Live" && game.status !== "Waiting"
            ? ` · ${shortDateTime(game.finishedAt ?? game.createdAt)}`
            : ""}
        </p>
      </div>
      <Pill tone={outcome.tone}>{outcome.label}</Pill>
    </Link>
  );
}
