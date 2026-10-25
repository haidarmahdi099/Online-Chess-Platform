import { useQuery } from "@tanstack/react-query";
import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { Trophy } from "lucide-react";
import { useState } from "react";
import { EmptyState, ErrorState, LoadingBlock, PageHeader, Pager } from "@/components/common/bits";
import { ratingsApi } from "@/lib/api/endpoints";
import { useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import { CATEGORY_LABEL } from "@/lib/chess-utils";
import { useAuth } from "@/lib/state/auth";
import type { RatingCategory } from "@/lib/api/types";
import { cn } from "@/lib/utils";

const CATEGORIES: RatingCategory[] = ["chess", "bullet", "blitz", "rapid", "classical", "puzzle"];

export const Route = createFileRoute("/leaderboard")({
  validateSearch: (s: Record<string, unknown>): { category: RatingCategory } => ({
    category: CATEGORIES.includes(s["category"] as RatingCategory)
      ? (s["category"] as RatingCategory)
      : "chess",
  }),
  head: () => ({ meta: [{ title: "Leaderboard — Echelon Chess" }] }),
  component: Leaderboard,
});

function Leaderboard() {
  const { category } = Route.useSearch();
  const navigate = useNavigate({ from: "/leaderboard" });
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const q = useQuery({
    queryKey: ["leaderboard", category, page],
    queryFn: () => ratingsApi.leaderboard(category, page, pageSize),
    placeholderData: (prev) => prev,
  });
  const { name, ready } = useUsernames((q.data?.items ?? []).map((e) => e.playerId));

  return (
    <div className="mx-auto max-w-[1000px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="Leaderboard"
        subtitle="The strongest players on Echelon, by rating category."
      />

      <div
        className="mt-8 flex gap-1.5 overflow-x-auto pb-1"
        role="tablist"
        aria-label="Rating category"
      >
        {CATEGORIES.map((c) => (
          <button
            key={c}
            role="tab"
            aria-selected={c === category}
            onClick={() => {
              setPage(1);
              void navigate({ search: { category: c } });
            }}
            className={cn(
              "shrink-0 rounded-md border px-4 py-2 font-mono text-xs uppercase tracking-[0.14em] transition-colors",
              c === category
                ? "border-primary bg-primary/15 text-foreground"
                : "border-border text-muted-foreground hover:text-foreground",
            )}
          >
            {CATEGORY_LABEL[c]}
          </button>
        ))}
      </div>

      <div className="mt-5">
        {q.isLoading ? (
          <LoadingBlock />
        ) : q.isError ? (
          <ErrorState message={errorMessage(q.error)} onRetry={() => void q.refetch()} />
        ) : q.data && q.data.items.length > 0 ? (
          <div className="panel overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[520px] text-sm">
                <thead>
                  <tr className="border-b border-border text-left">
                    {["#", "Player", "Rating", "Games", "W / D / L"].map((h, i) => (
                      <th
                        key={h}
                        className={cn("label-mono px-4 py-3 font-normal", i >= 2 && "text-right")}
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {q.data.items.map((e) => {
                    const me = !!user && e.playerId.toLowerCase() === user.userId.toLowerCase();
                    const uname = name(e.playerId);
                    return (
                      <tr
                        key={e.playerId}
                        className={cn(
                          "border-b border-border/60 last:border-0 hover:bg-secondary/50",
                          me && "bg-primary/10",
                        )}
                      >
                        <td className="px-4 py-3 font-mono">
                          {e.rank <= 3 ? (
                            <Trophy
                              className={cn(
                                "h-4 w-4",
                                e.rank === 1
                                  ? "text-chart-4"
                                  : e.rank === 2
                                    ? "text-muted-foreground"
                                    : "text-chart-5",
                              )}
                            />
                          ) : (
                            e.rank
                          )}
                        </td>
                        <td className="px-4 py-3 font-semibold">
                          {ready ? (
                            <Link
                              to="/players/$username"
                              params={{ username: uname }}
                              className="hover:text-primary hover:underline"
                            >
                              {uname}
                            </Link>
                          ) : (
                            uname
                          )}
                          {me && <span className="label-mono ml-2">you</span>}
                        </td>
                        <td className="px-4 py-3 text-right font-mono font-bold tabular-nums">
                          {e.rating}
                        </td>
                        <td className="px-4 py-3 text-right font-mono tabular-nums text-muted-foreground">
                          {e.gamesPlayed}
                        </td>
                        <td className="px-4 py-3 text-right font-mono tabular-nums text-muted-foreground">
                          {e.wins} / {e.draws} / {e.losses}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        ) : (
          <EmptyState
            title="No ranked players yet"
            text="Play rated games to appear on the board."
          />
        )}
        {q.data && (
          <Pager page={page} pageSize={pageSize} total={q.data.totalCount} onPage={setPage} />
        )}
      </div>
    </div>
  );
}
