# IEP Parent Platform — Build Progress

## Overall Status
- **Phase 0:** Scaffolding done, IaC/CI pending
- **Phase 1:** Auth done (refresh token + password reset deferred)
- **Phase 2:** Child profiles done
- **Phase 3:** IEP upload & storage done
- **Phase 4:** Document processing done

## Phase 0: Project Scaffolding & Infrastructure

| Task | Status | Notes |
|------|--------|-------|
| 0.1 Monorepo structure (`/web`, `/api`) | Done | Pre-existing |
| 0.2 Scaffold React frontend | Done | React 19 + Vite + TS + Tailwind |
| 0.3 Scaffold .NET API | Done | .NET 9, clean architecture |
| 0.4 Initialize Git repo | Done | Pre-existing |
| 0.5 Author IaC templates | Not Started | |
| 0.6 Deploy Azure resources via IaC | Not Started | |
| 0.7 CI/CD pipeline | Not Started | |
| 0.8 Local dev environment | Not Started | |

## Phase 1: Extend Auth & User Profile

| Task | Status | Notes |
|------|--------|-------|
| 1.1 Local JWT auth | Done | Pre-existing |
| 1.2 Frontend auth | Done | Pre-existing |
| 1.3 Add State/Jurisdiction to User | Done | Entity, config, DTOs, models, mappings, migration (AddUserState) |
| 1.4 User profile update endpoint | Done | `PUT /api/auth/me` + UpdateProfileAsync service method |
| 1.5 Refresh token flow | Deferred | Low priority for MVP |
| 1.6 Password reset flow | Deferred | Low priority for MVP |
| 1.7 Frontend: state selector | Done | StateSelector component with all 50 states + DC |
| 1.8 Frontend: profile edit page | Done | ProfilePage with name/state editing, route at `/profile`, nav link |

## Phase 1b: Firebase OAuth

| Task | Status | Notes |
|------|--------|-------|
| 1b.1–1b.5 | Not Started | Deferred until local auth complete |

## Phase 2: Child Profile Management

| Task | Status | Notes |
|------|--------|-------|
| 2.1 Data model + migration | Done | ChildProfile entity, EF config, AddChildProfiles migration |
| 2.2 Entity, repo, service layer | Done | IChildProfileRepository, IChildProfileService, DI registered |
| 2.3 API endpoints (CRUD) | Done | ChildrenController — GET, GET/:id, POST, PUT/:id, DELETE/:id |
| 2.4 Frontend | Done | List, create, detail/edit pages, nav link, dashboard links |

## Phase 3: IEP Document Upload & Storage

| Task | Status | Notes |
|------|--------|-------|
| 3.1 Azure Blob Storage integration | Done | IBlobStorageService in Domain, AzureBlobStorageService in Infrastructure, SAS URL generation |
| 3.2 Data model + migration | Done | IepDocument entity, EF config, AddIepDocuments migration |
| 3.3 Entity, repo, service layer | Done | IIepDocumentRepository, IIepDocumentService, ownership checks via ChildProfile.UserId |
| 3.4 API endpoints | Done | GET children/:id/ieps, POST children/:id/ieps (multipart upload), GET ieps/:id, GET ieps/:id/download, DELETE ieps/:id |
| 3.5 Frontend | Done | IepUpload (drag-and-drop), IepDocumentList (download/delete), integrated in child detail page |

**Config notes:**
- `ConnectionStrings:BlobStorage` in appsettings — set to `UseDevelopmentStorage=true` for local (Azurite), real connection string for Azure
- 50MB upload limit, PDF only validation
- Infrastructure project reference added to Api, `AddInfrastructure()` called in Program.cs

## Phase 4: Document Processing Pipeline

| Task | Status | Notes |
|------|--------|-------|
| 4.1 IepSection + Goal entities & config | Done | Entities in Domain, EF configs, AddIepSectionsAndGoals migration |
| 4.2 PDF text extraction | Done | PdfPig (prerelease 1.7.0-custom-5 for .NET 9) |
| 4.3 Claude structuring service | Done | IepProcessingService using Anthropic SDK v5, claude-sonnet-4-20250514, structured JSON prompt |
| 4.4 Background processing worker | Done | Channel-based queue + IHostedService (IepProcessingWorker) |
| 4.5 API endpoints (sections, reprocess) | Done | GET /api/ieps/:id/sections, POST /api/ieps/:id/process; upload auto-enqueues processing |
| 4.6 Frontend: IEP viewer page | Done | Section nav sidebar + content panel + goal display, route at `/ieps/:id` |
| 4.7 Frontend: document list links | Done | Document name links to viewer, "View" action for parsed docs |

**Config notes:**
- `Anthropic:ApiKey` in appsettings — required for processing
- Status lifecycle: uploaded → processing → parsed / error
- Reprocess available for error/uploaded status documents

## Phase 5–10

Not started.
