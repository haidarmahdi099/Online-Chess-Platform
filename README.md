# ChessPlatform — Online Chess Microservices Platform

A full-stack online chess platform built with .NET, React, PostgreSQL, Redis, Kafka, SignalR, Stockfish, and Docker.

## Project Overview

ChessPlatform is an online chess application for playing live games, challenging friends, solving puzzles, tracking player ratings, joining tournaments, and analyzing positions.

The backend uses a microservices architecture to separate responsibilities such as identity, game rules, realtime communication, puzzles, ratings, analysis, and tournaments. The React frontend communicates with the backend through a YARP API Gateway.

The project is designed to demonstrate full-stack development, service boundaries, real-time communication, event-driven workflows, authentication, containerization, and integration testing.

## Architecture

The application consists of a React single-page frontend, a public API Gateway, backend services, and supporting data and messaging infrastructure.

The browser uses the Gateway for HTTP requests and connects to the Realtime service through the Gateway’s SignalR hub. Backend services use PostgreSQL for persistent data, Kafka for asynchronous events, and Redis for realtime infrastructure.

## Backend Services

**Identity Service** handles account registration, login, email verification, password recovery, profiles, and social features. It manages access and refresh tokens, friend requests, blocks, and user preferences.

**Game Service** manages authoritative chess game state, legal moves, clocks, game history, PGN, chat, matchmaking, challenges, rematches, and player statistics. Game rules and results are validated by the backend.

**Realtime Service** provides SignalR communication for live games and tournaments. It supports player and spectator rooms, move and chat broadcasts, game actions, and live connection updates.

**Puzzle Service** provides puzzles, validates solution steps, records attempts, and returns player puzzle statistics and history.

**Puzzle Importer** is a command-line utility for importing and validating compressed Lichess puzzle data into the Puzzle database.

**Rating Service** provides player ratings, rating history, and leaderboards. It consumes game and puzzle events to update ratings asynchronously.

**Analysis Service** evaluates chess positions using Stockfish. Its container installs the Linux Stockfish engine for server-side analysis.

**Tournament Service** manages tournaments, registration, pairings, standings, match results, and game provisioning. It processes game completion events to update tournament progress.

**API Gateway** is implemented with YARP. It provides the browser-facing HTTP entry point, routes requests to backend services, and configures authentication policies, CORS, and rate limiting.

## Service Communication

The Gateway routes frontend HTTP requests to the appropriate backend service. The frontend uses it for REST APIs and the `/hubs/game` SignalR endpoint.

Services use direct HTTP calls when they need an immediate response, such as checking challenge eligibility or provisioning a tournament game.

Apache Kafka supports asynchronous event flows. Game completion and puzzle attempt events can trigger updates in the Rating and Tournament services. Outbox workers help publish persisted events, while consumers process them independently of the original request.

## Live Chess Gameplay

The Game service is authoritative for game state and move validation. The frontend uses `chess.js` for client-side move hints and notation, but the backend validates submitted game actions.

Players connect through SignalR to receive live game updates. Realtime broadcasts moves, chat messages, and game lifecycle events to the participants and, where allowed, spectators. Redis is configured as the SignalR backplane for realtime communication.

## Frontend

The frontend is a React and TypeScript single-page application built with Vite. It includes:

- Account registration, login, email verification, and password recovery
- Live game creation, matchmaking, and friend challenges
- Game boards in 2D and 3D
- Game history, moves, clocks, chat, and controls
- Puzzle solving and puzzle history
- Player profiles, ratings, and leaderboards
- Tournament browsing and tournament details
- Friends, settings, and spectator views

The frontend is deployed independently from the backend. Its browser requests go to the public Gateway URL configured through `VITE_API_BASE_URL`.

## Data Storage and Infrastructure

PostgreSQL stores persistent data. The Docker Compose setup uses separate databases for Identity, Game, Puzzle, Rating, and Tournament.

Redis supports realtime infrastructure, including SignalR scale-out. Apache Kafka provides event messaging between services.

Docker Compose runs the backend APIs, Gateway, PostgreSQL, Kafka, and Redis. The frontend has its own Compose project and runs as a separate Nginx container.

The supplied Compose configuration uses single instances of PostgreSQL, Kafka, and Redis. It is intended for local development and small single-host deployments, not a highly available production environment.

## Authentication and Security

The platform uses JWT authentication for protected API operations. Refresh tokens support ongoing sessions, while account security flows include email verification, password reset, password changes, and logout across sessions.

Gateway CORS configuration controls which frontend origins can access the backend. Secrets and service credentials are supplied through environment configuration and should not be committed to the repository.

## Technology Stack

**Backend:** C#, .NET 10, ASP.NET Core, Entity Framework Core, YARP, SignalR  
**Frontend:** React 19, TypeScript, Vite, Tailwind CSS, TanStack Router, TanStack Query, Three.js  
**Data and messaging:** PostgreSQL, Redis, Apache Kafka  
**Chess analysis:** Stockfish  
**Containers and automation:** Docker, Docker Compose, Playwright

## Running Locally

Create local environment files from the provided examples and replace backend placeholder credentials with private values:

```powershell
Copy-Item backend/.env.example backend/.env
Copy-Item frontend/.env.example frontend/.env
```

Start the backend from its directory:

```powershell
Set-Location backend
docker compose up --build -d
```

Start the frontend separately:

```powershell
Set-Location frontend
docker compose up --build -d
```

By default, the frontend is available at `http://localhost:3000` and the API Gateway at `http://localhost:5272`.

See `backend/DOCKER.md`, `frontend/README.md`, and `automation/playwright/README.md` for additional configuration and testing instructions.

## Testing

Backend unit and integration tests are organized under `backend/tests/`. Browser and end-to-end automation is isolated under `automation/playwright/`.

Playwright scenarios can exercise frontend behavior, API workflows, and live two-player SignalR games. Some scenarios require dedicated test accounts or seeded puzzle and tournament data. Use a test environment because end-to-end workflows can create game and tournament records.

## Project Objective

ChessPlatform demonstrates practical experience building a full-stack distributed application. The project focuses on microservices, chess domain logic, realtime gameplay, event-driven communication, authentication, database boundaries, containerized infrastructure, and automated testing.

## Author

Mahdi Haidar

## Repository

[Online Chess Platform on GitHub](https://github.com/haidarmahdi099/Online-Chess-Platform)

## License

No license has been selected for this repository. Until a license is added, the project remains under default copyright terms; public availability does not grant permission to reuse it.
