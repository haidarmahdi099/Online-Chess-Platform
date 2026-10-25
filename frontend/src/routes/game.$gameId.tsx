import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { AnimatePresence, motion } from "motion/react";
import { Download, Eye, Handshake, Loader2, RotateCcw, Swords } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { ChessBoard } from "@/components/chess/ChessBoard";
import { ErrorState, LoadingBlock, Pill } from "@/components/common/bits";
import { AnalysisPanel } from "@/components/game/AnalysisPanel";
import { ChatPanel } from "@/components/game/ChatPanel";
import { GameControls } from "@/components/game/GameControls";
import { MoveList, MoveRibbon } from "@/components/game/MoveList";
import { PlayerCard } from "@/components/game/PlayerCard";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { gamesApi, rematchApi } from "@/lib/api/endpoints";
import { useInbox, usePlayerRatings, useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import { endReasonText, gameCategory, timeControlLabel } from "@/lib/chess-utils";
import { useLiveGame, type LiveGameController } from "@/lib/game/useLiveGame";
import { playSound } from "@/lib/sound";
import { useAuth } from "@/lib/state/auth";
import { usePreferences } from "@/lib/state/preferences";

export const Route = createFileRoute("/game/$gameId")({
  head: () => ({ meta: [{ title: "Game — Echelon Chess" }] }),
  component: GamePage,
});

/** A short sound for each new move (capture / check / plain) and at the end of the game. */
function useMoveSounds(ctrl: LiveGameController) {
  const { prefs } = usePreferences();
  const seen = useRef<number | null>(null);
  const ended = useRef<boolean | null>(null);
  const { moves, status } = ctrl;

  useEffect(() => {
    if (seen.current === null) {
      seen.current = moves.length; // don't replay history on load
      return;
    }
    const last = moves.at(-1);
    if (moves.length > seen.current && last && prefs.sounds) {
      playSound(
        last.san.includes("+") || last.san.includes("#")
          ? "check"
          : last.san.includes("x")
            ? "capture"
            : "move",
        prefs.volume,
      );
    }
    seen.current = moves.length;
  }, [moves, prefs.sounds, prefs.volume]);

  useEffect(() => {
    const over = status === "Finished" || status === "Aborted";
    if (ended.current === null) {
      ended.current = over;
      return;
    }
    if (over && !ended.current && prefs.sounds) playSound("end", prefs.volume);
    ended.current = over;
  }, [status, prefs.sounds, prefs.volume]);
}

function GamePage() {
  const { gameId } = Route.useParams();
  return <GameView key={gameId} gameId={gameId} />;
}

function GameView({ gameId }: { gameId: string }) {
  const ctrl = useLiveGame(gameId);
  const { user, status: authStatus, realtimeStatus } = useAuth();
  const { prefs, set } = usePreferences();
  const [tab, setTab] = useState("moves");
  const [confirmResign, setConfirmResign] = useState(false);
  useMoveSounds(ctrl);

  const game = ctrl.game;
  const { name } = useUsernames([game?.whitePlayerId, game?.blackPlayerId]);
  const white = usePlayerRatings(game?.whitePlayerId);
  const black = usePlayerRatings(game?.blackPlayerId);

  if (ctrl.loading) return <LoadingBlock className="py-32" />;
  if (ctrl.loadError || !game) {
    return (
      <div className="mx-auto max-w-lg px-4 py-20">
        <ErrorState message={ctrl.loadError ?? "Game not found."} />
        <div className="mt-4 text-center">
          <Button asChild variant="outline">
            <Link to="/play">Back to play</Link>
          </Button>
        </div>
      </div>
    );
  }

  const cat = gameCategory(game);
  const ratingOf = (r: typeof white.data) => r?.[cat]?.rating;
  const finished = ctrl.status === "Finished" || ctrl.status === "Aborted";
  const bottomColor = ctrl.orientation;
  const topColor = bottomColor === "w" ? "b" : "w";
  const idOf = (c: "w" | "b") => (c === "w" ? game.whitePlayerId : game.blackPlayerId);
  const ratingFor = (c: "w" | "b") => ratingOf(c === "w" ? white.data : black.data);
  const clockOf = (c: "w" | "b") =>
    ctrl.clockKnown ? (c === "w" ? ctrl.whiteClockMs : ctrl.blackClockMs) : null;
  const capturedOf = (c: "w" | "b") =>
    c === "w" ? ctrl.captured.blackLost : ctrl.captured.whiteLost;
  const isPlayer = ctrl.isPlayer && ctrl.myColor !== null;
  const canAct = isPlayer && ctrl.status === "Live";
  const canAbort = canAct && ctrl.moves.length < 2;
  const drawFromOpponent =
    !!ctrl.drawOfferBy && !!user && ctrl.drawOfferBy.toLowerCase() !== user.userId.toLowerCase();
  const drawFromMe = !!ctrl.drawOfferBy && !drawFromOpponent;
  const perspective = prefs.perspective;

  return (
    <div className="mx-auto max-w-[1800px] px-3 py-4 sm:px-6 sm:py-6">
      <div className="mb-4 grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3">
        <div className="min-w-0">
          <h1 className="truncate font-display text-lg font-bold uppercase sm:text-2xl">
            {name(game.whitePlayerId)} <span className="text-muted-foreground">vs</span>{" "}
            {name(game.blackPlayerId)}
          </h1>
          <p className="label-mono mt-1">
            {game.rated ? "Rated" : "Casual"} ·{" "}
            {timeControlLabel(game.initialTimeSeconds, game.incrementSeconds)} {cat}
            {ctrl.presence > 1 && ` · ${ctrl.presence} connected`}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <motion.span
            key={ctrl.status}
            initial={{ scale: 0.85, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            transition={{ type: "spring", stiffness: 500, damping: 26 }}
          >
            <Pill
              tone={
                ctrl.status === "Live" ? "good" : ctrl.status === "Waiting" ? "primary" : "neutral"
              }
            >
              {ctrl.status === "Live"
                ? "Live"
                : ctrl.status === "Waiting"
                  ? "Starting"
                  : ctrl.status}
            </Pill>
          </motion.span>
          <span className="rounded-full bg-secondary px-3 py-1 font-mono text-[10px] uppercase tracking-[0.14em]">
            {ctrl.role === "spectator" ? (
              <span className="inline-flex items-center gap-1.5">
                <Eye className="h-3 w-3" /> Spectating
              </span>
            ) : (
              `You play ${ctrl.role}`
            )}
          </span>
          {authStatus === "authenticated" && realtimeStatus !== "connected" && !finished && (
            <span className="hidden items-center gap-1.5 rounded-full border border-primary/50 px-3 py-1 font-mono text-[10px] uppercase tracking-[0.14em] text-primary sm:inline-flex">
              <Loader2 className="h-3 w-3 animate-spin" /> {realtimeStatus}
            </span>
          )}
        </div>
      </div>

      {authStatus === "anonymous" && (
        <p className="mb-4 rounded-lg border border-border bg-surface px-4 py-3 text-sm text-muted-foreground">
          You're viewing a static snapshot.{" "}
          <Link
            to="/login"
            search={{ redirect: `/game/${gameId}` }}
            className="font-semibold text-primary hover:underline"
          >
            Sign in
          </Link>{" "}
          to follow the game live with moves and clocks.
        </p>
      )}

      <div className="grid gap-4 [@media(orientation:landscape)_and_(max-height:520px)]:grid-cols-[minmax(0,1fr)_300px] xl:grid-cols-[minmax(0,1fr)_380px] 2xl:grid-cols-[minmax(0,1fr)_420px]">
        <div className="min-w-0">
          <div className="panel p-2 sm:p-4">
            <PlayerCard
              name={name(idOf(topColor))}
              rating={ratingFor(topColor)}
              color={topColor}
              clockMs={clockOf(topColor)}
              active={ctrl.turn === topColor && ctrl.status === "Live"}
              capturedTypes={capturedOf(topColor)}
            />

            <div className="relative my-3 sm:my-4">
              <ChessBoard
                board={ctrl.board}
                orientation={ctrl.orientation}
                selected={ctrl.selected}
                legalTargets={ctrl.legalTargets}
                lastMove={ctrl.lastMove}
                checkSquare={ctrl.kingSquare}
                perspective={perspective}
                interactive={ctrl.canMove}
                onSquareClick={ctrl.onSquareClick}
                promotion={ctrl.pendingPromotion}
                onPromote={ctrl.choosePromotion}
                onCancelPromotion={ctrl.cancelPromotion}
              />

              <AnimatePresence>
                {ctrl.status === "Waiting" && (
                  <motion.div
                    key="waiting"
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    exit={{ opacity: 0 }}
                    className="absolute inset-0 z-10 grid place-items-center rounded-xl bg-background/70 backdrop-blur-sm"
                  >
                    <div className="panel px-8 py-6 text-center">
                      <Loader2 className="mx-auto h-6 w-6 animate-spin text-primary" />
                      <p className="mt-3 font-display text-lg font-bold uppercase">
                        {isPlayer && ctrl.consentAnswered
                          ? "Waiting for your opponent"
                          : "Game starting"}
                      </p>
                      <p className="label-mono mt-1.5">
                        The game begins once both players confirm.
                      </p>
                    </div>
                  </motion.div>
                )}
                {finished && (
                  <ResultOverlay
                    key="result"
                    ctrl={ctrl}
                    name={name}
                    onAnalyze={() => setTab("analysis")}
                  />
                )}
                {ctrl.inCheck && ctrl.status === "Live" && (
                  <motion.span
                    key="check"
                    initial={{ opacity: 0, y: -8 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0 }}
                    className="pointer-events-none absolute left-1/2 top-2 z-10 -translate-x-1/2 rounded-full bg-destructive px-3 py-1 font-mono text-[11px] uppercase tracking-[0.16em] text-destructive-foreground"
                  >
                    Check
                  </motion.span>
                )}
              </AnimatePresence>
            </div>

            <PlayerCard
              name={name(idOf(bottomColor))}
              rating={ratingFor(bottomColor)}
              color={bottomColor}
              clockMs={clockOf(bottomColor)}
              active={ctrl.turn === bottomColor && ctrl.status === "Live"}
              capturedTypes={capturedOf(bottomColor)}
            />

            <AnimatePresence>
              {drawFromOpponent && ctrl.status === "Live" && (
                <motion.div
                  initial={{ opacity: 0, height: 0 }}
                  animate={{ opacity: 1, height: "auto" }}
                  exit={{ opacity: 0, height: 0 }}
                  className="mt-3 overflow-hidden"
                >
                  <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-primary/50 bg-primary/10 px-4 py-3">
                    <span className="flex items-center gap-2 text-sm font-semibold">
                      <Handshake className="h-4 w-4 text-primary" /> Your opponent offers a draw
                    </span>
                    <span className="flex gap-2">
                      <Button size="sm" onClick={() => void ctrl.acceptDraw()}>
                        Accept
                      </Button>
                      <Button size="sm" variant="secondary" onClick={() => void ctrl.declineDraw()}>
                        Decline
                      </Button>
                    </span>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>

            <div className="mt-3">
              <MoveRibbon moves={ctrl.moves} viewPly={ctrl.viewPly} onSelect={ctrl.setViewPly} />
            </div>

            <div className="mt-3">
              <GameControls
                onFlip={ctrl.flip}
                onTogglePerspective={() => set("perspective", !prefs.perspective)}
                perspective={perspective}
                canAct={canAct}
                canAbort={canAbort}
                drawPending={drawFromMe}
                onOfferDraw={() => void ctrl.offerDraw()}
                onResign={() => setConfirmResign(true)}
                onAbort={() => void ctrl.abort()}
              />
            </div>
          </div>
        </div>

        <div className="min-w-0">
          <Tabs
            value={tab}
            onValueChange={setTab}
            className="panel flex h-full min-h-[420px] flex-col sm:min-h-[520px] xl:sticky xl:top-20 xl:h-[calc(100dvh-11rem)]"
          >
            <TabsList className="m-2 grid grid-cols-3 font-mono text-[10px] uppercase tracking-[0.04em] sm:text-xs sm:tracking-[0.12em]">
              <TabsTrigger value="moves">Moves</TabsTrigger>
              <TabsTrigger value="analysis">Analysis</TabsTrigger>
              <TabsTrigger value="chat">Chat</TabsTrigger>
            </TabsList>
            <TabsContent value="moves" className="m-0 min-h-0 flex-1">
              <MoveList moves={ctrl.moves} viewPly={ctrl.viewPly} onSelect={ctrl.setViewPly} />
            </TabsContent>
            <TabsContent value="analysis" className="m-0 min-h-0 flex-1">
              <AnalysisPanel
                fen={ctrl.fen}
                enabled={tab === "analysis" && authStatus === "authenticated"}
              />
            </TabsContent>
            <TabsContent
              value="chat"
              forceMount
              className="m-0 min-h-0 flex-1 data-[state=inactive]:hidden"
            >
              <ChatPanel
                messages={ctrl.chat}
                myId={user?.userId ?? null}
                canSend={isPlayer && ctrl.status === "Live"}
                onSend={ctrl.sendChat}
              />
            </TabsContent>
          </Tabs>
        </div>
      </div>

      {/* Both players must answer before a game can start. */}
      <Modal
        open={ctrl.needsConsent}
        persistent
        title="Allow spectators?"
        description="Choose whether other players may watch this game live. The game starts as soon as both players have answered."
      >
        <div className="grid gap-2 sm:grid-cols-2">
          <Button size="lg" onClick={() => void ctrl.respondConsent(true)}>
            <Eye className="mr-2 h-4 w-4" /> Allow
          </Button>
          <Button size="lg" variant="secondary" onClick={() => void ctrl.respondConsent(false)}>
            No spectators
          </Button>
        </div>
      </Modal>

      <Modal
        open={confirmResign}
        onClose={() => setConfirmResign(false)}
        title="Resign this game?"
        description="Your opponent will be awarded the win. This can't be undone."
      >
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={() => setConfirmResign(false)}>
            Keep playing
          </Button>
          <Button
            variant="destructive"
            onClick={() => {
              setConfirmResign(false);
              void ctrl.resign();
            }}
          >
            Resign
          </Button>
        </div>
      </Modal>
    </div>
  );
}

/* ---------- Result card with rematch / PGN / next game ---------- */

function ResultOverlay({
  ctrl,
  name,
  onAnalyze,
}: {
  ctrl: LiveGameController;
  name: (id: string) => string;
  onAnalyze: () => void;
}) {
  const { user } = useAuth();
  const game = ctrl.game!;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { rematches } = useInbox();
  const [requested, setRequested] = useState<string | null>(null);

  const me = user?.userId.toLowerCase();
  const iWon = !!game.winnerId && game.winnerId.toLowerCase() === me;
  const draw = game.status === "Finished" && !game.winnerId;
  let title = "Game over";
  if (game.status === "Aborted") title = "Game aborted";
  else if (draw) title = "Draw";
  else if (ctrl.isPlayer) title = iWon ? "You won" : "You lost";
  else title = `${name(game.winnerId ?? "")} won`;

  // Wait for the opponent's answer to my rematch request.
  const outgoing = useQuery({
    queryKey: ["rematch-outgoing", game.id],
    queryFn: rematchApi.outgoing,
    enabled: !!requested,
    refetchInterval: 3000,
  });
  const mine = outgoing.data?.find((r) => r.id === requested);
  useEffect(() => {
    if (mine?.status === "Accepted" && mine.rematchGameId)
      void navigate({ to: "/game/$gameId", params: { gameId: mine.rematchGameId } });
    if (mine && (mine.status === "Declined" || mine.status === "Expired")) {
      toast.info(`Rematch ${mine.status.toLowerCase()}.`);
      setRequested(null);
    }
  }, [mine, navigate]);

  const request = useMutation({
    mutationFn: () => rematchApi.request(game.id),
    onSuccess: (r) => {
      setRequested(r.id);
      toast.success("Rematch offered");
    },
    onError: (e) => toast.error(errorMessage(e)),
  });
  const cancelRequest = useMutation({
    mutationFn: (id: string) => rematchApi.cancel(id),
    onSuccess: () => {
      setRequested(null);
      toast.info("Rematch request cancelled");
    },
    onError: (e) => toast.error(errorMessage(e)),
  });
  const incoming = rematches.find((r) => r.originalGameId.toLowerCase() === game.id.toLowerCase());
  const acceptIncoming = useMutation({
    mutationFn: (id: string) => rematchApi.accept(id),
    onSuccess: (r) => {
      void queryClient.invalidateQueries({ queryKey: ["inbox"] });
      void navigate({ to: "/game/$gameId", params: { gameId: r.rematchGameId } });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const downloadPgn = async () => {
    try {
      const pgn = await gamesApi.pgn(game.id);
      const url = URL.createObjectURL(new Blob([pgn], { type: "application/x-chess-pgn" }));
      const a = document.createElement("a");
      a.href = url;
      a.download = `echelon-${game.id.slice(0, 8)}.pgn`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      toast.error(errorMessage(e, "Couldn't download the PGN."));
    }
  };

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.94 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0 }}
      className="absolute inset-0 z-10 grid place-items-center rounded-xl bg-background/75 p-3 backdrop-blur-sm"
    >
      <div className="panel w-full max-w-sm px-6 py-6 text-center">
        <p className="font-display text-3xl font-bold uppercase">{title}</p>
        <p className="label-mono mt-2">{endReasonText(game.endReason) || "Game over"}</p>
        <div className="mt-5 grid gap-2">
          {ctrl.isPlayer && game.status === "Finished" && (
            <>
              {incoming ? (
                <Button
                  onClick={() => acceptIncoming.mutate(incoming.id)}
                  disabled={acceptIncoming.isPending}
                >
                  <Swords className="mr-2 h-4 w-4" /> Accept rematch
                </Button>
              ) : (
                <Button
                  onClick={() => request.mutate()}
                  disabled={request.isPending || !!requested}
                >
                  {requested ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <RotateCcw className="mr-2 h-4 w-4" />
                  )}
                  {requested ? "Waiting for opponent…" : "Rematch"}
                </Button>
              )}
              {requested && !incoming && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => cancelRequest.mutate(requested)}
                  disabled={cancelRequest.isPending}
                >
                  Cancel rematch request
                </Button>
              )}
            </>
          )}
          <div className="grid grid-cols-2 gap-2">
            <Button variant="secondary" onClick={onAnalyze}>
              Analyse
            </Button>
            <Button variant="secondary" onClick={() => void downloadPgn()}>
              <Download className="mr-1.5 h-4 w-4" /> PGN
            </Button>
          </div>
          <Button asChild variant="outline">
            <Link to="/play">New game</Link>
          </Button>
        </div>
      </div>
    </motion.div>
  );
}
