ChessPlatform
ChessPlatform is a full-stack online chess platform for playing live games, solving puzzles, tracking ratings, joining tournaments, and analyzing positions with Stockfish.
Features
- Account registration, email verification, login, password recovery, and profile management
- Friend requests, player search, blocking, and direct game challenges
- Matchmaking, live two-player games, clocks, chat, draw offers, resignations, and rematches
- Spectator consent and live game viewing
- Puzzle solving, attempt history, and puzzle ratings
- Player ratings, rating history, and leaderboards
- Tournament registration, pairings, standings, match results, and lifecycle management
- Stockfish-powered position analysis
- Browser automation with Playwright
Architecture
The browser loads the frontend and sends HTTP and SignalR requests to the public API Gateway. The Gateway routes requests to the backend services over the internal Docker network.

flowchart LR
    Browser --> Frontend["React SPA<br/>Nginx or Vite"]
    Browser --> Gateway[".NET YARP Gateway<br/>HTTP and SignalR"]
    Gateway --> Identity["Identity"]
    Gateway --> Game["Game"]
    Gateway --> Realtime["Realtime"]
    Gateway --> Puzzle["Puzzle"]
    Gateway --> Rating["Rating"]
    Gateway --> Analysis["Analysis"]
    Gateway --> Tournament["Tournament"]

    Identity --> Postgres["PostgreSQL"]
    Game --> Postgres
    Puzzle --> Postgres
    Rating --> Postgres
    Tournament --> Postgres

    Game --> Kafka["Apache Kafka"]
    Puzzle --> Kafka
    Tournament --> Kafka
    Kafka --> Rating
    Kafka --> Realtime
    Kafka --> Tournament

    Realtime --> Redis["Redis<br/>SignalR backplane"]

    Services
    
    | Service | Responsibility |
|---|---|
| **Gateway** | Public entry point, reverse proxy, authentication policies, CORS, and rate limiting |
| **Identity** | Accounts, authentication, email verification, profiles, and social features |
| **Game** | Authoritative game state, moves, clocks, matchmaking, challenges, rematches, and player statistics |
| **Realtime** | SignalR game and tournament communication, presence, and event broadcasts |
| **Puzzle** | Puzzle retrieval, move validation, attempts, and player puzzle history |
| **Rating** | Player ratings, rating history, leaderboards, and event-driven rating updates |
| **Analysis** | Position evaluation using Stockfish |
| **Tournament** | Tournament registration, pairings, standings, results, and game provisioning |
| **Puzzle.Importer** | One-off utility for importing validated compressed Lichess puzzle data |

The backend uses a layered structure: API, Application, Domain, and Infrastructure projects. The Gateway is in backend/src/Gateway/; service projects are under backend/src/Services/.

Technology

Backend: .NET 10, ASP.NET Core, Entity Framework Core, PostgreSQL, Apache Kafka, Redis, YARP, SignalR, Stockfish
Frontend: React 19, TypeScript, Vite, Tailwind CSS 4, TanStack Router, TanStack Query, Three.js
Automation: Playwright Test
Run locally with Docker
Prerequisites
- Docker Desktop with its Linux container engine running
- .NET 10 SDK and Node.js 20 or later for local development and tests
- 
Configure environment
From the repository root, copy the example files:
Copy-Item backend/.env.example backend/.env
Copy-Item frontend/.env.example frontend/.env

Edit backend/.env and replace the example values with private credentials and secure random secrets. Do not commit .env files. The frontend’s VITE_API_BASE_URL is public build-time configuration and should point to a URL the user’s browser can reach.

Start the backend
Set-Location backend
docker compose up --build -d
docker compose ps
The Gateway is available at http://localhost:5272 by default.

Start the frontend
In a second PowerShell window:
Set-Location frontend
docker compose up --build -d
docker compose ps
Open http://localhost:3000. The frontend and backend use separate Compose projects; the browser connects to the Gateway URL configured in the frontend environment.

To stop each Compose project, run docker compose down from its directory. This keeps persisted data. To also delete backend database and broker volumes, run docker compose down --volumes from backend/.

For local .NET process startup and additional infrastructure details, see [`backend/DOCKER.md`](backend/DOCKER.md).

Testing
Run backend tests from backend/:
dotnet test .\Backend.sln
Some integration and end-to-end scenarios require running services, dedicated test accounts, or seeded data.

Playwright automation is isolated under automation/playwright/:
Set-Location automation/playwright
npm ci
npx playwright install chromium
Copy-Item .env.example .env
npm run test:e2e

Configure dedicated test users and any required puzzle or tournament fixtures in the local Playwright .env. Use a test environment because end-to-end workflows can create game and tournament records. See [`automation/playwright/README.md`](automation/playwright/README.md) for headed mode, reports, and full-suite requirements.

Deployment notes
The supplied Docker Compose setup is intended for local development and small single-host deployments. PostgreSQL, Kafka, and Redis run as single instances; this configuration does not provide high availability.

For a public deployment:
- Use HTTPS and configure the frontend’s VITE_API_BASE_URL to the public Gateway URL.
- Set FRONTEND_ORIGIN and FRONTEND_BASE_URL to the deployed frontend origin.
- Forward WebSocket traffic for /hubs/game.
- Store secrets in the host’s secret manager or environment configuration.
- Use persistent storage and backups for databases and broker data.
- Do not expose internal database or broker ports publicly.
Database migrations run on service startup by default in the supplied backend Compose configuration. For production releases, review migrations and consider applying them as a separate deployment step.
Repository layout
backend/                 .NET solution, services, Docker Compose, and scripts
frontend/                React application and standalone frontend container
automation/playwright/   Isolated browser end-to-end automation

License
No license has been selected yet. Until a license is added, the project remains under default copyright terms; public availability does not grant permission to reuse it.
