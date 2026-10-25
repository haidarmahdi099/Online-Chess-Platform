import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { CalendarClock, Play, Swords, Users } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { EmptyState, ErrorState, LoadingBlock, Pill } from "@/components/common/bits";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { tournamentsApi } from "@/lib/api/endpoints";
import { useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import type { TournamentMatch } from "@/lib/api/types";
import {
  categoryFor,
  shortDateTime,
  timeControlLabel,
  tournamentStatusLabel,
  tournamentStatusTone,
} from "@/lib/chess-utils";
import { hub } from "@/lib/realtime/hub";
import { useAuth } from "@/lib/state/auth";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/tournaments/$tournamentId")({
  head: () => ({ meta: [{ title: "Tournament — Echelon Chess" }] }),
  component: TournamentPage,
});

const RESULT_TEXT: Record<string, string> = {
  WhiteWin: "1-0",
  BlackWin: "0-1",
  Draw: "½-½",
  Bye: "Bye",
};

function TournamentPage() {
  const { tournamentId: id } = Route.useParams();
  const { user, status, isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const authed = status === "authenticated";
  const [confirm, setConfirm] = useState<null | "cancel" | "complete">(null);

  const details = useQuery({
    queryKey: ["tournament", id],
    queryFn: () => tournamentsApi.get(id),
    refetchInterval: 20_000,
  });
  const standings = useQuery({
    queryKey: ["tournament", id, "standings"],
    queryFn: () => tournamentsApi.standings(id),
    refetchInterval: 20_000,
  });
  const me = useQuery({
    queryKey: ["tournament", id, "me"],
    queryFn: () => tournamentsApi.me(id),
    enabled: authed,
  });

  // Friendly views of the same tournament, one small endpoint each.
  const participantsQ = useQuery({
    queryKey: ["tournament", id, "participants"],
    queryFn: () => tournamentsApi.participants(id),
    refetchInterval: 20_000,
  });
  const roundsQ = useQuery({
    queryKey: ["tournament", id, "rounds"],
    queryFn: () => tournamentsApi.rounds(id),
    refetchInterval: 20_000,
  });
  const matchesQ = useQuery({
    queryKey: ["tournament", id, "matches"],
    queryFn: () => tournamentsApi.matches(id),
    refetchInterval: 20_000,
  });
  const resultsQ = useQuery({
    queryKey: ["tournament", id, "results"],
    queryFn: () => tournamentsApi.results(id),
    refetchInterval: 20_000,
  });

  // Live updates: any tournament event simply refreshes the data.
  useEffect(() => {
    if (!authed) return;
    const off = hub.on("tournament:event", (e) => {
      if (e.tournamentId?.toLowerCase() === id.toLowerCase())
        void queryClient.invalidateQueries({ queryKey: ["tournament", id] });
    });
    hub.joinTournament(id).catch(() => undefined);
    return () => {
      off();
      void hub.leaveTournament(id);
    };
  }, [authed, id, queryClient]);

  const t = details.data;
  const ids = useMemo(
    () => [
      ...(t?.participantIds ?? []),
      ...(participantsQ.data?.participants ?? []),
      ...(t?.matches ?? []).flatMap((m) => [m.whitePlayerId, m.blackPlayerId]),
      ...(resultsQ.data?.results ?? []).flatMap((m) => [m.whitePlayerId, m.blackPlayerId]),
      ...(standings.data?.standings ?? []).map((s) => s.userId),
    ],
    [t, participantsQ.data, resultsQ.data, standings.data],
  );
  const { name } = useUsernames(ids);

  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["tournament", id] });
  const run = useMutation({
    mutationFn: ({ fn }: { fn: () => Promise<unknown>; ok: string }) => fn(),
    onSuccess: (_d, v) => {
      toast.success(v.ok);
      refresh();
    },
    onError: (e) => {
      toast.error(errorMessage(e));
      refresh();
    },
  });
  const act = (fn: () => Promise<unknown>, ok: string) => run.mutate({ fn, ok });

  // Pairings come from /rounds; the full tournament payload is only a fallback while it loads.
  const rounds = useMemo(() => {
    if (roundsQ.data)
      return roundsQ.data.rounds
        .map((r) => [r.roundNumber, r.matches] as [number, TournamentMatch[]])
        .sort((a, b) => b[0] - a[0]);
    const map = new Map<number, TournamentMatch[]>();
    (t?.matches ?? []).forEach((m) =>
      map.set(m.roundNumber, [...(map.get(m.roundNumber) ?? []), m]),
    );
    return [...map.entries()].sort((a, b) => b[0] - a[0]);
  }, [roundsQ.data, t]);

  // My match in the current round, from /matches.
  const myMatch = useMemo(() => {
    const me = user?.userId.toLowerCase();
    if (!me || !matchesQ.data) return null;
    return (
      matchesQ.data.matches.find(
        (m) =>
          m.roundNumber === matchesQ.data.currentRound &&
          (m.whitePlayerId.toLowerCase() === me || m.blackPlayerId?.toLowerCase() === me),
      ) ?? null
    );
  }, [matchesQ.data, user]);

  const finished = useMemo(
    () =>
      (resultsQ.data?.results ?? [])
        .filter((m) => m.blackPlayerId && m.result && m.result !== "Bye")
        .slice(-8)
        .reverse(),
    [resultsQ.data],
  );
  const players = participantsQ.data?.participants ?? t?.participantIds ?? [];

  if (details.isLoading) return <LoadingBlock className="py-32" />;
  if (details.isError || !t)
    return (
      <div className="mx-auto max-w-lg px-4 py-20">
        <ErrorState
          message={details.error ? errorMessage(details.error) : "Tournament not found."}
        />
      </div>
    );

  const registered =
    !!me.data?.isRegistered ||
    (!!user && t.participantIds.some((p) => p.toLowerCase() === user.userId.toLowerCase()));
  const canManage = !!me.data?.isOwner || isAdmin;
  const full = t.participantIds.length >= t.maxParticipants;
  const myId = user?.userId.toLowerCase();

  return (
    <div className="mx-auto max-w-[1200px] px-4 py-10 sm:px-6 sm:py-14">
      <Link to="/tournaments" className="label-mono hover:text-foreground">
        ← All tournaments
      </Link>

      <div className="panel mt-4 p-5 sm:p-7">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="font-display text-3xl font-bold uppercase sm:text-4xl">{t.name}</h1>
              <Pill tone={tournamentStatusTone(t.status)}>{tournamentStatusLabel(t.status)}</Pill>
            </div>
            {t.description && (
              <p className="mt-3 max-w-2xl text-sm text-muted-foreground">{t.description}</p>
            )}
            <div className="mt-4 flex flex-wrap gap-x-6 gap-y-2 font-mono text-xs text-muted-foreground">
              <span>
                {timeControlLabel(t.initialTimeSeconds, t.incrementSeconds)} ·{" "}
                {categoryFor(t.initialTimeSeconds, t.incrementSeconds)}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <Users className="h-3.5 w-3.5" /> {t.participantIds.length}/{t.maxParticipants}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <CalendarClock className="h-3.5 w-3.5" /> {shortDateTime(t.startsAtUtc)}
              </span>
              {t.totalRounds > 0 && (
                <span>
                  Round {t.currentRound}/{t.totalRounds}
                </span>
              )}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            {authed &&
              t.status === "Upcoming" &&
              (registered ? (
                <Button
                  variant="secondary"
                  disabled={run.isPending}
                  onClick={() => act(() => tournamentsApi.leave(id), "You left the tournament")}
                >
                  Leave
                </Button>
              ) : (
                <Button
                  disabled={run.isPending || full}
                  onClick={() => act(() => tournamentsApi.join(id), "You're in!")}
                >
                  {full ? "Full" : "Join tournament"}
                </Button>
              ))}
            {!authed && status === "anonymous" && t.status === "Upcoming" && (
              <Button asChild>
                <Link to="/login" search={{ redirect: `/tournaments/${id}` }}>
                  Sign in to join
                </Link>
              </Button>
            )}
          </div>
        </div>

        {canManage && (
          <div className="mt-6 flex flex-wrap items-center gap-2 border-t border-border pt-5">
            <span className="label-mono mr-2">Manage</span>
            {t.status === "Upcoming" && (
              <Button
                size="sm"
                disabled={run.isPending}
                onClick={() => act(() => tournamentsApi.start(id), "Tournament started")}
              >
                <Play className="mr-1.5 h-3.5 w-3.5" /> Start
              </Button>
            )}
            {t.status === "InProgress" && (
              <>
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={run.isPending}
                  onClick={() => act(() => tournamentsApi.pairings(id), "Next round paired")}
                >
                  Next round
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  disabled={run.isPending}
                  onClick={() =>
                    act(() => tournamentsApi.retryProvisioning(id), "Retrying game creation")
                  }
                >
                  Retry game creation
                </Button>
                <Button size="sm" variant="outline" onClick={() => setConfirm("complete")}>
                  Complete
                </Button>
              </>
            )}
            {(t.status === "Upcoming" || t.status === "InProgress") && (
              <Button size="sm" variant="destructive" onClick={() => setConfirm("cancel")}>
                Cancel
              </Button>
            )}
          </div>
        )}
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,380px)]">
        <div className="space-y-6">
          {myMatch && t?.status === "InProgress" && (
            <section className="panel flex flex-wrap items-center justify-between gap-3 border-primary/50 bg-primary/10 p-5">
              <div className="min-w-0">
                <p className="label-mono">Your match · Round {myMatch.roundNumber}</p>
                <p className="mt-1 truncate text-lg font-semibold">
                  {name(myMatch.whitePlayerId)} <span className="text-muted-foreground">vs</span>{" "}
                  {myMatch.blackPlayerId ? name(myMatch.blackPlayerId) : "BYE"}
                </p>
                <p className="mt-1 text-sm text-muted-foreground">
                  {myMatch.result
                    ? `Finished: ${RESULT_TEXT[myMatch.result] ?? myMatch.result}`
                    : myMatch.gameId
                      ? "Your game is ready."
                      : "Waiting for your game to be created…"}
                </p>
              </div>
              {myMatch.gameId && !myMatch.result && (
                <Button asChild>
                  <Link to="/game/$gameId" params={{ gameId: myMatch.gameId }}>
                    <Swords className="mr-1.5 h-4 w-4" /> Play now
                  </Link>
                </Button>
              )}
            </section>
          )}

          <section>
            <h2 className="mb-3 font-display text-xl font-bold uppercase">Pairings</h2>
            {rounds.length === 0 ? (
              <EmptyState
                title="No pairings yet"
                text={
                  t.status === "Upcoming"
                    ? "Pairings appear when the tournament starts."
                    : "No matches were played."
                }
              />
            ) : (
              <div className="space-y-5">
                {rounds.map(([round, matches]) => (
                  <div key={round}>
                    <p className="label-mono mb-2">Round {round}</p>
                    <ul className="panel divide-y divide-border">
                      {matches.map((m) => {
                        const mine =
                          !!myId &&
                          (m.whitePlayerId.toLowerCase() === myId ||
                            m.blackPlayerId?.toLowerCase() === myId);
                        return (
                          <li
                            key={m.id}
                            className={cn(
                              "flex flex-wrap items-center gap-3 px-4 py-3",
                              mine && "bg-primary/10",
                            )}
                          >
                            <div className="min-w-0 flex-1 text-sm font-semibold">
                              <span className="truncate">{name(m.whitePlayerId)}</span>
                              <span className="mx-2 text-muted-foreground">vs</span>
                              <span className="truncate">
                                {m.blackPlayerId ? name(m.blackPlayerId) : "BYE"}
                              </span>
                            </div>
                            {m.result ? (
                              <Pill tone="neutral">{RESULT_TEXT[m.result] ?? m.result}</Pill>
                            ) : (
                              <Pill
                                tone={
                                  m.status === "InProgress"
                                    ? "good"
                                    : m.status === "ProvisionFailed"
                                      ? "bad"
                                      : "neutral"
                                }
                              >
                                {m.status === "ProvisionFailed" ? "Needs attention" : m.status}
                              </Pill>
                            )}
                            {m.gameId && (
                              <Button
                                asChild
                                size="sm"
                                variant={
                                  mine && m.status === "InProgress" ? "default" : "secondary"
                                }
                              >
                                <Link to="/game/$gameId" params={{ gameId: m.gameId }}>
                                  <Swords className="mr-1.5 h-3.5 w-3.5" />{" "}
                                  {mine && m.status === "InProgress" ? "Play" : "View"}
                                </Link>
                              </Button>
                            )}
                            {canManage && m.status === "ProvisionFailed" && m.blackPlayerId && (
                              <span className="flex gap-1">
                                {(["WhiteWin", "Draw", "BlackWin"] as const).map((r) => (
                                  <Button
                                    key={r}
                                    size="sm"
                                    variant="outline"
                                    disabled={run.isPending}
                                    onClick={() =>
                                      act(
                                        () => tournamentsApi.setResult(id, m.id, r),
                                        "Result recorded",
                                      )
                                    }
                                  >
                                    {RESULT_TEXT[r]}
                                  </Button>
                                ))}
                              </span>
                            )}
                          </li>
                        );
                      })}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </section>
        </div>

        <div className="space-y-6">
          <section>
            <h2 className="mb-3 font-display text-xl font-bold uppercase">Standings</h2>
            {standings.data && standings.data.standings.length > 0 ? (
              <div className="panel overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-border text-left">
                      {["#", "Player", "Pts", "W/D/L"].map((h, i) => (
                        <th
                          key={h}
                          className={cn(
                            "label-mono px-3 py-2.5 font-normal",
                            i >= 2 && "text-right",
                          )}
                        >
                          {h}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {standings.data.standings.map((s) => (
                      <tr
                        key={s.userId}
                        className={cn(
                          "border-b border-border/60 last:border-0",
                          myId === s.userId.toLowerCase() && "bg-primary/10",
                        )}
                      >
                        <td className="px-3 py-2.5 font-mono">{s.rank}</td>
                        <td className="px-3 py-2.5 font-semibold">{name(s.userId)}</td>
                        <td className="px-3 py-2.5 text-right font-mono font-bold">{s.points}</td>
                        <td className="px-3 py-2.5 text-right font-mono text-muted-foreground">
                          {s.wins}/{s.draws}/{s.losses}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <EmptyState title="No standings yet" />
            )}
          </section>

          {finished.length > 0 && (
            <section>
              <h2 className="mb-3 font-display text-xl font-bold uppercase">Latest results</h2>
              <ul className="panel divide-y divide-border">
                {finished.map((m) => (
                  <li key={m.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                    <span className="min-w-0 flex-1 truncate font-semibold">
                      {name(m.whitePlayerId)}{" "}
                      <span className="font-normal text-muted-foreground">vs</span>{" "}
                      {name(m.blackPlayerId ?? "")}
                    </span>
                    <span className="font-mono text-xs">
                      {RESULT_TEXT[m.result ?? ""] ?? m.result}
                    </span>
                    {m.gameId && (
                      <Link
                        to="/game/$gameId"
                        params={{ gameId: m.gameId }}
                        className="label-mono hover:text-foreground"
                      >
                        Replay
                      </Link>
                    )}
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section>
            <h2 className="mb-3 font-display text-xl font-bold uppercase">
              Players ({participantsQ.data?.participantCount ?? players.length})
            </h2>
            {players.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                {players.map((p) => (
                  <Link
                    key={p}
                    to="/players/$username"
                    params={{ username: name(p) }}
                    className="rounded-md border border-border bg-surface px-3 py-1.5 text-sm hover:border-primary/60"
                  >
                    {name(p)}
                  </Link>
                ))}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">Nobody has joined yet.</p>
            )}
          </section>
        </div>
      </div>

      <Modal
        open={confirm !== null}
        onClose={() => setConfirm(null)}
        title={confirm === "cancel" ? "Cancel this tournament?" : "Complete this tournament?"}
        description={
          confirm === "cancel"
            ? "All pairings are dropped and players are notified. This can't be undone."
            : "Final standings are locked in. This can't be undone."
        }
      >
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={() => setConfirm(null)}>
            Back
          </Button>
          <Button
            variant={confirm === "cancel" ? "destructive" : "default"}
            onClick={() => {
              const which = confirm;
              setConfirm(null);
              if (which === "cancel") act(() => tournamentsApi.cancel(id), "Tournament cancelled");
              else act(() => tournamentsApi.complete(id), "Tournament completed");
            }}
          >
            Confirm
          </Button>
        </div>
      </Modal>
    </div>
  );
}
