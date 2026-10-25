/**
 * Token persistence shared by every tab.
 *
 * The backend rotates refresh tokens and treats a reused one as theft (it revokes the whole
 * family). So tabs must never refresh at the same time: see `refreshSession` in http.ts, which
 * serialises refreshes with the Web Locks API and re-reads this store inside the lock.
 */
export interface StoredSession {
  accessToken: string;
  refreshToken: string;
  userId: string;
  username: string;
}

type Listener = (session: StoredSession | null) => void;

const KEY = "echelon.session.v2";

class TokenStore {
  private session: StoredSession | null = null;
  private listeners = new Set<Listener>();
  private hydrated = false;

  constructor() {
    if (typeof window !== "undefined") {
      // Another tab signed in, refreshed or signed out.
      window.addEventListener("storage", (e) => {
        if (e.key !== KEY) return;
        this.session = this.read();
        this.emit();
      });
    }
  }

  private read(): StoredSession | null {
    if (typeof window === "undefined") return null;
    try {
      const raw = window.localStorage.getItem(KEY);
      return raw ? (JSON.parse(raw) as StoredSession) : null;
    } catch {
      return null;
    }
  }

  private emit() {
    this.listeners.forEach((l) => l(this.session));
  }

  /** Always returns what is persisted right now (cheap, and safe across tabs). */
  get(): StoredSession | null {
    if (!this.hydrated) {
      this.session = this.read();
      this.hydrated = true;
    }
    return this.session;
  }

  /** Re-reads storage, for use inside the refresh lock. */
  reload(): StoredSession | null {
    this.session = this.read();
    this.hydrated = true;
    return this.session;
  }

  set(session: StoredSession | null) {
    this.session = session;
    this.hydrated = true;
    try {
      if (session) window.localStorage.setItem(KEY, JSON.stringify(session));
      else window.localStorage.removeItem(KEY);
    } catch {
      /* storage unavailable: keep the in-memory copy */
    }
    this.emit();
  }

  /** Replaces only the tokens, keeping the identity fields. */
  setTokens(accessToken: string, refreshToken: string) {
    const cur = this.get();
    if (!cur) return;
    this.set({ ...cur, accessToken, refreshToken });
  }

  subscribe(listener: Listener) {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }
}

export const tokenStore = new TokenStore();
