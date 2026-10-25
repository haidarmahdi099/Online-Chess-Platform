import { useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { EmptyState, ErrorState, LoadingBlock, PageHeader, Pager } from "@/components/common/bits";
import { GameCard } from "@/components/game/GameCard";
import { gamesApi } from "@/lib/api/endpoints";
import { useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import { useRequireAuth } from "@/lib/state/auth";

export const Route = createFileRoute("/games")({
  head: () => ({ meta: [{ title: "My games — Echelon Chess" }] }),
  component: MyGames,
});

function MyGames() {
  const { status, user } = useRequireAuth();
  const [page, setPage] = useState(1);
  const pageSize = 15;
  const q = useQuery({
    queryKey: ["games", "mine", page],
    queryFn: () => gamesApi.list("mine", page, pageSize),
    enabled: status === "authenticated",
    placeholderData: (prev) => prev,
  });
  const stats = useQuery({
    queryKey: ["games", "my-stats"],
    queryFn: gamesApi.stats,
    enabled: status === "authenticated",
  });
  const { name } = useUsernames(
    (q.data?.items ?? []).flatMap((g) => [g.whitePlayerId, g.blackPlayerId]),
  );

  return (
    <div className="mx-auto max-w-[1000px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="My games"
        subtitle="Every game you've played. Open one to replay it and run the engine over it."
      />
      {stats.data && (
        <div className="mt-8 grid grid-cols-2 gap-3 sm:grid-cols-5">
          {[
            { l: "Played", v: stats.data.totalGames },
            { l: "Wins", v: stats.data.wins },
            { l: "Draws", v: stats.data.draws },
            { l: "Losses", v: stats.data.losses },
            {
              l: "Win rate",
              v:
                stats.data.finishedGames > 0
                  ? `${Math.round((stats.data.wins / stats.data.finishedGames) * 100)}%`
                  : "–",
            },
          ].map(({ l, v }) => (
            <div key={l} className="panel px-4 py-3">
              <p className="label-mono">{l}</p>
              <p className="mt-1 font-mono text-2xl font-bold tabular-nums">{v}</p>
            </div>
          ))}
          {stats.data.liveGames > 0 && (
            <p className="col-span-full text-sm text-muted-foreground">
              You have {stats.data.liveGames} game{stats.data.liveGames === 1 ? "" : "s"} in
              progress right now.
            </p>
          )}
        </div>
      )}
      <div className="mt-8 space-y-3">
        {q.isLoading || status === "loading" ? (
          <LoadingBlock />
        ) : q.isError ? (
          <ErrorState message={errorMessage(q.error)} onRetry={() => void q.refetch()} />
        ) : q.data && q.data.items.length > 0 ? (
          <>
            {q.data.items.map((g) => (
              <GameCard key={g.id} game={g} name={name} myId={user?.userId ?? null} />
            ))}
            <Pager page={page} pageSize={pageSize} total={q.data.totalCount} onPage={setPage} />
          </>
        ) : (
          <EmptyState title="No games yet" text="Play your first game and it will show up here." />
        )}
      </div>
    </div>
  );
}
