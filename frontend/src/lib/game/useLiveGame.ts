import { useQueryClient } from "@tanstack/react-query";
import { Chess, type Square as ChessSquare } from "chess.js";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { gamesApi } from "@/lib/api/endpoints";
import { ApiError, errorMessage } from "@/lib/api/http";
import type { ChatMessage, GameDetails, MoveHistoryItem } from "@/lib/api/types";
import { capturedFromBoard, piecesFromFen, START_FEN, type BoardPiece } from "@/lib/game/board";
import { hub } from "@/lib/realtime/hub";
import { useAuth } from "@/lib/state/auth";
import type { PieceColor } from "@/lib/types";

export type Role = "white" | "black" | "spectator";
export type Promo = "q" | "r" | "b" | "n";

interface ClockBase {
  whiteMs: number;
  blackMs: number;
  whiteToMove: boolean;
  running: boolean;
  /** performance.now() when the values above were true. */
  at: number;
  /** Set when frozen from a live game (its final values are trustworthy). */
  exact?: boolean;
}

const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

function sanFor(prevFen: string, from: string, to: string, promotion: string | null) {
  try {
    const c = new Chess(prevFen);
    const m = c.move({ from, to, ...(promotion ? { promotion } : {}) });
    return m.san;
  } catch {
    return `${from}${to}`;
  }
}

/**
 * Server-authoritative game controller.
 *
 * The backend validates every move, owns the clocks and decides the result. This hook only:
 *  - loads the game and its move history over REST,
 *  - joins the SignalR room and applies the events it receives,
 *  - shows a move instantly (optimistically) and rolls it back if the server rejects it.
 * chess.js is used purely for UX: legal-move hints, promotion detection and SAN for new moves.
 */
export function useLiveGame(gameId: string) {
  const { user, status: authStatus } = useAuth();
  const queryClient = useQueryClient();
  const myId = user?.userId ?? null;

  const [game, setGame] = useState<GameDetails | null>(null);
  const [moves, setMoves] = useState<MoveHistoryItem[]>([]);
  const [initialFen, setInitialFen] = useState(START_FEN);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [isPlayer, setIsPlayer] = useState(false);
  const [live, setLive] = useState(false); // realtime joined
  const [presence, setPresence] = useState(0);
  const [chat, setChat] = useState<ChatMessage[]>([]);
  const [drawOfferBy, setDrawOfferBy] = useState<string | null>(null);
  const [consentAnswered, setConsentAnswered] = useState(false);

  const [selected, setSelected] = useState<string | null>(null);
  const [viewPly, setViewPly] = useState<number | null>(null);
  const [flipped, setFlipped] = useState(false);
  const [pendingPromotion, setPendingPromotion] = useState<{
    from: string;
    to: string;
    color: PieceColor;
  } | null>(null);
  const [optimistic, setOptimistic] = useState<{ from: string; to: string; fen: string } | null>(
    null,
  );

  const clockRef = useRef<ClockBase | null>(null);
  const [, setTick] = useState(0);
  const movesRef = useRef(moves);
  movesRef.current = moves;
  const gameRef = useRef(game);
  gameRef.current = game;
  const initialFenRef = useRef(initialFen);
  initialFenRef.current = initialFen;

  /* ---------- Loading ---------- */

  const loadMoves = useCallback(async () => {
    try {
      const res = await gamesApi.moves(gameId);
      setInitialFen(res.initialFen || START_FEN);
      setMoves(res.moves);
    } catch {
      /* spectating a private game, or a transient error: keep what we have */
    }
  }, [gameId]);

  const loadGame = useCallback(async () => {
    try {
      const g = await gamesApi.get(gameId);
      setGame(g);
      setIsPlayer(true);
      return g;
    } catch (e) {
      if (e instanceof ApiError && (e.status === 404 || e.status === 403)) {
        const g = await gamesApi.getPublic(gameId);
        setGame(g);
        setIsPlayer(false);
        return g;
      }
      throw e;
    }
  }, [gameId]);

  const setClockFrom = useCallback(
    (whiteMs: number, blackMs: number, whiteToMove: boolean, running: boolean) => {
      clockRef.current = { whiteMs, blackMs, whiteToMove, running, at: performance.now() };
      setTick((t) => t + 1);
    },
    [],
  );

  const loadClock = useCallback(
    async (knownStatus?: string) => {
      try {
        const c = await gamesApi.clock(gameId);
        const live = (knownStatus ?? gameRef.current?.status) === "Live";
        setClockFrom(
          c.whiteRemainingMilliseconds,
          c.blackRemainingMilliseconds,
          c.whiteToMove,
          live && !!c.turnStartedAt,
        );
      } catch {
        /* clock arrives over realtime as well */
      }
    },
    [gameId, setClockFrom],
  );

  useEffect(() => {
    if (authStatus === "loading") return;
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    (async () => {
      try {
        const g = await loadGame();
        if (cancelled) return;
        if (authStatus === "authenticated") {
          await Promise.all([loadMoves(), loadClock(g.status)]);
        } else {
          setClockFrom(g.initialTimeSeconds * 1000, g.initialTimeSeconds * 1000, true, false);
        }
      } catch (e) {
        if (!cancelled) setLoadError(errorMessage(e, "Couldn't load this game."));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [authStatus, loadGame, loadMoves, loadClock, setClockFrom]);

  /* ---------- Realtime ---------- */

  const applyMove = useCallback(
    (from: string, to: string, promotion: string | null, fen: string) => {
      setMoves((prev) => {
        if (prev.length && prev[prev.length - 1]!.fen === fen) return prev; // already applied
        const prevFen = prev.length ? prev[prev.length - 1]!.fen : initialFenRef.current;
        const ply = prev.length + 1;
        const item: MoveHistoryItem = {
          ply,
          moveNumber: Math.ceil(ply / 2),
          color: ply % 2 === 1 ? "White" : "Black",
          from,
          to,
          promotion,
          san: sanFor(prevFen, from, to, promotion),
          fen,
          playedAt: new Date().toISOString(),
        };
        return [...prev, item];
      });
    },
    [],
  );

  useEffect(() => {
    if (authStatus !== "authenticated" || loading || loadError || !game) return;
    const g0 = gameRef.current;
    if (!g0) return;
    let disposed = false;

    const offs = [
      hub.on("game:snapshot", (s) => {
        if (!same(s.id, gameId)) return;
        setGame(s);
      }),
      hub.on("game:start", (e) => {
        if (!same(e.gameId, gameId)) return;
        setGame((g) =>
          g ? { ...g, status: "Live", startedAt: g.startedAt ?? new Date().toISOString() } : g,
        );
        void loadGame();
        void loadClock("Live");
        if (e.consentRejected) toast.info("Spectators are not allowed in this game.");
      }),
      hub.on("game:clock", (c) => {
        if (!same(c.gameId, gameId)) return;
        setClockFrom(
          c.whiteRemainingMilliseconds,
          c.blackRemainingMilliseconds,
          c.whiteToMove,
          gameRef.current?.status === "Live" && !!c.turnStartedAt,
        );
      }),
      hub.on("game:move", (e) => {
        if (!same(e.gameId, gameId)) return;
        const r = e.result;
        if (!r?.success || !r.fen) return;
        applyMove(e.from, e.to, e.promotion, r.fen);
        setGame((g) => (g ? { ...g, fen: r.fen! } : g));
        setOptimistic(null);
        setDrawOfferBy((by) => (r.drawOfferDeclined ? null : by));
      }),
      hub.on("game:end", (e) => {
        if (!same(e.gameId, gameId)) return;
        freezeClock();
        setGame((g) =>
          g
            ? {
                ...g,
                status: "Finished",
                winnerId: e.result.winnerId,
                endReason: e.result.endReason,
              }
            : g,
        );
        setDrawOfferBy(null);
        setOptimistic(null);
        setSelected(null);
        void loadGame();
        // Ratings are applied asynchronously (Kafka), so refresh them a little later.
        [1500, 5000].forEach((ms) =>
          window.setTimeout(() => {
            void queryClient.invalidateQueries({ queryKey: ["ratings"] });
            void queryClient.invalidateQueries({ queryKey: ["games"] });
          }, ms),
        );
      }),
      hub.on("game:aborted", (e) => {
        if (!same(e.gameId, gameId)) return;
        freezeClock();
        setGame((g) => (g ? { ...g, status: "Aborted", endReason: e.reason } : g));
        setDrawOfferBy(null);
        setOptimistic(null);
      }),
      hub.on("game:drawOffered", (e) => {
        if (same(e.gameId, gameId)) setDrawOfferBy(e.offeredBy);
      }),
      hub.on("game:drawDeclined", (e) => {
        if (!same(e.gameId, gameId)) return;
        setDrawOfferBy(null);
        if (e.reason === "declined" && myIdRef.current && !same(e.declinedBy, myIdRef.current)) {
          toast.info("Your draw offer was declined.");
        }
      }),
      hub.on("presence", (e) => {
        if (same(e.gameId, gameId)) setPresence(e.onlineUsers);
      }),
      hub.on("chat:message", (e) => {
        if (!same(e.gameId, gameId)) return;
        setChat((c) => [
          ...c,
          {
            id: `${e.sentAtUtc}-${c.length}`,
            userId: e.userId,
            message: e.message,
            createdAt: e.sentAtUtc,
          },
        ]);
      }),
    ];

    const finished = g0.status === "Finished" || g0.status === "Aborted";
    hub
      .joinGame(gameId, !isPlayer)
      .then(() => !disposed && setLive(true))
      .catch((e: unknown) => {
        // A finished game can still be reviewed from REST data alone.
        if (!finished && !disposed) toast.error(errorMessage(e, "Couldn't join the live game."));
      });

    return () => {
      disposed = true;
      offs.forEach((off) => off());
      setLive(false);
      void hub.leaveGame(gameId);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, loading, loadError, gameId, isPlayer, !!game]);

  const myIdRef = useRef(myId);
  myIdRef.current = myId;

  function freezeClock() {
    const c = clockRef.current;
    if (!c) return;
    const { whiteMs, blackMs } = readClock(c);
    clockRef.current = {
      ...c,
      whiteMs,
      blackMs,
      running: false,
      at: performance.now(),
      exact: true,
    };
    setTick((t) => t + 1);
  }

  // Chat history for players.
  useEffect(() => {
    if (!isPlayer || authStatus !== "authenticated") return;
    gamesApi
      .chat(gameId)
      .then(setChat)
      .catch(() => undefined);
  }, [gameId, isPlayer, authStatus]);

  /* ---------- Derived state ---------- */

  const status = game?.status ?? "Waiting";
  const role: Role =
    !game || !myId
      ? "spectator"
      : same(game.whitePlayerId, myId)
        ? "white"
        : same(game.blackPlayerId, myId)
          ? "black"
          : "spectator";
  const myColor: PieceColor | null = role === "white" ? "w" : role === "black" ? "b" : null;

  const liveFen = moves.length ? moves[moves.length - 1]!.fen : (game?.fen ?? initialFen);
  const baseFen = optimistic?.fen ?? liveFen;
  const displayFen =
    viewPly === null ? baseFen : viewPly < 0 ? initialFen : (moves[viewPly]?.fen ?? baseFen);

  const chess = useMemo(() => {
    try {
      return new Chess(liveFen);
    } catch {
      return new Chess();
    }
  }, [liveFen]);
  const turn: PieceColor = chess.turn() as PieceColor;

  const board = useMemo<BoardPiece[]>(() => piecesFromFen(displayFen), [displayFen]);
  const captured = useMemo(() => capturedFromBoard(board), [board]);

  const inCheck = useMemo(() => {
    try {
      return new Chess(displayFen).inCheck();
    } catch {
      return false;
    }
  }, [displayFen]);
  const kingSquare = useMemo(() => {
    if (!inCheck) return null;
    try {
      const t = new Chess(displayFen).turn();
      return board.find((p) => p.type === "k" && p.color === t)?.square ?? null;
    } catch {
      return null;
    }
  }, [inCheck, board, displayFen]);

  const lastMove = useMemo(() => {
    if (optimistic && viewPly === null) return { from: optimistic.from, to: optimistic.to };
    const idx = viewPly === null ? moves.length - 1 : viewPly;
    const m = moves[idx];
    return m ? { from: m.from, to: m.to } : null;
  }, [moves, viewPly, optimistic]);

  const canMove =
    isPlayer &&
    status === "Live" &&
    viewPly === null &&
    !!myColor &&
    turn === myColor &&
    !optimistic &&
    !pendingPromotion;

  const legalTargets = useMemo(() => {
    if (!selected || !canMove) return [] as string[];
    return chess
      .moves({ square: selected as ChessSquare, verbose: true })
      .map((m) => m.to as string);
  }, [selected, canMove, chess]);

  /* ---------- Clock readout ---------- */

  const clockTicking = status === "Live" && !!clockRef.current?.running;
  useEffect(() => {
    if (!clockTicking) return;
    const id = window.setInterval(() => setTick((t) => t + 1), 100);
    return () => window.clearInterval(id);
  }, [clockTicking]);

  const clockNow = clockRef.current ? readClock(clockRef.current) : null;
  const fallbackMs = (game?.initialTimeSeconds ?? 0) * 1000;
  const whiteClockMs = clockNow?.whiteMs ?? fallbackMs;
  const blackClockMs = clockNow?.blackMs ?? fallbackMs;
  const clockKnown =
    !!clockNow && (status === "Live" || status === "Waiting" || !!clockRef.current?.exact);

  /* ---------- Actions ---------- */

  const sendMove = useCallback(
    async (from: string, to: string, promotion?: Promo) => {
      let next: string;
      try {
        const c = new Chess(liveFen);
        c.move({ from, to, ...(promotion ? { promotion } : {}) });
        next = c.fen();
      } catch {
        return false;
      }
      setOptimistic({ from, to, fen: next });
      setSelected(null);
      try {
        await hub.invoke("game:move", gameId, from, to, promotion ?? null);
        // Safety net: if the broadcast never arrives, resync from the server.
        window.setTimeout(() => {
          setOptimistic((o) => {
            if (o) {
              void loadMoves();
              void loadGame();
            }
            return null;
          });
        }, 4000);
        return true;
      } catch (e) {
        setOptimistic(null);
        toast.error(errorMessage(e, "That move was rejected."));
        void loadMoves();
        return false;
      }
    },
    [gameId, liveFen, loadGame, loadMoves],
  );

  const onSquareClick = useCallback(
    (square: string) => {
      if (!canMove || !myColor) return;
      const piece = board.find((p) => p.square === square);
      if (selected) {
        const options = chess
          .moves({ square: selected as ChessSquare, verbose: true })
          .filter((m) => m.to === square);
        if (options.length) {
          if (options.some((m) => m.promotion)) {
            setPendingPromotion({ from: selected, to: square, color: myColor });
            return;
          }
          void sendMove(selected, square);
          return;
        }
      }
      setSelected(piece && piece.color === myColor ? square : null);
    },
    [canMove, myColor, board, selected, chess, sendMove],
  );

  const choosePromotion = useCallback(
    (piece: Promo) => {
      if (!pendingPromotion) return;
      const { from, to } = pendingPromotion;
      setPendingPromotion(null);
      void sendMove(from, to, piece);
    },
    [pendingPromotion, sendMove],
  );
  const cancelPromotion = useCallback(() => {
    setPendingPromotion(null);
    setSelected(null);
  }, []);

  const run = useCallback(async (label: string, fn: () => Promise<unknown>) => {
    try {
      await fn();
    } catch (e) {
      toast.error(errorMessage(e, `Couldn't ${label}.`));
    }
  }, []);

  const resign = useCallback(
    () => run("resign", () => hub.invoke("game:resign", gameId)),
    [gameId, run],
  );
  const offerDraw = useCallback(
    () =>
      run("offer a draw", async () => {
        await hub.invoke("game:offerDraw", gameId);
        toast.success("Draw offered.");
      }),
    [gameId, run],
  );
  const acceptDraw = useCallback(
    () => run("accept the draw", () => hub.invoke("game:acceptDraw", gameId)),
    [gameId, run],
  );
  const declineDraw = useCallback(
    () => run("decline the draw", () => hub.invoke("game:declineDraw", gameId)),
    [gameId, run],
  );
  const abort = useCallback(
    () => run("abort the game", () => gamesApi.abort(gameId)),
    [gameId, run],
  );
  const sendChat = useCallback(
    (message: string) => run("send the message", () => hub.invoke("chat:send", gameId, message)),
    [gameId, run],
  );

  const respondConsent = useCallback(
    async (allow: boolean) => {
      setConsentAnswered(true);
      try {
        try {
          await hub.invoke("game:respondSpectatorConsent", gameId, allow);
        } catch {
          // Live connection hiccup: the same answer can be sent over plain HTTP.
          await gamesApi.respondConsent(gameId, allow);
        }
        window.setTimeout(() => void loadGame(), 700);
      } catch (e) {
        setConsentAnswered(false);
        toast.error(errorMessage(e, "Couldn't send your answer."));
      }
    },
    [gameId, loadGame],
  );

  const orientation: PieceColor =
    (myColor ?? "w") === "w" ? (flipped ? "b" : "w") : flipped ? "w" : "b";

  return {
    game,
    loading,
    loadError,
    role,
    myColor,
    isPlayer,
    live,
    presence,
    status,
    moves,
    chat,
    fen: displayFen,
    liveFen,
    board,
    captured,
    turn,
    inCheck,
    kingSquare,
    lastMove,
    selected,
    legalTargets,
    canMove,
    pendingPromotion,
    optimisticPending: !!optimistic,
    viewPly,
    setViewPly,
    orientation,
    flip: () => setFlipped((f) => !f),
    whiteClockMs,
    blackClockMs,
    clockKnown,
    drawOfferBy,
    needsConsent: isPlayer && status === "Waiting" && !consentAnswered,
    consentAnswered,
    onSquareClick,
    choosePromotion,
    cancelPromotion,
    resign,
    offerDraw,
    acceptDraw,
    declineDraw,
    abort,
    sendChat,
    respondConsent,
    reload: async () => {
      await Promise.all([loadGame(), loadMoves(), loadClock()]);
    },
  };
}

function readClock(c: ClockBase) {
  if (!c.running) return { whiteMs: c.whiteMs, blackMs: c.blackMs };
  const elapsed = performance.now() - c.at;
  return c.whiteToMove
    ? { whiteMs: Math.max(0, c.whiteMs - elapsed), blackMs: c.blackMs }
    : { whiteMs: c.whiteMs, blackMs: Math.max(0, c.blackMs - elapsed) };
}

export type LiveGameController = ReturnType<typeof useLiveGame>;
