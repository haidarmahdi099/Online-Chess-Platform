import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { Search, Swords, UserMinus, UserPlus, X } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Avatar, EmptyState, ErrorState, LoadingBlock, PageHeader } from "@/components/common/bits";
import { ChallengeModal } from "@/components/game/ChallengeModal";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { challengesApi, socialApi, usersApi } from "@/lib/api/endpoints";
import { useInbox, useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import { timeControlLabel, timeAgo } from "@/lib/chess-utils";
import { useRequireAuth } from "@/lib/state/auth";

export const Route = createFileRoute("/friends")({
  head: () => ({ meta: [{ title: "Friends — Echelon Chess" }] }),
  component: Friends,
});

function PersonRow({
  name,
  sub,
  actions,
}: {
  name: string;
  sub?: string;
  actions: React.ReactNode;
}) {
  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-2 px-3 py-3 sm:px-4">
      <Avatar name={name} />
      <div className="min-w-[8rem] flex-1">
        <Link
          to="/players/$username"
          params={{ username: name }}
          className="block truncate text-sm font-semibold hover:text-primary hover:underline"
        >
          {name}
        </Link>
        {sub && <p className="label-mono mt-0.5">{sub}</p>}
      </div>
      <div className="flex shrink-0 gap-2 max-sm:w-full max-sm:justify-end">{actions}</div>
    </li>
  );
}

function Friends() {
  const { status, user } = useRequireAuth();
  const enabled = status === "authenticated";
  const queryClient = useQueryClient();
  const [challengeTarget, setChallengeTarget] = useState<{ id: string; name: string } | null>(null);
  const [q, setQ] = useState("");
  const [debounced, setDebounced] = useState("");

  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(q.trim()), 350);
    return () => window.clearTimeout(id);
  }, [q]);

  const friends = useQuery({
    queryKey: ["social", "friends"],
    queryFn: socialApi.friends,
    enabled,
  });
  const outgoing = useQuery({
    queryKey: ["social", "outgoing"],
    queryFn: socialApi.outgoing,
    enabled,
  });
  const blocks = useQuery({ queryKey: ["social", "blocks"], queryFn: socialApi.blocks, enabled });
  const sentChallenges = useQuery({
    queryKey: ["challenges", "outgoing"],
    queryFn: challengesApi.outgoing,
    enabled,
    refetchInterval: 15_000,
  });
  const search = useQuery({
    queryKey: ["users", "search", debounced],
    queryFn: () => usersApi.search(debounced, 20),
    enabled: enabled && debounced.length >= 2,
  });
  const { friendRequests } = useInbox();
  const names = useUsernames([
    ...(outgoing.data ?? []).map((o) => o.userId),
    ...(sentChallenges.data ?? []).map((c) => c.opponentUserId),
  ]);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["social"] });
    void queryClient.invalidateQueries({ queryKey: ["inbox"] });
    void queryClient.invalidateQueries({ queryKey: ["challenges"] });
  };
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

  const friendIds = new Set((friends.data ?? []).map((f) => f.userId));
  const pendingIds = new Set((outgoing.data ?? []).map((o) => o.userId));
  const pending = (sentChallenges.data ?? []).filter((c) => c.status === "Pending");

  return (
    <div className="mx-auto max-w-[900px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="Friends"
        subtitle="Add players, send direct challenges and manage who can reach you."
      />

      <Tabs defaultValue="friends" className="mt-8">
        <TabsList className="grid w-full grid-cols-4 font-mono text-[10px] uppercase tracking-[0.04em] sm:text-xs sm:tracking-[0.1em]">
          <TabsTrigger value="friends">Friends</TabsTrigger>
          <TabsTrigger value="requests">
            Requests
            {friendRequests.length > 0 && (
              <span className="ml-1.5 rounded-full bg-primary px-1.5 text-[10px] text-primary-foreground">
                {friendRequests.length}
              </span>
            )}
          </TabsTrigger>
          <TabsTrigger value="find">Find</TabsTrigger>
          <TabsTrigger value="blocked">Blocked</TabsTrigger>
        </TabsList>

        <TabsContent value="friends" className="mt-5 space-y-5">
          {friends.isLoading ? (
            <LoadingBlock />
          ) : friends.isError ? (
            <ErrorState
              message={errorMessage(friends.error)}
              onRetry={() => void friends.refetch()}
            />
          ) : friends.data && friends.data.length > 0 ? (
            <ul className="panel divide-y divide-border">
              {friends.data.map((f) => (
                <PersonRow
                  key={f.userId}
                  name={f.username}
                  actions={
                    <>
                      <Button
                        size="sm"
                        onClick={() => setChallengeTarget({ id: f.userId, name: f.username })}
                      >
                        <Swords className="mr-1.5 h-4 w-4" /> Challenge
                      </Button>
                      <Button
                        size="icon"
                        variant="ghost"
                        className="h-11 w-11 sm:h-9 sm:w-9"
                        aria-label={`Remove ${f.username}`}
                        onClick={() =>
                          act(() => socialApi.removeFriend(f.userId), "Friend removed")
                        }
                      >
                        <UserMinus className="h-4 w-4" />
                      </Button>
                    </>
                  }
                />
              ))}
            </ul>
          ) : (
            <EmptyState
              title="No friends yet"
              text="Use the Find tab to search for players by username."
            />
          )}

          {pending.length > 0 && (
            <div>
              <p className="label-mono mb-2">Challenges you sent</p>
              <ul className="panel divide-y divide-border">
                {pending.map((c) => (
                  <PersonRow
                    key={c.id}
                    name={names.name(c.opponentUserId)}
                    sub={`${timeControlLabel(c.initialTimeSeconds, c.incrementSeconds)} · ${c.rated ? "rated" : "casual"} · ${timeAgo(c.createdAtUtc)}`}
                    actions={
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => act(() => challengesApi.cancel(c.id), "Challenge cancelled")}
                      >
                        Cancel
                      </Button>
                    }
                  />
                ))}
              </ul>
            </div>
          )}
          {(sentChallenges.data ?? []).some((c) => c.status === "Accepted" && c.gameId) && (
            <div>
              <p className="label-mono mb-2">Accepted challenges</p>
              <ul className="panel divide-y divide-border">
                {(sentChallenges.data ?? [])
                  .filter((c) => c.status === "Accepted" && c.gameId)
                  .slice(0, 3)
                  .map((c) => (
                    <PersonRow
                      key={c.id}
                      name={names.name(c.opponentUserId)}
                      sub="accepted your challenge"
                      actions={
                        <Button asChild size="sm">
                          <Link to="/game/$gameId" params={{ gameId: c.gameId! }}>
                            Open game
                          </Link>
                        </Button>
                      }
                    />
                  ))}
              </ul>
            </div>
          )}
        </TabsContent>

        <TabsContent value="requests" className="mt-5 space-y-5">
          <div>
            <p className="label-mono mb-2">Received</p>
            {friendRequests.length > 0 ? (
              <ul className="panel divide-y divide-border">
                {friendRequests.map((r) => (
                  <PersonRow
                    key={r.id}
                    name={r.username}
                    sub={timeAgo(r.createdAt)}
                    actions={
                      <>
                        <Button
                          size="sm"
                          onClick={() => act(() => socialApi.accept(r.id), "Friend added")}
                        >
                          Accept
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => act(() => socialApi.decline(r.id), "Request declined")}
                        >
                          Decline
                        </Button>
                      </>
                    }
                  />
                ))}
              </ul>
            ) : (
              <EmptyState title="No incoming requests" />
            )}
          </div>
          {(outgoing.data?.length ?? 0) > 0 && (
            <div>
              <p className="label-mono mb-2">Sent</p>
              <ul className="panel divide-y divide-border">
                {outgoing.data!.map((o) => (
                  <PersonRow
                    key={o.id}
                    name={names.name(o.userId)}
                    sub={`sent ${timeAgo(o.createdAt)}`}
                    actions={<span className="label-mono">pending</span>}
                  />
                ))}
              </ul>
            </div>
          )}
        </TabsContent>

        <TabsContent value="find" className="mt-5 space-y-4">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Search by username (2+ characters)"
              className="pl-9"
            />
          </div>
          {debounced.length < 2 ? (
            <p className="text-center text-sm text-muted-foreground">
              Type at least two characters.
            </p>
          ) : search.isLoading ? (
            <LoadingBlock />
          ) : search.data && search.data.length > 0 ? (
            <ul className="panel divide-y divide-border">
              {search.data
                .filter((u) => u.userId !== user?.userId)
                .map((u) => (
                  <PersonRow
                    key={u.userId}
                    name={u.username}
                    actions={
                      <>
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => setChallengeTarget({ id: u.userId, name: u.username })}
                        >
                          <Swords className="mr-1.5 h-4 w-4" /> Challenge
                        </Button>
                        {friendIds.has(u.userId) ? (
                          <span className="label-mono self-center">friends</span>
                        ) : pendingIds.has(u.userId) ? (
                          <span className="label-mono self-center">requested</span>
                        ) : (
                          <Button
                            size="sm"
                            onClick={() =>
                              act(() => socialApi.sendRequest(u.userId), "Friend request sent")
                            }
                          >
                            <UserPlus className="mr-1.5 h-4 w-4" /> Add
                          </Button>
                        )}
                      </>
                    }
                  />
                ))}
            </ul>
          ) : (
            <EmptyState title="No players found" text="Check the spelling and try again." />
          )}
        </TabsContent>

        <TabsContent value="blocked" className="mt-5">
          {blocks.data && blocks.data.length > 0 ? (
            <ul className="panel divide-y divide-border">
              {blocks.data.map((b) => (
                <PersonRow
                  key={b.userId}
                  name={b.username}
                  actions={
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => act(() => socialApi.unblock(b.userId), "Unblocked")}
                    >
                      <X className="mr-1.5 h-4 w-4" /> Unblock
                    </Button>
                  }
                />
              ))}
            </ul>
          ) : (
            <EmptyState
              title="Nobody blocked"
              text="Blocked players can't challenge you or send friend requests."
            />
          )}
        </TabsContent>
      </Tabs>

      {challengeTarget && (
        <ChallengeModal
          open
          onClose={() => setChallengeTarget(null)}
          opponentId={challengeTarget.id}
          opponentName={challengeTarget.name}
        />
      )}
    </div>
  );
}
