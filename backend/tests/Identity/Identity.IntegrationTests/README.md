# Identity API integration tests

Run the suite from the repository root:

```powershell
dotnet test tests/Identity/Identity.IntegrationTests/Identity.IntegrationTests.csproj
```

By default, the fixture starts a disposable PostgreSQL 16 container with Testcontainers. Docker must be running. The API applies the Identity EF Core migrations to that disposable database before the tests start.

To use a dedicated disposable PostgreSQL database without Docker, set `IDENTITY_INTEGRATION_TEST_CONNECTION_STRING` to its connection string before running the same command. The database must be empty or reserved for these tests: the test host applies migrations and creates/deletes test accounts and social records.
