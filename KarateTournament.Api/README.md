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

To open the snapshot in WPF, start the API with its `https` launch profile and
select **Pobierz turniej z API**. The default URL is `https://localhost:7086`.
Set `KARATE_TOURNAMENT_API_URL` before launching WPF to use another API address.
The preview shows category availability, rosters and saved results. Use
**Wróć do turnieju lokalnego** to resume working with the local tournament.
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

## Edit participants and categories

`PUT /api/participants/{id}` and `PUT /api/categories/{id}` use the same body
and validation rules as their corresponding POST endpoints. A successful edit
returns 200 with the updated object. A missing resource returns 404 and invalid
input returns 400.

A participant edit replaces profile fields and the declared disciplines. The
participant's ID and existing category and team memberships are preserved.

A category edit replaces its settings and its full ordered lists of belts and
participant IDs. Send an empty participantIds list to clear the roster. The API
rejects an edit with 409 if the category is claimed, finished, or has teams,
matches or results. Claim and edit serialize on the same PostgreSQL row. Settings
and lists are saved in one transaction. Two organizer edits to an eligible
category apply in order, with the last committed edit taking effect.

## Delete participants and categories

`DELETE /api/participants/{id}` removes an unused participant. A reference from
a category, team, match or result returns 409. Remove draft category assignments
explicitly before deleting a participant.

`DELETE /api/categories/{id}` removes a category and its roster and belt links.
The participant records remain available. The same guards as for category edits
apply: an active claim, a finished category or existing competition data returns
409. Both endpoints return 204 after deletion and 404 if the resource is missing.

Category creation and edits lock the IDs in the incoming roster until commit.
If a concurrent deletion wins, the category write returns 400 for the missing ID.
If assignment wins, the participant deletion returns 409.

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
an already free category also returns 204. After release, generate a fresh session
token for the next claim. Claims survive API restarts and do not expire on
disconnection. Read endpoints expose isClaimed/claimedBy and keep the token private.

For an existing schema, explicitly apply `Data/Schema/add-category-claims.sql` with
your database tool before starting this version. It adds nullable columns without
deleting data and can be repeated. This is not an automatic schema upgrade; no EF
migrations or local database reset are performed. Fresh test databases use EnsureCreated.

## Recover a claimed category

Set `Organizer:RecoveryKey` in local user secrets or `Organizer__RecoveryKey` in
the API host environment. Use a random 32-byte key encoded as 64 hex characters
and send it over HTTPS as `Authorization: Bearer <key>`. Keep the key on the
organizer's station. The key protects the recovery routes. Recovery is disabled
with 503 until a valid configuration is present, and missing or wrong credentials
return 401 when it is enabled.

1. `GET /api/categories/{id}/claim-recovery` returns the current station name and
   an opaque `claimReference` for that session. Confirm it is the session to recover.
2. `POST /api/categories/{id}/claim-recovery` takes
   `{ "claimReference": "<reference from GET>" }`. It releases that session and
   permanently revokes its token for this category in the same transaction.

Recovery returns 204 on success, 400 for an invalid reference, 404 for a missing
category, or 409 when the reference is stale or the category has no active claim.
An old session cannot reclaim the category or overwrite results. Start a new
session with a fresh token. Recovery releases the server-side claim. Any work
saved locally on the old station remains there.

Apply `Data/Schema/add-category-claim-revocations.sql` explicitly to an existing
schema before starting this version. It creates the revocation table without
changing current claims and can be applied again safely.

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

## Complete Kumite

`POST /api/categories/{id}/kumite-results` takes sessionToken, matches, and
rankedParticipantIds. Matches use KumiteMatchResultRequest: bracket position,
participant/winner IDs, points, penalties, final time, senshu and overtime state.
The server assigns database match IDs. All timers are stored stopped.
Positions form a complete binary tree (final at 0, children at 2*i+1 and 2*i+2).
Every category participant starts exactly once in the leaves. Winners must advance
to the correct parent slot, and the final winner must rank first. Empty subtrees
and byes are allowed. Remaining ranking order is the local judge's decision.
As in WPF, the result score is N-rank+1, not the sum of fight points; points remain
on individual matches. Refereeing decisions are not recalculated by the server.

Only the current session can submit. One transaction saves the whole bracket and
ranking, finishes the category and releases its claim. Existing competition data
is not overwritten. Identical retries, including reordered match arrays, return
204; changed submissions or a wrong owner return 409; malformed results return
400; unknown categories return 404. No additional schema changes are required.
