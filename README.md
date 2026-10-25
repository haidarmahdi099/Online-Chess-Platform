# ChessPlatform

ChessPlatform is a full-stack online chess application with live multiplayer games, player accounts and social features, puzzles, ratings, tournaments, and Stockfish-powered analysis.

## Project structure

- `backend/` — .NET 10 services, API Gateway, PostgreSQL/Kafka/Redis infrastructure, migrations, and local scripts.
- `frontend/` — React/TypeScript single-page application, served locally by Vite or as a standalone Nginx container.
- `automation/playwright/` — isolated Playwright end-to-end tests and HTML reports.

## Run locally with Docker

Install Docker Desktop and start its Linux container engine. Copy the example environment files and replace the backend placeholders with private values before starting services:

```powershell
Copy-Item backend/.env.example backend/.env
Copy-Item frontend/.env.example frontend/.env
```

Start the backend Compose project:

```powershell
Set-Location backend
docker compose up --build -d
```

Start the frontend in a separate PowerShell window:

```powershell
Set-Location frontend
docker compose up --build -d
```

By default, open the app at `http://localhost:3000`; the Gateway is at `http://localhost:5272`. The frontend container is independent from the backend Compose project and calls the Gateway URL configured at frontend build time.

For detailed environment variables, migrations, service behavior, testing, and deployment notes, see [`backend/DOCKER.md`](backend/DOCKER.md), [`frontend/README.md`](frontend/README.md), and [`automation/playwright/README.md`](automation/playwright/README.md).

## Tests

Backend tests are in `backend/tests/`. Playwright browser automation is in `automation/playwright/`; it requires a running application stack and local test fixtures for scenarios that use accounts or seeded data.

## License

No license has been selected for this repository yet.
