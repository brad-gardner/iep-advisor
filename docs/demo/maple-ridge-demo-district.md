# Maple Ridge Local Schools — the demo district

A fictional Ohio district you can create, demo on, and reset back to its starting state as many times as
you like. Every row is invented: no real child, family or staff member appears anywhere in it.

- **Password for every account:** `Demo!2026pw`
- **Email domain:** `@mapleridge.example` (a reserved fictional domain — the reset sweeps on it, so nothing
  real can ever be caught by it)
- **Spreadsheet of logins:** [`maple-ridge-demo-logins.xlsx`](./maple-ridge-demo-logins.xlsx)

## Create, reset, rebuild

```bash
cd api

# create it (no-op if a demo district already exists)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7251 \
  dotnet run --project IepAssistant.Api --no-launch-profile -- seed-demo

# put it back to its starting state — the one command to run between demos
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7251 \
  dotnet run --project IepAssistant.Api --no-launch-profile -- seed-demo --fresh

# remove it and leave it removed
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7251 \
  dotnet run --project IepAssistant.Api --no-launch-profile -- seed-demo --reset
```

`--fresh` runs the reset and the seed in one process. Because every date in the district is computed from
"today", a rebuild also rolls the calendar forward: meetings are always in the next month and the overdue
annual reviews are always genuinely overdue.

Notes:

- `ASPNETCORE_URLS` is set as an environment variable rather than a `--urls` flag on purpose: `dotnet run`
  forwards `--urls` to the app *ahead* of the verb. (The CLI now finds `seed-demo` anywhere in its
  arguments, so this is belt-and-braces rather than a requirement.)
- The command is **refused in Production**.
- `--reset` deletes only rows reachable from the demo district, plus any account on the fictional domain.
  Nothing else in the database is touched. Audit-log rows are immutable by design and are left in place.
- A full rebuild takes a few minutes: it drives the real services end to end, so every finalized document
  really is finalized, every PDF really is rendered, and every goal record really is projected.

## What is in it

**3 buildings** — Maple Ridge Elementary (K–5), Maple Ridge Middle School (6–8), Maple Ridge High School (9–12).

**17 staff**, the mix a district of this size actually employs:

| Count | Role | Where |
| --- | --- | --- |
| 1 | Director of Special Education (District Admin) | district-wide |
| 3 | Principal (School Admin) | one per building |
| 6 | Intervention Specialist (Teacher) | two per building |
| 4 | SLP, OT, PT, School Psychologist (Related Service Provider) | serve all buildings |
| 3 | General Education Teacher | one per building |

**42 students**, 13–16 per building, with caseloads of about 7 students per intervention specialist. The
disability mix follows IDEA's real shape — specific learning disability is much the largest group, then
speech/language, autism and other health impairment, with the low-incidence categories appearing once each.
Four families have a home language other than English (Spanish and Hmong).

**Every student has an IEP team** of four to six people: the case manager as lead (Owner access), the
building principal as LEA representative, the building's general education teacher, and the related-service
staff the student's disability actually calls for.

**Documents** — every student has the paperwork their situation implies:

| Where the student is | How many | What exists |
| --- | --- | --- |
| On an IEP | 31 | A finalized Ohio IEP with goals, services, accommodations and progress data |
| Just reevaluated | 4 | A finalized ETR, then this year's finalized IEP, and a closed reevaluation case |
| Newly eligible | 3 | An IEP being written right now — a draft, not yet finalized |
| Initial evaluation finished | 2 | Consent, assessments, a finalized ETR, eligibility determined, first IEP started |
| Initial evaluation running | 2 | An open case: one awaiting consent, one mid-assessment with work overdue |

The IEPs are filled in, not stubs: all 14 sections of the Ohio form including future planning, special
instructional factors, present levels, ESY, LRE and placement, statewide testing, meeting participants and
signatures. Goals, services and accommodations differ by disability category, and students in grade 9 and
up carry postsecondary transition goals. Progress observations run fortnightly, trending upward for most
students and flat for roughly one in seven — so the progress view has something to interpret.

**14 meetings** — four already held (with the decisions the team recorded, including a disagreement, and
attendance taken, one related-service provider excused by written agreement), eight scheduled across the next
month with RSVPs in (the family has accepted on all but one, which is still waiting on a reply), and two
further out whose notes say the family has not settled on a time. Each is created by the student's own case
manager, so the staff logins have their own calendars, and no student has more than one meeting.

**8 families** — seven linked to their child's record; two of those have been sent the IEP draft and have
replied with questions, an agreement and an acknowledgement. One invite is deliberately left unaccepted:
the pending row a district chases. Parents share their child's surname.

**1 student account** — Kevin Olsen, grade 11, invited by his case manager, for the student view of
transition planning.

**10 family contact attempts**, including a three-attempt sequence (voicemail, no answer, letter) on one
student — the documented outreach a district needs when a family does not respond.

## Logins

Every account below uses the password `Demo!2026pw`.

| Role | Email |
| --- | --- |
| Director of Special Education (District Admin) | dana.whitfield@mapleridge.example |
| Principal, Maple Ridge Elementary | priya.raman@mapleridge.example |
| Principal, Maple Ridge Middle School | marcus.boyd@mapleridge.example |
| Principal, Maple Ridge High School | helen.osei@mapleridge.example |
| Intervention Specialist, Elementary | jordan.rivera@mapleridge.example |
| Intervention Specialist, Elementary | alicia.moreno@mapleridge.example |
| Intervention Specialist, Middle School | morgan.chen@mapleridge.example |
| Intervention Specialist, Middle School | derek.vaughn@mapleridge.example |
| Intervention Specialist, High School | casey.nguyen@mapleridge.example |
| Intervention Specialist, High School | tasha.bell@mapleridge.example |
| Speech-Language Pathologist (all buildings) | sam.okafor@mapleridge.example |
| Occupational Therapist (all buildings) | robin.alvarez@mapleridge.example |
| Physical Therapist (all buildings) | noor.haddad@mapleridge.example |
| School Psychologist (all buildings) | elena.vogel@mapleridge.example |
| General Education Teacher, Elementary | avery.thompson@mapleridge.example |
| General Education Teacher, Middle School | riley.patel@mapleridge.example |
| General Education Teacher, High School | devon.brooks@mapleridge.example |
| Parent of Aiden Bennett (sent a draft, has replied) | jamie.bennett@mapleridge.example |
| Parent of Fiona Hayes (sent a draft, has replied) | alexis.hayes@mapleridge.example |
| Parent of Hannah Kim | monique.kim@mapleridge.example |
| Parent of Julia Mitchell | victor.mitchell@mapleridge.example |
| Parent of Quentin Underwood | sonia.underwood@mapleridge.example |
| Parent of Yusuf Castillo | andre.castillo@mapleridge.example |
| Parent of Elias Iverson | rosa.iverson@mapleridge.example |
| Parent of Oscar Silva — **invite pending, cannot log in** | deborah.silva@mapleridge.example |
| Student — Kevin Olsen, grade 11 | kevin.olsen.student@mapleridge.example |

## A demo path that works

1. **Dana Whitfield (district admin)** — the district dashboard: overdue annual reviews, what is due in the
   next 30 days, evaluation clocks running, work spread across three buildings.
2. **Jordan Rivera or Alicia Moreno (elementary intervention specialist)** — a real caseload: their own
   students, their own meetings, the drafts they owe.
3. **Open Fiona Hayes** — a finished Ohio IEP: goals with baselines and criteria, services with frequency
   and duration, accommodations, the rendered PDF, and the progress graph behind each goal.
4. **Open Zoe Dunn or Oscar Silva** — an IEP mid-authoring, to show the authoring experience rather than a
   finished artifact.
5. **Open Owen Sanders or Caleb Gibbs** — an evaluation with the clock running, consent tracked and an
   evaluator assignment overdue.
6. **Log in as Alexis Hayes (parent)** — the same IEP from the family's side, the draft they were sent, and
   the questions they asked back.
7. **Log in as Kevin Olsen (student)** — transition planning from the student's own account.
8. **Reset** with `seed-demo --fresh` before the next demo.

## Where the roster lives

- `api/IepAssistant.Api/Seeding/DemoRoster.cs` — the district: buildings, staff, all 42 students, families.
  Edit here to change who exists.
- `api/IepAssistant.Api/Seeding/DemoIepContent.cs` — the goals, services, accommodations and transition
  plans each disability category produces.
- `api/IepAssistant.Api/Seeding/DemoSeeder.cs` — drives the real application services to build and remove
  it. `SeedAsync` validates the roster first and fails fast on a contradiction (a parent linked to the wrong
  child, a student in a building that does not teach their grade, a building with no case manager).
