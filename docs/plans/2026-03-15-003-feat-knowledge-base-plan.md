---
title: "feat: Knowledge Base — Searchable Legal Reference with Glossary"
type: feat
status: completed
date: 2026-03-15
origin: docs/brainstorms/2026-03-14-platform-features-brainstorm.md
---

# feat: Knowledge Base — Searchable Legal Reference with Glossary

## Overview

A searchable knowledge base of IEP/special education legal provisions, parent rights, and glossary terms. Content stored in the database as `KnowledgeBaseEntry` records, seeded with federal IDEA provisions in plain language. Supports keyword search, category filtering, and state-specific content (starting with federal, pilot states added later). Written in brand voice — empowering, plain-spoken, parent-first.

This replaces the static IEP 101 page content with a dynamic, searchable system while keeping IEP 101 as the entry point.

(see brainstorm: `docs/brainstorms/2026-03-14-platform-features-brainstorm.md`, Feature 9)

## Problem Statement / Motivation

Parents need accessible legal reference material during IEP meetings and when reviewing analysis results. The current IEP 101 page is static HTML — it can't be searched, filtered by topic, or extended with state-specific content. The analysis service already references IDEA provisions, but parents have no way to look them up independently.

## Proposed Solution

### Data Model

#### New entity: `KnowledgeBaseEntry`

```csharp
public class KnowledgeBaseEntry : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;       // Plain-language explanation
    public string Category { get; set; } = string.Empty;       // "rights", "provisions", "glossary", "process", "tips"
    public string? LegalReference { get; set; }                 // e.g., "34 CFR 300.320"
    public string? State { get; set; }                          // null = federal, "CA" = California-specific
    public string? Tags { get; set; }                           // comma-separated: "goals,SMART,measurable"
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Categories

| Category | Description | Example entries |
|----------|-------------|-----------------|
| `rights` | Parent rights under IDEA | "Right to participate in IEP meetings", "Prior Written Notice" |
| `provisions` | Key IDEA provisions | "34 CFR 300.320 — IEP Content", "34 CFR 300.114 — LRE" |
| `glossary` | Term definitions | "FAPE", "LRE", "Related Services", "Transition" |
| `process` | IEP process guidance | "What happens at an IEP meeting", "How to request an evaluation" |
| `tips` | Practical tips | "How to prepare for your first IEP meeting", "Questions to ask about goals" |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/knowledge-base` | List/search entries (query, category, state filters) |
| GET | `/api/knowledge-base/{id}` | Get a single entry |
| GET | `/api/knowledge-base/categories` | List available categories with counts |

No auth required for reading — knowledge base is publicly accessible (helps with SEO, trust-building). Admin endpoints for CRUD can be added later.

### Search

For MVP, use SQL `LIKE` search on Title, Content, Tags, and LegalReference. The query parameter does a case-insensitive contains match across all text fields. Combined with category and state filters.

```sql
WHERE (@query IS NULL OR Title LIKE '%' + @query + '%' OR Content LIKE '%' + @query + '%' OR Tags LIKE '%' + @query + '%')
  AND (@category IS NULL OR Category = @category)
  AND (State IS NULL OR State = @userState)  -- always include federal + user's state
```

RAG with Azure AI Search + vector embeddings is deferred to a later phase per the brainstorm.

### Seed Data

The migration seeds ~40-50 entries covering:
- **Rights (8-10):** Participation, prior written notice, consent, independent evaluation, records access, dispute resolution, stay-put, FAPE
- **Provisions (10-12):** Key 34 CFR sections (300.320, 300.324, 300.114-120, 300.300-311, 300.322, 300.503, 300.501)
- **Glossary (15-20):** FAPE, LRE, IDEA, IEP, Related Services, Transition, PWN, Due Process, Accommodation, Modification, Present Levels, Annual Goals, Reevaluation, Eligibility, ESY
- **Process (5-7):** IEP meeting flow, evaluation process, annual review, reevaluation timeline, transfer of rights at 18
- **Tips (5-7):** Meeting preparation, asking the right questions, understanding your rights, what to do if you disagree

All written in brand voice (empowering, plain-spoken, parent-first).

## Technical Approach

### Phase 1: Backend — Entity, Service, Controller, Seed Data

**New files:**

| File | Description |
|------|-------------|
| `api/IepAssistant.Domain/Entities/KnowledgeBaseEntry.cs` | Entity |
| `api/IepAssistant.Domain/Data/Configurations/KnowledgeBaseEntryConfiguration.cs` | EF config with indexes on Category, State |
| `api/IepAssistant.Services/Models/KnowledgeBaseModels.cs` | KnowledgeBaseEntryModel, KnowledgeBaseSearchRequest, CategoryCount |
| `api/IepAssistant.Services/Interfaces/IKnowledgeBaseService.cs` | Interface |
| `api/IepAssistant.Services/Implementations/KnowledgeBaseService.cs` | Search + retrieval |
| `api/IepAssistant.Api/Controllers/KnowledgeBaseController.cs` | Public endpoints (no [Authorize] required) |

**Modified files:**

| File | Change |
|------|--------|
| `api/IepAssistant.Domain/Data/ApplicationDbContext.cs` | Add DbSet |
| `api/IepAssistant.Services/DependencyInjection.cs` | Register service |

**EF Migration:** Creates table + seeds initial content via raw SQL INSERT statements in the migration `Up` method.

### Phase 2: Frontend — Knowledge Base Pages

**New files:**

| File | Description |
|------|-------------|
| `web/src/features/knowledge-base/api/knowledge-base-api.ts` | API client |
| `web/src/features/knowledge-base/hooks/use-knowledge-base.ts` | Search hook with debounced query |
| `web/src/features/knowledge-base/components/knowledge-base-page.tsx` | Main page with search + category tabs + entry list |
| `web/src/features/knowledge-base/components/knowledge-base-entry-card.tsx` | Single entry display |
| `web/src/features/knowledge-base/components/knowledge-base-search.tsx` | Search input with debounce |
| `web/src/features/knowledge-base/components/category-tabs.tsx` | Category filter tabs |

**Modified files:**

| File | Change |
|------|--------|
| `web/src/types/api.ts` | Add knowledge base types |
| `web/src/features/onboarding/components/iep-101-page.tsx` | Replace static content with link to knowledge base, or embed KB entries |
| `web/src/components/layouts/sidebar.tsx` | Update "IEP 101" nav item to point to knowledge base |
| `web/src/app/routes.tsx` | Add `/knowledge-base` route, update `/iep-101` to redirect |

**UI Design:**

```
┌─────────────────────────────────────────────┐
│ Knowledge Base                               │
│                                              │
│ [🔍 Search IEP terms, rights, provisions...] │
│                                              │
│ All | Rights | Provisions | Glossary |        │
│ Process | Tips                                │
│                                              │
│ ┌──────────────────────────────────────────┐ │
│ │ FAPE — Free Appropriate Public Education │ │
│ │                                          │ │
│ │ Every child with a disability is         │ │
│ │ entitled to a free education that is     │ │
│ │ designed to meet their unique needs...   │ │
│ │                                          │ │
│ │ Legal Reference: 34 CFR 300.17          │ │
│ │ Tags: rights, eligibility               │ │
│ └──────────────────────────────────────────┘ │
│                                              │
│ ┌──────────────────────────────────────────┐ │
│ │ Prior Written Notice                     │ │
│ │ ...                                      │ │
│ └──────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

- Search with 300ms debounce
- Category tabs as brand Badges
- Each entry as a brand Card with Lora heading
- Legal reference shown as a muted eyebrow
- Tags as small badges
- State-specific entries show a state badge

## Acceptance Criteria

### Functional Requirements

- [ ] Knowledge base searchable by keyword (searches title, content, tags, legal reference)
- [ ] Entries filterable by category (rights, provisions, glossary, process, tips)
- [ ] Entries filterable by state (federal always included, plus user's state)
- [ ] Individual entry viewable with full content
- [ ] Category counts shown on filter tabs
- [ ] Search results update with 300ms debounce
- [ ] ~40-50 entries seeded covering federal IDEA provisions, rights, glossary, process, tips
- [ ] All content written in brand voice
- [ ] Knowledge base accessible from sidebar navigation
- [ ] IEP 101 page links to / integrates with knowledge base
- [ ] Public access (no subscription required)

### Non-Functional Requirements

- [ ] Search responds in <200ms for typical queries
- [ ] Brand UI components throughout
- [ ] Entries are soft-deletable for content management

## Dependencies & Risks

**Dependencies:** None — standalone feature

**Risks:**
- Content accuracy: legal information must be accurate. Consider adding a disclaimer.
- Content volume: 40-50 entries is manageable in a migration. Scaling to hundreds would need a proper admin CMS.
- SQL LIKE search is sufficient for MVP but won't scale for full-text search. Azure AI Search deferred.

## Sources & References

### Origin
- **Brainstorm:** [docs/brainstorms/2026-03-14-platform-features-brainstorm.md](docs/brainstorms/2026-03-14-platform-features-brainstorm.md) — Feature 9: Knowledge Base. Key decisions: state-specific content starting with pilot states, RAG deferred, manual content curation with version tracking.

### Internal References
- IEP 101 page: `web/src/features/onboarding/components/iep-101-page.tsx`
- Analysis service legal references: `api/IepAssistant.Services/Implementations/IepAnalysisService.cs` (IDEA provisions in prompt)
- Sidebar: `web/src/components/layouts/sidebar.tsx`
