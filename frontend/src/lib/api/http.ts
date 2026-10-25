import { API_URL } from "@/lib/config";
import { tokenStore } from "@/lib/api/tokens";

/** Error codes the backend returns, mapped to wording a player can act on. */
const FRIENDLY: Record<string, string> = {
  email_not_verified: "Verify your email first. Check your inbox for the six-digit code.",
  invalid_credentials: "Wrong username/email or password.",
  too_many_attempts: "Too many attempts. Wait a few minutes and try again.",
  rate_limited: "You're going too fast. Try again in a moment.",
  username_taken: "That username is taken.",
  email_taken: "An account with this email already exists.",
  invalid_token: "That token is invalid or has expired.",
  invalid_password: "Password is incorrect.",
  invalid_username: "Usernames are 3–20 characters: letters, numbers and underscores.",
  not_a_player: "You are not a player in this game.",
  not_your_turn: "It isn't your turn.",
  timeout: "Time has run out.",
  game_not_finished: "The game is still in progress.",
  challenge_not_pending: "That challenge is no longer pending.",
  challenge_already_pending: "You already have a pending challenge with this player.",
  rematch_already_pending: "A rematch request is already pending.",
  rematch_not_pending: "That rematch request is no longer pending.",
  blocked_relationship: "You can't do that because one of you has blocked the other.",
  already_friends: "You are already friends.",
  request_exists: "A friend request already exists.",
  user_not_found: "Player not found.",
  already_seeking: "You are already searching for a game.",
  engine_unavailable: "The analysis engine is unavailable right now.",
};

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | undefined;
  readonly retryAfterSeconds: number | undefined;
  readonly details: unknown;

  constructor(
    status: number,
    message: string,
    code?: string,
    details?: unknown,
    retryAfter?: number,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.details = details;
    this.retryAfterSeconds = retryAfter;
  }
}

/** The backend answers with ProblemDetails, `{code,message}`, `{error}` or plain text. */
function toApiError(status: number, statusText: string, body: unknown, retryHeader: string | null) {
  const obj = body && typeof body === "object" ? (body as Record<string, unknown>) : {};
  const str = (v: unknown) => (typeof v === "string" && v.trim() ? v : undefined);
  const code = str(obj["code"]) ?? str(obj["error"]) ?? str(obj["errorCode"]);
  const fromBody =
    str(obj["message"]) ??
    str(obj["errorMessage"]) ??
    str(obj["detail"]) ??
    (typeof body === "string" ? str(body) : undefined);
  const retry =
    typeof obj["retryAfterSeconds"] === "number"
      ? obj["retryAfterSeconds"]
      : retryHeader
        ? Number(retryHeader) || undefined
        : undefined;

  let message = (code && FRIENDLY[code]) ?? fromBody;
  if (!message) {
    if (status === 429) message = FRIENDLY["rate_limited"]!;
    else if (status === 401) message = "Please sign in again.";
    else if (status === 403) message = "You don't have permission to do that.";
    else if (status === 404) message = "Not found.";
    else if (status === 503) message = "A service is temporarily unavailable. Try again shortly.";
    else if (status >= 500) message = "The server had a problem. Try again shortly.";
    else message = `${status} ${statusText}`.trim();
  }
  if (status === 429 && retry && message === FRIENDLY["rate_limited"])
    message = `${message} (${retry}s)`;
  return new ApiError(status, message, code, body, retry);
}

/* ---------------- Refresh (serialised across tabs) ---------------- */

let refreshInFlight: Promise<boolean> | null = null;

async function callRefresh(): Promise<boolean> {
  const current = tokenStore.reload();
  if (!current) return false;
  let res: Response;
  try {
    res = await fetch(`${API_URL}/auth/refresh`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ refreshToken: current.refreshToken }),
    });
  } catch {
    throw new ApiError(0, "Can't reach the server.");
  }
  if (!res.ok) {
    // The refresh token is spent, revoked or expired: the session is over.
    if (res.status === 400 || res.status === 401 || res.status === 403) tokenStore.set(null);
    return false;
  }
  const data = (await res.json()) as { accessToken: string; refreshToken: string };
  tokenStore.setTokens(data.accessToken, data.refreshToken);
  return true;
}

/**
 * Gets a fresh access token. `failedToken` is the one that just got a 401: if another tab already
 * replaced it while we waited for the lock, we simply use theirs instead of refreshing again.
 */
export function refreshSession(failedToken?: string): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const run = async () => {
        const latest = tokenStore.reload();
        if (!latest) return false;
        if (failedToken && latest.accessToken !== failedToken) return true;
        return callRefresh();
      };
      if (typeof navigator !== "undefined" && "locks" in navigator) {
        return await navigator.locks.request("echelon-refresh", run);
      }
      return await run();
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

/* ---------------- Requests ---------------- */

export interface RequestOptions {
  body?: unknown;
  auth?: boolean;
  signal?: AbortSignal;
  query?: Record<string, string | number | boolean | undefined | null>;
  /** Response parsing. PGN comes back as plain text. */
  as?: "json" | "text";
}

function buildUrl(path: string, query?: RequestOptions["query"]) {
  const url = `${API_URL}${path}`;
  if (!query) return url;
  const qs = new URLSearchParams();
  Object.entries(query).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") qs.set(k, String(v));
  });
  const s = qs.toString();
  return s ? `${url}?${s}` : url;
}

export async function request<T>(
  method: string,
  path: string,
  opts: RequestOptions = {},
): Promise<T> {
  const { body, auth = true, signal, query, as = "json" } = opts;

  const send = async (token: string | undefined) => {
    const init: RequestInit = {
      method,
      headers: {
        ...(body !== undefined ? { "content-type": "application/json" } : {}),
        ...(token ? { authorization: `Bearer ${token}` } : {}),
      },
    };
    if (body !== undefined) init.body = JSON.stringify(body);
    if (signal) init.signal = signal;
    try {
      return await fetch(buildUrl(path, query), init);
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") throw e;
      throw new ApiError(0, "Can't reach the server. Check your connection.");
    }
  };

  // Always send the token when we have one: some "anonymous" routes still personalise their reply.
  const startToken = tokenStore.get()?.accessToken;
  let res = await send(auth || startToken ? startToken : undefined);

  if (res.status === 401 && startToken) {
    const ok = await refreshSession(startToken);
    if (ok) res = await send(tokenStore.get()?.accessToken);
  }

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    let parsed: unknown = text;
    try {
      parsed = text ? JSON.parse(text) : undefined;
    } catch {
      /* plain text body */
    }
    throw toApiError(res.status, res.statusText, parsed, res.headers.get("retry-after"));
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (as === "text") return text as T;
  return (text ? JSON.parse(text) : undefined) as T;
}

export const http = {
  get: <T>(path: string, opts?: RequestOptions) => request<T>("GET", path, opts),
  post: <T>(path: string, body?: unknown, opts?: RequestOptions) =>
    request<T>("POST", path, { ...opts, body }),
  put: <T>(path: string, body?: unknown, opts?: RequestOptions) =>
    request<T>("PUT", path, { ...opts, body }),
  del: <T>(path: string, body?: unknown, opts?: RequestOptions) =>
    request<T>("DELETE", path, { ...opts, body }),
};

export function errorMessage(
  e: unknown,
  fallback = "Something went wrong. Please try again.",
): string {
  return e instanceof ApiError ? e.message : fallback;
}
