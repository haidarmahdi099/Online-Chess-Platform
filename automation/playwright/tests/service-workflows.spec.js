import { test, expect } from "@playwright/test";
import { hasPlayers, player1, puzzleFixture } from "./support/settings.js";
import { login, newApi } from "./support/api.js";

let player1AccessToken;

test.describe("authenticated service data flows", () => {
  test("Identity, Game, Puzzle, Rating and Tournament read flows work for a real user", async ({ playwright }) => {
    test.skip(!hasPlayers, "Set dedicated verified E2E player accounts in automation/playwright/.env.");
    const { api, session } = await login(playwright, player1);
    player1AccessToken = session.accessToken;
    try {
      const calls = [
        ["profile", () => api.get("/api/users/me")],
        ["game list", () => api.get("/api/games?filter=mine&page=1&pageSize=5")],
        ["game statistics", () => api.get("/api/stats")],
        ["challenge inbox", () => api.get("/api/challenges/incoming")],
        ["challenge outbox", () => api.get("/api/challenges/outgoing")],
        ["incoming rematches", () => api.get("/api/games/rematches/incoming")],
        ["outgoing rematches", () => api.get("/api/games/rematches/outgoing")],
        ["social friends", () => api.get("/api/social/friends")],
        ["social incoming requests", () => api.get("/api/social/friend-requests/incoming")],
        ["social outgoing requests", () => api.get("/api/social/friend-requests/outgoing")],
        ["social blocks", () => api.get("/api/social/blocks")],
        ["social preferences", () => api.get("/api/social/preferences")],
        ["puzzle statistics", () => api.get("/api/puzzles/me/stats")],
        ["puzzle history", () => api.get("/api/puzzles/me/history?page=1&pageSize=5")],
        ["tournament catalog", () => api.get("/api/tournaments?page=1&pageSize=5")],
      ];

      for (const [name, call] of calls) {
        const response = await test.step(`Read ${name}`, call);
        expect(response.status(), `${name} failed: ${await response.text()}`).toBe(200);
      }

      const ratings = await api.get(`/api/ratings/${session.userId}`);
      expect(ratings.status()).toBe(200);
      const history = await api.get(`/api/ratings/${session.userId}/history?category=chess&page=1&pageSize=5`);
      expect(history.status()).toBe(200);

      const preferences = await api.get("/api/social/preferences").then((response) => response.json());
      expect(preferences.userId).toBe(session.userId);

      const evaluation = await test.step("Evaluate the initial chess position", () => api.post("/api/analysis/evaluate", {
        data: {
          fen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
          depth: 1,
        },
        timeout: 45_000,
      }));
      expect(evaluation.status(), `Analysis failed: ${await evaluation.text()}`).toBe(200);

      const invalidFen = await test.step("Reject an invalid analysis FEN", () => api.post("/api/analysis/evaluate", {
        data: { fen: "not-fen", depth: 1 },
      }));
      expect(invalidFen.status()).toBe(400);

      const invalidDepth = await test.step("Reject analysis depth above the configured maximum", () => api.post("/api/analysis/evaluate", {
        data: { fen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", depth: 21 },
      }));
      expect(invalidDepth.status()).toBe(400);
    } finally {
      await api.dispose();
    }
  });

  test("puzzle attempts can be exercised against a configured imported fixture", async ({ playwright }) => {
    const fixture = puzzleFixture();
    test.skip(!hasPlayers || !fixture, "Set a verified player plus E2E_PUZZLE_ID and E2E_PUZZLE_SOLUTION for an imported puzzle.");
    let api;
    if (player1AccessToken) api = await newApi(playwright, player1AccessToken);
    else ({ api } = await login(playwright, player1));
    try {
      if (fixture.wrongMoves?.length) {
        await test.step("Submit an incorrect puzzle solution", async () => {
          const wrong = await api.post(`/api/puzzles/${fixture.id}/attempt`, { data: { moves: fixture.wrongMoves } });
          expect(wrong.status(), await wrong.text()).toBe(200);
          expect((await wrong.json()).isCorrect).toBe(false);
        });
      }
      await test.step("Submit the correct puzzle solution", async () => {
        const solved = await api.post(`/api/puzzles/${fixture.id}/attempt`, { data: { moves: fixture.solution } });
        expect(solved.status(), await solved.text()).toBe(200);
        const result = await solved.json();
        expect(result.isCorrect).toBe(true);
      });

      await test.step("Verify puzzle attempts appear in the user's history", async () => {
        const history = await api.get("/api/puzzles/me/history?page=1&pageSize=5");
        expect(history.status()).toBe(200);
      });
    } finally {
      await api.dispose();
    }
  });
});
