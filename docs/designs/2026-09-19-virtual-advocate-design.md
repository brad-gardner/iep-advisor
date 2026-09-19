# Design Discussion: Virtual Advocate + Journal

**Date:** 2026-09-19
**Feature:** A per-child, private, persistent, tool-using AI chat for parents ("the advocate") plus a dated rich-text Journal that feeds it.
**Origin:** `docs/brainstorms/2026-09-19-virtual-advocate-brainstorm.md`

## Current State

- **Claude access** is one method: `IClaudeClient.CompleteAsync(ClaudeCompletionRequest)` → `ClaudeClient` (`api/IepAssistant.Services/Implementations/ClaudeClient.cs`) wrapping community **`Anthropic.SDK` 5.10.0**. Single user turn, optional PDF, adaptive thinking, `OutputConfig.Effort` from `AnthropicOptions`, every failure classified into `ClaudeFailureKind` and thrown as `ClaudeApiException`. No tools, no streaming, no multi-turn.
- **Multi-turn exists only as prompt folding**: `IepAssistService.ChatAsync` (educator) folds prior turns + a compact draft into one call; ephemeral.
- **Parent-private AI Q&A pattern**: `DraftQuestionService` (`Implementations/DraftQuestionService.cs`) — access via `ParentAccessResolver`, untrusted text through `DraftPromptBuilder.Data`/`PromptText`, answer persisted as `ParentDraftNote`, usage row `UsageRecord{OperationType="draft_question"}`, disclaimer from `DraftPrompts.Disclaimer`, citations parsed with a tolerant parser.
- **Evidence bundle pattern**: `IStudentEvidenceService` builds a role-filtered projection for staff (docs/solutions best-practice 2026-09-15). There is no parent-side equivalent.
- **Access**: `IAccessService.HasMinimumRoleAsync(childId, userId, AccessRole)` (Viewer/Collaborator/Owner over `ChildProfile` + `ChildAccess`); school link via `ChildLink`; state via `District.StateCode` / `School.StateCode` / `User.State`.
- **Knowledge base**: `KnowledgeBaseEntry{Title, Content, Category, LegalReference, State, Tags}`; 44 federal entries seeded in migration `20260315020052_AddKnowledgeBase`; `IKnowledgeBaseService.SearchAsync(query, category, state)` does `LIKE` search and includes `State == null || State == state`.
- **Contributions**: `ParentContribution` (standing facts, optional share) with `about-my-child-card.tsx` on the child overview tab; controller `ParentContributionsController`.
- **Rich text**: `RichTextEditor` (TipTap, markdown out, `MarkdownLimit`), `Markdown` renderer (react-markdown + rehype-sanitize), API-side `RichTextSanitizer` + `MarkdownLinkAst` gate.
- **Subscription**: `User.SubscriptionStatus` ("active"/…), `UsageRecord` counting per subscription year; analysis capped at 5/child/year via `TryReserveUsageAsync`. District-billed operations carry `DistrictId`.
- **Web**: React 19 + Vite, feature folders, axios `apiClient` with Bearer token; parent child pages under `/children/:childId/*` tabs (`web/src/app/routes.tsx:236-251`). No SSE / streaming anywhere.
- **Tests**: 75 service test files on SQLite in-memory; `IClaudeClient` mocked.

## Patterns to Follow

1. **`ClaudeClient` error contract** — extend, don't fork: the new streaming/tool path must classify failures through the same `Classify` + `ClaudeApiException` arms.
2. **`DraftQuestionService` shape** for the parent-side service: access resolution first, untrusted text only via `PromptText.Data`, usage row, disclaimer, canned failure message.
3. **`IStudentEvidenceService`** as the model for "one role-filtered projection": the advocate's tools will be a parent-scoped equivalent, `IAdvocateToolset`, and nothing else has a code path into the model.
4. **`ParentContributionsController` + `contributions` feature folder** as the template for the Journal (controller, service, DTOs, `*-api.ts`, card component, tests).
5. **Markdown-at-rest** conventions from docs/solutions 2026-09-17: `RichTextSanitizer` at persist, `Markdown` at render, `MarkdownLimit` on the editor, and the advocate's own markdown replies rendered through the same sanitised `Markdown` component.
6. **`KnowledgeBaseEntry` seed via EF migration `InsertData`** for Ohio content, `State = "OH"`.
7. **Service tests on SQLite** with a fake `IClaudeClient` that scripts tool calls, mirroring `DraftQuestionServiceTests`.

## Desired End State

### Backend

```
IClaudeClient
  CompleteAsync(...)                                   // unchanged
  + StreamWithToolsAsync(ClaudeToolRequest, IToolExecutor, IAsyncEnumerable<ClaudeStreamEvent>)
```

- `ClaudeToolRequest { SystemPrompt, Messages (prior turns), Tools (name/description/JSON schema), MaxTokens, MaxToolRounds }`.
- `ClaudeClient.StreamWithToolsAsync` runs the loop: `StreamClaudeMessageAsync` → emit `TextDelta` events as they arrive → on `stop_reason == "tool_use"` invoke `IToolExecutor.ExecuteAsync(name, inputJson)` for every `ToolUseContent` in the turn, append one user message with all `ToolResultContent`s (parallel tool calls answered in one message; failures returned with `IsError = true`, never dropped), emit `ToolStarted`/`ToolFinished` events, repeat until `end_turn`, `MaxToolRounds` reached, or `max_tokens`. Final event carries the full text, tool trace, and usage. `PromptCaching = AutomaticToolsAndSystem` so the frozen system prompt + tool list are cached across turns.
- Thinking stays adaptive with configured effort; tool inputs parsed with `System.Text.Json`, never string-matched.

```
IAdvocateService
  ListThreadsAsync(userId, childId)
  CreateThreadAsync(userId, childId, title?)
  GetThreadAsync(userId, threadId)               // messages + citations
  RenameThreadAsync / DeleteThreadAsync
  SendMessageAsync(userId, threadId, text, ct) -> IAsyncEnumerable<AdvocateStreamEvent>
IAdvocateToolset (parent-scoped; built per request from the resolved childId)
IJournalService (CRUD, parent-scoped, Collaborator+ for writes)
```

- `SendMessageAsync`: validate (≤ 2 000 chars), `HasMinimumRoleAsync(childId, userId, Collaborator)`, fair-use check (`advocate_message` count this subscription year vs. plan cap; trial cap), persist the user message, build the system prompt (advocate persona, trust rules, disclaimer, child's state, today's date), load the last N turns (budgeted), stream the loop, persist the assistant message with `CitationsJson` + `ToolTraceJson` + usage, write `UsageRecord`. Any `ClaudeApiException` → a stored assistant message is **not** written; the stream ends with an `Error` event carrying a friendly message; the user message stays so they can retry.
- **Tools (all read-only, all take the childId from the closure, never from the model):**
  `search_knowledge_base(query, category?)` → federal + child's state entries with ids and legal refs; `get_child_summary()`; `list_documents()` (IEPs, ETRs, progress reports, authored/shared versions with dates and analysis status); `get_document_analysis(documentType, documentId)`; `get_document_section(documentId, sectionType)` (raw section text, budgeted); `get_goals_and_progress(iepDocumentId?)`; `compare_iep_versions(iepIdA, iepIdB)` (existing comparison service); `list_journal(sinceDays?, tag?)`; `list_contributions()`; `get_meeting_prep()`; `list_meetings_and_deadlines()`; `get_shared_draft(revisionId)` (only revisions the parent can already open); `list_advocacy_goals()`.
  Every tool result is JSON with a stable `sourceRef` per item (`{ kind, id, label }`) and every free-text field wrapped by `PromptText.Data`. Results are budgeted (per-tool char caps + `truncated: true` flag).
- **Citations**: the system prompt asks the model to end with a `<sources>` block listing the `sourceRef`s it relied on; the parser is tolerant (missing block ⇒ no citations, never a failure). Citations map to deep links in the UI.
- **Handoffs**: the model may emit `<suggest kind="prep_question|journal_entry|open_kb|open_goal" ...>` blocks; the API strips them from the stored markdown and returns them as structured `suggestions` the UI renders as cards. No tool writes anything.
- **Entities**: `AdvocateThread`, `AdvocateMessage`, `JournalEntry` (see ERD in the plan). Thread delete cascades messages.
- **Endpoints** (parent, `[Authorize]`):
  `GET/POST /api/children/{childId}/advocate/threads`, `GET/PATCH/DELETE /api/advocate/threads/{id}`, `POST /api/advocate/threads/{id}/messages` (SSE response: `text/event-stream`, events `delta`, `tool`, `done`, `error`), `GET /api/advocate/usage`;
  `GET/POST /api/children/{childId}/journal`, `PUT/DELETE /api/journal/{id}`.
- **Seed**: ~35 Ohio entries (`State="OH"`), citing OAC 3301-51-01/-05/-06/-07/-09/-11 and ORC 3323 (ETR timelines, IEP timelines, PWN, consent, IEE, LRE, discipline/manifestation, dispute resolution — mediation/complaint/due process, transition at 14, ESY, extended time for evaluation, parent participation), plain language in brand voice.

### Web

- `web/src/features/advocate/`: `advocate-api.ts` (thread CRUD + `streamMessage` via `fetch` + `ReadableStream` SSE parser with abort), `hooks/use-advocate-thread.ts`, components: `advocate-page.tsx` (thread list + conversation, phone-first), `message-list.tsx`, `assistant-message.tsx` (sanitised `Markdown`, sources chips → deep links, suggestion cards), `tool-activity.tsx` ("Checking your documents…"), `composer.tsx`, `privacy-banner.tsx`, `usage-notice.tsx`.
- Route `/children/:childId/advocate` as a child tab; launcher buttons on IEP/ETR/goal/analysis pages ("Ask the advocate about this") that open the thread with a prefilled opening context (`?about=goal:123`).
- `web/src/features/journal/`: `journal-api.ts`, `journal-card.tsx` on the child overview tab (list, add/edit drawer with `RichTextEditor`, date picker, tag select, optional links), full list at `/children/:childId/journal`.

## Design Decisions

1. **Stay on `Anthropic.SDK` 5.10.0** — it has streaming, tools, and caching; switching SDKs is out of scope and would touch every AI service.
2. **Manual tool loop inside `ClaudeClient`, not `Common.Tool.InvokeAsync`** — the SDK's reflection-based invocation would bind tools to static methods; our tools need the per-request childId closure and access checks. Loop is ~80 lines and testable.
3. **SSE over `fetch`, not SignalR/WebSockets** — one-directional, no new dependency, works through the existing Bearer interceptor pattern; cancellation via `AbortController` → `CancellationToken`.
4. **One tool set, closed over `childId`** — the model never passes a child id; ids it does pass (document ids) are re-checked to belong to that child before any read. Mirrors the "untrusted ids never reach a prompt" learning.
5. **Citations by `sourceRef`, not free text** — a citation is only shown if it matches a `sourceRef` the tools actually returned in this turn.
6. **Bounded loop**: `MaxToolRounds = 6`, per-tool result cap ~6 000 chars, total tool-result budget per turn ~30 000 chars, history budget last 12 turns / ~20 000 chars. When a cap is hit the system prompt tells the model to answer with what it has and say what it couldn't check.
7. **Fair use**: `advocate_message` counted per user per subscription year; cap 300 (paid) / 20 (trial or beta code) surfaced by `GET /api/advocate/usage`; soft banner at 80 %, block at 100 % with the same subscription CTA the analysis cap uses. Included in the plan — no per-message paywall.
8. **Threads are private to the asking parent** (`ParentUserId`), even for co-parents with `ChildAccess` — same rule as `ParentDraftNote`. Journal entries are shared among everyone with access to the child (they describe the child, not a conversation).
9. **Journal is markdown-at-rest** with the existing sanitiser and a 4 000-char markdown limit; `OccurredOn` is a date (no time); tags are a fixed enum (`Incident, Communication, Medical, Progress, Other`) — no runtime editing needed, matches `ParentContributionKind`.
10. **Model/effort** come from `AnthropicOptions` as everywhere else; no per-feature override.
11. **Prompt caching**: `PromptCacheType.AutomaticToolsAndSystem`; system prompt is frozen text + a small dynamic tail (child first name, state, date) placed *after* the cached prefix in the first user turn, not in the system block.
12. **Logging**: tool trace (names, arg sizes, result sizes, durations) and usage tokens logged per turn and stored on the message for support; never the tool result bodies.

## Open Questions

1. **Trial allowance**: 20 messages total for non-subscribed users — or none (paid-only, with the marketing page describing it)? *Assumed: 20.*
2. **Journal visibility to co-parents**: shared among all users with `ChildAccess` (assumed) vs. private to the author like threads?
3. **Thread history in the prompt**: last 12 turns. Summarising older turns is deferred — acceptable?
4. **Ohio content review**: who signs off on the ~35 entries before launch? Plan will ship them behind a `IsActive` flag so review can happen after merge.

## Testing Strategy

- **Service tests (SQLite)**: `ClaudeClientToolLoopTests` with a fake SDK stream (tool_use → result → end_turn; max rounds; parallel tool calls in one user message; `is_error` result); `AdvocateServiceTests` — access denied for non-linked user; thread privacy between co-parents; every tool refuses a document id from another child; injection text inside a journal entry stays inside data tags; citations only from returned `sourceRef`s; usage cap blocks at limit; failure leaves no assistant row; `JournalServiceTests` — CRUD, role gating, sanitiser applied, limit enforced; `KnowledgeBase` — Ohio entries returned for `state="OH"` and hidden for `"PA"`.
- **Controller/SSE test**: stream emits `delta`/`tool`/`done` frames and `error` on `ClaudeApiException`.
- **Web (vitest)**: SSE parser unit tests (split frames, abort), advocate page renders streaming text, sources chips link correctly, suggestion cards call the right handoff, journal card with the editor stand-in.
- **Live check**: parent with an uploaded IEP asks "is the reading goal measurable?" and gets an answer citing the goal; asks "what's the ETR timeline in Ohio?" and gets the OAC 3301-51-06 entry cited.
