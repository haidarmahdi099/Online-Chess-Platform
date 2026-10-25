# Game API integration tests

Run from the repository root:

```powershell
dotnet test tests/Game/Game.IntegrationTests/Game.IntegrationTests.csproj
```

The fixture starts a disposable PostgreSQL 16 container by default and applies the Game EF Core migrations before serving requests. Docker must be running. It disables the Kafka background publisher and uses a fake Identity challenge-eligibility response so the suite exercises Game without requiring Kafka or a running Identity service.

To run without Docker, set `GAME_INTEGRATION_TEST_CONNECTION_STRING` to an empty, dedicated PostgreSQL test database before running the command. The fixture applies migrations and writes test games, seeks, challenges, rematches, moves, and chat messages to that database.
