---
module: "RichText"
date: "2026-09-17"
problem_type: "security_issue"
component: "react_component"
symptoms:
  - "Every paragraph-style field (notes, reasons, decisions, questions, replies, student entries, the authored RichText field) was a plain textarea; formatting was impossible and stored text rendered as one flat block"
  - "Once fields stored markdown, a `[text](javascript:…)` link passed the persist-time sanitizer untouched and reached the PDF as a live hyperlink annotation"
  - "GFM tables and 128-deep nesting either vanished from the PDF or threw inside Markdig, leaving the document version's PDF permanently in Error"
  - "Underline serialised to `++text++` that nothing rendered; the whole TipTap stack shipped in the single 1.7 MB bundle to every page; a lazy-loaded editor remounted mid-typing when the idle warm-up landed"
root_cause: "missing_validation"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [tiptap, markdown, rich-text, react-markdown, rehype-sanitize, markdig, questpdf, sanitizer, url-scheme, react-lazy, suspense, immediately-render, code-review]
---

# Rich text everywhere: TipTap in, markdown at rest, and what a markdown-at-rest system has to defend

## Problem

The request was simple to state — "every text entry that takes paragraph-style text should be a rich text editor that stores markdown, using TipTap, modelled on keystone's toolbar" — and touched 31 fields across 15 features plus every read view of those values, the authored-document PDF composers, prompts and the sanitizer. Plain textareas gave educators no way to structure a rationale or a family summary, and the values rendered as flat `whitespace-pre-wrap` text.

Environment: React 19 + Vite 7 + vitest (jsdom), TipTap 3.31 (`@tiptap/react`, `@tiptap/starter-kit`, `@tiptap/markdown`, `@tiptap/extensions`), react-markdown 10 + remark-gfm + rehype-sanitize; .NET 9 API with Markdig 1.3 and QuestPDF 2024.12.

## What we built

- `web/src/components/ui/rich-text-editor.tsx` (public shell) + `rich-text-editor-impl.tsx` (TipTap, `React.lazy`) + `rich-text-editor-limit.ts` (`MarkdownLimit`, a `filterTransaction` extension that caps the *serialised markdown* length — the thing the server actually stores). Toolbar ported from keystone minus table/hr/underline. Markdown in via `contentType: 'markdown'`, out via `getMarkdown()`.
- `web/src/components/ui/markdown.tsx` — memoised react-markdown + remark-gfm + rehype-sanitize (no raw HTML), `target=_blank rel=noopener`, `disableLinks` for use inside buttons.
- A global vitest stand-in (`web/src/test/setup.ts`) renders the editor as a `<textarea>` with the same id/label/testid so 500+ feature tests kept driving fields with `fireEvent.change`; the real editor has its own unmocked suites.
- API: `MarkdownText` (Markdig AST → structured plain text), `MarkdownPdfPlan` (a pure IR: runs with bold/italic/strike/href, headings, quote depth, flat depth-indexed list items) consumed by `AuthoredDocumentPdfDocument`; `RichTextSanitizer` now understands markdown link syntax.

## What review found (pass 1: 6 P1 / 14 P2; pass 2: 3 P1 / 4 P2; pass 3: 3 P2; pass 4: 2 P2; pass 5: 1 P1 / 2 P2; pass 6: 1 P1 / 1 P2; pass 7: 1 P1 — passes 3–7 were all the link gate; pass 8: confirmation)

1. **Markdown links bypass an HTML sanitizer.** `RichTextSanitizer` only matched `<a href>`; `[x](javascript:alert(1))` has no angle brackets and reached `QuestPDF.Hyperlink()`. Fixed at both ends: a depth-counting destination scanner (nested parens, titles, `<>` form) and reference-definition handling (`[ref]: javascript:…` lines dropped) at persist time, plus `IsSafeUrl` re-checked on Markdig's *resolved* URL at the PDF sink. The sink check is the one that cannot be bypassed by syntax.
1b. **A hand-written scanner for markdown links keeps losing to CommonMark** — `\]` in labels, `\(` in destinations, parens inside a quoted title, a title on the next line, a title spanning a line ending: four review passes each found one more. The durable fix is `MarkdownLinkAst`: parse with Markdig (`UsePreciseSourceLocation`), and rewrite the exact source span of every link/image/autolink/reference definition whose *resolved* URL fails the allowlist. The gate now follows the same parser the sinks use; the regex passes stay in front of it as cheap first-line defence. Two more rounds were still needed on the AST pass itself: outermost-span selection (a nested edit applied first left the outer link intact) and running to a fixed point — CommonMark forbids a link inside a link label, so `[[z](javascript:a)](javascript:b)` parses as *one* link and collapsing it exposes the next; a constant iteration cap was itself a bypass, and bounding by input length was a CPU-exhaustion path (each pass re-parses the document; Markdig is super-linear on this construct — 3 000 levels ≈ 75 s). The resolution is a cap of four passes plus a syntax-breaking fallback: if the text is still changing, every remaining `](` is escaped so no inline link can form. Real content never nests links; adversarial pastes degrade to text.
2. **Markdig `Table` is a `ContainerBlock`** — a switch over `LeafBlock` types silently dropped it. Both walkers now recurse into any unknown container.
3. **Markdig throws `ArgumentException` at its own 128 nesting limit** *before* any depth cap of ours runs, and the PDF service persists `Error` forever because content is frozen. `MarkdownText.Parse` now catches and returns a literal-paragraph document. Lowering `MaximumNestingDepth` only moves the throw.
4. **QuestPDF nested Row→Column→Row per list level was exponential** (23 deep = 25 s). Lists are now flat records with a depth and one `PaddingLeft(depth×14)` row each — linear.
5. **Underline** serialises to a non-standard `++text++` — markdown has no underline; the mark is disabled, not just hidden.
6. **CharacterCount counts plain text; the server caps the markdown string** — replaced by `MarkdownLimit` and `isMarkdownOverLimit` gating submit on all 18 forms with a cap.
7. **Bundle**: +199 kB gzip in the single chunk for every page → `React.lazy` split (main 486 → 340 kB gzip; editor chunk 148 kB).
8. **The lazy split then created two timing bugs**: (a) with `immediatelyRender` (default) TipTap builds the editor *during render*, and every render React discards (API response mid-render, Suspense retry) orphaned an `Editor` with no view — profiled as several `createEditor` calls per cold open and an "editor view is not available" throw; `immediatelyRender: false` creates it once after commit. (b) A module-level "already loaded" cache read on every render swapped element type (Suspense → direct) the moment the idle warm-up landed, remounting a field someone was typing in. The render path is now chosen once per instance via `useState(() => LoadedImpl)`.
9. Smaller: an in-flight ask-question request wiped text typed while waiting (`disabled={isAsking}`); `<label for>` does not focus a contenteditable (`onClick → editor.commands.focus()`); markdown links nested inside `<button role="option">` (`disableLinks`); Escape in the link control dropped focus; the question itself rendered as raw markdown.

## Why these fixes address the root cause

The migration changed the *trust boundary of a string*: text that used to be inert became a document format with links, structure and a parser on both ends. Every defence that assumed "HTML is the only dangerous syntax" or "the parser never throws" or "rendering cost is linear in text length" had to be re-checked at each sink (persist, web render, PDF, prompts). The sink-side checks (rehype-sanitize's default schema on the web, `IsSafeUrl` on the resolved AST in the PDF) are the load-bearing ones; the persist-time sanitizer is defence in depth.

## Verification

- API: `dotnet build` 0 new warnings; `dotnet test IepAssistant.Services.Tests` 1003 passed (884 on main) — includes `MarkdownPdfPlanBuilderTests` asserting bold/italic/strike flags and null `Href` for unsafe schemes, 200-deep quotes / 150-level alternating nesting through both PDF composers, a 30-deep list under a Stopwatch (~4 ms), sanitizer cases for nested parens, titles, reference definitions and next-line destinations.
- Web: `tsc -b`, `test:types`, vitest 566 passed (497 on main; real-editor suites cover markdown in/out, limit enforcement, link control, warm-up no-remount by DOM identity, `RichTextField` autosave with the real TipTap editor), lint exactly at the 36-error baseline, `vite build` (two chunks), `guard:ux`.
- Live (production build served with `vite preview`, demo educator): editor mounts in the schedule-meeting drawer, `**the**` input rule renders bold, bullet list via toolbar, no console errors.

**Limitations.** Timings were taken on a heavily loaded machine (two other processes at 90 %/66 % CPU), so absolute cold-open numbers are not representative; the relative findings (exponential list cost, per-render editor creation, chunk split) were confirmed by profile shape and by the checked-in tests rather than wall-clock. QuestPDF output was checked by composing the `MarkdownPdfPlan` IR and by `pdftotext` on a sample in one worker run, not by a checked-in PDF-text assertion. Prompts (`DraftPromptBuilder`, `DocumentAssistService`) now receive markdown; `PromptText.StripHtml` was verified to leave GFM untouched except for literal `<`/`>` inside inline code spans, which the toolbar cannot produce.

## Prevention

- When a stored string gains a format, enumerate its sinks and re-check each: sanitizer, renderer, exporter, prompt, search/summary.
- Sanitize a format with its own parser, not a regex approximation of it — and re-validate at every sink anyway.
- Never let a parser exception decide a persisted status; wrap `Markdown.Parse` (and any third-party parser) at the boundary and degrade to literal text.
- With TipTap under React 19 + Suspense/lazy, use `immediatelyRender: false`; do not read module-level "loaded" caches during render — decide once per instance.
- Enforce limits on the representation the server stores (serialised markdown), not on what the editor counts.
- Profile the first cold open of any lazily loaded editor, not just the warm re-open.

## Related

- `docs/solutions/logic-errors/2026-09-16-family-draft-sharing-untrusted-ids-in-prompts-and-hidden-editor-reveal.md` (PromptText guards)
- `docs/solutions/security-issues/2026-09-16-record-lifecycle-upload-guards-in-flight-dialogs-and-lineage-trajectories.md`
- Follow-ups: `todos/167-pending-p3-rich-text-editors.md`
