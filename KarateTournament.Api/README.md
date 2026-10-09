# KarateTournament API - local database

The API uses PostgreSQL. The database is available only on the local machine by default.

## Start PostgreSQL

From the repository root, copy `.env.example` to `.env`, replace the sample password, then run:

```powershell
docker compose up -d
```

## Configure the API

Store the connection string in .NET user secrets. Use the same password as in `.env`:

```powershell
dotnet user-secrets set "ConnectionStrings:TournamentDatabase" "Host=localhost;Port=55432;Database=karate_tournament;Username=karate_app;Password=replace-with-a-local-password" --project .\KarateTournament.Api\KarateTournament.Api.csproj
dotnet run --project .\KarateTournament.Api\KarateTournament.Api.csproj
```

For deployment, provide `ConnectionStrings__TournamentDatabase` as an environment variable instead.

Check database connectivity at `https://localhost:7086/health/database`. The endpoint returns `503` when PostgreSQL cannot be reached.

The EF Core model maps categories, participants, teams, bracket matches, results, and
individual judge scores. Ordered relationships preserve participant, team, bracket,
and judge-score ordering. Each database represents one tournament.

This setup does not create application tables or run EF Core migrations. The API does
not yet expose tournament read/write endpoints.
