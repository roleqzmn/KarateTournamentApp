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
dotnet run --project .\KarateTournament.Api\KarateTournament.Api.csproj --launch-profile https
```

For deployment, provide `ConnectionStrings__TournamentDatabase` as an environment variable instead.

Check database connectivity at `https://localhost:7086/health/database`. The endpoint returns `503` when PostgreSQL cannot be reached.

On startup, the API creates the EF Core schema when the database is empty. The model
maps categories, participants, teams, bracket matches, results, and individual judge
scores. Ordered relationships preserve participant, team, bracket, and judge-score
ordering. Each database represents one tournament.

Schema setup uses `EnsureCreated`; it does not run EF Core migrations or update an
existing schema. Future model changes will need an explicit schema-upgrade strategy.

## API access

With both access keys absent, tournament routes accept loopback connections for
local development. For tournament use, configure two distinct random 32-byte
keys encoded in hex as `Access__OrganizerKey` and `Access__JudgeKey` on the host.
With keys configured, tournament requests require HTTPS and a bearer key.
Either key can read the tournament and judge competitions. Only the organizer
key can create, edit, delete and import records. Recovery uses its separate
organizer recovery key.

On each WPF station set `KARATE_TOURNAMENT_API_URL` and
`KARATE_TOURNAMENT_ACCESS_KEY` before launch. Set `KARATE_TOURNAMENT_ROLE=organizer`
on the organizer's station or `judge` to open the judging window. The app opens
the judging window by default. Set the role to `local` for the file-based
tournament. The API enforces the privileges associated with the supplied key.

For a LAN host, set `ASPNETCORE_URLS=https://0.0.0.0:7086` and configure Kestrel
with a certificate valid for the host name used by the stations. Use
`ASPNETCORE_Kestrel__Certificates__Default__Path` and
`ASPNETCORE_Kestrel__Certificates__Default__Password` for a PFX. Launch the API
with `--no-launch-profile`. Install trust for the issuing certificate on the
stations and allow inbound TCP 7086 in Windows Firewall only on the tournament
network. The PostgreSQL port stays bound to loopback. Keep the database
connection string and all keys in host configuration, outside the repository.

A two-station check starts with the organizer creating and populating a category.
Both stations load the tournament and attempt to claim the same category. One wins
and the other sees it occupied on refresh or receives 409 during a simultaneous
attempt. Disconnect the first station, finish or close and resume the local
session, reconnect, and send the result. Refresh on the second station and
check the ranking and completed status.

## Back up or restore the database

Run this from the repository root with PostgreSQL running:

```powershell
.\KarateTournament.Api\Operations\TournamentDatabase.ps1 -Action Backup -File .\tournament.dump
.\KarateTournament.Api\Operations\TournamentDatabase.ps1 -Action Restore -File .\tournament.dump -NewDatabase karate_restore
```

The backup includes tournament data, active claims and revocations. Local judge
checkpoints live separately in each Windows profile under
`%LOCALAPPDATA%\KarateTournamentApp\Judging`. Keep checkpoint files private.
Restore creates a new database and stops if that name already exists. Start a
separate API instance with the restored database in its connection string to
verify the restored categories and results before using that database.

## Read the tournament snapshot

`GET /api/tournament` returns all participants and categories, including ordered
category members, belts, bracket matches, teams, final results, and judge scores.
The endpoint reads these related records from one consistent database snapshot.

To open the snapshot in WPF, start the API with its `https` launch profile and
select **Pobierz turniej z API**. The default URL is `https://localhost:7086`.
Set `KARATE_TOURNAMENT_API_URL` before launching WPF to use another API address.
The preview shows category availability, rosters and saved results. Use
**Wróć do turnieju lokalnego** to resume working with the local tournament.

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

In WPF, open the API tournament and use **Zapisz zawodnika w API**. The registry
includes unassigned participants. On a confirmed save, WPF displays the assigned
ID and clears the form. Rejected data stays in the form for correction. If a
response is lost, refresh the registry and check for the participant before retrying.

In API mode, **Import Excel** sends one batch to `POST /api/participants/batch`.
The API validates the entire batch before saving in one transaction. A 400
response names the rejected entries and adds nobody. Valid entries are added
to the registry. Importing the same file again creates additional records.
Use **Nowa konkurencja** or **Edytuj ustawienia i skład** to assign participants
and adjust their order. On the registry list, **Edytuj** opens a participant's
profile in the registration form.

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

In WPF, **Przejmij i rozpocznij** opens the judge panel and public scoreboard
for Kumite and individually scored categories. WPF saves the session token
before claiming and checkpoints progress every two seconds and on close.
Use **Wznów sesję** to reopen local progress during a network outage. An
unconfirmed claim is retried with the same token. The timer starts paused after
reopening a checkpoint. **Zwolnij blokadę** discards the local checkpoint after
the API confirms the release.

After finishing, close the judge panel and use **Wyślij wyniki**. A failed upload
keeps the local checkpoint. The next attempt uses the original session token
and the same ranking. **Archiwizuj lokalnie** keeps a copy outside the active
list and leaves any server-side claim untouched.

## Recover a claimed category

Set `Organizer:RecoveryKey` in local user secrets or `Organizer__RecoveryKey` in
the API host environment. Use a random 32-byte key encoded as 64 hex characters
and send it over HTTPS as `Authorization: Bearer <key>`. Keep the key on the
organizer's station. The key protects the recovery routes. Recovery is disabled
with 503 until a valid configuration is present, and missing or wrong credentials
return 401 when it is enabled.

In WPF set `KARATE_TOURNAMENT_RECOVERY_KEY` before launch and use **Odzyskaj**
on the claimed category.

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
