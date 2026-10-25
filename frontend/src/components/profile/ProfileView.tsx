import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { motion } from "motion/react";
import { Ban, Swords, UserCheck, UserMinus, UserPlus } from "lucide-react";
import { useMemo, useState } from "react";
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { toast } from "sonner";
import { Avatar, ErrorState, LoadingBlock, Pill } from "@/components/common/bits";
import { ChallengeModal } from "@/components/game/ChallengeModal";
import { Button } from "@/components/ui/button";
import { gamesApi, ratingsApi, socialApi, usersApi } from "@/lib/api/endpoints";
import { rememberUsername, usePlayerRatings } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import type { RatingCategory } from "@/lib/api/types";
import { CATEGORY_LABEL, shortDate } from "@/lib/chess-utils";
import { useAuth } from "@/lib/state/auth";
import { cn } from "@/lib/utils";

const CATS: RatingCategory[] = ["bullet", "blitz", "rapid", "classical", "puzzle", "chess"];

export function ProfileView({ username }: { username: string }) {
  const { user, status } = useAuth();
  const queryClient = useQueryClient();
  const [category, setCategory] = useState<RatingCategory>("chess");
  const [challenge, setChallenge] = useState(false);

  const profile = useQuery({
    queryKey: ["user", username.toLowerCase()],
    queryFn: async () => {
      const p = await usersApi.byUsername(username);
      rememberUsername(p.userId, p.username);
      return p;
    },
  });
  const id = profile.data?.userId;
  const isMe = !!user && !!id && user.userId.toLowerCase() === id.toLowerCase();
  const ratings = usePlayerRatings(id);
  const stats = useQuery({
    queryKey: ["stats", id],
    queryFn: () => gamesApi.playerStats(id!),
    enabled: !!id,
  });
  const history = useQuery({
    queryKey: ["ratings", id, "history", category],
    queryFn: () => ratingsApi.history(id!, category, 1, 100),
    enabled: !!id,
  });

  // Relationship with me, to show the right actions.
  const authed = status === "authenticated" && !isMe;
  const friends = useQuery({
    queryKey: ["social", "friends"],
    queryFn: socialApi.friends,
    enabled: authed,
  });
  const outgoing = useQuery({
    queryKey: ["social", "outgoing"],
    queryFn: socialApi.outgoing,
    enabled: authed,
  });
  const blocks = useQuery({
    queryKey: ["social", "blocks"],
    queryFn: socialApi.blocks,
    enabled: authed,
  });
  const isFriend = !!friends.data?.some((f) => f.userId === id);
  const requested = !!outgoing.data?.some((o) => o.userId === id);
  const blocked = !!blocks.data?.some((b) => b.userId === id);

  const refreshSocial = () => void queryClient.invalidateQueries({ queryKey: ["social"] });
  const act = (fn: () => Promise<unknown>, ok: string) =>
    fn().then(
      () => {
        toast.success(ok);
        refreshSocial();
      },
      (e: unknown) => toast.error(errorMessage(e)),
    );

  const chart = useMemo(
    () =>
      [...(history.data?.items ?? [])]
        .sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc))
        .map((h, i) => ({
          i,
          rating: h.newRating,
          label: shortDate(h.createdAtUtc),
          change: h.change,
        })),
    [history.data],
  );

  if (profile.isLoading) return <LoadingBlock className="py-32" />;
  if (profile.isError || !profile.data) {
    return (
      <div className="mx-auto max-w-lg px-4 py-20">
        <ErrorState message={profile.error ? errorMessage(profile.error) : "Player not found."} />
      </div>
    );
  }
  const p = profile.data;
  const s = stats.data?.stats;
  const winRate = s && s.finishedGames > 0 ? Math.round((s.wins / s.finishedGames) * 100) : null;

  return (
    <div className="mx-auto max-w-[1100px] px-4 py-10 sm:px-6 sm:py-14">
      <motion.div
        initial={{ opacity: 0, y: 14 }}
        animate={{ opacity: 1, y: 0 }}
        className="panel flex flex-wrap items-center justify-between gap-5 p-5 sm:p-7"
      >
        <div className="flex min-w-0 items-center gap-4">
          <Avatar name={p.username} className="h-16 w-16 text-xl" />
          <div className="min-w-0">
            <h1 className="truncate font-display text-3xl font-bold uppercase">{p.username}</h1>
            <p className="label-mono mt-1.5">Joined {shortDate(p.createdAt)}</p>
            <div className="mt-2 flex gap-2">
              {isMe && <Pill tone="primary">You</Pill>}
              {isFriend && <Pill tone="good">Friend</Pill>}
              {blocked && <Pill tone="bad">Blocked</Pill>}
            </div>
          </div>
        </div>
        {authed && (
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => setChallenge(true)} disabled={blocked}>
              <Swords className="mr-2 h-4 w-4" /> Challenge
            </Button>
            {isFriend ? (
              <Button
                variant="secondary"
                onClick={() => void act(() => socialApi.removeFriend(p.userId), "Friend removed")}
              >
                <UserMinus className="mr-2 h-4 w-4" /> Unfriend
              </Button>
            ) : requested ? (
              <Button variant="secondary" disabled>
                <UserCheck className="mr-2 h-4 w-4" /> Request sent
              </Button>
            ) : (
              <Button
                variant="secondary"
                disabled={blocked}
                onClick={() =>
                  void act(() => socialApi.sendRequest(p.userId), "Friend request sent")
                }
              >
                <UserPlus className="mr-2 h-4 w-4" /> Add friend
              </Button>
            )}
            <Button
              variant="outline"
              onClick={() =>
                void act(
                  () => (blocked ? socialApi.unblock(p.userId) : socialApi.block(p.userId)),
                  blocked ? "Unblocked" : "Player blocked",
                )
              }
            >
              <Ban className="mr-2 h-4 w-4" /> {blocked ? "Unblock" : "Block"}
            </Button>
          </div>
        )}
        {isMe && (
          <Button asChild variant="secondary">
            <Link to="/settings">Edit account</Link>
          </Button>
        )}
      </motion.div>

      <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[
          { l: "Games", v: s?.totalGames ?? "–" },
          { l: "Wins", v: s?.wins ?? "–" },
          { l: "Draws", v: s?.draws ?? "–" },
          { l: "Win rate", v: winRate === null ? "–" : `${winRate}%` },
        ].map((x) => (
          <div key={x.l} className="panel p-4">
            <p className="label-mono">{x.l}</p>
            <p className="mt-1.5 font-mono text-2xl font-bold tabular-nums">{x.v}</p>
          </div>
        ))}
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="panel p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="font-display text-lg font-bold uppercase">Rating history</h2>
            <div className="flex flex-wrap gap-1.5">
              {CATS.map((c) => (
                <button
                  key={c}
                  onClick={() => setCategory(c)}
                  className={cn(
                    "rounded-md px-2.5 py-1 font-mono text-[11px] uppercase tracking-[0.1em]",
                    c === category
                      ? "bg-primary text-primary-foreground"
                      : "bg-secondary text-muted-foreground hover:text-foreground",
                  )}
                >
                  {CATEGORY_LABEL[c]}
                </button>
              ))}
            </div>
          </div>
          <div className="mt-4 h-64">
            {history.isLoading ? (
              <LoadingBlock />
            ) : chart.length < 2 ? (
              <p className="grid h-full place-items-center text-sm text-muted-foreground">
                Play more rated games to build a rating curve.
              </p>
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={chart} margin={{ top: 8, right: 12, bottom: 0, left: -12 }}>
                  <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" />
                  <XAxis
                    dataKey="label"
                    tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                    minTickGap={32}
                  />
                  <YAxis
                    domain={["dataMin - 20", "dataMax + 20"]}
                    tick={{ fill: "var(--muted-foreground)", fontSize: 11 }}
                  />
                  <Tooltip
                    contentStyle={{
                      background: "var(--surface)",
                      border: "1px solid var(--border)",
                      borderRadius: 8,
                      fontSize: 12,
                    }}
                    formatter={(v: number) => [v, "Rating"]}
                  />
                  <Line
                    type="monotone"
                    dataKey="rating"
                    stroke="var(--primary)"
                    strokeWidth={2.5}
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>

        <div className="panel p-5">
          <h2 className="font-display text-lg font-bold uppercase">Ratings</h2>
          {ratings.isLoading ? (
            <LoadingBlock />
          ) : ratings.data ? (
            <ul className="mt-3 divide-y divide-border">
              {CATS.map((c) => {
                const r = ratings.data[c];
                return (
                  <li key={c} className="flex items-center justify-between py-3">
                    <span>
                      <span className="block text-sm font-semibold">{CATEGORY_LABEL[c]}</span>
                      <span className="label-mono">{r.gamesPlayed} games</span>
                    </span>
                    <span className="font-mono text-xl font-bold tabular-nums">{r.rating}</span>
                  </li>
                );
              })}
            </ul>
          ) : (
            <p className="mt-3 text-sm text-muted-foreground">Ratings unavailable.</p>
          )}
        </div>
      </div>

      {authed && (
        <ChallengeModal
          open={challenge}
          onClose={() => setChallenge(false)}
          opponentId={p.userId}
          opponentName={p.username}
        />
      )}
    </div>
  );
}
