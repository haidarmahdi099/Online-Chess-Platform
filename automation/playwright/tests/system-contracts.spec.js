import { test, expect } from "@playwright/test";
import { apiUrl } from "./support/settings.js";
import { newApi, json } from "./support/api.js";

test.describe("running Compose stack: public endpoints and edge contracts", () => {
  test("Gateway readiness is ready", async ({ playwright }) => {
    const api = await newApi(playwright);
    const response = await test.step("Request Gateway readiness", () => api.get("/health/ready"));
    expect(response.status()).toBe(200);
    await expect(response).toBeOK();
    expect((await response.json()).status).toBe("Ready");
    await api.dispose();
  });

  test("public service routes return valid paged response shapes", async ({ playwright }) => {
    const api = await newApi(playwright);
    const cases = [
      ["Rating leaderboard", "/api/leaderboard?category=chess&page=1&pageSize=5"],
      ["Puzzle catalog", "/api/puzzles?page=1&pageSize=5"],
      ["Tournament catalog", "/api/tournaments?page=1&pageSize=5"],
    ];

    for (const [name, path] of cases) {
      const response = await test.step(name, () => api.get(path));
      expect(response.status(), `${name} route returned ${await response.text()}`).toBe(200);
      const body = await json(response);
      expect(body.items, `${name} should return an items array`).toEqual(expect.any(Array));
      expect(body.page).toBe(1);
      expect(body.pageSize).toBe(5);
      expect(body.totalCount).toEqual(expect.any(Number));
    }

    const nextPuzzle = await api.get("/api/puzzles/next");
    expect([200, 404], "No puzzle imported is a valid empty-data state").toContain(nextPuzzle.status());
    await api.dispose();
  });

  test("public routes reject invalid pagination and filters", async ({ playwright }) => {
    const api = await newApi(playwright);
    for (const path of [
      "/api/leaderboard?category=unknown&page=1&pageSize=5",
      "/api/leaderboard?category=chess&page=0&pageSize=5",
      "/api/puzzles?page=0&pageSize=5",
      "/api/puzzles?page=1&pageSize=5&minRating=1800&maxRating=1000",
      "/api/tournaments?page=0&pageSize=5",
    ]) {
      const response = await test.step(`Reject invalid request: ${path}`, () => api.get(path));
      expect(response.status(), `${path} should be rejected as invalid input`).toBe(400);
    }
    await api.dispose();
  });

  test("protected service routes enforce authentication", async ({ playwright }) => {
    const api = await newApi(playwright);
    const protectedRequests = [
      ["profile", () => api.get("/api/users/me")],
      ["game list", () => api.get("/api/games?filter=mine&page=1&pageSize=5")],
      ["game seek", () => api.post("/api/games/seek", { data: { timeControl: { initialSeconds: 60, incrementSeconds: 0, category: "bullet" }, rated: false } })],
      ["challenge inbox", () => api.get("/api/challenges/incoming")],
      ["social preferences", () => api.get("/api/social/preferences")],
      ["puzzle statistics", () => api.get("/api/puzzles/me/stats")],
      ["analysis evaluation", () => api.post("/api/analysis/evaluate", { data: { fen: "invalid", depth: 1 } })],
      ["tournament creation", () => api.post("/api/tournaments", { data: {} })],
      ["SignalR negotiation", () => api.post("/hubs/game/negotiate?negotiateVersion=1")],
    ];

    for (const [name, send] of protectedRequests) {
      const response = await test.step(`Enforce authentication for ${name}`, send);
      expect(response.status(), `Expected auth rejection, got ${response.status()} from ${response.url()}`).toBe(401);
    }
    await api.dispose();
  });

  test("unknown public resources return not-found rather than gateway errors", async ({ playwright }) => {
    const api = await newApi(playwright);
    const missingId = "00000000-0000-0000-0000-000000000001";
    const game = await api.get(`/api/games/${missingId}/public`);
    const tournament = await api.get(`/api/tournaments/${missingId}`);
    expect(game.status()).toBe(404);
    expect(tournament.status()).toBe(404);
    await api.dispose();
  });
});

test.describe("frontend guest journeys", () => {
  test("typed login fields are visible in the browser recording", async ({ page }) => {
    await page.goto("/login");
    const username = page.getByLabel("Username or email");
    const password = page.getByLabel("Password", { exact: true });

    await test.step("Type a demo username into the login field", () => username.pressSequentially("e2e-demo-player", { delay: 100 }));
    await test.step("Type a demo password into the password field", () => password.pressSequentially("DemoPassword123", { delay: 100 }));

    await expect(username).toHaveValue("e2e-demo-player");
    await expect(password).toHaveValue("DemoPassword123");
    await page.waitForTimeout(1_000);
  });

  test("authentication forms are reachable and validate empty input", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page.getByText("Enter your username or email.")).toBeVisible();
    await expect(page.getByText("Enter your password.")).toBeVisible();

    await page.goto("/register");
    await expect(page.getByRole("heading", { name: "Create account" })).toBeVisible();
    await page.getByRole("button", { name: "Create account" }).click();
    await expect(page.getByText(/3–20 characters/)).toBeVisible();
    await expect(page.getByText(/valid email address/i)).toBeVisible();
  });

  test("guest pages load without frontend exceptions", async ({ page }) => {
    const pageErrors = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    for (const path of ["/", "/leaderboard", "/puzzles", "/tournaments"]) {
      const response = await page.goto(path);
      expect(response?.status(), `${path} should render`).toBeLessThan(500);
      await expect(page.locator("body")).not.toBeEmpty();
    }
    expect(pageErrors, `Browser page errors: ${pageErrors.join(" | ")}`).toEqual([]);
  });
});

test("the configured frontend and Gateway URLs are the ones under test", async () => {
  expect(apiUrl).toMatch(/^https?:\/\//);
});
