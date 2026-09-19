---
module: "Advocate"
date: "2026-09-19"
problem_type: "best_practice"
component: "service_object"
symptoms:
  - "Every parent-facing AI surface was scoped to one artifact (one PDF's analysis, one shared draft); nothing could answer 'is this goal weaker than last year's?' or 'anything from the last month I should raise?' across the child's whole record"
  - "The only Claude path was a single non-streaming completion; adding a chat needed tools, streaming and multi-turn on a community SDK with no worked example in the codebase"
  - "Review found the usual streaming-agent traps once built: a client abort disposed an async iterator mid-MoveNext, the usage cap was check-then-act, a byte-cut tool result became invalid JSON, a screen reader never heard the finished answer"
root_cause: "missing_workflow_step"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [claude, tool-use, streaming, sse, async-iterator, cancellation, prompt-injection, usage-cap, serializable, deadlock, aria-live, remark, code-review, anthropic-sdk]
---

# A tool-using Claude "advocate" over SSE in ASP.NET Core: what the streaming agent had to get right

## Problem

Parents needed one place to ask anything a professional advocate would answer — IEPs, ETRs, the process, federal and Ohio law — *in the context of their child's whole record*. The plan (`docs/plans/2026-09-19-001-…`) chose a **tool-using agent** (Claude calls read-only, parent-scoped tools as it needs them) over a single "case brief" call, streamed over SSE, with a dated Journal feeding it. The build was straightforward; five review passes then surfaced a cluster of defects that are each a *class* of mistake in any streaming agent, which is why this is worth keeping.

## Environment

- .NET 9 / EF Core 9 (SQL Server in QA, SQLite in-memory tests); community `Anthropic.SDK` 5.10.0 (not the official SDK); Claude via `IClaudeClient`
- React 19 + Vite 7; SSE consumed with `fetch` + `ReadableStream` (Bearer auth rules out `EventSource`)
- Branch `feat/virtual-advocate-and-journal`, HEAD `e4d61f5`

## What was built (the shape that worked)

- **`IClaudeClient.StreamWithToolsAsync`** — a manual loop over the SDK's `StreamClaudeMessageAsync`: buffer each turn's raw events, `new Message(outputs)` to assemble the assistant turn (thinking blocks and signatures included), yield text deltas as they arrive, execute every `ToolUseContent` and return **all** results in **one** user message (`is_error` for failures, never dropped), stop on `end_turn` / `MaxToolRounds` / `max_tokens`. Two SDK quirks needed handling: a no-argument tool call streams blank `partial_json` and the assembler drops it (re-add with `{}`), and `max_tokens` mid-`tool_use` makes assembly throw (treat as truncated, not `InvalidResponse`).
- **One parent-scoped toolset** (`AdvocateToolset : IToolExecutor`) closed over the resolved `childId`. Every id the model passes is re-checked to belong to that child (same "Not found." for sibling, stranger and nonexistent); every free-text field passes `PromptText.Data`; every item carries a `sourceRef` registered in `ReturnedRefs`, and the answer parser keeps only citations whose refs were returned *this turn*. Nothing staff-only has a code path in. This is the load-bearing security property and agent-smith traced it end to end.
- **Frozen system prompt + static tool list** so `PromptCaching = AutomaticToolsAndSystem` actually caches; all volatile context (child name, state, date, launcher sentence) lives in the current user turn.
- **SSE endpoint** that runs the service's pre-checks *before* `Response.StartAsync` so validation/403/404/429/503 are ordinary JSON, then streams `delta|tool|done|error` frames with per-frame flush and `: ping` comments.
- **Web**: a WHATWG-correct SSE parser (frames split across chunks, CRLF, comments), per-thread state read through `forThread(...)` so a late callback can never leak into another thread, and handoffs (`prep_question`, `journal_entry`, `open_kb`, `open_goal`) that navigate into existing flows — the advocate never writes to the record.

## What review found (5 passes: 10 P2 fixed, 16 P3 follow-ups)

1. **Disposing an async iterator with a `MoveNextAsync` in flight throws `NotSupportedException`.** The controller raced `enumerator.MoveNextAsync()` against pings and threw on `RequestAborted`; the `await using` then disposed the compiler-generated iterator mid-MoveNext (reproduced in a scratch probe), the service's `finally` ran detached against a disposing scoped `DbContext`, and the exception middleware tried to set headers on a started response. Fix: always **observe the pending `next`** (any exception, not just cancellation) before returning/throwing from the ping-wait helper; catch `OperationCanceledException when ct.IsCancellationRequested` in the action; `if (Response.HasStarted) return;` in the middleware.
2. **A usage cap that is counted after success is not a cap.** Concurrent sends all passed a plain `CountAsync`, and a scripted client could read the full answer from the deltas and abort before `done` — billed to the operator, never counted. Fix: **reserve up front** in a Serializable count-then-insert transaction in the same unit of work as the user row, before the model is called; release only when *nothing was forwarded* (a tool frame counts as output — it already cost tool round-trips). Two residuals followed: the reservation's refund went through the change tracker and was poisoned by a failed assistant-row save (use `ExecuteDeleteAsync`), and the deadlock victim surfaces from `SaveChangesAsync` wrapped in `DbUpdateException`, so a `catch (SqlException)` never fired — and a retry on the same `DbContext` re-Adds the still-tracked rows (detach Added entries first; the fail-first test showed 2 usage records without it).
3. **`json[..cap]` is not truncation.** Trimming "longest array first" emptied the most valuable arrays (goal analyses) while untrimmable nested prose survived, then a byte cut produced invalid JSON with the `truncated` marker (appended last) gone. Fix: escalate structurally — drain registered arrays in *declared priority order*, drop whole low-value keys into a `dropped` list (attached *inside* the loop so its own bytes count), halve top-level strings, and fall back to a minimal always-valid object.
4. **Retry re-persisted the question.** The UI's Retry re-POSTs the same text; the service unconditionally inserted a new user row. Fix: reuse the latest unanswered user row with identical text and exclude it from history.
5. **A live region under an `aria-busy` ancestor is dropped, not replayed.** The finished answer was announced from a status node inside the conversation region while it was still busy; Gecko fills `container-busy` from any ancestor and NVDA returns early on it. Fix: render the `role="status"` node as a *sibling outside* the region, carry no `aria-busy` at all, hide only the token bubble (`aria-hidden` + `disableLinks`), leave tool rows announced, and announce plain words derived from the **renderer's own remark parse** (a hand-rolled regex stripper diverged from what was rendered: intraword `_`, `4*5=20`, hard breaks). Clear the announcement on thread switch so reopening does not re-read it.
6. **Menus portaled to `document.body` are inert under a `showModal()` Drawer.** The thread kebab could not be used on phones. Fix: portal into `closest('dialog[open]') ?? document.body`.
7. **Abandoning a run without clearing its keyed state** left a thread "streaming" forever (readonly composer, inert Stop). Fix: clear `pending/streaming/failure` in the one place a run is dropped without its own cleanup (the thread-switch effect); every other terminal path already owned its cleanup.
8. Smaller: `?addQuestion=` was a GET-triggered write (now server-persisted and de-duplicated, still a follow-up to require confirmation); a state hint keyed off the parent's profile while the server had already resolved Ohio from the linked district (added `GET /advocate/context`); `disabled` on a checkbox mid-toggle blurs keyboard users (`aria-disabled` + hook guard instead).

## Verification

- `dotnet test IepAssistant.Services.Tests`: **1227 passed** (baseline 1003) — includes the tool-loop tests (parallel tool results in one message, `is_error`, max rounds), toolset ownership theories (sibling/stranger/nonexistent ids per tool), injection payloads in journal/contribution/section/analysis text, reservation-before-model-call proven from an independent `DbContext`, thread-deleted-mid-stream keeps usage, interceptor-driven deadlock retry (1 row / 1 record) and give-up (nothing persisted), FitToCap escalation/valid-JSON, Ohio seed Up/Down on SQLite.
- Web: vitest **695 passed** (baseline 566); `tsc -b`, `test:types`, `vite build` (advocate page is its own lazy chunk), `guard:ux` green; eslint at the 36-error baseline.
- Live smoke (local API on 7200 against the QA DB, demo parent): a real question streamed 306 deltas over four tool rounds, cited two Ohio KB entries, the journal entry and a meeting, and its `prep_question` suggestion persisted through the meeting-prep handoff. Phone layout captured at 400 px.
- **Not verified:** real SQL Server deadlock (SQLite cannot raise 1205 — the retry is proven through a `SaveChangesInterceptor` and a settable classifier seam); screen-reader behaviour on real NVDA/VoiceOver (reasoned from Gecko/NVDA/Blink sources); the SSE endpoint has no controller-host test (framing/cancellation covered by `SseWriterTests`, the pipeline by service tests). Several review fixes changed test expectations deliberately (e.g. `ClaudeApiException` after a delta now *keeps* the reservation); those are the accepted semantics, not weakened assertions.

## Why this works

The recurring root cause was **state owned in one place and released in another** — the iterator vs. the ping loop, the usage count vs. the usage write, the tracked entities vs. the retry, the run vs. its per-thread UI state, the announcement vs. the busy region. Each fix moves the release next to the owner: observe `next` where it was started, reserve where you count, detach where you failed, clear where you abandon, announce outside what is busy. The security property survives because it lives in *one* class (the toolset) that every id and every string must pass through.

## Prevention

- In an SSE/streaming action, never let an exception escape while an enumerator's `MoveNextAsync` is pending; observe it, then decide. Guard global exception middleware with `Response.HasStarted`.
- Any quota that gates a billable call must be **reserved before the call** (count-then-insert under Serializable, or an app lock), and refunds must bypass the change tracker. Match `DbUpdateException.InnerException`, not just the provider exception, and detach Added entries before retrying on the same context.
- Tool results are a contract with the model: cap them *structurally* with a declared priority, and make every exit a valid document that still says it was truncated.
- Give every tool one closure-bound scope (child/tenant) and re-validate every id the model supplies; register the refs you return and filter citations to them.
- Keep the system prompt and tool list byte-stable; put anything dynamic in the user turn, and log cache-read tokens so you can see when caching silently stops (follow-up todo 194/205).
- A `role="status"` announcement must not live under an `aria-busy` ancestor; derive spoken text from the same parser that renders it.
- Any popover portaled to `document.body` must instead target the nearest open `<dialog>`.
- When per-entity UI state is keyed (thread/tab), the code path that *abandons* an operation must clear that key's state — hiding it via a selector is not enough.

## Related

- `docs/solutions/logic-errors/2026-09-16-family-draft-sharing-untrusted-ids-in-prompts-and-hidden-editor-reveal.md` — the "untrusted ids never reach a prompt" rule this feature generalises
- `docs/solutions/best-practices/2026-09-15-grounding-ai-suggestions-in-a-role-filtered-evidence-bundle-with-citations-and-carry-forward-provenance.md` — the single role-filtered projection pattern the toolset mirrors
- `docs/solutions/security-issues/2026-09-17-rich-text-everywhere-tiptap-markdown-at-rest-and-its-sinks.md` — sinks for the new stored markdown (journal, assistant answers)
- Follow-ups: `todos/176`, `180`, `186`–`195`, `197`, `199`, `200`, `202`–`211`, `213`, `214`, `217`, `233` (P3)
