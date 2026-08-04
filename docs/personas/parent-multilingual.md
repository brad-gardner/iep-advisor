# P2 — Rosa, Parent (multilingual)

**Type:** Primary
**Status:** Draft — unvalidated
**Journeys:** [J1 Parent-only adoption](../journeys/J1-parent-only-adoption.md) ◆ · [J2 School onboarding](../journeys/J2-school-onboarding.md) · [J3 ETR](../journeys/J3-etr-eligibility.md) · [J4 IEP build](../journeys/J4-collaborative-iep-build.md) ◆ · [J5 School-only](../journeys/J5-school-only-fallback.md) · [J6 Meeting day](../journeys/J6-meeting-day.md) ◆ · [J7 Progress](../journeys/J7-progress-monitoring.md) · [J8 Annual review](../journeys/J8-annual-review.md) ◆

> "I understood the meeting. I just understood it eight seconds after everyone else, and by then they had moved on."

## Snapshot

| | |
|---|---|
| **Who** | Parent of a 12-year-old receiving speech and reading services; Spanish is the home language |
| **Relationship to product** | Same advocate role as Dana (P1) — every job-to-be-done in [parent-primary.md](parent-primary.md) applies. This file documents what **language changes**, not a different person |
| **English** | Conversational. Comfortable in daily life, not in a room of professionals using clinical vocabulary at speed |
| **Interpreter reality** | The district provides one. Sometimes by phone. Sometimes late. Sometimes a bilingual staff member with no special-education vocabulary |
| **Tech comfort** | Phone-native. Uses WhatsApp and voice messages far more than email |
| **Device** | **Phone almost exclusively.** May not have a laptop at home |
| **Time budget** | Fragmented; often reads between work shifts |
| **Emotional baseline** | Engaged and under-served. Aware that the process is happening around them at a speed they can't interrupt |

## What language actually changes

Rosa is not "Dana who needs translation." Language turns three ordinary frictions into structural exclusion:

1. **The comprehension delay is a participation delay.** In a fast meeting, understanding a sentence eight seconds late means the moment to respond has passed. Rosa's silence gets read as agreement.
2. **Translated documents arrive after decisions.** A Spanish IEP delivered two weeks post-meeting satisfies a legal requirement and serves no one.
3. **Writing is harder than reading.** Rosa can read English better than they can compose it. Any text box that expects English input silently reduces what Rosa contributes to a fraction of what they think.

The consequence: **Rosa's input is systematically smaller than Rosa's engagement.** That gap is the design target.

## Jobs to be Done

*(P1's five jobs apply in full. These are additional.)*

6. *"Let me read everything in Spanish the moment it exists — not the version that arrives after it's decided."*
7. *"Let me write my concerns in Spanish and have the team receive them as real input, not as something that needed translating."*
8. *"Help me keep up in the meeting in real time, so my silence isn't mistaken for agreement."*
9. *"Let me hear it, not just read it"* — audio matters where written Spanish literacy is lower than spoken fluency.

## Pains & Frictions

*(All of P1's, plus:)*

- **Translation lags the decision.** By the time the Spanish version exists, the English one was signed.
- **The interpreter is a bottleneck and a filter.** Serial interpretation halves available airtime; a non-specialist interpreter flattens "specially designed instruction" into "special classes."
- **Written input is self-censored.** Rosa writes three sentences in imperfect English where they had a paragraph in Spanish.
- **Machine translation of clinical text fails exactly where it matters** — the terms of art are the whole point.
- **Phone-only means PDFs are hostile.** A 22-page scanned document on a 6-inch screen is not a readable document.
- **Nobody asks which language Rosa wants** — it's inferred from a registration field set years ago, or from a name.

## Context of Use

- Phone, vertical, often one-handed, often in short windows between other obligations
- Voice input is more natural than typing for anything longer than a sentence
- May be sharing device access within the household; **privacy on a shared phone is a real concern**
- In the meeting: listening hard, phone available but attention fully committed

## AI Trust Posture

Rosa's trust bar for AI is **higher** than Dana's, because the failure mode is worse: a mistranslation isn't confusion, it's a wrong decision made on their behalf.

| Wants | Won't tolerate |
|---|---|
| Documents in Spanish immediately, at the same moment the English exists | Translation that silently drops or mangles clinical terms |
| To write in Spanish and be *received* in Spanish by a system that carries meaning across | Their words appearing to the team as "translated" and therefore approximate |
| Real-time meeting support that keeps pace | AI standing in for a human interpreter without saying so |
| Audio playback of translated content | Losing the ability to see the original alongside |

**Trust rules:**
- Always show **provenance** — original text reachable from any translation, one tap.
- Never present machine translation as an official/legal translation. Say what it is.
- Preserve **terms of art**: keep the English term visible alongside the explanation ("*specially designed instruction* — instrucción especialmente diseñada: ...").
- AI translation **supplements** the district's interpreter obligation; it must never be positioned as replacing it.

## Language & Accessibility

- Full bilingual UI, not just document translation — navigation, buttons, notifications, emails
- Both directions: Rosa writes Spanish → Steph (P4) reads English → Steph replies English → Rosa reads Spanish
- Language is a **per-user preference**, asked explicitly and changeable, never inferred
- Text-to-speech for translated content
- Phone-first document rendering: structured, scrollable content, not an embedded PDF

## Anti-Goals — what makes Rosa leave

- **Second-class Spanish**: a UI that's translated but where the important content isn't
- Being **routed into a "translated experience"** that has fewer capabilities than the English one
- Contributions that arrive to the team **marked as lesser** because they came through translation
- **A language choice made for them** based on their name or an old registration field
- Anything that lets the district treat AI translation as **discharging their interpreter obligation**

## Design Implications

1. **Bilingual is an architectural property, not a feature.** Content is stored language-tagged with its origin language, and rendered per-reader. Retrofitting this is expensive — decide it now.
2. **Translation is simultaneous with authoring, not a downstream step.** When Steph saves a goal, Rosa can read it in Spanish. No queue, no publish step.
3. **Bidirectional by default.** Rosa's structured input (concerns, vision, questions) is written in Spanish, delivered to the team in English, and stored in both — with the Spanish original always retrievable.
4. **Term-of-art glossary is a product asset.** A curated bilingual special-education glossary that constrains translation of key terms. This is a moat, not a nice-to-have.
5. **Provenance is always one tap away.** Every translated string shows "translated from English — view original."
6. **Voice input and audio output** across the parent surface, not just accessibility settings.
7. **Live meeting support** (J6) must run on a phone, passively, without demanding attention.
8. **Ask the language question explicitly at signup and honor it everywhere**, including outbound email.
9. **Design for shared devices** — a fast, obvious way to hide sensitive content.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Translated documents routinely arrive after decisions are made | **Assumption** (widely reported) | Ask districts about their translation turnaround |
| Rosa's written input shrinks when English is required | **Assumption** | A/B: same-language vs. English-only input volume |
| Phone-only is the dominant access pattern for this persona | **Assumption** | Device analytics segmented by language preference |
| Machine translation of clinical special-ed text is unreliable without a glossary | **Partial evidence** (known LLM behavior on domain terms) | Expert review of translated samples |
| Districts will accept AI translation as a supplement, not a substitute | **Assumption** | Direct question to Karen (P7) during pilot |
| Product today has no translation capability | **Evidence** (no i18n in `web/src`) | — |
| Spanish is the highest-volume target language | **Assumption** | Pilot district demographics |

## Open Questions

- Which languages beyond Spanish, and how is that prioritized — by district demographics or by market?
- Does the school side ever need to *see* the original-language input, or only the translation?
- Is there a legal exposure in the district relying on our translation? Does that need explicit disclaimers or district-level configuration?
- Does the live meeting support (J6) require the school's consent to run?
