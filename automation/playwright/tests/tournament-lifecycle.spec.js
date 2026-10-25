import { test, expect } from "@playwright/test";
import { admin, hasAdmin, hasPlayers, player1, player2 } from "./support/settings.js";
import { login, newApi } from "./support/api.js";

test.describe("tournament lifecycle", () => {
  test("admin creates a future tournament, players join/leave, and the owner cancels it", async ({ playwright }) => {
    test.skip(!hasAdmin || !hasPlayers, "Set an admin plus two dedicated verified players in automation/playwright/.env.");

    const owner = await login(playwright, admin);
    const first = await login(playwright, player1);
    const second = await login(playwright, player2);
    const ownerApi = await newApi(playwright, owner.session.accessToken);
    const firstApi = await newApi(playwright, first.session.accessToken);
    const secondApi = await newApi(playwright, second.session.accessToken);
    let tournamentId;

    try {
      const startsAtUtc = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString();
      const created = await test.step("Admin creates a future tournament", () => ownerApi.post("/api/tournaments", {
        data: {
          name: `Playwright lifecycle ${crypto.randomUUID()}`,
          description: "Disposable end-to-end test tournament",
          startsAtUtc,
          initialTimeSeconds: 180,
          incrementSeconds: 0,
          maxParticipants: 4,
        },
      }));
      expect(created.status(), await created.text()).toBe(201);
      const tournament = await created.json();
      tournamentId = tournament.id;
      expect(tournamentId).toBeTruthy();

      const joinedFirst = await test.step("Player 1 joins the tournament", () => firstApi.post(`/api/tournaments/${tournamentId}/join`));
      expect(joinedFirst.status(), await joinedFirst.text()).toBe(200);
      const joinedSecond = await test.step("Player 2 joins the tournament", () => secondApi.post(`/api/tournaments/${tournamentId}/join`));
      expect(joinedSecond.status(), await joinedSecond.text()).toBe(200);

      const duplicateJoin = await test.step("Reject duplicate participant registration", () => firstApi.post(`/api/tournaments/${tournamentId}/join`));
      expect(duplicateJoin.status()).toBe(409);

      const participants = await (await ownerApi.get(`/api/tournaments/${tournamentId}/participants`)).json();
      expect(participants.participantCount).toBe(2);
      expect(participants.participants).toEqual(expect.arrayContaining([first.session.userId, second.session.userId]));

      const myStatus = await firstApi.get(`/api/tournaments/${tournamentId}/me`);
      expect(myStatus.status()).toBe(200);
      expect((await myStatus.json()).isRegistered).toBe(true);

      const leave = await test.step("Player 2 leaves the tournament", () => secondApi.delete(`/api/tournaments/${tournamentId}/leave`));
      expect(leave.status()).toBe(204);
      const statusAfterLeave = await (await secondApi.get(`/api/tournaments/${tournamentId}/me`)).json();
      expect(statusAfterLeave.isRegistered).toBe(false);

      const rejoin = await test.step("Allow Player 2 to rejoin before registration closes", () => secondApi.post(`/api/tournaments/${tournamentId}/join`));
      expect(rejoin.status()).toBe(200);

      const cancelled = await test.step("Admin cancels the upcoming tournament", () => ownerApi.post(`/api/tournaments/${tournamentId}/cancel`));
      expect(cancelled.status(), await cancelled.text()).toBe(204);
      const details = await (await ownerApi.get(`/api/tournaments/${tournamentId}`)).json();
      expect(details.status).toBe("Cancelled");

      const joinAfterCancel = await firstApi.post(`/api/tournaments/${tournamentId}/join`);
      expect(joinAfterCancel.status()).toBe(409);
    } finally {
      if (tournamentId) {
        try {
          const cancel = await ownerApi.post(`/api/tournaments/${tournamentId}/cancel`);
          if (![204, 409].includes(cancel.status())) console.warn(`Tournament cleanup returned HTTP ${cancel.status()}.`);
        } catch { /* Best-effort cleanup. */ }
      }
      await Promise.all([ownerApi.dispose(), firstApi.dispose(), secondApi.dispose(), owner.api.dispose(), first.api.dispose(), second.api.dispose()]);
    }
  });
});
