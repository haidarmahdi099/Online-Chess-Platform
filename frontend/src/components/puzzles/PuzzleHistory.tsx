import { useQuery } from "@tanstack/react-query";
import { CheckCircle2, XCircle } from "lucide-react";
import { useState } from "react";
import { EmptyState, ErrorState, LoadingBlock, Pager, Pill } from "@/components/common/bits";
import { puzzlesApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { shortDateTime } from "@/lib/chess-utils";

const PAGE_SIZE = 15;

/** The signed-in player's puzzle attempts (GET /puzzles/me/history). */
export function PuzzleHistory() {
  const [page, setPage] = useState(1);
  const q = useQuery({
    queryKey: ["puzzle-history", page],
    queryFn: () => puzzlesApi.myHistory(page, PAGE_SIZE),
    placeholderData: (prev) => prev,
  });

  if (q.isLoading) return <LoadingBlock />;
  if (q.isError)
    return <ErrorState message={errorMessage(q.error)} onRetry={() => void q.refetch()} />;
  if (!q.data || q.data.items.length === 0)
    return (
      <EmptyState
        title="No attempts yet"
        text="Solve your first puzzle and it will show up here."
      />
    );

  return (
    <>
      <ul className="panel divide-y divide-border">
        {q.data.items.map((a) => (
          <li key={a.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
            {a.isCorrect ? (
              <CheckCircle2 className="h-5 w-5 shrink-0 text-live" aria-label="Solved" />
            ) : (
              <XCircle className="h-5 w-5 shrink-0 text-destructive" aria-label="Failed" />
            )}
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold">Puzzle #{a.puzzleId.slice(0, 6)}</p>
              <p className="truncate font-mono text-xs text-muted-foreground">
                {a.submittedMoves.join(" ")}
              </p>
            </div>
            <Pill tone={a.isCorrect ? "good" : "bad"}>{a.isCorrect ? "Solved" : "Failed"}</Pill>
            <span className="label-mono">{shortDateTime(a.attemptedAtUtc)}</span>
          </li>
        ))}
      </ul>
      <Pager page={page} pageSize={PAGE_SIZE} total={q.data.totalCount} onPage={setPage} />
    </>
  );
}
