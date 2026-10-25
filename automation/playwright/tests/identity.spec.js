import { test, expect } from "@playwright/test";
import { hasPlayers, player1 } from "./support/settings.js";
import { login, newApi } from "./support/api.js";

test.describe("Identity workflows", () => {
  test("registration and verification reject malformed and invalid inputs", async ({ playwright }) => {
    const api = await newApi(playwright);
    const register = await test.step("Reject malformed registration input", () => api.post("/api/auth/register", {
      data: { username: "x", email: "not-an-email", password: "weak" },
    }));
    expect(register.status()).toBe(400);

    const verify = await test.step("Reject an invalid email verification code", () => api.post("/api/auth/verify-email", {
      data: { email: `missing-${crypto.randomUUID()}@example.test`, code: "000000" },
    }));
    expect(verify.status()).toBe(400);
    expect((await verify.json()).code).toBe("invalid_verification_code");
    await api.dispose();
  });

  test("valid user can sign in through the real frontend", async ({ page }) => {
    test.skip(!hasPlayers, "Set dedicated verified E2E player accounts in automation/playwright/.env.");
    await page.goto("/login");
    await page.getByLabel("Username or email").fill(player1.username);
    await page.getByLabel("Password", { exact: true }).fill(player1.password);

    const profileLoaded = page.waitForResponse(
      (response) => response.url().includes("/api/users/me") && response.status() === 200,
    );
    await page.getByRole("button", { name: "Sign in" }).click();
    await profileLoaded;
    await expect(page).not.toHaveURL(/\/login/);
  });

  test("refresh rotates the session and logout revokes the replacement token", async ({ playwright }) => {
    test.skip(!hasPlayers, "Set dedicated verified E2E player accounts in automation/playwright/.env.");
    const { api, session } = await login(playwright, player1);
    try {
      const profile = await api.get("/api/users/me");
      expect(profile.status()).toBe(200);
      expect((await profile.json()).userId).toBe(session.userId);

      const refresh = await test.step("Rotate the refresh token", () => api.post("/api/auth/refresh", { data: { refreshToken: session.refreshToken } }));
      expect(refresh.status()).toBe(200);
      const rotated = await refresh.json();
      expect(rotated.refreshToken).toBeTruthy();
      expect(rotated.refreshToken).not.toBe(session.refreshToken);

      const logout = await test.step("Log out and revoke the rotated token", () => api.post("/api/auth/logout", { data: { refreshToken: rotated.refreshToken } }));
      expect(logout.status()).toBe(204);
      const replay = await api.post("/api/auth/refresh", { data: { refreshToken: rotated.refreshToken } });
      expect(replay.status()).toBe(400);
      const staleReplay = await api.post("/api/auth/refresh", { data: { refreshToken: session.refreshToken } });
      expect(staleReplay.status()).toBe(400);
    } finally {
      await api.dispose();
    }
  });
});
