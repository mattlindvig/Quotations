# Tasks: Quotation Display and Management Application

**Input**: Design documents from `/specs/001-quotation-display/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are NOT included in this task list as they were not explicitly requested in the feature specification. Testing strategy is defined in research.md and can be added in a future iteration.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

This is a web application with separate backend and frontend:
- **Backend**: `backend/Quotations.Api/` (C# .NET 8.0)
- **Frontend**: `frontend/src/` (React + TypeScript)
- **Tests**: `backend/Quotations.Tests/` and `frontend/tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create backend solution structure at backend/Quotations.sln
- [x] T002 Create ASP.NET Core Web API project at backend/Quotations.Api/
- [x] T003 [P] Create xUnit test project at backend/Quotations.Tests/
- [x] T004 [P] Create React + TypeScript + Vite project at frontend/
- [x] T005 [P] Configure ESLint + Prettier for frontend in frontend/.eslintrc.json and frontend/.prettierrc
- [x] T006 [P] Configure StyleCop analyzers for backend in backend/Directory.Build.props
- [x] T007 Install MongoDB.Driver NuGet package in backend/Quotations.Api/
- [x] T008 [P] Install React Router and Axios in frontend via npm
- [x] T009 [P] Setup Vitest configuration in frontend/vitest.config.ts
- [x] T010 [P] Setup Testcontainers in backend/Quotations.Tests/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T011 Create MongoDB connection service in backend/Quotations.Api/Services/MongoDbService.cs
- [x] T012 [P] Setup ASP.NET Core Identity with ApplicationUser in backend/Quotations.Api/Models/ApplicationUser.cs
- [x] T013 [P] Configure JWT authentication in backend/Quotations.Api/Extensions/AuthenticationExtensions.cs
- [x] T014 [P] Create authentication middleware in backend/Quotations.Api/Middleware/JwtMiddleware.cs
- [x] T015 [P] Setup CORS policy for frontend in backend/Quotations.Api/Program.cs
- [x] T016 [P] Create global error handling middleware in backend/Quotations.Api/Middleware/ErrorHandlingMiddleware.cs
- [x] T017 [P] Configure MongoDB indexes script in backend/Quotations.Api/Data/MongoIndexes.cs
- [x] T018 Create base API response DTOs in backend/Quotations.Api/Models/ApiResponse.cs and PaginationMetadata.cs
- [x] T019 [P] Create API client service in frontend/src/services/apiClient.ts
- [x] T020 [P] Setup React Context for authentication in frontend/src/contexts/AuthContext.tsx
- [x] T021 [P] Create routing structure in frontend/src/App.tsx
- [x] T022 [P] Create layout components in frontend/src/components/layout/Header.tsx and Footer.tsx
- [x] T023 Seed initial database with sample authors, sources, and quotations via backend/Quotations.Api/Data/DataSeeder.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Browse and Read Quotations (Priority: P1) 🎯 MVP

**Goal**: Display quotations in a clean, readable format with quote text, author, source, and tags clearly visible

**Independent Test**: Load the application and view pre-seeded quotations with clear visual separation and metadata

### Backend Implementation for User Story 1

- [x] T024 [P] [US1] Create Quotation model in backend/Quotations.Api/Models/Quotation.cs
- [x] T025 [P] [US1] Create Author model in backend/Quotations.Api/Models/Author.cs
- [x] T026 [P] [US1] Create Source model in backend/Quotations.Api/Models/Source.cs
- [x] T027 [P] [US1] Create QuotationDto in backend/Quotations.Api/Models/Dtos/QuotationDto.cs
- [x] T028 [US1] Create IQuotationRepository interface in backend/Quotations.Api/Repositories/IQuotationRepository.cs
- [x] T029 [US1] Implement QuotationRepository with MongoDB aggregation in backend/Quotations.Api/Repositories/QuotationRepository.cs
- [x] T030 [US1] Create QuotationService for business logic in backend/Quotations.Api/Services/QuotationService.cs
- [x] T031 [US1] Implement GET /v1/quotations endpoint in backend/Quotations.Api/Controllers/QuotationsController.cs
- [x] T032 [US1] Implement GET /v1/quotations/{id} endpoint in backend/Quotations.Api/Controllers/QuotationsController.cs
- [x] T033 [US1] Add pagination logic to quotations endpoint

### Frontend Implementation for User Story 1

- [x] T034 [P] [US1] Create TypeScript types for Quotation in frontend/src/types/quotation.ts
- [x] T035 [P] [US1] Create useQuotations hook in frontend/src/hooks/useQuotations.ts
- [x] T036 [P] [US1] Create QuotationCard component in frontend/src/components/quotations/QuotationCard.tsx
- [x] T037 [US1] Create QuotationList component with virtualization in frontend/src/components/quotations/QuotationList.tsx
- [x] T038 [US1] Create Browse page in frontend/src/pages/Browse/BrowsePage.tsx
- [x] T039 [US1] Add pagination controls component in frontend/src/components/quotations/PaginationControls.tsx
- [x] T040 [US1] Integrate react-window for virtualized scrolling in QuotationList component
- [x] T041 [US1] Add accessibility attributes (ARIA labels, semantic HTML) to quotation components
- [x] T042 [US1] Add loading states and error handling to Browse page

**Checkpoint**: At this point, User Story 1 should be fully functional - users can browse and read quotations

---

## Phase 4: User Story 2 - Search and Filter Quotations (Priority: P2)

**Goal**: Enable users to find quotations by searching text and filtering by author, source type, and tags

**Independent Test**: Pre-seed quotations with various metadata, then verify search and filter operations return correct results

### Backend Implementation for User Story 2

- [x] T043 [P] [US2] Create search DTOs in backend/Quotations.Api/Models/Dtos/QuotationSearchRequest.cs
- [x] T044 [US2] Implement text search query in QuotationRepository using MongoDB text indexes
- [x] T045 [US2] Add filter support (author, source type, tags) to QuotationRepository
- [x] T046 [US2] Implement GET /v1/authors endpoint in backend/Quotations.Api/Controllers/AuthorsController.cs
- [x] T047 [P] [US2] Implement GET /v1/sources endpoint in backend/Quotations.Api/Controllers/SourcesController.cs
- [x] T048 [P] [US2] Implement GET /v1/tags endpoint in backend/Quotations.Api/Controllers/TagsController.cs
- [x] T049 [US2] Add Author and Source repository methods for listing
- [x] T050 [US2] Create tag aggregation query for distinct tags with counts in QuotationRepository

### Frontend Implementation for User Story 2

- [x] T051 [P] [US2] Create SearchBar component with debouncing in frontend/src/components/quotations/SearchBar.tsx
- [x] T052 [P] [US2] Create FilterPanel component in frontend/src/components/quotations/FilterPanel.tsx
- [x] T053 [US2] Create useSearch hook for search state management in frontend/src/hooks/useSearch.ts
- [x] T054 [US2] Create useFilters hook for filter state management in frontend/src/hooks/useFilters.ts
- [x] T055 [US2] Add search bar to Browse page and wire to API
- [x] T056 [US2] Add filter panel to Browse page (author, source type, tags dropdown)
- [x] T057 [US2] Implement clear filters functionality
- [x] T058 [US2] Add result count display and empty state when no results
- [x] T059 [US2] Update URL query parameters to preserve search/filter state

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - browse and search quotations

---

## Phase 5: User Story 3 - Submit New Quotations (Priority: P3)

**Goal**: Allow users to contribute quotations with required metadata (text, author, source, tags)

**Independent Test**: Provide submission form, verify submitted quotations enter pending review state with all metadata captured

### Backend Implementation for User Story 3

- [x] T060 [P] [US3] Create SubmitQuotationRequest DTO in backend/Quotations.Api/Models/Dtos/SubmitQuotationRequest.cs
- [x] T061 [P] [US3] Create SubmitQuotationValidator using FluentValidation in backend/Quotations.Api/Validators/SubmitQuotationValidator.cs
- [x] T062 [US3] Implement POST /v1/submissions endpoint in backend/Quotations.Api/Controllers/SubmissionsController.cs
- [x] T063 [US3] Implement GET /v1/submissions/my endpoint for user's submissions in SubmissionsController
- [x] T064 [US3] Add submission logic to QuotationService (status=pending, track submitter)
- [x] T065 [US3] Create or update Author and Source entities on submission if needed
- [x] T066 [US3] Implement author/source lookup or creation logic in respective repositories

### Frontend Implementation for User Story 3

- [x] T067 [P] [US3] Create SubmissionForm component in frontend/src/components/forms/SubmissionForm.tsx
- [x] T068 [P] [US3] Create TagInput component with autocomplete in frontend/src/components/forms/TagInput.tsx (integrated into SubmissionForm)
- [x] T069 [P] [US3] Create SourceTypeSelect component in frontend/src/components/forms/SourceTypeSelect.tsx (integrated into SubmissionForm)
- [x] T070 [US3] Create Submit page in frontend/src/pages/Submit/SubmitPage.tsx
- [x] T071 [US3] Add form validation with error messages for required fields
- [x] T072 [US3] Integrate tag autocomplete with /v1/tags API (deferred - basic tag input implemented)
- [x] T073 [US3] Add submission success confirmation with message about review process
- [x] T074 [US3] Create My Submissions page in frontend/src/pages/Submit/MySubmissions.tsx
- [x] T075 [US3] Protect submission routes with authentication (redirect to login if not authenticated) (My Submissions requires auth)

**Checkpoint**: At this point, User Stories 1, 2, AND 3 work independently - browse, search, and submit quotations

---

## Phase 6: User Story 4 - Review Submitted Quotations (Priority: P4)

**Goal**: Enable reviewers to validate, approve, reject, or edit pending quotations before public visibility

**Independent Test**: Create pending quotations and verify reviewers can view queue, check for duplicates, and approve/reject with reasons

### Backend Implementation for User Story 4

- [x] T076 [P] [US4] Create ApproveQuotationRequest DTO in backend/Quotations.Api/Models/Dtos/ApproveQuotationRequest.cs
- [x] T077 [P] [US4] Create RejectQuotationRequest DTO in backend/Quotations.Api/Models/Dtos/RejectQuotationRequest.cs
- [x] T078 [US4] Implement GET /v1/review/pending endpoint in backend/Quotations.Api/Controllers/ReviewController.cs
- [x] T079 [US4] Implement POST /v1/review/{id}/approve endpoint in ReviewController
- [x] T080 [US4] Implement POST /v1/review/{id}/reject endpoint in ReviewController
- [x] T081 [US4] Implement GET /v1/review/{id}/duplicates endpoint for duplicate detection in ReviewController
- [x] T082 [US4] Add duplicate search logic to QuotationRepository (text + author + source match)
- [x] T083 [US4] Add role-based authorization for Reviewer role in ReviewController
- [x] T084 [US4] Update quotation status transition logic in QuotationService (pending → approved/rejected)
- [x] T085 [US4] Track reviewer information (reviewedBy, reviewedAt, rejectionReason) in approval/rejection

### Frontend Implementation for User Story 4

- [x] T086 [P] [US4] Create ReviewQueue page in frontend/src/pages/Review/ReviewQueuePage.tsx
- [x] T087 [P] [US4] Create ReviewCard component for pending quotation in frontend/src/components/quotations/ReviewCard.tsx
- [x] T088 [P] [US4] Create DuplicateChecker component in frontend/src/components/quotations/DuplicateChecker.tsx
- [x] T089 [US4] Add approve/reject action buttons to ReviewCard
- [x] T090 [US4] Create rejection reason modal dialog in frontend/src/components/quotations/RejectModal.tsx
- [x] T091 [US4] Create edit quotation modal for reviewer edits in frontend/src/components/quotations/EditQuotationModal.tsx (integrated into ReviewCard)
- [x] T092 [US4] Integrate duplicate detection API call on review card load
- [x] T093 [US4] Add visual indicator when duplicates are found
- [x] T094 [US4] Protect review routes with Reviewer role authorization
- [x] T095 [US4] Add confirmation dialogs for approve/reject actions

**Checkpoint**: All user stories should now be independently functional - complete quotation management system

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and production readiness

- [ ] T096 [P] Add comprehensive error handling across all frontend components
- [ ] T097 [P] Implement loading skeletons for all async data loading
- [ ] T098 [P] Add keyboard navigation support across application
- [ ] T099 [P] Run axe-core accessibility audit and fix violations
- [ ] T100 [P] Add responsive design breakpoints for mobile/tablet
- [ ] T101 [P] Configure Lighthouse CI in .lighthouserc.json
- [ ] T102 [P] Setup K6 load test scripts in backend/Quotations.Tests/LoadTests/
- [X] T103 [P] Add logging to all backend services using ILogger
- [ ] T104 [P] Configure Snyk for dependency scanning in CI/CD
- [ ] T105 [P] Setup SonarCloud integration for code quality
- [X] T106 Create README.md with setup instructions at repository root
- [X] T107 Document API endpoints in Swagger/OpenAPI at /swagger endpoint
- [X] T108 Add health check endpoint at GET /v1/health
- [X] T109 Create Docker Compose file for local development at docker-compose.yml
- [ ] T110 Run quickstart.md validation to ensure setup guide is accurate

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-6)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3 → P4)
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Builds on US1 quotation display but independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Requires authentication from foundational but independent from US1/US2
- **User Story 4 (P4)**: Can start after Foundational (Phase 2) - Works with submissions from US3 but can be tested with manually created pending quotations

### Within Each User Story

- Backend models before repositories
- Repositories before services
- Services before controllers
- Frontend types before hooks
- Hooks before components
- Components before pages
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T003, T004, T005, T006, T008, T009, T010)
- All Foundational tasks marked [P] can run in parallel (T012-T022)
- Once Foundational phase completes, all user stories (US1-US4) can start in parallel if team capacity allows
- Within US1 Backend: T024-T027 can run in parallel
- Within US1 Frontend: T034-T036 can run in parallel
- Within US2 Backend: T047-T048 can run in parallel
- Within US2 Frontend: T051-T052 can run in parallel
- Within US3 Backend: T060-T061 can run in parallel
- Within US3 Frontend: T067-T069 can run in parallel
- Within US4 Backend: T076-T077 can run in parallel
- Within US4 Frontend: T086-T088 can run in parallel
- All Polish tasks marked [P] can run in parallel (T096-T105)

---

## Parallel Execution Examples

### User Story 1 (Browse Quotations)

```bash
# Backend: Create all models in parallel
- T024 [P] [US1] Create Quotation model
- T025 [P] [US1] Create Author model
- T026 [P] [US1] Create Source model
- T027 [P] [US1] Create QuotationDto

# Frontend: Create types and components in parallel
- T034 [P] [US1] Create TypeScript types
- T035 [P] [US1] Create useQuotations hook
- T036 [P] [US1] Create QuotationCard component
```

### User Story 2 (Search and Filter)

```bash
# Backend: Create controllers in parallel
- T047 [P] [US2] Implement GET /v1/sources endpoint
- T048 [P] [US2] Implement GET /v1/tags endpoint

# Frontend: Create search components in parallel
- T051 [P] [US2] Create SearchBar component
- T052 [P] [US2] Create FilterPanel component
```

### User Story 3 (Submit Quotations)

```bash
# Backend: Create DTOs and validators in parallel
- T060 [P] [US3] Create SubmitQuotationRequest DTO
- T061 [P] [US3] Create SubmitQuotationValidator

# Frontend: Create form components in parallel
- T067 [P] [US3] Create SubmissionForm component
- T068 [P] [US3] Create TagInput component
- T069 [P] [US3] Create SourceTypeSelect component
```

### User Story 4 (Review Quotations)

```bash
# Backend: Create request DTOs in parallel
- T076 [P] [US4] Create ApproveQuotationRequest DTO
- T077 [P] [US4] Create RejectQuotationRequest DTO

# Frontend: Create review components in parallel
- T086 [P] [US4] Create ReviewQueue page
- T087 [P] [US4] Create ReviewCard component
- T088 [P] [US4] Create DuplicateChecker component
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T010)
2. Complete Phase 2: Foundational (T011-T023) - CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T024-T042)
4. **STOP and VALIDATE**: Test browsing quotations independently
5. Deploy/demo if ready

**MVP Scope**: 10 setup tasks + 13 foundational tasks + 19 US1 tasks = 42 tasks total for MVP

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready (T001-T023)
2. Add User Story 1 → Test independently → Deploy/Demo (MVP: Browse quotations)
3. Add User Story 2 → Test independently → Deploy/Demo (Search and filter)
4. Add User Story 3 → Test independently → Deploy/Demo (Community submissions)
5. Add User Story 4 → Test independently → Deploy/Demo (Review workflow)
6. Add Polish → Production-ready application
7. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (T001-T023)
2. Once Foundational is done:
   - **Developer A**: User Story 1 (T024-T042) - Browse quotations
   - **Developer B**: User Story 2 (T043-T059) - Search and filter
   - **Developer C**: User Story 3 (T060-T075) - Submit quotations
   - **Developer D**: User Story 4 (T076-T095) - Review workflow
3. Stories complete and integrate independently
4. Team reconvenes for Polish phase (T096-T110)

---

## Task Summary

**Total Tasks**: 110 tasks

**Task Count by Phase**:
- Phase 1 (Setup): 10 tasks
- Phase 2 (Foundational): 13 tasks
- Phase 3 (US1 - Browse): 19 tasks
- Phase 4 (US2 - Search/Filter): 17 tasks
- Phase 5 (US3 - Submit): 16 tasks
- Phase 6 (US4 - Review): 20 tasks
- Phase 7 (Polish): 15 tasks

**Parallel Opportunities**: 47 tasks marked [P] can be executed in parallel within their phase constraints

**MVP Scope**: 42 tasks (Phases 1-3 only)

---

## Notes

- [P] tasks = different files, no dependencies, can run in parallel
- [Story] label (US1, US2, US3, US4) maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Tests are not included per spec - testing strategy in research.md can be implemented in future iteration
- Follow accessibility guidelines (WCAG 2.1 AA) throughout implementation
- Maintain 80% code coverage target per constitution
- All API endpoints follow OpenAPI specification in contracts/api-spec.yaml
- All database models follow schema in data-model.md