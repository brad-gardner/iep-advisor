---
module: "Email + notifications (EmailService, NotificationService, digest, .ics)"
date: "2026-10-06"
problem_type: logic_error
component: service_object
symptoms:
  - "Two separately written Spanish date formatters made the same meeting read differently in the bell notification and the email"
  - "A Spanish student invite read 'en la escuela su escuela' when the school was unknown"
  - "The Spanish digest still contained an English 'for' in its meeting rows"
  - "Merging the formatters silently changed one English notification date from 'Oct 15, 2026' to 'October 15, 2026'"
  - "Invite emails interpolated names into HTML without encoding, and share-invite tokens were not URL-escaped"
root_cause: logic_error
resolution_type: code_fix
severity: medium
tags: [i18n, email, notifications, recipient-language, date-format, html-encoding, ics, digest, spanish]
---

# Troubleshooting: emails and notifications in each recipient's language

## Problem

Phase 4 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` made every email and notification follow its recipient's language:

- **Recipient language:** the saved `PreferredLanguage`. Pre-account invitees get the sender's language and a `?lang=` link.
- **Notifications:** built per recipient, batch-loaded and grouped by language.

Two workers built emails and notifications in parallel, and a third closed the gaps between them. The integration seams are where the defects showed up.

## Environment

- .NET 9; `IStringLocalizer` with `Emails`, `Notifications` and `Messages` resx.
- `CultureScope` changes only the UI culture, so dates need an explicit culture.
- Date: 2026-10-06 / 2026-10-07.

## Symptoms and causes

1. **Divergent date formatting.** Each worker wrote its own explicit-culture helpers.
   - The Spanish short dates and the meeting "at" phrasing differed between the two sets.
   - Neither handled "a la 1:00" vs "a las 2:00".
2. **Sentence assembly from fragments.**
   - **Student invite:** a pre-built English phrase was passed into the email ("at Lincoln High"). Once the clause was localized, `"en la escuela {0}"` combined with the fallback "su escuela" read as "en la escuela su escuela".
   - **Digest:** meeting rows kept a literal " for ".
3. **Consolidation changed English.** Merging the formatters mapped a short-date call site onto `LongDate`, which changed the English output. No test pinned that body.
4. **Pre-existing encoding gaps** surfaced while touching every template.
5. **The `.ics` file was built by the caller before `EmailService` saw it**, so its labels didn't follow the recipient.

## Solution

- **One `LocalizedDateFormat`** with named formats (`LongDate`, `ShortDate`, `ShortDateTime`, `MeetingDateTime`) and correct "a la"/"a las". Emails and notifications both use it, and English output matches main.
- **Structured data instead of fragments.** The student invite now carries child name and school name, and each clause renders from resx: `en {0}` / `en su escuela`. Digest rows use the localized connector.
- **Recipient language is resolved once.** Callers that already loaded it (digest, notification email) pass `recipientLanguage`, and the body and `.ics` use the same value. Pre-account links carry `lang`, and so do the RSVP and cancel-deletion links.
- **Encoding.** Every user or DB value in email HTML is HTML-encoded, and every token is URL-escaped.
- **Calendar file fixes.** The `.ics` description newline is fixed (it showed a literal `\n`), and the calendar download uses one language.
- **Startup culture.** `DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture` are pinned to `en`, so background work never depends on the host OS culture.
- **Pinned English text.** An English assertion now pins the evaluator-overdue body.

## Verification

- **API:** `dotnet test IepAssistant.Services.Tests`: 1653 passed. Covered:
  - per-kind Spanish email tests;
  - pre-account `lang` links;
  - HTML-encoding tests;
  - Spanish `.ics`;
  - fan-out with one English and one Spanish recipient;
  - `LocalizedDateFormat` tests;
  - the pinned English evaluator-overdue date.
- **Web:** 1377 tests passed.
- **CI bundle check (new):** the main chunk is 410.16 kB gzip. The build warns above 405 kB and fails above 418 kB.
- **Review:** 3 passes (3 P2 → 1 → 0). The final pass compared English text against main for every changed email and notification.
- **Not verified:**
  - a real mail-client render;
  - native-speaker review;
  - the index-seek cost of `ToLower()` email matching (kept because the SQLite tests compare case-sensitively; todos/249).

## Prevention

- When parallel workers will each need formatting helpers, add a shared module before they start, so they don't each write one.
- Never pass pre-built English phrases across a boundary that may localize them. Pass structured data and assemble the sentence from one resx string.
- When merging helpers, add a test that pins the English output for every call site you move.
- Build every derived artifact (body, `.ics`, links) from one resolved recipient language.
- Treat "touching every template" as a chance to audit encoding.

## Related

- `docs/solutions/logic-errors/2026-10-06-ai-response-language-structured-output-enums-and-audience.md`
- `docs/solutions/logic-errors/2026-10-06-i18n-status-codes-from-localized-text-and-lazy-fallback-language.md`
- `docs/i18n/glossary-es.md`
