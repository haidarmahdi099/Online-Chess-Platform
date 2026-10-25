import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { EmptyState, ErrorState, LoadingBlock, Pager, Pill } from "@/components/common/bits";
import { puzzlesApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { cn } from "@/lib/utils";

const BANDS = [
  { label: "All", min: undefined, max: undefined },
  { label: "Beginner · <1000", min: undefined, max: 999 },
  { label: "1000–1399", min: 1000, max: 1399 },
  { label: "1400–1799", min: 1400, max: 1799 },
  { label: "1800–2199", min: 1800, max: 2199 },
  { label: "Master · 2200+", min: 2200, max: undefined },
] as const;

const PAGE_SIZE = 18;

/** Public puzzle catalogue (GET /puzzles) with a rating filter. */
export function PuzzleLibrary() {
  const [band, setBand] = useState(0);
  const [page, setPage] = useState(1);
  const b = BANDS[band]!;

  const q = useQuery({
    queryKey: ["puzzles", "library", band, page],
    queryFn: () => puzzlesApi.list(page, PAGE_SIZE, b.min, b.max),
    placeholderData: (prev) => prev,
  });

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        {BANDS.map((x, i) => (
          <button
            key={x.label}
            type="button"
            onClick={() => {
              setBand(i);
              setPage(1);
            }}
            className={cn(
              "rounded-md border px-3 py-1.5 font-mono text-xs transition-colors",
              i === band
                ? "border-primary bg-primary/10 text-foreground"
                : "border-border bg-secondary text-muted-foreground hover:border-primary/50",
            )}
          >
            {x.label}
          </button>
        ))}
      </div>

      <div className="mt-5">
        {q.isLoading ? (
          <LoadingBlock />
        ) : q.isError ? (
          <ErrorState message={errorMessage(q.error)} onRetry={() => void q.refetch()} />
        ) : q.data && q.data.items.length > 0 ? (
          <>
            <p className="label-mono mb-3">
              {q.data.totalCount.toLocaleString()} puzzle{q.data.totalCount === 1 ? "" : "s"} in
              this range
            </p>
            <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {q.data.items.map((p) => (
                <li key={p.id} className="panel p-4">
                  <div className="flex items-center justify-between">
                    <span className="label-mono">#{p.id.slice(0, 6)}</span>
                    <span className="font-mono text-xl font-bold tabular-nums">{p.rating}</span>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-1.5">
                    {p.themes.length > 0 ? (
                      p.themes.map((t) => <Pill key={t}>{t}</Pill>)
                    ) : (
                      <span className="text-xs text-muted-foreground">No themes</span>
                    )}
                  </div>
                </li>
              ))}
            </ul>
            <Pager page={page} pageSize={PAGE_SIZE} total={q.data.totalCount} onPage={setPage} />
          </>
        ) : (
          <EmptyState title="No puzzles in this range" text="Try a different rating band." />
        )}
      </div>
    </div>
  );
}
