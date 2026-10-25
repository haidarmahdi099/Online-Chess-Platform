import * as signalR from "@microsoft/signalr";
import { HUB_URL } from "@/lib/config";
import { tokenStore } from "@/lib/api/tokens";
import type { GameClock, GameDetails, MoveResult, SpectatorConsentResult } from "@/lib/api/types";

export type HubStatus = "disconnected" | "connecting" | "connected" | "reconnecting";

/** Server → client events, exactly as the Realtime service emits them (camelCase JSON). */
export interface HubEvents {
  "game:snapshot": GameDetails;
  "game:matched": {
    gameId: string;
    whitePlayerId: string;
    blackPlayerId: string;
    initialSeconds: number;
    incrementSeconds: number;
    category: string;
    rated: boolean;
  };
  "game:start": { gameId: string; spectatorsAllowed: boolean; consentRejected: boolean };
  "game:spectatorConsent": { gameId: string; result: SpectatorConsentResult };
  "game:move": {
    gameId: string;
    from: string;
    to: string;
    promotion: string | null;
    result: MoveResult;
  };
  "game:clock": GameClock;
  "game:end": {
    gameId: string;
    type: string;
    result: {
      winnerId: string | null;
      result: string | null;
      endReason: string | null;
      rated: boolean;
    };
  };
  "game:aborted": { gameId: string; reason: string };
  "game:drawOffered": { gameId: string; offeredBy: string };
  "game:drawDeclined": { gameId: string; declinedBy: string; reason: string };
  presence: { gameId: string; onlineUsers: number };
  "chat:message": { gameId: string; userId: string; message: string; sentAtUtc: string };
  "tournament:event": { tournamentId: string; eventType: string } & Record<string, unknown>;
}

type Handler<K extends keyof HubEvents> = (payload: HubEvents[K]) => void;

const EVENT_NAMES: (keyof HubEvents)[] = [
  "game:snapshot",
  "game:matched",
  "game:start",
  "game:spectatorConsent",
  "game:move",
  "game:clock",
  "game:end",
  "game:aborted",
  "game:drawOffered",
  "game:drawDeclined",
  "presence",
  "chat:message",
  "tournament:event",
];

/**
 * One SignalR connection for the whole app.
 *
 *  - The access token is read through a factory, so reconnects always use the latest one.
 *  - A connection keeps the identity it had when it started, so after every token refresh we
 *    tell the hub (`auth:refresh`), otherwise hub commands start failing after 15 minutes.
 *  - Rooms (a game, tournaments) are remembered and re-joined after a reconnect.
 */
class HubClient {
  private connection: signalR.HubConnection | null = null;
  private listeners = new Map<string, Set<(payload: never) => void>>();
  private statusListeners = new Set<() => void>();
  private _status: HubStatus = "disconnected";
  private wanted = false;
  private starting: Promise<void> | null = null;
  private room: { gameId: string; spectator: boolean } | null = null;
  private tournaments = new Set<string>();
  private unsubscribeTokens: (() => void) | null = null;

  get status() {
    return this._status;
  }

  private setStatus(s: HubStatus) {
    if (this._status === s) return;
    this._status = s;
    this.statusListeners.forEach((l) => l());
  }

  subscribeStatus = (listener: () => void) => {
    this.statusListeners.add(listener);
    return () => {
      this.statusListeners.delete(listener);
    };
  };

  getStatus = () => this._status;

  on<K extends keyof HubEvents>(event: K, handler: Handler<K>) {
    let set = this.listeners.get(event);
    if (!set) {
      set = new Set();
      this.listeners.set(event, set);
    }
    set.add(handler as (payload: never) => void);
    return () => {
      set.delete(handler as (payload: never) => void);
    };
  }

  private dispatch(event: string, payload: unknown) {
    this.listeners.get(event)?.forEach((h) => (h as (p: unknown) => void)(payload));
  }

  private build() {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => tokenStore.get()?.accessToken ?? "" })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 15000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    EVENT_NAMES.forEach((name) =>
      connection.on(name, (payload: unknown) => this.dispatch(name, payload)),
    );

    connection.onreconnecting(() => this.setStatus("reconnecting"));
    connection.onreconnected(async () => {
      this.setStatus("connected");
      await this.restoreRooms();
    });
    connection.onclose(() => {
      this.setStatus("disconnected");
      // Automatic reconnect gave up (long outage): keep trying while someone still needs us.
      if (this.wanted) window.setTimeout(() => void this.start(), 5000);
    });
    return connection;
  }

  /** Connects (idempotent). Safe to call whenever the user is signed in. */
  start(): Promise<void> {
    this.wanted = true;
    if (!this.unsubscribeTokens) {
      let last = tokenStore.get()?.accessToken;
      this.unsubscribeTokens = tokenStore.subscribe((session) => {
        const next = session?.accessToken;
        if (
          next &&
          next !== last &&
          this.connection?.state === signalR.HubConnectionState.Connected
        ) {
          void this.connection.invoke("auth:refresh", next).catch(() => undefined);
        }
        last = next;
      });
    }
    if (this.connection?.state === signalR.HubConnectionState.Connected) return Promise.resolve();
    if (this.starting) return this.starting;

    this.starting = (async () => {
      this.connection ??= this.build();
      this.setStatus("connecting");
      try {
        await this.connection.start();
        this.setStatus("connected");
        await this.restoreRooms();
      } catch {
        this.setStatus("disconnected");
        if (this.wanted) window.setTimeout(() => void this.start(), 4000);
      } finally {
        this.starting = null;
      }
    })();
    return this.starting;
  }

  async stop() {
    this.wanted = false;
    this.room = null;
    this.tournaments.clear();
    this.unsubscribeTokens?.();
    this.unsubscribeTokens = null;
    const c = this.connection;
    this.connection = null;
    if (c) await c.stop().catch(() => undefined);
    this.setStatus("disconnected");
  }

  private async restoreRooms() {
    if (this.room)
      await this.invokeNow(
        this.room.spectator ? "game:joinSpectator" : "game:join",
        this.room.gameId,
      ).catch(() => undefined);
    for (const id of this.tournaments)
      await this.invokeNow("tournament:join", id).catch(() => undefined);
  }

  /** Resolves once connected, or rejects after the timeout. */
  async ready(timeoutMs = 8000) {
    if (this._status === "connected") return;
    void this.start();
    await new Promise<void>((resolve, reject) => {
      const timer = window.setTimeout(() => {
        off();
        reject(new Error("Realtime connection timed out."));
      }, timeoutMs);
      const off = this.subscribeStatus(() => {
        if (this._status === "connected") {
          window.clearTimeout(timer);
          off();
          resolve();
        }
      });
    });
  }

  private invokeNow<T = void>(method: string, ...args: unknown[]) {
    if (!this.connection) return Promise.reject(new Error("Not connected."));
    return this.connection.invoke<T>(method, ...args);
  }

  async invoke<T = void>(method: string, ...args: unknown[]): Promise<T> {
    await this.ready();
    try {
      return await this.invokeNow<T>(method, ...args);
    } catch (e) {
      throw new Error(hubErrorMessage(e));
    }
  }

  async joinGame(gameId: string, spectator: boolean) {
    this.room = { gameId, spectator };
    await this.invoke(spectator ? "game:joinSpectator" : "game:join", gameId);
  }

  async leaveGame(gameId: string) {
    if (this.room?.gameId === gameId) this.room = null;
    if (this._status !== "connected") return;
    await this.invokeNow("game:leave", gameId).catch(() => undefined);
  }

  async joinTournament(id: string) {
    this.tournaments.add(id);
    await this.invoke("tournament:join", id);
  }

  async leaveTournament(id: string) {
    this.tournaments.delete(id);
    if (this._status !== "connected") return;
    await this.invokeNow("tournament:leave", id).catch(() => undefined);
  }
}

/** SignalR wraps hub errors as "An unexpected error occurred invoking 'x' on the server. <message>". */
function hubErrorMessage(e: unknown) {
  const raw = e instanceof Error ? e.message : String(e);
  const m = /HubException:\s*(.*)$/s.exec(raw);
  return (m?.[1] ?? raw).trim() || "The server rejected that action.";
}

export const hub = new HubClient();
