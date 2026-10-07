# PromptQuest

A learning game for practicing prompt engineering. The player sees a scene
(a pond, a frog, a lily pad) and a short goal, and describes the result they
want in plain language. An AI (Google Gemini) turns that description into
CSS; the CSS is applied to the scene inside an isolated `iframe`, and an
automated validator checks whether the goal was reached. The only success
metric is prompt length: the game is about describing the result precisely
and concisely, not about writing CSS by hand.

The AI never sees the lily pad (the target) or the player-facing goal/hint
text — only the frog(s) and, on grid levels, the frog's starting cell. The
player is the only one who can see where things need to go; translating
that into words the AI can act on is the whole exercise.

## Stack

- **Backend**: ASP.NET Core 9 (Minimal API), one project (`PromptQuest.Web`)
  serving both the JSON API and the static frontend.
- **Database**: PostgreSQL via EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL`).
  Two storage modes, selected by `PromptQuest:UseInMemoryStorage` — see
  "Running locally" below.
- **AI**: Google Gemini via the `Google.GenAI` package. Model and API key
  come from configuration (`Gemini:Model`, `Gemini:ApiKey`). Requests are
  rate-limited per player using the built-in `System.Threading.RateLimiting`
  (no extra package).
- **Frontend**: plain HTML/CSS/JavaScript (ES modules), no framework and no
  build step.
- **Validation engine**: a sandboxed `iframe` (`sandbox="allow-scripts"`,
  without `allow-same-origin`) plus a `postMessage` protocol — player code
  and the AI's output never run on the server and never get access to the
  parent page's DOM or storage.

## Running locally

Two storage modes, selected by `PromptQuest:UseInMemoryStorage` (this flag
only has any effect in the `Development` environment; every other
environment always requires PostgreSQL):

### Without a database (default in Development)

There is no shared development database yet, so
`appsettings.Development.json` defaults `PromptQuest:UseInMemoryStorage` to
`true` — players, attempts and progress live in process memory and **are
lost on every restart**.

```
dotnet run --project PromptQuest.Web
```

Open `http://localhost:5280` (the `http` profile in `launchSettings.json`).

### With PostgreSQL

Turn the flag off explicitly and supply a connection string — the
recommended way is
[`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(secrets live outside the repository, in your user profile), not
`appsettings*.json`:

```
dotnet user-secrets init --project PromptQuest.Web
dotnet user-secrets set "PromptQuest:UseInMemoryStorage" "false" --project PromptQuest.Web
dotnet user-secrets set "ConnectionStrings:Default" "Host=<host>;Database=<db>;Username=<user>;Password=<password>" --project PromptQuest.Web
dotnet user-secrets set "Gemini:ApiKey" "<Gemini API key>" --project PromptQuest.Web
dotnet run --project PromptQuest.Web
```

Without a connection string (or with an empty `Host`/`Database`/`Username`)
the app fails fast at startup with a clear configuration error in this mode
— it never falls back to in-memory silently. In `Development`, with this
mode on, the schema (`Players`, `Attempts`, `LevelProgresses`) is
created/updated automatically on startup (`Database.Migrate()`); in
in-memory mode no migration runs at all.

For environments with their own deployment step, migrations can be applied
manually instead:

```
dotnet ef database update --project PromptQuest.Web
```

(requires the `dotnet-ef` tool: `dotnet tool install --global dotnet-ef` if
not already installed). Migrations live in `PromptQuest.Web/Migrations/`
(`InitialCreate`, `AddBestPromptLength`).

**Secrets must never be stored in `appsettings.json`** — only via
`dotnet user-secrets` (development) or environment variables (prod/CI). As
of the last audit, `PromptQuest.Web/appsettings.json` in this repository
still contains a committed Gemini API key — see "Known limitations".

### Running tests

One-time setup before the first run of the browser tests — install Chromium
for Playwright (once per machine, not part of `dotnet test` itself):

```
dotnet build PromptQuest.Web.Tests
pwsh PromptQuest.Web.Tests/bin/Debug/net9.0/playwright.ps1 install chromium
```

(`powershell` instead of `pwsh` works on Windows). Without this step the
tests in `RunnerBrowserTests.cs` fail with a missing-browser error; the rest
of the suite is unaffected.

```
dotnet test PromptQuest.Web.Tests
```

Tests never call the real Gemini API (code generation is replaced with a
fake implementation) and never open a connection to a real database: the
HTTP tests (`ApiIntegrationTests`, `PromptQuestWebFactory`) boot the app
under `Development` with `PromptQuest:UseInMemoryStorage=true` — the same
switch and the same in-memory branch a normal local run without a database
uses; the EF/Postgres branch
(`AddStorage_SelectsLeaderboardServiceByConfiguration`) is only checked by
resolving dependencies, without a real connection. As of this writing the
suite has 93 tests and covers: the level catalog (the AI-facing scene
contains no lily pads or target-position data, `goal` text contains no
words/numbers giving away the position), building the AI system instruction
(`{CURRENT_CSS}` substitution, `goal`/`hint` isolation), provider-failure
handling (exception/timeout/empty response always yield `Success=false`
and empty `Code`), leaderboard counting/sorting, selecting the
`ILeaderboardService` implementation by configuration, the per-player AI
rate limit (429 once exceeded), and — in a real Chromium instance via
`Microsoft.Playwright` (`RunnerBrowserTests.cs`) — that each of the 13
levels is actually solved by its own reference CSS and not solved by empty
code, that the scene never creates a scroll inside the `iframe`, and that
the lily pad is visible (non-zero size, opaque fill).

## Repository structure

### Root

| Path | Purpose |
|---|---|
| `PromptQuest.sln` | Solution file: `PromptQuest.Web` and `PromptQuest.Web.Tests`. |
| `README.md` | This file. |
| `.gitignore` / `.gitattributes` | Standard ignore rules and line-ending normalization. |

### `PromptQuest.Web/`

| Path | Purpose |
|---|---|
| `Program.cs` | Storage selection via `StorageRegistration.AddStorage` (`PromptQuest:UseInMemoryStorage`, Development-only, default `true`; PostgreSQL everywhere else), automatic migration on startup in Development when a real database is in use, Gemini client and rate limiter registration, static files (with `Cache-Control: no-cache`) and endpoints. |
| `appsettings.json` / `appsettings.Development.json` | `PromptQuest`/`Gemini` configuration; secrets (`ConnectionStrings:Default`, `Gemini:ApiKey`) do not belong here — only via `dotnet user-secrets`/environment variables. |
| `AI/AiAgent.cs` | `ICodeGenerationService` implementation on top of Gemini: system instruction built from `level.SystemPrompt` + `level.AiScene`; any failure returns `Success=false`. |
| `Configuration/AppOptions.cs` | Typed model of the `PromptQuest` configuration section (prompt/code length limits, AI timeout and rate limit, etc.). |
| `Models/` | `Player`, `LevelProgress` (incl. `BestPromptLength`), `Attempt` (+ `CodeSource`), `LevelDefinition` (incl. `AiScene`), `LevelScene`, `LevelValidation`, `LevelCheck`. |
| `Dtos/` | HTTP API contracts (camelCase JSON). |
| `Services/ICodeGenerationService.cs`, `AiRateLimiter.cs`, `JsonLevelStore.cs`, `ManualCodeGenerationService.cs` | Code-generation interface, per-player AI rate limiter, `Data/levels.json` reader, an unused manual-entry stub. |
| `Services/Storage/` | `StorageRegistration.AddStorage` (implementation selection), `IPlayerStore`/`IAttemptStore`/`ILevelStore` interfaces; `Storage/Db/` — EF/Postgres (`AppDbContext`, `AppDbContextFactory` for migrations, `EfPlayerStore`, `EfAttemptStore`, `EfLeaderboardService`); `Storage/InMemory/` — the no-database implementations. |
| `Migrations/` | `InitialCreate` (`Players`, `Attempts`, `LevelProgresses` tables), `AddBestPromptLength` (column + index for the metric). |
| `Endpoints/` | Minimal API: players, levels, attempts, per-level leaderboard. |
| `Data/levels.json` | The 13-level catalog; each level has `scene` (for rendering and the validator) and `aiScene` (the reduced view sent to the AI). |
| `wwwroot/` | Frontend: `index.html`, `css/app.css`, `js/*.js` (routing, screens, prompt character counter, `fetch` wrapper), `sandbox/runner.html` + `runner.js` (the runner inside the `iframe`). |

### `PromptQuest.Web.Tests/`

An xUnit project: `LevelCatalogTests`, `AiAgentTests`, `LeaderboardTests`,
`ApiIntegrationTests` (+ `PromptQuestWebFactory` — a test factory that
swaps in a fake `ICodeGenerationService`), `RunnerBrowserTests` (real
Chromium via `Microsoft.Playwright` — needs a one-time browser install, see
"Running tests" above).

## Architecture

```
Browser                                     Server (ASP.NET Core)
+------------------------------+            +------------------------------+
| UI (index.html + JS modules) |  REST/JSON | Endpoints (Minimal API)      |
|   - nickname entry           |<---------->|   /api/players               |
|   - level list                |            |   /api/levels                |
|   - play screen               |            |   /api/attempts              |
|   - leaderboard               |            |   /api/leaderboard/levels    |
|            |                  |            |            |                 |
|            | postMessage      |            |            v                 |
|            v                  |            | Services                     |
| +---------------------------+ |            |   ILevelStore   (json)       |
| | sandbox <iframe>          | |            |   IPlayerStore  (EF/memory)  |
| |  runner.html + runner.js  | |            |   IAttemptStore (EF/memory)  |
| |  scene + code + checks    | |            |   ILeaderboardService        |
| +---------------------------+ |            |   AiRateLimiter (per player) |
+--------------------------------+           |   ICodeGenerationService     |
                                              |     (AiAgent -> Gemini)      |
                                              +------------------------------+
```

### Interface seams

The codebase is organized around a handful of small interfaces, each with
an in-memory and a database/AI implementation, so storage and AI concerns
can be swapped and tested independently:

```csharp
public interface IPlayerStore
{
    Task<Player?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Player>  CreateAsync(string nickname, CancellationToken ct = default);
    Task<LevelProgress?> GetProgressAsync(Guid playerId, string levelId,
                                          CancellationToken ct = default);
    Task UpsertProgressAsync(LevelProgress progress, CancellationToken ct = default);
}

public interface IAttemptStore
{
    Task<Attempt?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Attempt attempt, CancellationToken ct = default);
    Task UpdateAsync(Attempt attempt, CancellationToken ct = default);
    Task<int> CountAsync(Guid playerId, string levelId, CancellationToken ct = default);
}

public interface ILevelStore
{
    Task<IReadOnlyList<LevelDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<LevelDefinition?> GetAsync(string levelId, CancellationToken ct = default);
}

public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetForLevelAsync(
        string levelId, int take, CancellationToken ct = default);
}

public interface ICodeGenerationService
{
    Task<CodeGenerationResult> GenerateAsync(
        LevelDefinition level, string prompt, CancellationToken ct = default);
}

public sealed record CodeGenerationResult(
    bool       Success,
    string     Code,
    bool       ManualEntry,
    CodeSource Source,      // Manual | Ai
    string?    Error);
```

`ILeaderboardService` has no "global" method on purpose — summing prompt
lengths across different levels isn't a meaningful ranking, so there is no
global leaderboard anywhere in the app.

| Interface | In-memory (`Storage/InMemory/`) | Database/AI (`Storage/Db/`, `AI/`) |
|---|---|---|
| `IPlayerStore` | `InMemoryPlayerStore` (`ConcurrentDictionary`) | `EfPlayerStore` (PostgreSQL) |
| `IAttemptStore` | `InMemoryAttemptStore` | `EfAttemptStore` |
| `ILeaderboardService` | `InMemoryLeaderboardService` | `EfLeaderboardService` |
| `ILevelStore` | `JsonLevelStore` — reads `Data/levels.json` at startup (used regardless of storage mode) | |
| `ICodeGenerationService` | `AiAgent` (Gemini) — always used; `ManualCodeGenerationService` exists in the code but is no longer registered in DI | |

Both the in-memory and the EF implementation of a given interface are
selected together, in one place (`StorageRegistration.AddStorage`), so the
app never ends up with a mix of in-memory and database-backed stores.

### How the AI gets its task

For every player prompt, `AI/AiAgent.cs` calls Gemini with a system
instruction built from the level's `SystemPrompt` (with the `{CURRENT_CSS}`
placeholder resolved from `level.AiScene.BaseCss`, not the full scene) and
`level.AiScene.Html`. The player-visible `goal`/`hint` text is never sent to
the model in any form — the model cannot solve a level from a description
of the goal, bypassing the player's own prompt. `level.AiScene` is a
reduced view of the scene: it keeps the same element ids as the real scene
(the pond, the frogs, the grid) but contains no lily pads and no CSS rules
that would reveal where a target is — the correct solution is unreachable
without what the player described in their prompt while looking at the
picture.

On any request failure (exception, timeout, empty response) `GenerateAsync`
returns `Success=false` with an empty `Code` and the reason in `Error` — the
error text never ends up in `Code`. `POST /api/attempts` then responds
`502` and does not create an attempt; exceeding the per-player rate limit
responds `429` with a `Retry-After` header, and no attempt is created
either.

## Level description format

`Data/levels.json` is an array of level objects. The fields that matter
most:

| Field | Type | Description |
|---|---|---|
| `id` / `order` / `title` | string / int / string | Identifier, catalog order, player-facing title. |
| `goal` | string | A short goal, visible only to the player, worded without words/numbers that give away the position. **Never sent to the AI.** |
| `hint` | string | A generic hint ("Describe to the AI what you see in the picture."). |
| `scene.html` / `scene.baseCss` | string | The full scene (with lily pads), used to render the game and to validate the result. |
| `aiScene.html` / `aiScene.baseCss` | string | The reduced scene sent to the AI — same element ids as `scene`, no lily pads, no position-revealing rules. |
| `codeTemplate` | string | Starting content of the code field (manual-entry mode only). |
| `systemPrompt` | string | The level's system instruction, sent to the AI together with `aiScene`. |
| `validation.checks` | array | The checks that must all pass (see "Checks" below). |
| `forbiddenPatterns` | array | Regular expressions that fail the run immediately if the player's code matches them. |

## Design principles

- **`goal`/`hint` never reach the AI.** The AI call is built from
  `level.SystemPrompt` + `level.AiScene` only; `level.Goal`/`level.Hint`
  (and the full `scene`) are never read by `AiAgent`. Otherwise the model
  could solve the level from the goal text, skipping the player's prompt
  entirely.
- **`aiScene` never contains a lily pad or its position.** The AI sees the
  pond and the frog(s) (and, on grid levels, the grid structure/labels) but
  never the target — the correct solution is unreachable without the
  player's own description of what they see.
- **The only success metric is prompt length** (Unicode characters, server-
  side `Trim()`, computed from the already-stored `Attempt.Prompt` — the
  client cannot supply its own value). No score, no elapsed time, no
  attempt count, no global leaderboard across levels.

## The 13 levels

All levels are about frogs and lily pads inside `#pond` (a flex or grid
container); `goal`/`hint` never name a position, direction, number or CSS
property.

| ID | Player's task | Check |
|---|---|---|
| `css-01-justify` | Frog and lily pad at the pond's right edge | `overlapCenter` |
| `css-02-align` | Frog and lily pad at the pond's bottom edge | `overlapCenter` |
| `css-03-center` | Frog and lily pad in the center of the pond | `overlapCenter` |
| `css-04-reverse` | Three frogs on their own-colored lily pads, mirrored order | 3x `overlapCenter` |
| `css-05-spread` | Three frogs spread evenly, the outer two at the edges | 3x `overlapCenter` |
| `css-06-grid` | 3x3 grid, lily pad in one cell | `containedIn` |
| `css-07-two-spots` | 4x3 grid, two frogs, each lily pad its own color in its own cell | 2x `overlapCenter` |
| `css-08-big-lily` | A lily pad spanning 2x2 cells of a 3x3 grid — both frogs must fit on it without overlapping each other | 2x `containedIn` + `noOverlap` |
| `css-09-two-ponds` | Two independent grid ponds, each with its own frog/lily pair | 2x `containedIn` |
| `css-10-four-colors` | 4x3 grid, four frogs each on their own-colored lily pad | 4x `containedIn` |
| `css-11-shift` | 8x6 grid with lettered columns (A-H) and numbered rows (1-6); 6 colored frogs, each lily pad shifted by the same vector | 6x `overlapCenter` |
| `css-12-rainbow` | 6x3 grid with labels; 6 frogs on top in a scrambled order, lily pads below in rainbow order | 6x `overlapCenter` |
| `css-13-rotate` | 6x6 grid, no labels; each lily pad sits at the point symmetric to its frog's starting cell (180 deg rotation about the grid's center) | 6x `overlapCenter` |

Levels 11-12 introduce a labelled grid — column letters and row numbers as
real text nodes in `aiScene.html` (not CSS `content`) — a coordinate
language shared by the player and the model that never reveals where a
lily pad is. `css-13` does not need labels: it is already a CSS grid (like
`css-06`-`css-10`), so a cell can be described relative to other cells
without letters/numbers. The point of each of these three is to replace a
frog-by-frog enumeration with a single rule that is expensive to describe
by listing every cell and easy to state imprecisely: `css-11-shift` checks
whether the player notices the shared shift vector; `css-12-rainbow`
checks that the lily pad order is named as a single well-known sequence,
independent of the frogs' scrambled order; `css-13-rotate` checks that the
player names the operation precisely (a 180 deg rotation, not "mirrored",
which for an asymmetric layout gives different cells).

The validation engine (`wwwroot/sandbox/runner.js`) implements 10 kinds of
checks (`overlapCenter`, `containedIn`, `noOverlap`, `orderX`, `orderY`,
`computedStyle`, `selectorMatches`, `textContent`, `classOnElements`,
`elementCount`); the current catalog only uses `overlapCenter`,
`containedIn` and (on `css-08-big-lily` only) `noOverlap` — the rest are
implemented and available for future levels but currently unused.

## HTTP API

All endpoints return camelCase JSON; errors are `ProblemDetails` with a
`detail` field.

| Method & path | Purpose |
|---|---|
| `POST /api/players` | Create a player from a nickname, get back a `playerId`. |
| `GET /api/players/{playerId}` | The player and their progress (`promptLength` per level); 404 if not found. |
| `GET /api/levels` | Level list (short form, with `bestPromptLength` if `playerId` is passed). |
| `GET /api/levels/{levelId}` | Full level detail: `scene`, checks, code template (no `systemPrompt`, no `aiScene` — those never reach the browser). |
| `POST /api/attempts` | Register an attempt and get code back from the AI. `429` with `Retry-After` if the per-player rate limit is exceeded (10/min by default, `AppOptions.AiRequestsPerMinutePerPlayer`); `502` if the AI didn't respond — in both cases no attempt is created. |
| `POST /api/attempts/{attemptId}/result` | Submit a run's outcome, get back `promptLength` and whether it's a new personal best. |
| `GET /api/leaderboard/levels/{levelId}` | Top entries for one level (`take` parameter, default 20), sorted by `promptLength` ascending, ties broken by earliest completion. |

There is no global leaderboard endpoint; leaderboard responses never
include `playerId`, only a nickname and `promptLength`.

## AI rate limiting

`AiRateLimiter` wraps `System.Threading.RateLimiting.PartitionedRateLimiter`,
partitioned by `playerId`, applied programmatically inside the
`POST /api/attempts` handler (not via the ASP.NET Core `RateLimiting`
middleware, since the partition key lives in the request body and isn't
available yet at the middleware's partitioning stage). Default: 10 requests
per player per minute (`PromptQuest:AiRequestsPerMinutePerPlayer`),
configurable. Exceeding it returns `429` with a `Retry-After` header and
does not create an attempt — this is a guard against uncontrolled spend
against the paid Gemini quota, not a general anti-abuse system.

## Security

- Player code and the AI's output never run on the server — only inside a
  sandboxed `<iframe sandbox="allow-scripts">` in the player's own browser,
  without `allow-same-origin`, so the document gets an opaque origin: it
  cannot reach the parent page's DOM, `localStorage`, cookies, or the
  application's API.
- The iframe is re-created before every run. A watchdog on the parent side
  removes it and counts the run as failed if no result arrives within
  `validation.timeoutMs` (guards against an infinite loop in player code).
- Message authenticity is checked by comparing `event.source` against the
  iframe's `contentWindow` and matching the `runId` of the request.
- Prompt length for the personal-best record is computed server-side from
  the already-stored `Attempt.Prompt` — the client cannot influence the
  value directly.
- `MaxPromptLength`/`MaxCodeLength` cap prompt and code length server-side.
- `AiRateLimiter` caps AI requests per player per minute (see above).

## Known limitations

- **The pass/fail check runs entirely in the player's browser.** The
  sandboxed iframe reports `passed` and the list of checks to the parent
  page, which the player's browser then sends to the server; there is no
  server-side re-verification of the scene geometry. A player who wanted to
  could forge the `passed` value sent to `POST /api/attempts/{id}/result`
  and record a personal best without actually solving the level — this is
  a conscious trust boundary, not an oversight, but it means the
  leaderboard is not resistant to a motivated client.
- **The AI's behavior on short or ambiguous prompts has only been checked
  by hand, not by an automated test against the real model.** The
  automated suite (`AiAgentTests`) replaces Gemini with fakes that
  simulate specific failure modes (exception, timeout, empty response) to
  verify `AiAgent`'s own error handling; none of it sends a real prompt to
  Gemini and checks the resulting CSS. Whether a given short prompt
  reliably produces a working or a failing result has only been verified
  manually, level by level, not systematically.
- **In-memory mode loses all data on every restart.** This is also the
  default mode in `Development`, since there is no shared development
  database yet.
- **No CI and no committed deploy configuration on this branch** — the
  test suite is run manually (`dotnet test`).
- A Gemini API key is currently committed in `PromptQuest.Web/appsettings.json`
  in this repository — it needs to be rotated and removed from the file
  (and from git history) separately from this change.
- `ManualCodeEntry` (manual CSS entry without the AI) remains in the code
  as an alternate mode for testing the validation engine without a real AI
  call, but is off by default.
- `Microsoft.Extensions.AI` is a dependency in the `.csproj` that the code
  does not use; `ManualCodeGenerationService` exists but is no longer
  registered in DI; the historical `LevelProgress.BestAttempts`/
  `BestTimeMs`/`BestScore`/`TotalAttempts` and `Attempt.ElapsedMs` fields
  remain in the schema from an earlier scoring design but are no longer
  computed by any code path.
