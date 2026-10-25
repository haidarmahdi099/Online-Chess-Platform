/**
 * Challenges I've sent and am waiting on. The ChallengeWatcher polls each one
 * (GET /challenges/{id}) and tells me when it is accepted, declined or expired.
 * Kept in sessionStorage so a page refresh doesn't lose track of them.
 */
const KEY = "echelon.watchedChallenges";
const listeners = new Set<() => void>();
let cache: string[] | null = null;

function read(): string[] {
  if (cache) return cache;
  try {
    const raw = sessionStorage.getItem(KEY);
    cache = raw ? (JSON.parse(raw) as string[]) : [];
  } catch {
    cache = [];
  }
  return cache;
}

function write(ids: string[]) {
  cache = ids;
  try {
    sessionStorage.setItem(KEY, JSON.stringify(ids));
  } catch {
    /* storage unavailable — the in-memory copy still works for this tab */
  }
  listeners.forEach((l) => l());
}

export const watchedChallenges = {
  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
  getSnapshot: read,
  getServerSnapshot: () => [] as string[],
  add(id: string) {
    const ids = read();
    if (!ids.includes(id)) write([...ids, id]);
  },
  remove(id: string) {
    write(read().filter((x) => x !== id));
  },
};
