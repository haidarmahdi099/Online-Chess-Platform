import { test, expect } from "@playwright/test";
import { hasPlayers, player1, player2 } from "./support/settings.js";
import { login, newApi, pollUntil } from "./support/api.js";
import { createGameConnection, waitForHubEvent } from "./support/signalr.js";

async function getMoveHistory(api, gameId) {
  const response = await api.get(`/api/games/${gameId}/moves`);
  if (response.status() !== 200) throw new Error(`Move history returned ${response.status()}.`);
  const body = await response.json();
  return Array.isArray(body) ? body : body.moves;
}

test.describe("two-player live game workflow", () => {
  test("challenge acceptance creates a playable game and broadcasts legal moves to both players", async ({ playwright }) => {
    test.skip(!hasPlayers, "Set two dedicated verified E2E players in automation/playwright/.env.");

    const first = await login(playwright, player1);
    const second = await login(playwright, player2);
    const player1Api = await newApi(playwright, first.session.accessToken);
    const player2Api = await newApi(playwright, second.session.accessToken);
    const player1Hub = createGameConnection(first.session.accessToken);
    const player2Hub = createGameConnection(second.session.accessToken);
    let challengeId;
    let gameId;
    let joinedPlayer1 = false;
    let joinedPlayer2 = false;

    try {
      const created = await test.step("Player 1 creates a challenge", () => player1Api.post("/api/challenges", {
        data: {
          opponentUserId: second.session.userId,
          initialTimeSeconds: 300,
          incrementSeconds: 0,
          rated: false,
        },
      }));
      expect(created.status(), await created.text()).toBe(201);
      challengeId = (await created.json()).id;
      expect(challengeId).toBeTruthy();

      const duplicate = await test.step("Reject a duplicate pending challenge", () => player1Api.post("/api/challenges", {
        data: {
          opponentUserId: second.session.userId,
          initialTimeSeconds: 300,
          incrementSeconds: 0,
          rated: false,
        },
      }));
      expect(duplicate.status()).toBe(409);

      const incoming = await test.step("Show the challenge in Player 2's inbox", () => player2Api.get("/api/challenges/incoming"));
      expect(incoming.status()).toBe(200);
      expect((await incoming.json()).some((item) => item.id === challengeId)).toBe(true);

      const accepted = await test.step("Player 2 accepts the challenge and creates a game", () => player2Api.post(`/api/challenges/${challengeId}/accept`));
      expect(accepted.status(), await accepted.text()).toBe(200);
      const acceptance = await accepted.json();
      expect(acceptance.status).toBe("Accepted");
      gameId = acceptance.gameId;
      expect(gameId).toBeTruthy();

      const repeatedAccept = await test.step("Reject accepting the challenge a second time", () => player2Api.post(`/api/challenges/${challengeId}/accept`));
      expect(repeatedAccept.status()).toBe(409);

      const [player1Game, player2Game] = await test.step("Verify both players can read the created game", () => Promise.all([
        player1Api.get(`/api/games/${gameId}`),
        player2Api.get(`/api/games/${gameId}`),
      ]));
      expect(player1Game.status()).toBe(200);
      expect(player2Game.status()).toBe(200);

      await test.step("Connect both players to the SignalR game hub", () => Promise.all([player1Hub.start(), player2Hub.start()]));
      const player1Snapshot = waitForHubEvent(player1Hub, "game:snapshot", (snapshot) => snapshot.id === gameId || snapshot.gameId === gameId);
      const player2Snapshot = waitForHubEvent(player2Hub, "game:snapshot", (snapshot) => snapshot.id === gameId || snapshot.gameId === gameId);
      await test.step("Join both players to the game and receive snapshots", () => Promise.all([
        player1Hub.invoke("game:join", gameId),
        player2Hub.invoke("game:join", gameId),
      ]));
      await Promise.all([player1Snapshot, player2Snapshot]);
      joinedPlayer1 = true;
      joinedPlayer2 = true;

      const player1Started = waitForHubEvent(player1Hub, "game:start", (event) => event.gameId === gameId);
      const player2Started = waitForHubEvent(player2Hub, "game:start", (event) => event.gameId === gameId);
      await test.step("Both players allow spectators and resolve consent to start the game", async () => {
        await player1Hub.invoke("game:respondSpectatorConsent", gameId, true);
        await player2Hub.invoke("game:respondSpectatorConsent", gameId, true);
      });
      await Promise.all([player1Started, player2Started]);

      const liveGame = await player1Api.get(`/api/games/${gameId}`);
      expect(liveGame.status()).toBe(200);
      const gameState = await liveGame.json();
      expect(gameState.status).toBe("Live");
      expect([first.session.userId, second.session.userId]).toContain(gameState.whitePlayerId);
      const whiteHub = gameState.whitePlayerId === first.session.userId ? player1Hub : player2Hub;
      const blackHub = gameState.whitePlayerId === first.session.userId ? player2Hub : player1Hub;
      const whiteApi = gameState.whitePlayerId === first.session.userId ? player1Api : player2Api;

      await test.step("Reject Black moving before White's first move", () => expect(blackHub.invoke("game:move", gameId, "e7", "e5", null)).rejects.toThrow());
      expect(await getMoveHistory(whiteApi, gameId)).toHaveLength(0);

      const whiteMoveEvent = waitForHubEvent(player1Hub, "game:move", (move) => move.gameId === gameId && move.from === "e2" && move.to === "e4");
      const blackMoveEvent = waitForHubEvent(player2Hub, "game:move", (move) => move.gameId === gameId && move.from === "e2" && move.to === "e4");
      await test.step("White moves e2-e4 and both clients receive the move", () => whiteHub.invoke("game:move", gameId, "e2", "e4", null));
      await Promise.all([whiteMoveEvent, blackMoveEvent]);

      const persistedAfterFirstMove = await test.step("Verify the first move is persisted by the Game API", () => pollUntil(async () => {
        return getMoveHistory(whiteApi, gameId);
      }, (moves) => Array.isArray(moves) && moves.length >= 1));
      expect(persistedAfterFirstMove.length).toBeGreaterThanOrEqual(1);

      const whiteSecondMove = waitForHubEvent(player1Hub, "game:move", (move) => move.gameId === gameId && move.from === "e7" && move.to === "e5");
      const blackSecondMove = waitForHubEvent(player2Hub, "game:move", (move) => move.gameId === gameId && move.from === "e7" && move.to === "e5");
      await test.step("Black moves e7-e5 and both clients receive the move", () => blackHub.invoke("game:move", gameId, "e7", "e5", null));
      await Promise.all([whiteSecondMove, blackSecondMove]);

      const finalMoves = await test.step("Verify both moves are persisted", () => pollUntil(async () => {
        return getMoveHistory(player1Api, gameId);
      }, (moves) => Array.isArray(moves) && moves.length >= 2));
      expect(finalMoves).toHaveLength(2);
    } finally {
      if (gameId) {
        try {
          if (joinedPlayer1) await player1Hub.invoke("game:resign", gameId);
          else {
            const abort = await player1Api.post(`/api/games/${gameId}/abort`);
            if (![200, 204, 409].includes(abort.status())) console.warn(`Game cleanup returned HTTP ${abort.status()}.`);
          }
        } catch {
          try { await player1Api.post(`/api/games/${gameId}/abort`); } catch { /* Best-effort cleanup. */ }
        }
      } else if (challengeId) {
        try { await player1Api.delete(`/api/challenges/${challengeId}`); } catch { /* Best-effort cleanup. */ }
      }
      await Promise.allSettled([player1Hub.stop(), player2Hub.stop()]);
      await Promise.all([player1Api.dispose(), player2Api.dispose(), first.api.dispose(), second.api.dispose()]);
    }
  });
});
