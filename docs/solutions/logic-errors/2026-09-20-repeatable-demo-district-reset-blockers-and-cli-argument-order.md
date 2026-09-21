---
module: "DemoSeeder"
date: "2026-09-20"
problem_type: "logic_error"
component: "cli_command"
symptoms:
  - "`dotnet run --project IepAssistant.Api --urls https://localhost:7251 -- seed-demo --fresh` silently started the web server instead of seeding, and sat there indefinitely"
  - "`seed-demo --reset` failed on a district that had actually been demoed: 'The DELETE statement conflicted with the REFERENCE constraint FK_UsageRecords_ChildProfiles_ChildProfileId'"
  - "Half the demo district's students had no document at all, so most student records opened empty"
  - "A meeting's participant list never contained the family, and a finalized IEP's PDF header always read 'Meeting date: —'"
root_cause: "incorrect_assumption"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "medium"
tags: [seed-demo, demo-data, ef-core, foreign-keys, no-action, cli-arguments, dotnet-run, ordering, sql-server, test-data]
---

# A demo district you can reset is a different thing from a demo district you can create

## Problem
`seed-demo` built a demo district, and `--reset` removed it — on a district nobody had used. The moment a
person actually demoed the AI features, the reset started failing, and rebuilding between demos was a
two-command dance that nothing tested. Separately, the district it built was thin enough that most of the
product's screens were empty when you opened them.

## What went wrong

### 1. `dotnet run --urls X -- verb` puts `--urls X` in front of the verb
The CLI dispatched on `args[0] == "seed-demo"`. `dotnet run` forwards options it does not recognise to the
application **ahead of** the arguments after `--`, so the process saw
`["--urls", "https://localhost:7251", "seed-demo", "--fresh"]`, fell straight through the seed branch, and
ran `app.Run()`. There is no error to see: a seeding command that quietly becomes a web server looks
exactly like a seeding command that has hung. Ten minutes of `sample`, DMV queries and blob-endpoint checks
went into a managed stack trace that said `HostingAbstractionsHostExtensions.Run(IHost)`.

**Fix:** find the verb anywhere in `args` and read only the arguments *after* it as options; reject an
unknown option rather than ignoring it. Documented commands now set `ASPNETCORE_URLS` as an environment
variable instead of passing `--urls`.

### 2. A reset that only works on a district nobody has used is not a reset
`ResetAsync` walked everything the *seeder* creates. It did not walk what the *application* creates once
someone uses the district. Five foreign keys into `ChildProfiles`/`Users` are `NO ACTION`, so a single row
in any of them stops the delete:

| Table | References | Appears when |
| --- | --- | --- |
| `UsageRecords` | `ChildProfileId`, `UserId`, `DistrictId` | anyone uses an AI feature |
| `AdvocateThreads` | `ParentUserId` | a parent opens the advocate |
| `MeetingPrepChecklists` | `ChildProfileId` | a parent preps for a meeting |
| `ProgressReports` | `ChildProfileId` | a parent uploads a progress report |
| `StudentWorkspaces` | `UserId` | a student account is used |

Everything else hanging off a `ChildProfile` (journal entries, prep questions, advocacy goals, analysis
runs, uploaded IEP/ETR documents) is `CASCADE` and needs no help — which is exactly why the gap was easy
to miss: the tables that break a reset are the minority, and they only acquire rows after a human has
actually demoed the thing.

**Fix:** delete those five explicitly, in both the district path and the stranded-accounts path. When
auditing a delete cascade, the query to run is not "what does the seeder create" but:

```sql
SELECT OBJECT_NAME(fk.parent_object_id) AS FromTable, OBJECT_NAME(fk.referenced_object_id) AS ToTable,
       fk.delete_referential_action_desc
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.referenced_object_id) IN ('ChildProfiles','Users','SchoolStudents')
ORDER BY ToTable;
```

### 3. Seed order is domain order, and getting it wrong shows
Documents were created before meetings, and families were linked last. Two consequences, both visible in a
demo:

- `AuthoredDocumentPdfService` stamps the header with the student's **latest held meeting**. No meeting
  existed when the PDF rendered, so every IEP read `Meeting date: —`.
- `MeetingService.CreateAsync` builds its default participant list from the team **plus accepted family
  links plus the student account**. Linking families afterwards meant no meeting had a parent on it.

**Fix:** link families → schedule meetings → finalize documents → evaluation cases → share drafts. That is
also the order a district lives in: the team meets, then the IEP is signed.

### 4. `MeetingStatus.Proposed` is not reachable through the service
`SetStatusAsync` accepts only `Scheduled`, `Held` and `Continued`. The seeder asked for `Proposed`, ignored
the returned `ServiceResult`, and the data silently disagreed with the documentation that described it.
**Any seeder call whose result is dropped is a claim nobody checks** — the fix was to stop faking the state
and carry "waiting on the family" in the meeting's notes instead.

### 5. Modulo-generated data looks generated
`disability = Disabilities[i % Disabilities.Length]` gives a perfectly even spread across IDEA's 14
categories, which no district on earth has, and every student got the same two goals. An explicit roster
(`DemoRoster.cs`) and content chosen by disability category (`DemoIepContent.cs`) cost ~700 lines and are
the difference between a screenshot and a demo. The roster is also now *validated* —
`DemoRoster.Validate()` fails fast on a parent linked to the wrong child, a duplicate email, a student in a
building that does not teach their grade, or a building with no case manager — and unit-tested, so a
contradiction surfaces in 20ms instead of four minutes into a run against a live database.

## Resolution
- `seed-demo` gained `--fresh` (reset + seed in one process, each in its own DI scope so the seed starts
  with a clean change tracker) and position-independent verb parsing with unknown-option rejection.
- `ResetAsync` clears the five `NO ACTION` parent-product tables; both reset paths share one list of demo
  account ids.
- The district: 3 buildings, 17 staff, 42 students, 35 finalized IEPs, 6 ETRs, 5 IEPs in progress, 8
  evaluation cases across every stage of the 60-day clock, 14 meetings, 7 linked families, 1 student
  account. Every student has a document and a 4–6 person team.
- `DemoRosterTests` (27 cases) covers roster consistency, the track mix, the disability distribution, per-
  category goal/service/accommodation content, and the grade-9 transition rule.

## Verification
Live on QA: `--fresh` removed a 42-student district and rebuilt it in 366s; 41/41 PDFs rendered; every
login authenticates and the pending-invite parent correctly does not; a teacher login sees 8 students where
the district admin sees 42; a downloaded IEP is a populated 6-page PR-07 form.

## Prevention
- Reset paths are only proven against a database someone has **used**, not one a seeder just wrote.
- When a CLI verb travels through `dotnet run`, never trust its position.
- Check the `ServiceResult` of every seeder call. A dropped failure becomes a documentation lie.
