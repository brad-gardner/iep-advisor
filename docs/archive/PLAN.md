# IEP Parent Platform — MVP Implementation Plan

**Source:** [Product Spec v0.1](https://www.notion.so/316a70d4ed5481529611ddaed8c58de2)
**Auth:** Custom JWT auth (local accounts) already built; Firebase OAuth to be layered on
**Doc Processing:** Claude API directly (no Azure AI Document Intelligence for MVP)
**Infrastructure:** All Azure resources deployed as a unit via IaC (Bicep preferred, Terraform acceptable)

---

## Architecture Overview

| Layer | Technology | Status |
|-------|-----------|--------|
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS | Scaffolded |
| Backend | ASP.NET Core Web API (.NET 9), clean architecture | Scaffolded |
| Auth (local) | Custom JWT + BCrypt (AuthController, AuthService) | Done |
| Auth (OAuth) | Firebase Auth — Google/Apple OAuth, linked to local accounts | Not started |
| Database | Azure SQL Database + EF Core 9 | Configured |
| Storage | Azure Blob Storage (IEP documents) | Not started |
| Document Processing | PDF text extraction (.NET library) + Claude API for structuring | Not started |
| LLM | Claude API (Anthropic) | Not started |
| Search/RAG | Azure AI Search (vector search) | Not started |
| Background Jobs | Azure Functions or Hangfire | Not started |
| Hosting | Azure App Service (API) + Azure Static Web Apps (frontend) | Not started |
| IaC | Bicep (preferred) or Terraform | Not started |
| Logging | Serilog + Elastic APM | Configured |

---

## Existing Codebase Summary

### Backend (`/api`) — .NET 9, Clean Architecture
```
IepAssistant.Api/           Controllers, DTOs, Middleware
IepAssistant.Domain/        Entities, Data (DbContext, Migrations), Repositories
IepAssistant.Services/      Interfaces, Implementations (AuthService, UserService)
IepAssistant.Infrastructure/ Data access, Repository implementations
```

**What's built:**
- [x] JWT auth with BCrypt password hashing (login, register, get-me)
- [x] User entity (Id, Email, PasswordHash, FirstName, LastName, Role, IsActive, audit fields)
- [x] Admin user management (CRUD, soft delete)
- [x] Generic repository pattern
- [x] Global exception middleware
- [x] Health check endpoint
- [x] EF Core with SQL Server, migrations
- [x] Serilog structured logging
- [x] CORS configured for frontend dev server
- [x] OpenAPI/Scalar API docs

**What's NOT built yet:**
- State/jurisdiction on User entity
- Child profiles
- IEP document models
- Blob storage integration
- Claude API integration
- Any domain-specific features

### Frontend (`/web`) — React 19 + Vite + TypeScript
```
src/
├── app/           App root, providers, routes
├── components/    Layouts (auth-layout, main-layout)
├── features/auth/ Login, Register, Dashboard pages + auth API/context/hooks
├── lib/           API client (Axios), auth token utilities
└── types/         TypeScript interfaces
```

**What's built:**
- [x] Login page with email/password
- [x] Register page with name + email + password
- [x] Dashboard page (welcome/user info)
- [x] JWT auth context + useAuth hook
- [x] Protected/public route wrappers
- [x] Axios client with JWT interceptor + 401 redirect
- [x] Auth/Main layouts with Tailwind dark theme

**What's NOT built yet:**
- State/jurisdiction selection
- Child profile UI
- IEP upload/viewer
- Any feature beyond auth

---

## Phase 0: Infrastructure as Code & DevOps

- [x] ~~**0.1** Initialize monorepo structure (`/web`, `/api`)~~
- [x] ~~**0.2** Scaffold React + Vite + TypeScript frontend~~
- [x] ~~**0.3** Scaffold ASP.NET Core Web API~~
- [ ] **0.4** Initialize Git repo, add `.gitignore` for .NET + Node
- [ ] **0.5** Author IaC templates (Bicep or Terraform)
  - Azure SQL Database
  - Azure Blob Storage (with container for IEP docs)
  - Azure Key Vault (secrets and config)
  - Azure App Service (API)
  - Azure Static Web Apps (frontend)
  - Azure AI Search
  - Azure Application Insights
  - Parameterized for dev/staging/prod environments
- [ ] **0.6** Deploy Azure resources via IaC
- [ ] **0.7** Set up CI/CD pipeline (GitHub Actions) including IaC deployment step
- [ ] **0.8** Set up local development environment (Docker Compose for SQL, env configs)

---

## Phase 1: Extend Auth & User Profile

Local auth is already functional. This phase adds missing fields and the password reset flow.

- [x] ~~**1.1** Local JWT auth (register, login, get-me)~~
- [x] ~~**1.2** Frontend auth (login/register pages, auth context, protected routes)~~
- [ ] **1.3** Add `State` / `Jurisdiction` field to User entity + migration
- [ ] **1.4** User profile update endpoint (`PUT /api/users/me`) — allow user to set state, display name
- [ ] **1.5** Implement refresh token flow (currently throws `NotImplementedException`)
- [ ] **1.6** Password reset flow (forgot-password + reset-password endpoints, email sending)
- [ ] **1.7** Frontend: state/jurisdiction selector (dropdown on profile/onboarding)
- [ ] **1.8** Frontend: profile edit page

---

## Phase 1b: Firebase OAuth (Layered On)

- [ ] **1b.1** Set up Firebase project and configure OAuth providers (Google, optionally Apple)
- [ ] **1b.2** Frontend: add "Sign in with Google" button via Firebase Auth SDK
- [ ] **1b.3** Backend: Firebase token validation middleware (runs alongside local JWT validation)
  - Validate Firebase ID tokens
  - Auto-create or link to existing local account by email
- [ ] **1b.4** Account linking logic
  - If Firebase OAuth user's email matches existing local account, link them
  - If no match, create new local user record from Firebase claims
- [ ] **1b.5** Frontend: unified auth state handling both local JWT and Firebase sessions

---

## Phase 2: Child Profile Management

- [ ] **2.1** Data model + migration
  - `ChildProfiles` table: userId (FK), firstName, lastName (optional), dateOfBirth, gradeLevel, disabilityCategory, schoolDistrict, created/updated timestamps
- [ ] **2.2** Entity, repository, service layer (following existing patterns)
- [ ] **2.3** API endpoints — CRUD scoped to authenticated user
  - POST `/api/children` — create child profile
  - GET `/api/children` — list user's children
  - GET `/api/children/{id}` — get child details
  - PUT `/api/children/{id}` — update child
  - DELETE `/api/children/{id}` — soft delete
- [ ] **2.4** Frontend
  - Child profile creation form
  - Profile display/edit page
  - Dashboard updated to show child profiles with linked IEPs

---

## Phase 3: IEP Document Upload & Storage

- [ ] **3.1** Azure Blob Storage integration
  - Blob service client in Infrastructure layer
  - Secure path-based isolation (`users/{userId}/ieps/{docId}/`)
  - Upload/download/delete operations
- [ ] **3.2** Data model + migration
  - `IepDocuments` table: childProfileId (FK), fileName, blobUri, uploadDate, iepDate, status (uploaded/processing/parsed/error), pageCount, created/updated timestamps
- [ ] **3.3** Entity, repository, service layer
- [ ] **3.4** API endpoints
  - POST `/api/children/{id}/ieps` — upload IEP document
  - GET `/api/children/{id}/ieps` — list IEPs for child
  - GET `/api/ieps/{id}` — get IEP details + download URL
  - DELETE `/api/ieps/{id}` — delete IEP and blob
- [ ] **3.5** Frontend
  - Upload component (drag-and-drop, file picker, PDF only for MVP)
  - Upload progress indicator
  - IEP list/timeline view per child

---

## Phase 4: Document Processing Pipeline

- [ ] **4.1** Background processing infrastructure (Hangfire or Azure Function triggered on upload)
- [ ] **4.2** PDF text extraction
  - .NET PDF library (e.g., PdfPig) for text-based PDFs
  - Fallback: send PDF to Claude API with vision for scanned/image-based documents
- [ ] **4.3** Claude-powered IEP structuring (single-pass)
  - Send extracted text (or PDF directly for scanned docs) to Claude
  - Prompt engineered to extract and classify into standard IEP sections: student profile, present levels, annual goals, services, accommodations, placement, transition planning
  - Extract individual goals with structured fields
  - Return structured JSON for storage
- [ ] **4.4** Data models + migration
  - `IepSections` table (iepDocumentId FK, sectionType enum, rawText, parsedContent JSON, displayOrder)
  - `Goals` table (iepSectionId FK, goalText, domain/area, baseline, targetCriteria, measurementMethod, timeframe)
- [ ] **4.5** Processing status updates (webhook or polling from frontend)
- [ ] **4.6** Frontend: parsed IEP viewer
  - Side-by-side view: original PDF and parsed sections
  - Section navigation
  - Processing status indicator

> **Note:** If scanned/degraded PDF quality becomes an issue post-MVP, Azure AI Document Intelligence can be added as a dedicated OCR layer upstream of Claude.

---

## Phase 5: Knowledge Base (Federal + Pilot States)

- [ ] **5.1** Data model + migration
  - `KnowledgeBaseEntries` table: jurisdiction (federal or state code), topic, subcategory, title, content, sourceCitation, sourceUrl, effectiveDate, version, created/updated timestamps
- [ ] **5.2** Curate federal knowledge base
  - IDEA core provisions
  - Section 504 basics
  - FERPA relevant provisions
  - Key OSEP guidance
  - Parent rights and procedural safeguards
- [ ] **5.3** Curate pilot state knowledge base (3–5 states TBD)
  - State special education codes
  - State-specific timelines and procedures
  - State dispute resolution processes
- [ ] **5.4** Azure AI Search index
  - Vector embeddings for knowledge base entries
  - Semantic search configuration
  - Ingestion pipeline for adding/updating entries
- [ ] **5.5** RAG retrieval API
  - Search endpoint returning relevant KB entries for a query + jurisdiction

---

## Phase 6: LLM Integration — Plain-Language Explanations

- [ ] **6.1** Claude API integration layer (shared service)
  - API client with retry logic, error handling, rate limiting
  - System prompt engineering (parent-advocate framing)
  - Citation tracking — every response references source material
- [ ] **6.2** RAG pipeline
  - Query → vector search → retrieve relevant KB entries → augment prompt → Claude response
  - Jurisdiction-aware context injection
- [ ] **6.3** Explanation endpoints
  - POST `/api/explain/section` — explain an IEP section in plain language
  - POST `/api/explain/term` — explain a specific term or phrase
  - POST `/api/explain/rights` — explain relevant parent rights for a context
- [ ] **6.4** Frontend
  - Highlight-to-explain interaction on parsed IEP
  - Explanation panel/modal with source citations
  - Auto-linked glossary terms within parsed document
  - "What does this mean for my child?" contextual button

---

## Phase 7: Goal Analysis & Red Flag Detection

- [ ] **7.1** Goal analysis prompts and logic
  - SMART criteria evaluation
  - Alignment check (present levels ↔ goals)
  - Boilerplate/generic detection
  - Baseline and measurement method assessment
- [ ] **7.2** Red flag detection
  - Missing required IEP components
  - Vague/unmeasurable language detection
  - Missing timelines or evaluation dates
  - (Cross-version checks deferred to Phase 1.5)
- [ ] **7.3** Analysis data model + migration
  - `Analyses` table: iepDocumentId or goalId, analysisType, rating (green/yellow/red), summary, details (JSON), suggestions, relatedKbEntryIds, created timestamp
- [ ] **7.4** API endpoints
  - POST `/api/ieps/{id}/analyze` — trigger full IEP analysis
  - GET `/api/ieps/{id}/analysis` — get analysis results
  - GET `/api/goals/{id}/analysis` — get goal-specific analysis
- [ ] **7.5** Frontend
  - Color-coded assessment per goal and section (green/yellow/red)
  - Expandable suggestions and improvement recommendations
  - Suggested questions for IEP meetings
  - Links to relevant legal provisions

---

## Phase 8: Communication Support

- [ ] **8.1** Template library
  - 3–5 core templates: request IEP meeting, prior written notice request, disagreement letter, consent/refusal letter, progress update request
  - Data model: `CommunicationTemplates` table (name, category, templateBody, variables)
- [ ] **8.2** LLM-assisted drafting
  - POST `/api/communications/draft` — generate customized letter from template + child context + IEP data
  - Appropriate tone and legal framing via prompt engineering
- [ ] **8.3** Meeting prep workflow
  - POST `/api/ieps/{id}/meeting-prep` — generate concerns summary, talking points, relevant rights
- [ ] **8.4** Communication storage + migration
  - `Communications` table: childProfileId, iepDocumentId (optional), type, subject, body, status (draft/final), created/updated
- [ ] **8.5** Frontend
  - Template browser and selection
  - Draft editor with LLM suggestions
  - Meeting prep summary view
  - Export to email/PDF

---

## Phase 9: Dashboard & Polish

- [ ] **9.1** Parent dashboard
  - Overview: child profile card, latest IEP status, recent analysis highlights
  - Quick actions: upload IEP, view analysis, prep for meeting
- [ ] **9.2** Navigation and layout
  - Responsive design (mobile-friendly)
  - Consistent component library (consider shadcn/ui or similar)
- [ ] **9.3** Error handling and loading states throughout
- [ ] **9.4** Legal disclaimers and terms of service
  - "Information, not legal advice" disclaimer
  - Privacy policy (FERPA-sensitive data handling)
- [ ] **9.5** Basic onboarding flow (first-time user walkthrough)

---

## Phase 10: Testing & Launch Prep

- [ ] **10.1** Unit tests for API (xUnit)
- [ ] **10.2** Unit tests for frontend (Vitest + React Testing Library)
- [ ] **10.3** Integration tests for document processing pipeline
- [ ] **10.4** Integration tests for RAG + LLM pipeline
- [ ] **10.5** Security review (FERPA compliance checklist, encryption audit)
- [ ] **10.6** Performance testing (document processing, LLM response times)
- [ ] **10.7** Deploy to Azure staging environment via IaC
- [ ] **10.8** User acceptance testing with 5–10 parents
- [ ] **10.9** Production deployment via IaC (same templates, prod parameters)

---

## Key Decisions to Make

| # | Decision | Options | Status |
|---|----------|---------|--------|
| 1 | Pilot states for knowledge base | Need to select 3–5 states | TODO |
| 2 | UI component library | shadcn/ui, MUI, Ant Design, Chakra | TODO |
| 3 | Background job framework | Azure Functions vs Hangfire | TODO |
| 4 | IaC tooling | Bicep (preferred) vs Terraform | TODO |
| 5 | Firebase OAuth providers | Google + Apple? Google only for MVP? | TODO |
| 6 | Monetization model | Freemium, subscription, grant-funded | TODO |
| 7 | Section 504 support | Include in MVP or defer | TODO |
| 8 | LLM fallback strategy | Claude-only or multi-model | TODO |

---

## Out of Scope (MVP)

- Full 50-state knowledge base coverage
- School district user roles
- IEP authoring/creation tools
- Trend analysis and diff views (Phase 1.5)
- Native mobile apps
- School district SIS/IEP system integrations
- Multi-language support
