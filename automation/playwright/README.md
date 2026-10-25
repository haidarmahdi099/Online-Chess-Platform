# ChessPlatform end-to-end automation

This is an isolated Playwright project. All automation code, dependencies, reports, and local test configuration live under `automation/playwright`; it does not modify frontend or backend application files.

## What it covers

- Compose Gateway readiness and public API response contracts, invalid inputs, missing resources, and authentication enforcement.
- Frontend guest routes and login/register form behavior, plus a real frontend login when a dedicated player is configured.
- Identity login, refresh-token rotation/replay rejection, and malformed registration/email-verification inputs.
- Authenticated user, game, challenge, social, puzzle, rating, tournament, and analysis read/evaluation flows.
- Challenge creation, duplicate rejection, acceptance, live two-player SignalR connection, move broadcasts, persistence, turn-order rejection, and game cleanup.
- Imported-puzzle attempt flow and admin tournament create/join/leave/cancel lifecycle when fixtures are configured.

Tests that need configured accounts or seeded data are explicitly skipped when those fixtures are absent. The full command requires two verified players, an admin account, and an imported puzzle with known correct and incorrect move lines; with these fixtures it exercises every scenario in the suite.

## Prerequisites

1. Start the backend Compose stack and frontend using the project's existing run instructions. Defaults are Gateway `http://localhost:5272` and frontend `http://localhost:3000`.
2. Use dedicated local/test accounts only. Both players must be email verified and allowed to receive challenges. Never put production credentials in this folder.
3. For the tournament lifecycle test, configure a test account with the `Admin` role.
4. For the puzzle attempt test, import a puzzle and configure its ID, solution moves, and a known incorrect move line. Moves are comma-separated UCI moves in this app's “user moves only” convention.

## Install

From this folder in PowerShell:

```powershell
npm ci
npx playwright install chromium
Copy-Item .env.example .env
```

Edit `.env` with the dedicated local test credentials and fixtures. `.env` and all generated output are git-ignored.

## Run

```powershell
npm run test:e2e
```

The HTML report opens automatically after the run and shows each test's result, elapsed time, named workflow steps, failure details, and a browser video for each test that launches a page. It is saved to `playwright-report` so you can reopen it later. A demo login test types sample values into the username and password fields without submitting them.

To watch Chrome itself while the tests run, use headed mode:

```powershell
npm run test:e2e:headed
```

Headed mode adds a short delay between browser operations. To adjust the delay, set `E2E_SLOW_MO` in milliseconds before running the command. To watch tests execute live with their steps in Playwright's runner UI, use:

```powershell
npm run test:e2e:ui
```

This runs all available scenarios and skips account/data-dependent scenarios when their fixtures are missing. To require every account and seeded fixture so the full lifecycle scenarios can run:

```powershell
npm run test:e2e:full
```

Useful commands:

```powershell
npm run test:e2e:ui
npm run report
```

The report is written to `playwright-report`; traces, screenshots, and videos from failures are under `test-results`.

## Isolation and test data

The live game test creates a casual challenge and game between the two configured players, then resigns or aborts it during cleanup. The tournament test creates a future-dated tournament with a unique name and cancels it during cleanup. These workflows create disposable records in the configured backend database, so use a local/test environment. A failed process can interrupt cleanup; remove leftover records only from that test database.

Registration email delivery is intentionally not intercepted or bypassed. Use already verified test accounts for authenticated scenarios. The invalid registration/verification checks do not create valid users.
