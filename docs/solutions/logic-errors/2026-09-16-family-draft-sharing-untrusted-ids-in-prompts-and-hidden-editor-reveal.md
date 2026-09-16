---
module: "FamilyDraftSharing"
date: "2026-09-16"
problem_type: "logic_error"
component: "service_object"
symptoms:
  - "Parent home card for a shared draft linked to /children/{id}/shared-drafts/{revisionNumber}, which resolves to a different or missing revision once more than one document has ever been shared"
  - "A parent-supplied targetRowId was spliced verbatim into the Claude user prompt outside any data tag"
  - "AI section explanations were generated and cached but never matched to a template section, so narrative sections had no Explain affordance"
  - "Converge's jump-to-field silently did nothing after the editor was kept mounted behind a hidden div, and later still raced React Router's transition-wrapped search-param update"
  - "Double-tapping Mark as reviewed (or Draft with AI) could surface a 500 from the unique index instead of the idempotent success the UI promised"
root_cause: "logic_error"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [family-draft-sharing, prompt-injection, citations, revision-id, hidden-attribute, react-router, start-transition, request-animation-frame, unique-index, db-update-exception, ef-core, n-plus-one, code-review]
---

# Family draft sharing: untrusted ids never reach a prompt, links carry the entity id, and a hidden editor is revealed *before* you scroll to it

## Problem
Plan 6 (`docs/plans/2026-09-15-006-…`) let staff share a frozen IEP draft revision with the family,
gave parents cached plain-language explanations, private AI-answered questions with citations, and
per-item responses that staff resolve in a Converge tab; it also added post-meeting family summaries.
The implementation passed its 762 backend / 407 web tests and a live QA smoke, and three review passes
still found a cluster of defects that are worth keeping because each is a *class*, not a typo.

## What went wrong

1. **Two ids that look alike.** `SharedDraftRevision` has a global `Id` and a per-document
   `RevisionNumber` (1, 2, 3 … restarting per instance). The route `/children/{childId}/shared-drafts/{rev}`
   resolves `rev` as the `Id`. Notifications and the home feed each built the link independently; the
   home feed used `RevisionNumber`. It agreed with `Id` for exactly the first revision ever created in the
   database — which is what every test and the QA smoke exercised.
2. **One untrusted value skipped the guard.** Draft text and parent questions went through
   `DraftPromptBuilder.Data()`/`OneLine()` inside `<draft>`/`<question>` data tags, and there was even a
   regression test for injection *in draft text*. The optional `targetRowId` (a client-supplied string)
   was formatted into a bare sentence — "The parent is asking specifically about [F:…|R:{rowId}]" — with
   no encoding and no data tag. Self-directed only (the answer lands in that parent's own private note),
   but a genuine hole in a guarantee the class documents.
3. **Generated content the UI could not attach.** The explanation prompt asked the model to echo
   "the section title exactly as it appears in the draft" — but the rendered draft lines never included
   the section title. The model paraphrased ("Measurable annual goals" vs "Section 6: Measurable Annual
   Goals"), the client matched by title, nothing matched, and narrative sections (Present Levels) had no
   Explain button while the district was billed for the call.
4. **`hidden` is not "unmounted".** Review asked for the editor to stay mounted while the Converge tab
   shows (so the assistant chat thread and autosave queue survive a tab round-trip). The obvious
   `<div hidden>` did that — and turned Converge's "jump to field" into a silent no-op, because
   `scrollIntoView()`/`focus()` do nothing under `display:none`. The first fix (reveal, then jump on the
   next `requestAnimationFrame`) still raced: react-router v7 wraps `setSearchParams` in
   `startTransition`, so "the next frame" is not a happens-before for the reveal.
5. **Check-then-act on a unique index.** `AcknowledgeAsync` and `MeetingSummaryService.DraftAsync`
   did `FirstOrDefault` → insert. The sibling `DraftExplanationService` already caught `DbUpdateException`
   and read back the winner; the other two didn't, so a double-tap became a 500.
6. **Lists carrying blobs.** The one revision projection served both lists and single-revision reads,
   so every list row pulled the whole frozen IEP JSON and then ran two more queries per row.

## Solution

- **Links by entity id, asserted in tests.** `HomeService` now links `/shared-drafts/{d.Id}`; the test
  seeds revision *number 7* on a fresh instance and asserts the exact path, so number and id cannot
  coincide by accident (`HomeServiceTests.ParentHome_DocumentsToReview_IncludesUnacknowledgedActiveSharedDraft`).
- **Resolve before you echo.** `DraftQuestionService` builds the target id and only echoes it when
  `rendered.Resolve(id)` finds a line *we rendered* — the echoed text is the canonical `DraftLine.Id`,
  inside a `<target>` tag; unknown ids are dropped. `[MaxLength(64)]` on both request DTOs plus a
  service-level bound match the `nvarchar(64)` column. `PromptText` now holds the single copy of
  `OneLine`/`Truncate`/`Data`/`StripHtml` used by both prompt builders. Test:
  `Ask_TargetRowId_IsOnlyEchoedWhenItResolvesToARenderedLine`.
- **Name the section on every line, resolve server-side.** Draft lines render as
  `[id] Section title › Field: text`; `DraftLine` carries `SectionId`; `RenderedDraft.ResolveSection`
  maps the model's title to a template section (unique exact match, else the *longest* contained title,
  else `null` on a same-length tie — never a guess) and the explanation's `sectionId` becomes the template
  section id. The client matches by id, then title; `FrozenSectionList` renders "Explain this section" per
  section. Live QA: sections resolved to template ids 27 and 21 after the cached rows were cleared.
- **Wait for the reveal to commit.** `jumpToFieldWhenVisible` polls a frame at a time (bounded, 60)
  until the target has no `[hidden]` ancestor, then scrolls/focuses; it degrades to the old jump if the
  reveal never lands. The test queues rAF callbacks and flushes them by hand: no scroll across two frames
  while the wrapper is hidden, scroll + focus once it is un-hidden.
- **Idempotent on conflict.** Both insert paths catch `DbUpdateException`, clear the tracker, and read
  back the winner (the summary path is filtered `when (existing == null)`).
- **Slim list projection, batched extras.** `RevisionRow` dropped `ValuesJson` (`LoadValuesJsonAsync`
  for the two single-revision paths); `MapForStaffAsync`/`MapForParentAsync` batch acknowledgements and
  open counts over the id set (three queries per list). `DraftResponseService` fetches the blob once per
  *targeted* revision. Test: `Lists_AttributeAcknowledgementsAndOpenCounts_ToTheRightRevision`.
- Also from review: persisted answer citations (`ParentDraftNote.CitationsJson`, migration
  `AddParentDraftNoteCitations`) so a reloaded note still shows what grounded it; `aria-live` on the
  async AI panels; `FamilySummaryPanel` keyed by meeting id so a meeting switch cannot save A's draft
  under B; Save/Send made mutually exclusive; `useConverge` keeps last-good data through a refresh and
  `ConvergePanel` only replaces the tree when there is *no* data.

## Why this addresses the root cause
Each defect was an invariant that held in the one path the tests walked (first revision, well-formed
row id, exact-title echo, editor unmounted, single request, one revision in the list) and broke on the
second. The fixes make the invariant structural — the id comes from the entity, the prompt only echoes
what the server rendered, the section id is resolved where the titles are known, the jump observes the
DOM instead of assuming a schedule, the race is handled at the index that defines it — and each has a
test that walks the *second* path.

## Verification
- `dotnet build IepAssistant.sln && dotnet test IepAssistant.Services.Tests` → 766 passed.
- `npx tsc -b --noEmit && npm run test:types && npx vitest run` → 416 passed; `npm run lint` at the
  36-error baseline; `npm run build`; `npm run guard:ux`.
- Live QA (API on 7200, parent brad@sht.dev ↔ student 21, doc 5): home link `/children/136/shared-drafts/2`;
  forged `targetRowId` inert, 70-char id → 400; batched staff/parent lists correct across two revisions;
  explanations regenerate with section ids; acknowledge idempotent.
- Three review passes (8 configured reviewers, Sonnet): pass 1 → 3 P1 / 15 P2 / 6 P3; pass 2 → 5 P2 /
  3 P3; pass 3 → 1 P2; pass 4 (targeted at the round-3 diff) → 0 P1/P2, recorded in `todos/REVIEWED` at `aca4e54`.
- Limitation: the reveal poll (including its frame-budget fallback, `section-dom.test.ts`) is verified with
  hand-flushed frames, not a real router transition; a browser-level test would close that gap.

## Prevention
- When an entity has both an id and a human-facing number, name the number's property so it cannot be
  mistaken for the id in a link (`RevisionNumber`, never `Revision`), build links in one place, and test
  with a number ≠ id.
- Every client-supplied string that reaches a prompt goes through the shared `PromptText` guard *and* a
  data tag — or, better, is resolved to a server-known id first and never echoed raw.
- If the model must reference something by name, put that name in the prompt and resolve it server-side;
  do not expect the client to match paraphrases.
- `hidden` keeps state but kills layout: any "scroll/focus into it" caller must wait for visibility, and
  a `setSearchParams` reveal is a transition — observe the DOM rather than counting frames.
- Any `FirstOrDefault` → `Add` on a column with a unique index needs the `DbUpdateException` read-back.

## Related
- Plan: `docs/plans/2026-09-15-006-feat-parent-draft-sharing-review-ai-questions-plan.md`
- Findings: `todos/088–112` (P3 follow-ups in `todos/106-pending-p3-parent-draft-sharing.md`)
- Prior learning on grounded AI and tolerant JSON: `docs/solutions/best-practices/2026-09-15-grounding-ai-suggestions-…`
