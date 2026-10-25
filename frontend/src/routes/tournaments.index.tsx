import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { motion } from "motion/react";
import { CalendarClock, Plus, Users } from "lucide-react";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import {
  EmptyState,
  ErrorState,
  LoadingBlock,
  PageHeader,
  Pager,
  Pill,
} from "@/components/common/bits";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { tournamentsApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import {
  categoryFor,
  shortDateTime,
  timeControlLabel,
  tournamentStatusLabel,
  tournamentStatusTone,
} from "@/lib/chess-utils";
import { useAuth } from "@/lib/state/auth";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/tournaments/")({
  head: () => ({ meta: [{ title: "Tournaments — Echelon Chess" }] }),
  component: Tournaments,
});

const FILTERS = [
  { v: undefined, l: "All" },
  { v: "Upcoming", l: "Upcoming" },
  { v: "InProgress", l: "In progress" },
  { v: "Completed", l: "Completed" },
] as const;

function Tournaments() {
  const { isAdmin } = useAuth();
  const [status, setStatus] = useState<string | undefined>(undefined);
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const pageSize = 12;
  const q = useQuery({
    queryKey: ["tournaments", status, page],
    queryFn: () => tournamentsApi.list(status, page, pageSize),
    placeholderData: (prev) => prev,
  });

  return (
    <div className="mx-auto max-w-[1200px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="Tournaments"
        subtitle="Swiss-style events with live standings. Join before the start time and play your pairings."
        actions={
          isAdmin && (
            <Button onClick={() => setCreating(true)}>
              <Plus className="mr-2 h-4 w-4" /> Create tournament
            </Button>
          )
        }
      />

      <div className="mt-8 flex flex-wrap gap-1.5">
        {FILTERS.map((f) => (
          <button
            key={f.l}
            onClick={() => {
              setStatus(f.v);
              setPage(1);
            }}
            className={cn(
              "rounded-md border px-4 py-2 font-mono text-xs uppercase tracking-[0.14em]",
              status === f.v
                ? "border-primary bg-primary/15"
                : "border-border text-muted-foreground hover:text-foreground",
            )}
          >
            {f.l}
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
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {q.data.items.map((t, i) => (
                <motion.div
                  key={t.id}
                  initial={{ opacity: 0, y: 14 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: Math.min(i, 8) * 0.04 }}
                >
                  <Link
                    to="/tournaments/$tournamentId"
                    params={{ tournamentId: t.id }}
                    className="panel block h-full p-5 transition-colors hover:border-primary/60"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <h2 className="font-display text-lg font-bold uppercase leading-tight">
                        {t.name}
                      </h2>
                      <Pill tone={tournamentStatusTone(t.status)}>
                        {tournamentStatusLabel(t.status)}
                      </Pill>
                    </div>
                    {t.description && (
                      <p className="mt-2 line-clamp-2 text-sm text-muted-foreground">
                        {t.description}
                      </p>
                    )}
                    <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 font-mono text-xs text-muted-foreground">
                      <span>
                        {timeControlLabel(t.initialTimeSeconds, t.incrementSeconds)} ·{" "}
                        {categoryFor(t.initialTimeSeconds, t.incrementSeconds)}
                      </span>
                      <span className="inline-flex items-center gap-1.5">
                        <Users className="h-3.5 w-3.5" /> {t.participantCount}/{t.maxParticipants}
                      </span>
                      <span className="inline-flex items-center gap-1.5">
                        <CalendarClock className="h-3.5 w-3.5" /> {shortDateTime(t.startsAtUtc)}
                      </span>
                    </div>
                  </Link>
                </motion.div>
              ))}
            </div>
            <Pager page={page} pageSize={pageSize} total={q.data.totalCount} onPage={setPage} />
          </>
        ) : (
          <EmptyState
            title="No tournaments here"
            text={
              isAdmin ? "Create the first one." : "Check back soon — new events are announced here."
            }
          />
        )}
      </div>

      {isAdmin && <CreateTournament open={creating} onClose={() => setCreating(false)} />}
    </div>
  );
}

function CreateTournament({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const defaultStart = () => {
    const d = new Date(Date.now() + 60 * 60 * 1000);
    d.setMinutes(0, 0, 0);
    d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
    return d.toISOString().slice(0, 16);
  };
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [startsAt, setStartsAt] = useState(defaultStart);
  const [minutes, setMinutes] = useState("3");
  const [inc, setInc] = useState("0");
  const [max, setMax] = useState("16");

  const create = useMutation({
    mutationFn: () =>
      tournamentsApi.create({
        name: name.trim(),
        description: description.trim() || null,
        startsAtUtc: new Date(startsAt).toISOString(),
        initialTimeSeconds: Math.round(Number(minutes) * 60),
        incrementSeconds: Math.round(Number(inc)),
        maxParticipants: Math.round(Number(max)),
      }),
    onSuccess: (t) => {
      toast.success("Tournament created");
      void queryClient.invalidateQueries({ queryKey: ["tournaments"] });
      onClose();
      void navigate({ to: "/tournaments/$tournamentId", params: { tournamentId: t.id } });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (name.trim().length < 3) {
      toast.error("Give the tournament a name (3+ characters).");
      return;
    }
    if (!startsAt || Number.isNaN(new Date(startsAt).getTime())) {
      toast.error("Pick a start time.");
      return;
    }
    create.mutate();
  };

  return (
    <Modal open={open} onClose={onClose} title="Create tournament" className="max-w-lg">
      <form onSubmit={submit} className="space-y-4">
        <label className="block space-y-1.5">
          <span className="label-mono">Name</span>
          <Input value={name} onChange={(e) => setName(e.target.value)} maxLength={80} />
        </label>
        <label className="block space-y-1.5">
          <span className="label-mono">Description (optional)</span>
          <Input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={300}
          />
        </label>
        <label className="block space-y-1.5">
          <span className="label-mono">Starts at (your local time)</span>
          <Input
            type="datetime-local"
            value={startsAt}
            onChange={(e) => setStartsAt(e.target.value)}
          />
        </label>
        <div className="grid grid-cols-3 gap-3">
          <label className="space-y-1.5">
            <span className="label-mono">Minutes</span>
            <Input
              inputMode="decimal"
              value={minutes}
              onChange={(e) => setMinutes(e.target.value)}
            />
          </label>
          <label className="space-y-1.5">
            <span className="label-mono">Increment (s)</span>
            <Input inputMode="numeric" value={inc} onChange={(e) => setInc(e.target.value)} />
          </label>
          <label className="space-y-1.5">
            <span className="label-mono">Max players</span>
            <Input inputMode="numeric" value={max} onChange={(e) => setMax(e.target.value)} />
          </label>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? "Creating…" : "Create"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
