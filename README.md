# IEP Advisor

A special-education record and collaboration platform for **schools and districts** (authoring, meetings, family
collaboration, records, exports) with a **parent** product (understand your child's IEP/ETR, contribute, review drafts).

- `api/` — .NET 9 + EF Core 9 (SQL Server), Serilog, QuestPDF, Azure Blob / ACS, Claude via `IClaudeClient`
- `web/` — React 19 + Vite 7 + TypeScript (strict) + Tailwind, vitest + Testing Library
- `marketing/` — static site (`index.html`, `trust.html`)
- `docs/` — plans (`docs/plans`), solved-problem notes (`docs/solutions`), gap analysis (`docs/gap`), personas/journeys, ops runbooks, archive

## Run it locally

Ports are **7200** (API) and **5200** (web) — port 7000 is held by macOS Control Center and `5173` is not what the
invite links expect (`App:FrontendUrl`).

```bash
# API (Development settings; SQL Server connection string in api/IepAssistant.Api/appsettings.Development.json)
cd api
ASPNETCORE_ENVIRONMENT=Development dotnet run --project IepAssistant.Api --no-launch-profile --urls https://localhost:7200

# Web (proxies /api to 7200)
cd web
npm install
npx vite --port 5200
```

Migrations:

```bash
cd api
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project IepAssistant.Domain --startup-project IepAssistant.Api
```

### Demo district

`seed-demo` creates the fictional **Maple Ridge Local Schools**: 3 buildings, 17 staff across every role, 42 students
with IEP teams, 35 finalized Ohio IEPs and 6 ETRs (goals, services, accommodations and transition plans that vary by
disability), 5 IEPs mid-authoring, 8 evaluation cases at every stage of the clock, 14 meetings (4 held, 10 upcoming), 7 linked families and a
student account. Every account uses the password the command prints. Refused in Production.

```bash
cd api
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7251
dotnet run --project IepAssistant.Api --no-launch-profile -- seed-demo           # create
dotnet run --project IepAssistant.Api --no-launch-profile -- seed-demo --fresh   # reset back to its starting state
dotnet run --project IepAssistant.Api --no-launch-profile -- seed-demo --reset   # remove it
```

`--fresh` is the one command to run between demos: it removes the district and rebuilds it, rolling every date forward
so meetings are always in the next month and the overdue reviews are always genuinely overdue. Logins, contents and a
demo script are in [`docs/demo/maple-ridge-demo-district.md`](docs/demo/maple-ridge-demo-district.md) with a
[spreadsheet of the accounts](docs/demo/maple-ridge-demo-logins.xlsx). The roster itself (who exists) lives in
`api/IepAssistant.Api/Seeding/DemoRoster.cs`.

## Checks

```bash
# API
cd api && dotnet build IepAssistant.sln && dotnet test IepAssistant.Services.Tests

# Web
cd web && npx tsc -b --noEmit && npm run test:types && npx vitest run && npm run lint && npm run build && npm run guard:ux
```

`npm run lint` carries a known baseline of pre-existing errors; CI fails only if the count grows. CI
(`.github/workflows/checks.yml`) runs backend tests, the web checks and `dotnet ef migrations has-pending-model-changes`;
the deploy workflows depend on it.

## Where things are

- **Plans** — `docs/plans/` (the school-sale readiness series is `2026-09-15-001` … `008`; each records its live verification)
- **Solved problems** — `docs/solutions/<category>/` (read before touching prompts, dialogs, uploads, exports, finalize)
- **Gap analysis** — `docs/gap/` (personas/journeys in `docs/personas`, `docs/journeys`)
- **Ops** — `docs/ops/` (pilot golden-path runbook, school launch checklist)
- **Archive** — `docs/archive/` (`PLAN.md`, `PROGRESS.md` from the parent-product era)
- **Trust & privacy** — `marketing/trust.html` (data handling, retention, sub-processors, AI governance, accessibility)

## Conventions

- Immutable records (`AuthoredDocumentVersions`, `IepVersions`, `SharedDraftRevisions`, `AccessAuditLogs`) are guarded by
  `ImmutableVersionInterceptor` and SQL Server triggers; the only mutable columns are the whitelisted ones.
- Every value that reaches an AI prompt goes through `PromptText`/`DraftPromptBuilder`; ids the model must reference are
  resolved server-side. Every uploaded PDF goes through `PdfUploadGuard`.
- Dialogs that await a request pass their submitting flag as `preventClose` (`Modal`, `Drawer`; `ConfirmDialog` forwards `loading`).
- Outbound email is queued (`OutboundEmails`) and sent by `OutboundEmailWorker`; failures are visible at `/admin/email`.
