# Virtual Advocate (Parent AI Chat) — Brainstorm

**Date:** 2026-09-19
**Status:** Draft
**Precedes:** `/sht:plan`
**Builds on:** `DraftQuestionService` (plan 2026-09-15-006), `IepAssistService` educator chat, Knowledge Base (plan 2026-03-15-003), Parent Contributions / student evidence bundle (plan 2026-09-15-002), persona `docs/personas/parent-primary.md`, journey `docs/journeys/J1-parent-only-adoption.md`.

## Context

Parents on IEP Advisor get plain-language analysis of uploaded documents, per-goal explanations and private questions on a *shared draft*, and a searchable knowledge base. What they don't have is what a paid advocate gives them: someone who knows their child's whole file, knows the process and the law, and will answer *any* question in context — "can they cut his speech minutes without a meeting?", "what's an ETR supposed to include in Ohio?", "is this reading goal weaker than last year's?", "what do I say if they refuse an IEE?"

Every existing AI surface is scoped to one artifact (one revision, one draft, one analysis). The advocate is scoped to the **child** — and to everything the parent knows about that child, including things that never make it into a school document.

## What We're Building

### 1. The Virtual Advocate — a per-child, persistent AI chat for parents

- A chat surface on the parent side, scoped to a selected child, that answers questions about IEPs, ETRs, the IEP process, parent rights, and federal + state special-education law, **in the context of that child's full record**.
- It reads everything the parent can see about the child: profile, uploaded IEPs / ETRs / progress reports and their analysis results, goals and progress history, IEP version comparisons, contributions, meeting-prep notes and advocacy goals, shared drafts and the parent's private draft notes, meeting dates and obligations — and the new Journal (below). It reads **nothing** the parent can't see (no staff-only notes, no other students).
- It answers from federal (IDEA / 34 CFR) and the child's **state** knowledge-base content — Ohio first — and cites what it used. When it falls back to general model knowledge it says so.
- **Persistent threads** per child: parents can revisit, rename, and delete conversations. Threads are private to the parent (the same privacy boundary as prep notes and analyses — never visible to the school, never part of the evidence bundle). Family members linked to the same child do not see each other's threads.
- **Answer + suggest, no actions (v1).** When an answer implies a next step, the advocate offers a one-tap handoff the parent completes in the normal UI: "Add to meeting-prep questions", "Save to journal", "Open this KB entry", "Open goal X". It never writes to the record itself.
- **Trust posture** (from the persona): plain language at a middle-school reading level; specific, not hedged; every claim traceable to a document line, journal entry, or KB citation the parent can tap; framed as advocacy information, **never legal advice**, with the existing `DraftPrompts.Disclaimer` pattern; and it will say "this looks fine" when it does.
- Included in the paid plan with a generous fair-use cap; free / trial users get a small taste. Usage recorded per message via the existing `UsageRecord` pattern.

### 2. Journal — dated parent notes and updates about the child

- A new, separate feature: dated, rich-text entries (TipTap, markdown at rest — the editor already exists) such as "9/12 — sent home early after a meltdown in math; teacher said the para was out". Optional tags (incident, communication, medical, progress, other) and an optional link to a document / goal / meeting.
- Private to the family. Not shared with the school in v1 (Contributions remain the share-with-school channel for standing facts). The advocate always has the journal as a **recent-events timeline**, so "anything happen lately I should raise?" works.
- Distinct from Contributions on purpose: Contributions are standing facts (strengths, concerns, what works); the Journal is *what happened, when*.

## Why This Approach

We chose a **tool-using advocate** (approach B) over the single-call "case brief" pattern used by `DraftQuestionService` (A) and RAG over raw PDFs (C).

- **It scales with the record.** A parent with five years of IEPs, ETRs, and progress reports blows past any per-turn character budget; a tool loop pulls only what the question needs.
- **Citations are exact.** The advocate cites the section, goal, journal entry, or KB entry it actually read — not a line of a lossy brief. That is the persona's non-negotiable ("Dana will quote it in a meeting").
- **It's the natural home for later actions.** v1 is read-only, but "draft a PWN request letter" or "add these three questions to prep" are just more tools with a confirmation card; the architecture doesn't change.
- **Cheaper on simple questions.** "What does LRE mean?" should cost one KB lookup, not the whole record.
- **The cost is a new capability in `ClaudeClient`** (tool-use loop, tool definitions, per-tool result tagging as data) and a stricter test burden on data boundaries — every tool must enforce the same parent access rules as the API. That is bounded and worth it.

RAG over PDFs was rejected because the analysis pipeline already produces structured extracts for every document type; re-chunking the raw PDFs would re-solve a solved problem and add embedding infrastructure.

## Key Decisions

1. **Scope = the child.** One thread belongs to one child; the parent must hold an accepted `ChildLink` (Viewer for reading threads, Collaborator+ for asking). Switching children switches threads.
2. **Read tools only, parent-scoped.** Every tool takes the resolved `childId` from the access check — never from the model — and returns only what the parent's own endpoints would return. Candidate tool set: `search_knowledge_base(query, state)`, `get_child_summary`, `list_documents`, `get_document_analysis(id)`, `get_document_section(id, section)`, `get_goals_and_progress`, `compare_iep_versions(a, b)`, `list_journal(since?, tag?)`, `list_contributions`, `get_meeting_prep`, `list_meetings_and_deadlines`, `get_shared_draft(rev)`.
3. **Tool results are data.** Every tool result is wrapped in the same data tags as `DraftPromptBuilder.Data`; the system prompt treats tool output and document text as untrusted content, never instructions (the AST/markdown defences from plan 8 apply to rendered answers).
4. **Bounded loop.** A hard cap on tool rounds per turn (e.g. 6) and on total tokens; if the cap is hit the advocate answers with what it has and says what it couldn't check.
5. **Ohio first, curated KB.** Seed `KnowledgeBaseEntry` rows with `State = "OH"` covering the Ohio Operating Standards for the Education of Children with Disabilities (OAC 3301-51), ORC 3323, ETR/IEP timelines, PWN, IEE, dispute resolution, transition. The state comes from the child's school/district when linked, else the parent's `User.State`; if unknown, the advocate answers federally and asks. Adding a state = adding seed content, not code.
6. **Streaming responses.** Chat needs token streaming (and a "checking your documents…" tool-activity indicator) to feel alive; `ClaudeClient` gains a streaming path used only here for now.
7. **Persistence model.** `AdvocateThread { ChildProfileId, ParentUserId, Title, CreatedAt, UpdatedAt }` and `AdvocateMessage { ThreadId, Role, ContentMarkdown, CitationsJson, ToolTraceJson?, CreatedAt }`. Deleting a thread hard-deletes its messages. Tool traces are stored for debugging/audit but not shown beyond a "sources" list.
8. **Privacy boundary is explicit in the UI.** Header label: *Private — only you can see this. The advocate can read your child's documents and journal; it cannot change anything or contact the school.*
9. **Journal is its own entity** (`JournalEntry { ChildProfileId, OccurredOn, Tag, ContentMarkdown, LinkedDocumentId?, LinkedGoalId?, LinkedMeetingId?, CreatedById }`), private to the family, editable by Collaborator+ on the child, with the existing markdown-at-rest sanitisation.
10. **Handoffs, not actions.** Suggested next steps render as cards that deep-link into existing flows with prefilled content; nothing is persisted until the parent confirms there.
11. **Subscription.** Included in the paid plan; per-message `UsageRecord` with `OperationType = "advocate_message"`; fair-use cap per subscription year surfaced softly (banner at 80 %, block with upgrade/contact at 100 %); trial gets a small fixed allowance. Never a per-question paywall moment inside a thread.
12. **Entry points.** A dedicated *Advocate* page per child in parent navigation, plus a launcher from document/analysis/goal pages that opens the child's thread with that artifact as the opening context ("Ask the advocate about this goal").
13. **Failure posture.** Claude/tool failures return a friendly retry and never a half-answer; the existing `ClaudeFailureKind` handling and model-retirement guard apply.

## Resolved Questions

- **Grounding?** Full child record + a new Journal feed. (Resolved above.)
- **Notes model?** New dated Journal, separate from Contributions.
- **State law?** Ohio first, curated KB entries with citations; model knowledge only as labelled fallback.
- **Persistence / pricing?** Persistent per-child threads, included in the subscription with fair-use cap.
- **Actions?** v1 answers and suggests handoffs only.
- **Architecture?** Tool-using advocate (B).

## Open Questions

Non-blocking; carry into the plan with the stated assumption.

1. **Fair-use numbers.** Assume 300 messages / subscription year per parent and 20 for trial; tune from usage data.
2. **Journal sharing later?** Assume v1 is private-only; a per-entry "share with school" toggle (mirroring Contributions) can come later if the school side wants it.
3. **Multilingual.** The persona set includes a multilingual parent; assume the advocate answers in the parent's UI language when that lands, and English-only for v1.
4. **Model choice per tool round.** Assume the same model as analysis for v1; a cheaper model for tool-planning rounds is an optimisation to measure, not decide now.
5. **Conversation memory across threads.** Assume threads are independent; a "what we've discussed before" tool over prior threads is a v2 candidate.
6. **Ohio content authorship.** Assume the seed is written in-house in brand voice with section citations, reviewed by someone with Ohio SpEd advocacy experience before launch. Who reviews is Brad's call.

## Out of Scope (v1)

- Any write action by the advocate (letters, prep edits, journal writes) beyond confirmable handoffs.
- School- or student-side access to the advocate or the journal.
- States other than Ohio beyond federal coverage.
- Voice input, document upload from inside the chat.
