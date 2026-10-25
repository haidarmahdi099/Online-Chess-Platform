import { useQuery } from "@tanstack/react-query";
import { challengesApi, ratingsApi, rematchApi, socialApi, usersApi } from "@/lib/api/endpoints";
import { useAuth } from "@/lib/state/auth";

/* ---- Usernames: the backend hands out bare GUIDs, so every list resolves names here ---- */

const nameCache = new Map<string, string>();

export const fallbackName = (id: string) => `Player ${id.slice(0, 4)}`;

/**
 * Resolves user ids to usernames in batches of 100 (POST /users/lookup) and caches them for the
 * session. `name(id)` is safe to call before the lookup finishes.
 */
export function useUsernames(ids: (string | null | undefined)[]) {
  const unique = [...new Set(ids.filter((i): i is string => !!i))].sort();
  const key = unique.join(",");
  const query = useQuery({
    queryKey: ["usernames", key],
    enabled: unique.length > 0,
    staleTime: Infinity,
    queryFn: async () => {
      const missing = unique.filter((id) => !nameCache.has(id));
      for (let i = 0; i < missing.length; i += 100) {
        const found = await usersApi.lookup(missing.slice(i, i + 100));
        found.forEach((u) => nameCache.set(u.userId, u.username));
      }
      return true;
    },
  });
  const name = (id: string | null | undefined) =>
    id ? (nameCache.get(id) ?? fallbackName(id)) : "—";
  return { name, ready: unique.length === 0 || query.isSuccess };
}

export function rememberUsername(id: string, username: string) {
  nameCache.set(id, username);
}

/* ---- Ratings ---- */

export const usePlayerRatings = (playerId: string | undefined) =>
  useQuery({
    queryKey: ["ratings", playerId],
    queryFn: () => ratingsApi.player(playerId!),
    enabled: !!playerId,
    staleTime: 30_000,
  });

/* ---- Inbox: challenges, rematches and friend requests addressed to me ---- */

export function useInbox() {
  const { status } = useAuth();
  const enabled = status === "authenticated";
  const refetchInterval = 12_000;
  const challenges = useQuery({
    queryKey: ["inbox", "challenges"],
    queryFn: challengesApi.incoming,
    enabled,
    refetchInterval,
  });
  const rematches = useQuery({
    queryKey: ["inbox", "rematches"],
    queryFn: rematchApi.incoming,
    enabled,
    refetchInterval,
  });
  const friendRequests = useQuery({
    queryKey: ["inbox", "friends"],
    queryFn: socialApi.incoming,
    enabled,
    refetchInterval,
  });
  const count =
    (challenges.data?.length ?? 0) +
    (rematches.data?.length ?? 0) +
    (friendRequests.data?.length ?? 0);
  return {
    challenges: challenges.data ?? [],
    rematches: rematches.data ?? [],
    friendRequests: friendRequests.data ?? [],
    count,
  };
}
