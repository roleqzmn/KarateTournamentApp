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

## Create a category

`POST /api/categories` accepts name, optional minAge/maxAge, sex, categoryType,
a non-empty allowedBelts array, and participantIds (which may be empty).
Age bounds must be non-negative and minAge must not exceed maxAge. Enums must be
defined, belts and participant IDs must not repeat, and participant IDs must be
positive and exist in the database. Invalid input returns 400 with field errors.

The database assigns the category ID. One SaveChanges operation atomically saves
the category, its belts and memberships in request order. The response is 201
with CategoryDto; Location points to `/api/categories/{id}`, where the new category can
be read. A category starts unfinished, with no matches, teams or results.
Assignments are explicit: this operation does not automatically group participants
or check eligibility by age, sex, belt or discipline. It does not generate brackets.

For a PostgreSQL smoke test, create a participant and then a category using its ID.
Verify the snapshot contains the category with the requested member/belt order.
An unknown participant ID must return 400 and leave no partially created category.

## Read categories

`GET /api/categories` returns summaries ordered by ID: name, discipline, sex,
age bounds, completion flag, participant count and team count. It returns `[]`
when there are no categories. The query does not load brackets or results.

`GET /api/categories/{id}` returns a full CategoryDto, or 404 if it does not exist.
It uses the same mapping and ordering as `/api/tournament`. Related collections
are loaded with split queries in a repeatable-read transaction for a consistent
snapshot. This is a read operation; it does not claim or lock the category.

## Automated integration tests

Start Docker Desktop (Linux containers), then run `dotnet test KarateTournament.Api.Tests`.
Tests marked `Category=Integration` start an isolated PostgreSQL 17 container per test,
bound to loopback, and run the API through WebApplicationFactory. No local tournament
database or user secrets are needed. Containers are disposed after each test.
To run only tests without Docker, add `--filter "Category!=Integration"`.

## Claim a category for local judging

`POST /api/categories/{id}/claim` takes `{ "sessionToken": "<UUID>", "stationName": "Tatami 1" }`.
The client generates a fresh random UUID per judging session and persists it locally
before sending. Keep this token private; stationName is a display label (1–100 trimmed
characters), not an identity. A conditional database UPDATE grants one owner. Retrying
with the same token and label succeeds. Finished or differently claimed categories
return 409; missing categories return 404; invalid input returns 400. Success is 204.

`POST /api/categories/{id}/release` takes `{ "sessionToken": "<UUID>" }`. Only the
current token can release an active claim (204); another token gets 409. Releasing
an already free category also returns 204. A delayed old release cannot clear a new
claim. Claims survive API restarts and do not expire on disconnection. Read endpoints
expose isClaimed/claimedBy, never the token. Results submission, organizer recovery,
and authentication are separate future steps; this token is not user authentication.

For an existing schema, explicitly apply `Data/Schema/add-category-claims.sql` with
your database tool before starting this version. It adds nullable columns without
deleting data and can be repeated. This is not an automatic schema upgrade; no EF
migrations or local database reset are performed. Fresh test databases use EnsureCreated.

## Complete an individually scored category

`POST /api/categories/{id}/individual-results` accepts sessionToken and results:
`[{ "participantId": 1, "rank": 1, "judgeScores": [7, 8, 9] }]`.
It supports Kata, Kihon and both Kobudo disciplines. Results must cover all category
participants once, with ranks 1..N and at least three notes each (0–10, two decimals).
Final scores and discarded notes are calculated by the shared calculator: sum all
three notes, or discard one minimum and one maximum if there are more than three.
Ranks must follow descending scores; the client resolves equal-score ties.

Only the claiming session can finish the category. One transaction saves notes and
results, finishes the category and releases its claim. A write failure rolls back
all of these changes. Success returns 204, invalid input 400, a missing category 404,
and ownership/completion conflicts 409. The same session can retry identical results
even after an API restart. A canonical payload hash rejects changed submissions.
Apply `Data/Schema/add-category-completion.sql` explicitly to existing databases
before starting this version. Completion tokens and hashes are not exposed in DTOs.
