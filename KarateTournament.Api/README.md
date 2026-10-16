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

On startup, the API creates the EF Core schema when the database is empty. The model
maps categories, participants, teams, bracket matches, results, and individual judge
scores. Ordered relationships preserve participant, team, bracket, and judge-score
ordering. Each database represents one tournament.

Schema setup uses `EnsureCreated`; it does not run EF Core migrations or update an
existing schema. Future model changes will need an explicit schema-upgrade strategy.

## Read the tournament snapshot

`GET /api/tournament` returns all participants and categories, including ordered
category members, belts, bracket matches, teams, final results, and judge scores.
The endpoint reads these related records from one consistent database snapshot.
WPF integration will be added in later steps.

## Create a participant

`POST /api/participants` accepts firstName, lastName, age, optional club, belt,
sex, and a non-empty categoryTypes array. Enums use their numeric values from
KarateClassLibrary. Names must not be blank, age must be non-negative, enum
values must be defined, and category types must not repeat. Invalid data returns
400 with field errors. Names and club are trimmed; an empty club is stored as null.

The database assigns the ID. A single SaveChanges operation atomically saves the
participant and their category types. The response is 201 with ParticipantDto and
a Location header pointing to `GET /api/participants/{id}` (404 if not found).
Category types declare disciplines; this endpoint does not assign categories or
generate brackets. Participants with identical names are allowed.

Run validation tests with:

```powershell
dotnet test .\KarateTournament.Api.Tests\KarateTournament.Api.Tests.csproj
```

For a PostgreSQL smoke test, start the API with the https launch profile and use
KarateTournament.Api.http. Verify POST returns 201, GET by the returned ID and
GET /api/tournament contain the participant, and the data survives an API restart.
Also verify an invalid request returns 400 without adding a participant.
