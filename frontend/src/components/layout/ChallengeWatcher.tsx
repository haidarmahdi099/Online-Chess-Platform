import { useQueries } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useSyncExternalStore } from "react";
import { toast } from "sonner";
import { challengesApi } from "@/lib/api/endpoints";
import { fallbackName, useUsernames } from "@/lib/api/hooks";
import { watchedChallenges } from "@/lib/challenges/watch";

/**
 * Follows the challenges I sent: when the opponent accepts, offers a one-click jump into the
 * game; when they decline (or it expires/is cancelled) says so. Renders nothing.
 */
export function ChallengeWatcher() {
  const ids = useSyncExternalStore(
    watchedChallenges.subscribe,
    watchedChallenges.getSnapshot,
    watchedChallenges.getServerSnapshot,
  );
  const navigate = useNavigate();

  const results = useQueries({
    queries: ids.map((id) => ({
      queryKey: ["challenge", id],
      queryFn: () => challengesApi.get(id),
      refetchInterval: 5_000,
      retry: false,
    })),
  });

  const opponents = results.map((r) => r.data?.opponentUserId);
  const { name } = useUsernames(opponents);

  useEffect(() => {
    results.forEach((r, i) => {
      const id = ids[i]!;
      if (r.isError) {
        // 404 / 403: it's gone, stop polling it.
        watchedChallenges.remove(id);
        return;
      }
      const c = r.data;
      if (!c || c.status === "Pending") return;
      const who = c.opponentUserId ? name(c.opponentUserId) : fallbackName(id);
      watchedChallenges.remove(id);
      if (c.status === "Accepted" && c.gameId) {
        const gameId = c.gameId;
        toast.success(`${who} accepted your challenge`, {
          duration: 15_000,
          action: {
            label: "Join game",
            onClick: () => void navigate({ to: "/game/$gameId", params: { gameId } }),
          },
        });
      } else if (c.status === "Declined") {
        toast.info(`${who} declined your challenge.`);
      } else if (c.status === "Expired") {
        toast.info(`Your challenge to ${who} expired.`);
      }
    });
  }, [results, ids, name, navigate]);

  return null;
}
