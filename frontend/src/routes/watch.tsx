import { useQuery } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { motion } from "motion/react";
import { Eye } from "lucide-react";
import { useState } from "react";
import {
  EmptyState,
  ErrorState,
  LoadingBlock,
  PageHeader,
  Pager,
  Pill,
  SignInPrompt,
} from "@/components/common/bits";
import { MiniBoard } from "@/components/game/MiniBoard";
import { Button } from "@/components/ui/button";
import { gamesApi } from "@/lib/api/endpoints";
import { usePlayerRatings, useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import type { GameDetails } from "@/lib/api/types";
import { gameCategory, timeControlLabel } from "@/lib/chess-utils";
import { useAuth } from "@/lib/state/auth";

export const Route = createFileRoute("/watch")({
  head: () => ({ meta: [{ title: "Watch live — Echelon Chess" }] }),
  component: Watch,
});

function Tile({ game, name }: { game: GameDetails; name: (id: string) => string }) {
  const w = usePlayerRatings(game.whitePlayerId);
  const b = usePlayerRatings(game.blackPlayerId);
  const cat = gameCategory(game);
  return (
    <Link
      to="/game/$gameId"
      params={{ gameId: game.id }}
      className="panel group block p-3 transition-colors hover:border-primary/60"
    >
      <MiniBoard fen={game.fen} />
      <div className="mt-3 flex items-center justify-between gap-2">
        <div className="min-w-0 text-sm">
          <p className="truncate font-semibold">
            {name(game.blackPlayerId)}{" "}
            <span className="font-mono text-xs text-muted-foreground">
              {b.data?.[cat].rating ?? ""}
            </span>
          </p>
          <p className="truncate font-semibold">
            {name(game.whitePlayerId)}{" "}
            <span className="font-mono text-xs text-muted-foreground">
              {w.data?.[cat].rating ?? ""}
            </span>
          </p>
        </div>
        <div className="shrink-0 text-right">
          <Pill tone="good">Live</Pill>
          <p className="label-mono mt-1.5">
            {timeControlLabel(game.initialTimeSeconds, game.incrementSeconds)} {cat}
          </p>
        </div>
      </div>
    </Link>
  );
}

function Watch() {
  const { status } = useAuth();
  const [page, setPage] = useState(1);
  const pageSize = 12;
  const q = useQuery({
    queryKey: ["games", "live", page],
    queryFn: () => gamesApi.list("live", page, pageSize),
    enabled: status === "authenticated",
    refetchInterval: 10_000,
    placeholderData: (prev) => prev,
  });
  const { name } = useUsernames(
    (q.data?.items ?? []).flatMap((g) => [g.whitePlayerId, g.blackPlayerId]),
  );

  return (
    <div className="mx-auto max-w-[1400px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="Watch live"
        subtitle="Games where both players allowed spectators. Open one to follow it move by move."
      />
      <div className="mt-8">
        {status === "anonymous" ? (
          <SignInPrompt text="Sign in to watch live games." redirect="/watch" />
        ) : q.isLoading ? (
          <LoadingBlock />
        ) : q.isError ? (
          <ErrorState message={errorMessage(q.error)} onRetry={() => void q.refetch()} />
        ) : q.data && q.data.items.length > 0 ? (
          <>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {q.data.items.map((g, i) => (
                <motion.div
                  key={g.id}
                  initial={{ opacity: 0, y: 14 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: Math.min(i, 8) * 0.04 }}
                >
                  <Tile game={g} name={name} />
                </motion.div>
              ))}
            </div>
            <Pager page={page} pageSize={pageSize} total={q.data.totalCount} onPage={setPage} />
          </>
        ) : (
          <EmptyState
            title="No live games right now"
            text="Games appear here as soon as two players start one with spectators allowed."
            action={
              <Button asChild>
                <Link to="/play">
                  <Eye className="mr-2 h-4 w-4" /> Start one
                </Link>
              </Button>
            }
          />
        )}
      </div>
    </div>
  );
}
