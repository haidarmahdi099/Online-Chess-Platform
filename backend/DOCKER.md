# Run the backend with Docker Compose

This Compose stack builds and starts the seven APIs, Gateway, PostgreSQL with the five service
databases, Kafka, and Redis. Only the Gateway is published to the host; the APIs and data services
stay on the Compose network. The frontend has its own Docker Compose project under `frontend/` and
can be started or stopped independently. `Puzzle.Importer` is a one-off interactive dataset utility,
not a long-running service, so it is not started with the backend stack.

## Start

From the `backend` directory, create `backend/.env` from `.env.example` if it does not exist. Replace
all example credentials with private values before using the stack. The existing env file is loaded
automatically by Docker Compose; do not commit it.

```powershell
$env:COMPOSE_BAKE = 'false'
docker compose up --build -d
docker compose ps
docker compose logs -f gateway
```

Open the Gateway directly at `http://localhost:5272` (or `GATEWAY_PORT`). To start the frontend in
its separate container, open another PowerShell window and run:

```powershell
Set-Location ..\frontend
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose up --build -d
docker compose ps
```

Open the frontend at `http://localhost:3000` (or its `FRONTEND_PORT`). Its browser requests go to
`VITE_API_BASE_URL` in `frontend/.env`; for local use this defaults to the host-published Gateway at
`http://localhost:5272`. Set `FRONTEND_ORIGIN` in `backend/.env` to the browser's frontend origin.

For a public deployment, set `FRONTEND_ORIGIN` and `FRONTEND_BASE_URL` to the public HTTPS frontend
origin and set `VITE_API_BASE_URL` to the browser-reachable Gateway HTTPS URL. Allow the frontend
origin in Gateway CORS and forward WebSocket connections to the Gateway's `/hubs/game` route.

Use the backend Compose project and frontend Compose project independently. If the local
`Start-All.ps1` frontend is still running, stop it before starting the frontend container so port
3000 is free.

To stop the services while keeping database and broker data:

```powershell
docker compose down
```

Stop the frontend separately from the `frontend` directory with `docker compose down`.

To remove persistent databases and broker data as well, run `docker compose down --volumes`. That
deletes local Compose volumes.

## Configuration notes

- Set a JWT signing secret and the shared internal tournament key to random values of at least 32
  bytes. All API services and the Gateway receive the same JWT issuer, audience, and signing key.
- Identity requires `BREVO_API_KEY` and `BREVO_SENDER_EMAIL` when running in Production.
- The database containers create one database per persisted service on their first initialization.
  Database migrations are enabled on service startup by default for this single-instance stack. Set
  `DATABASE_MIGRATE_ON_STARTUP=false` when applying reviewed migrations as a separate deployment step.
- PostgreSQL, Kafka, and Redis are not published on host ports. Use a managed service for production
  deployments that need high availability, or add restricted host bindings for local administration.
- The included Kafka and Redis configuration is a single-node setup for development or a small
  single-host deployment, not a highly available production cluster.

The API image build uses `Dockerfile.service`; Analysis has its own Dockerfile because its runtime
image installs Stockfish. The Docker build context is `backend/`.
