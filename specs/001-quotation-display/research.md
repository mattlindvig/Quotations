# Research: Quotation Display and Management Application

**Phase**: 0 (Research & Best Practices)
**Date**: 2025-10-28
**Branch**: `001-quotation-display`

## Overview

This document resolves all "NEEDS CLARIFICATION" items identified in the Technical Context and Constitution Check sections of the implementation plan. Research focuses on technology choices, best practices, and architectural patterns for the React + ASP.NET Core + MongoDB stack.

## Research Items

### 1. Frontend State Management Library

**Decision**: React Context API + Custom Hooks

**Rationale**:
- The application has moderate state complexity (quotations list, filters, search state, review queue)
- Context API is sufficient for global state (user auth, filter state)
- Custom hooks provide reusable logic for data fetching and state management
- Avoids Redux boilerplate for medium-complexity application
- Zustand considered but unnecessary overhead for current requirements

**Alternatives Considered**:
- **Redux Toolkit**: Powerful but overkill for this application; adds unnecessary complexity and bundle size
- **Zustand**: Simpler than Redux but still adds dependency when Context API meets needs
- **Recoil**: Experimental status and smaller ecosystem make it less suitable

**Implementation Pattern**:
```typescript
// Context for filters
const FilterContext = createContext()

// Custom hooks for data fetching
const useQuotations = () => {
  const [quotations, setQuotations] = useState([])
  const [loading, setLoading] = useState(false)
  // ... fetch logic
}
```

---

### 2. Authentication Library (Backend)

**Decision**: ASP.NET Core Identity + JWT Bearer Tokens

**Rationale**:
- ASP.NET Core Identity provides complete user management (registration, login, password reset)
- JWT tokens enable stateless API authentication (supports horizontal scaling per constitution)
- Reviewers need role-based access control (RBAC) - Identity supports roles out of the box
- Browsing quotations is public (no auth required), submissions require authentication
- Industry standard approach for React SPA + .NET API architecture

**Alternatives Considered**:
- **Auth0/Okta**: Third-party SaaS adds cost and external dependency; internal Identity sufficient
- **Custom JWT implementation**: Reinventing the wheel; Identity provides battle-tested security
- **Session-based auth**: Violates stateless API requirement for horizontal scaling

**Implementation Details**:
- JWT issued on login with 1-hour expiration, refresh token for renewal
- Roles: "User" (can submit), "Reviewer" (can review), "Admin" (full access)
- Public endpoints: GET /quotations, GET /quotations/{id}, GET /quotations/search
- Authenticated endpoints: POST /quotations (submissions)
- Reviewer endpoints: GET /review/pending, POST /review/{id}/approve, etc.

---

### 3. Frontend Testing Framework

**Decision**: Vitest + React Testing Library

**Rationale**:
- Vitest is Vite-native (faster than Jest for Vite projects)
- React Testing Library enforces testing best practices (user-centric tests)
- Vitest supports ESM modules natively (Jest requires configuration)
- Faster test execution (HMR for tests, parallel execution)
- Compatible with Jest assertions (easy migration if needed)

**Alternatives Considered**:
- **Jest + React Testing Library**: Industry standard but slower with Vite, requires additional config
- **Cypress Component Testing**: Excellent but heavier for unit tests; better for e2e

**Testing Strategy**:
- Unit tests: Individual components, hooks, utilities
- Integration tests: Page-level components with API mocking (MSW - Mock Service Worker)
- Accessibility tests: @testing-library/jest-dom matchers + axe-core
- Target: 80% coverage minimum per constitution

---

### 4. Backend Integration Testing Approach

**Decision**: Testcontainers with MongoDB Docker Container

**Rationale**:
- Real MongoDB instance ensures tests reflect production behavior
- Testcontainers manages container lifecycle automatically (starts/stops per test run)
- Tests run against actual MongoDB queries, indexes, aggregations
- In-memory MongoDB alternatives don't support all MongoDB features (aggregation pipelines, transactions)
- CI/CD compatible (GitHub Actions, Azure DevOps support Docker)

**Alternatives Considered**:
- **In-memory MongoDB (Mongo2Go)**: Doesn't support aggregation pipelines and text search fully
- **Shared MongoDB instance**: Test pollution, slow cleanup, not isolated
- **Mocked repository**: Doesn't test actual MongoDB query behavior

**Implementation Pattern**:
```csharp
public class QuotationsIntegrationTests : IAsyncLifetime
{
    private MongoDbContainer _mongoContainer;
    private IMongoDatabase _database;

    public async Task InitializeAsync()
    {
        _mongoContainer = new MongoDbBuilder().Build();
        await _mongoContainer.StartAsync();
        // ... setup
    }
}
```

---

### 5. React Accessibility Patterns

**Decision**: Comprehensive WCAG 2.1 AA Implementation Strategy

**Rationale**: Constitution mandates accessibility compliance; React ecosystem provides excellent tooling.

**Best Practices**:

1. **Semantic HTML**:
   - Use `<button>` for actions, `<a>` for navigation
   - Proper heading hierarchy (`<h1>` → `<h2>` → `<h3>`)
   - `<main>`, `<nav>`, `<article>` for landmarks

2. **ARIA Labels**:
   - `aria-label` for icon-only buttons
   - `aria-labelledby` for form field associations
   - `aria-live` regions for dynamic content (search results, filter updates)

3. **Keyboard Navigation**:
   - All interactive elements focusable via Tab
   - Custom focus styles (outline, ring)
   - Skip links for navigation
   - Modal trap focus when open

4. **Screen Reader Support**:
   - Descriptive alt text for images
   - Loading state announcements (`aria-live="polite"`)
   - Form validation errors announced
   - Button purposes clearly stated

5. **Tools & Libraries**:
   - `eslint-plugin-jsx-a11y`: Lint-time accessibility checks
   - `@axe-core/react`: Runtime accessibility auditing (development)
   - `react-focus-lock`: Focus management for modals
   - Testing: `@testing-library/jest-dom` + `axe-core` for automated tests

**Component Patterns**:
```tsx
// Search input with ARIA
<input
  type="search"
  aria-label="Search quotations by text, author, or source"
  aria-describedby="search-help"
  onChange={handleSearch}
/>
<div id="search-help" className="sr-only">
  Results update as you type
</div>

// Loading state
<div role="status" aria-live="polite">
  {loading ? "Loading quotations..." : `${count} quotations found`}
</div>
```

---

### 6. MongoDB Indexing Strategy

**Decision**: Multi-Field Indexes for Search and Filter Performance

**Rationale**: Spec requires fast search/filter (SC-001: < 30 seconds); indexes critical for 100k+ quotations.

**Index Strategy**:

1. **Text Search Index**:
```javascript
db.quotations.createIndex(
  {
    text: "text",
    "author.name": "text",
    "source.title": "text"
  },
  { weights: { text: 10, "author.name": 5, "source.title": 3 } }
)
```
   - Enables full-text search across quote text, author, source
   - Weighted scoring (quote text most relevant)

2. **Filter Indexes**:
```javascript
// Compound index for common filter combinations
db.quotations.createIndex({
  status: 1,           // approved/pending/rejected
  "author.id": 1,      // filter by author
  "source.type": 1,    // filter by source type (book, movie, etc.)
  submittedAt: -1      // sort by date
})

// Tag filtering (multi-key index)
db.quotations.createIndex({ "tags": 1 })
```

3. **Review Queue Index**:
```javascript
db.quotations.createIndex({
  status: 1,
  submittedAt: 1
})
```
   - Efficient query for pending quotations ordered by submission date

**Query Optimization**:
- Pagination: Use limit + skip for initial pages, cursor-based for deep pagination
- Aggregation pipelines for complex filters (multiple tags + author + type)
- Explain plan analysis during development to verify index usage

---

### 7. React Virtualization for Large Lists

**Decision**: `react-window` for Virtualized Quotation Lists

**Rationale**:
- Spec requires 100+ quotations display without degradation (SC-006)
- Virtualization renders only visible items (DOM efficiency)
- `react-window` is lightweight (11kb), maintained by Google Chrome team
- Supports variable row heights (quotations vary in length)

**Alternatives Considered**:
- **react-virtualized**: More features but heavier (30kb+); `react-window` is successor
- **No virtualization**: Acceptable for < 50 items, degrades with 100+

**Implementation Pattern**:
```tsx
import { VariableSizeList } from 'react-window'

<VariableSizeList
  height={800}
  itemCount={quotations.length}
  itemSize={index => quotations[index].estimatedHeight}
  width="100%"
>
  {({ index, style }) => (
    <QuotationCard
      key={quotations[index].id}
      quotation={quotations[index]}
      style={style}
    />
  )}
</VariableSizeList>
```

**Performance Impact**:
- Without virtualization: 1000+ DOM nodes, slow scrolling, high memory
- With virtualization: ~20-30 DOM nodes (visible + buffer), smooth 60fps scrolling

---

### 8. Linting Configurations

**Decision**: ESLint (Frontend) + .NET Analyzers (Backend)

**Frontend (ESLint)**:
- `eslint-config-airbnb`: Industry-standard React/TypeScript rules
- `eslint-plugin-jsx-a11y`: Accessibility linting
- `@typescript-eslint`: TypeScript-specific rules
- Prettier integration for formatting

**Backend (.NET Analyzers)**:
- StyleCop.Analyzers: C# style consistency
- SonarAnalyzer.CSharp: Code quality and security
- Microsoft.CodeAnalysis.NetAnalyzers: Built-in .NET analyzers

**Enforcement**:
- Pre-commit hooks (Husky + lint-staged) for frontend
- Build-time enforcement (treat warnings as errors in CI)
- Zero linting errors policy per constitution

---

### 9. Performance Regression Testing

**Decision**: K6 for Load Testing + Lighthouse CI for Frontend Performance

**Backend (K6)**:
- Load test critical endpoints (GET /quotations, POST /quotations, search)
- Verify p95 latency < 200ms under load (100-1000 concurrent users)
- Automated in CI/CD pipeline (threshold gates)

**Frontend (Lighthouse CI)**:
- Performance score > 90
- Accessibility score > 95 (WCAG 2.1 AA)
- Runs on every PR (regression detection)

**Baseline Metrics**:
```yaml
# .lighthouserc.json
{
  "ci": {
    "assert": {
      "assertions": {
        "categories:performance": ["error", {"minScore": 0.9}],
        "categories:accessibility": ["error", {"minScore": 0.95}]
      }
    }
  }
}
```

---

### 10. Security Scanning Tools

**Decision**: OWASP Dependency-Check + Snyk + SAST Analysis

**Dependency Scanning**:
- **npm audit**: Built-in vulnerability scanning for frontend dependencies
- **Snyk**: Continuous monitoring, automated PRs for security updates (free for open source)
- **OWASP Dependency-Check**: .NET NuGet package vulnerability scanning

**SAST (Static Application Security Testing)**:
- **SonarCloud**: Free for open source, detects security hotspots (SQL injection, XSS, etc.)
- **CodeQL**: GitHub Advanced Security (free for public repos)

**Security Checklist**:
- [ ] No high/critical vulnerabilities in dependencies
- [ ] HTTPS enforced (HSTS headers)
- [ ] JWT secrets stored in environment variables (never committed)
- [ ] MongoDB connection strings in secure configuration
- [ ] Input validation on all API endpoints
- [ ] Rate limiting on authentication endpoints
- [ ] CORS configured correctly (whitelist origins)

---

## Summary of Decisions

| Area | Decision | Key Benefit |
|------|----------|-------------|
| State Management | React Context API + Custom Hooks | Simplicity without Redux overhead |
| Authentication | ASP.NET Core Identity + JWT | Stateless, scalable, role-based access |
| Frontend Testing | Vitest + React Testing Library | Fast, Vite-native, user-centric tests |
| Backend Integration Tests | Testcontainers + MongoDB | Real database behavior, isolated tests |
| Accessibility | Comprehensive WCAG 2.1 AA | Inclusive design, constitution compliant |
| MongoDB Indexing | Text search + compound filter indexes | Sub-30s search per SC-001 |
| Virtualization | react-window | Smooth scrolling for 100+ items |
| Linting | ESLint + StyleCop + Sonar | Zero-error policy enforcement |
| Performance Testing | K6 + Lighthouse CI | Regression detection, SLA compliance |
| Security | Snyk + OWASP + SAST | Continuous vulnerability monitoring |

## Next Steps

All NEEDS CLARIFICATION items resolved. Proceed to Phase 1:
1. Generate data-model.md (entity schemas)
2. Generate API contracts (OpenAPI spec)
3. Generate quickstart.md (development guide)
4. Update agent context with final technology choices