import { expect } from "@playwright/test";
import { apiUrl } from "./settings.js";

export async function newApi(playwright, accessToken) {
  return playwright.request.newContext({
    baseURL: apiUrl,
    extraHTTPHeaders: accessToken ? { Authorization: `Bearer ${accessToken}` } : {},
    timeout: 20_000,
  });
}

export async function login(playwright, credentials) {
  const loginApi = await newApi(playwright);
  const response = await loginApi.post("/api/auth/login", {
    data: { usernameOrEmail: credentials.username, password: credentials.password },
  });
  expect(response.status(), `Login failed for the configured E2E account: ${await response.text()}`).toBe(200);
  const session = await response.json();
  expect(session.accessToken).toBeTruthy();
  expect(session.refreshToken).toBeTruthy();
  await loginApi.dispose();
  const api = await newApi(playwright, session.accessToken);
  return { api, session };
}

export async function json(response) {
  const text = await response.text();
  return text ? JSON.parse(text) : undefined;
}

export async function pollUntil(read, predicate, { timeoutMs = 15_000, intervalMs = 250 } = {}) {
  const deadline = Date.now() + timeoutMs;
  let lastValue;
  let lastError;
  while (Date.now() < deadline) {
    try {
      lastValue = await read();
      if (predicate(lastValue)) return lastValue;
      lastError = undefined;
    } catch (error) {
      lastError = error;
    }
    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
  if (lastError) throw lastError;
  throw new Error(`Condition was not met within ${timeoutMs}ms. Last value: ${JSON.stringify(lastValue)}`);
}
