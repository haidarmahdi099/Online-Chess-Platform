# Echelon Chess — frontend

React 19 · Vite · TypeScript · Tailwind 4 · TanStack Router/Query · Framer Motion · Three.js (3D board) · SignalR

A client-only SPA. It talks to the ChessPlatform **gateway** only and can be deployed on any static host,
independently of the backend.

## Run locally

```powershell
npm ci
copy .env.example .env      # VITE_API_BASE_URL=http://localhost:5272
npm run dev                 # http://localhost:3000
```

Port **3000** is deliberate: it is the only origin the gateway's CORS policy allows by default
(`Cors:AllowedOrigins`) and the base URL used in password-reset emails (`Frontend:BaseUrl`).

`npm run typecheck`, `npm run lint`, `npm run build`.

## Production deployment

The frontend is a static single-page application. Build it with `npm ci` and `npm run build`, then
publish the contents of `dist/`. The static host must rewrite unknown paths to `/index.html` so
direct links and browser refreshes work for routes such as `/game/<id>`.

`VITE_API_BASE_URL` is embedded into the frontend at build time; it is a public URL, never a secret.
It must be an address that the user's browser can reach. For local development, use
`http://localhost:5272`. For deployed use, set it to the public HTTPS Gateway URL and allow the
frontend origin in Gateway CORS. SignalR connects to `/hubs/game` on that Gateway URL.

### Run the frontend as its own container

This Compose project contains only the frontend. It does not start or join the backend's Compose
project. Start the backend separately, then from this directory configure `.env` and launch the
frontend:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose up --build -d
docker compose ps
docker compose logs -f frontend
```

Open `http://localhost:3000` (or the configured `FRONTEND_PORT`). The image uses a non-root Nginx
server and serves the SPA with route fallback. The browser connects directly to the Gateway URL in
`VITE_API_BASE_URL`; for local use the backend Gateway should be published at `http://localhost:5272`.
Set the backend's `FRONTEND_ORIGIN` to the exact frontend origin, including its port.

Stop just the frontend container with:

```powershell
docker compose down
```

## Architecture

```
src/lib/api/        http.ts (fetch + refresh), tokens.ts, types.ts (wire DTOs), endpoints.ts, hooks.ts
src/lib/realtime/   hub.ts   one SignalR connection: events, room restore, auth:refresh
src/lib/game/       useLiveGame.ts   server-authoritative game controller, board.ts helpers
src/lib/state/      auth, preferences, theme providers
src/routes/         file-based routes (TanStack Router)
src/components/     chess (2D/3D board), game, layout, common, ui
```

Rules the code follows:

- **The backend is authoritative.** Moves, resign and draw go through the hub (`game:move`, …) so the
  opponent is notified; REST moves would not broadcast. chess.js is only used for legal-move hints and SAN.
- **Refresh tokens rotate, and reuse revokes the family.** Refreshes are serialised across tabs with the
  Web Locks API and the token store is re-read inside the lock (`http.ts`).
- **The hub keeps the token it connected with**, so every refresh is followed by `auth:refresh`.
- **Names:** the API returns bare GUIDs; `useUsernames` batches `POST /users/lookup` (max 100) and caches.
- Games start `Waiting`; both players answer the spectator-consent prompt before the game goes `Live`.
- Rating changes arrive asynchronously (Kafka), so ratings are refetched shortly after `game:end`.

## Backend notes found while integrating

1. Email verification uses a six-digit code. Password reset still uses the reset token returned by the
   email flow; the reset page accepts that token.
2. A pending draw offer is not part of the game DTO, so it cannot be restored after a page reload.
3. Challenge/rematch acceptance emits no realtime event; the sender's UI polls.
4. `.env.example` in the backend contained a real-looking Brevo key: rotate it and use placeholders.
