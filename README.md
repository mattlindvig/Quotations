# Quotations - Display and Management Application

A full-stack web application for displaying, searching, filtering, submitting, and managing quotations from various sources (books, movies, speeches, etc.).

## Project Status

**Current Phase**: Phase 6 Complete ✅ - Core Implementation Done

**Completed Tasks**: 95/110 (86%) - Phases 1-6: T001-T095

**Next Phase**: Phase 7 - Polish & Cross-Cutting Concerns (T096-T110)

### Completed Phases:
- ✅ **Phase 1**: Project setup, dependencies, linting, testing configuration
- ✅ **Phase 2**: Core infrastructure (MongoDB, JWT auth, error handling, API client, routing, database seeding)
- ✅ **Phase 3**: User Story 1 - Browse quotations with pagination
- ✅ **Phase 4**: User Story 2 - Search and filter quotations
- ✅ **Phase 5**: User Story 3 - Submit new quotations
- ✅ **Phase 6**: User Story 4 - Review and approve quotations (Reviewer/Admin workflow)

### Core Features:
- **Browse & Search**: Full-text search, filter by author/source/tags, pagination, responsive design
- **Submit Quotations**: User submission form with validation and duplicate checking
- **Review Workflow**: Reviewer/Admin approval queue with approve/reject actions
- **User Management**: JWT authentication with role-based access (User, Reviewer, Admin)
- **My Submissions**: Users can track their submitted quotations and statuses

## Quick Links

- **Feature Spec**: [specs/001-quotation-display/spec.md](specs/001-quotation-display/spec.md)
- **Tasks**: [specs/001-quotation-display/tasks.md](specs/001-quotation-display/tasks.md)
- **API Spec**: [specs/001-quotation-display/contracts/api-spec.yaml](specs/001-quotation-display/contracts/api-spec.yaml)
- **Developer Guide**: [specs/001-quotation-display/quickstart.md](specs/001-quotation-display/quickstart.md)

## Technology Stack

### Backend
- **Framework**: ASP.NET Core 8.0 Web API
- **Language**: C# 12
- **Database**: MongoDB 6.0+ with MongoDB.Driver
- **Authentication**: JWT Bearer tokens with role-based authorization
- **Validation**: FluentValidation
- **Testing**: xUnit, Moq, FluentAssertions
- **Architecture**: Repository pattern, service layer, DTOs

### Frontend
- **Framework**: React 18 with TypeScript
- **Build Tool**: Vite
- **Routing**: React Router v6
- **HTTP Client**: Axios with interceptors
- **Testing**: Vitest, React Testing Library
- **Styling**: Custom CSS with responsive design
- **Optimization**: React.memo, useMemo, useCallback, virtualization

### DevOps & Tools
- **Linting**: ESLint (frontend), Roslyn analyzers (backend)
- **Code Quality**: SonarCloud (planned - Phase 7)
- **Security Scanning**: Snyk (planned - Phase 7)
- **Performance**: Lighthouse CI, K6 load testing (planned - Phase 7)
- **Containerization**: Docker Compose (planned - Phase 7)

## Getting Started

### Quick Start with Docker Compose (Recommended)

```bash
# Coming in Phase 7 - T109
docker-compose up
# Frontend: http://localhost:5173
# Backend: https://localhost:5000
# MongoDB: mongodb://localhost:27017
```

### Manual Setup

#### Prerequisites
- **Node.js 18+** (for frontend)
- **.NET SDK 8.0+** (for backend)
- **MongoDB 6.0+** (local install or Docker)

#### 1. Database Setup

**Option A: Docker (Recommended)**
```bash
docker run -d -p 27017:27017 --name quotations-mongo \
  -e MONGO_INITDB_ROOT_USERNAME=admin \
  -e MONGO_INITDB_ROOT_PASSWORD=password123 \
  mongo:6.0
```

**Option B: Local MongoDB**
- Install MongoDB from https://www.mongodb.com/try/download/community
- Ensure MongoDB is running on `localhost:27017`

#### 2. Backend Setup

```bash
cd backend
dotnet restore
dotnet build

# Run migrations/seed data
dotnet run --project Quotations.Api

# Backend runs at https://localhost:5000
# Swagger UI: https://localhost:5000/swagger
```

**Backend Environment Variables** (optional - has defaults):
```bash
# Create backend/Quotations.Api/appsettings.Development.json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "QuotationsDb"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "QuotationsApi",
    "Audience": "QuotationsApp",
    "ExpiryMinutes": 60
  }
}
```

#### 3. Frontend Setup

```bash
cd frontend
npm install
npm run dev

# Frontend runs at http://localhost:5173
```

**Frontend Environment Variables**:
```bash
# Create frontend/.env.development
VITE_API_BASE_URL=https://localhost:5000/api/v1
```

#### 4. Verify Installation

1. Open http://localhost:5173 in your browser
2. You should see the Browse page with sample quotations
3. Try searching and filtering
4. Log in with default credentials (see Authentication section below)
5. Try submitting a new quotation
6. Log in as Reviewer to approve/reject submissions

See [quickstart.md](specs/001-quotation-display/quickstart.md) for detailed setup instructions and troubleshooting.

## Project Structure

```
Quotations/
├── backend/
│   ├── Quotations.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/             # API controllers (v1)
│   │   ├── Models/                  # Domain entities (Author, Source, Quotation, User)
│   │   ├── DTOs/                    # Request/Response DTOs
│   │   ├── Services/                # Business logic layer
│   │   ├── Repositories/            # Data access layer (MongoDB)
│   │   ├── Validators/              # FluentValidation validators
│   │   ├── Middleware/              # Error handling, auth middleware
│   │   └── Program.cs               # App configuration & DI
│   └── Quotations.Tests/            # xUnit tests
│       ├── Unit/                    # Unit tests (services, validators)
│       ├── Integration/             # Integration tests (API, MongoDB)
│       └── LoadTests/               # K6 load tests (Phase 7)
├── frontend/
│   ├── src/
│   │   ├── components/              # React components
│   │   │   ├── layout/              # Header, Footer
│   │   │   ├── quotations/          # QuotationCard, SearchBar, FilterPanel
│   │   │   └── common/              # Pagination, ErrorBoundary
│   │   ├── pages/                   # Page-level components
│   │   │   ├── Browse/              # BrowsePage
│   │   │   ├── Submit/              # SubmitPage
│   │   │   ├── MySubmissions/       # MySubmissionsPage
│   │   │   └── Review/              # ReviewQueuePage
│   │   ├── contexts/                # React Context (AuthContext)
│   │   ├── services/                # API clients (quotationsService, authService)
│   │   ├── types/                   # TypeScript type definitions
│   │   └── App.tsx                  # Root component with routing
│   └── tests/                       # Vitest tests
├── specs/                           # Feature specifications
│   └── 001-quotation-display/
│       ├── spec.md                  # Feature specification
│       ├── tasks.md                 # Task breakdown (110 tasks)
│       ├── quickstart.md            # Developer setup guide
│       └── contracts/               # API contracts
└── docker-compose.yml               # Multi-container setup (Phase 7)
```

## Authentication & Authorization

### Default Development Users

The system seeds three default users for testing:

| Username | Password | Role | Purpose |
|----------|----------|------|---------|
| `testuser` | `Test123!` | User | Regular user - can browse, search, submit quotations |
| `reviewer` | `Review123!` | Reviewer | Can review and approve/reject submissions |
| `admin` | `Admin123!` | Admin | Full access to all features |

### Role-Based Access Control

- **Anonymous**: Browse and search quotations (read-only)
- **User**: Browse, search, submit quotations, view own submissions
- **Reviewer**: User permissions + review queue access, approve/reject submissions
- **Admin**: All permissions + user management (future)

### JWT Token Flow

1. User logs in via `POST /api/v1/auth/login`
2. Backend validates credentials and returns JWT token
3. Frontend stores token in memory (AuthContext)
4. Token included in `Authorization: Bearer <token>` header for protected endpoints
5. Backend validates token and extracts user claims (userId, roles)

## API Endpoints

### Authentication
- `POST /api/v1/auth/register` - Register new user
- `POST /api/v1/auth/login` - Login and receive JWT token

### Quotations
- `GET /api/v1/quotations` - Browse quotations (paginated, filterable)
  - Query params: `page`, `pageSize`, `authorId`, `sourceType`, `tags`
- `GET /api/v1/quotations/search` - Search quotations (full-text)
  - Query params: `q`, `page`, `pageSize`
- `GET /api/v1/quotations/{id}` - Get quotation by ID
- `POST /api/v1/quotations` - Submit new quotation (auth required)
- `GET /api/v1/quotations/my-submissions` - Get user's submissions (auth required)
- `GET /api/v1/quotations/check-duplicate` - Check for duplicate quotations
  - Query params: `text`, `authorName`

### Review (Reviewer/Admin only)
- `GET /api/v1/quotations/pending` - Get pending quotations for review
- `PUT /api/v1/quotations/{id}/approve` - Approve quotation
- `PUT /api/v1/quotations/{id}/reject` - Reject quotation with reason

### Metadata
- `GET /api/v1/authors` - List all authors
- `GET /api/v1/authors/{id}` - Get author by ID
- `POST /api/v1/authors` - Create author (auth required)
- `GET /api/v1/sources` - List all sources
- `GET /api/v1/sources/{id}` - Get source by ID
- `POST /api/v1/sources` - Create source (auth required)
- `GET /api/v1/tags` - List tags with usage counts

Full API documentation available at `/swagger` when running backend (Phase 7 - T107).

## Database Schema

### Collections

**Users**
```javascript
{
  _id: ObjectId,
  username: string,
  displayName: string,
  passwordHash: string,
  roles: string[],  // ["User", "Reviewer", "Admin"]
  createdAt: DateTime
}
```

**Authors**
```javascript
{
  _id: ObjectId,
  name: string,
  bio: string,
  bornYear: int?,
  diedYear: int?,
  createdAt: DateTime
}
```

**Sources**
```javascript
{
  _id: ObjectId,
  title: string,
  type: string,  // "Book", "Movie", "Speech", "Interview", "Other"
  year: int?,
  description: string,
  createdAt: DateTime
}
```

**Quotations**
```javascript
{
  _id: ObjectId,
  text: string,
  context: string,
  author: {
    id: ObjectId,
    name: string
  },
  source: {
    id: ObjectId,
    title: string,
    type: string,
    year: int?
  },
  tags: string[],
  status: string,  // "Pending", "Approved", "Rejected"
  submittedBy: {
    userId: ObjectId,
    username: string
  },
  reviewedBy: {
    userId: ObjectId,
    username: string
  }?,
  rejectionReason: string?,
  submittedAt: DateTime,
  reviewedAt: DateTime?,
  createdAt: DateTime
}
```

**Indexes:**
- `Quotations.text` - Text index for full-text search
- `Quotations.status` - Filter by status
- `Quotations.submittedBy.userId` - User submissions lookup

## Development

### Running Tests

**Backend:**
```bash
cd backend
dotnet test
```

**Frontend:**
```bash
cd frontend
npm test              # Run all tests
npm run test:watch    # Watch mode
npm run test:coverage # Coverage report
```

### Code Quality

**Linting:**
```bash
# Frontend
cd frontend
npm run lint
npm run lint:fix

# Backend - automatically runs with build
cd backend
dotnet build
```

**Type Checking:**
```bash
cd frontend
npm run type-check
```

### Performance Targets (Phase 7)

- Lighthouse Performance: >90
- First Contentful Paint: <1.5s
- Time to Interactive: <3s
- API Response Times: p95 <200ms
- Support 100+ concurrent users

### Accessibility (Phase 7)

- WCAG 2.1 AA compliance
- Keyboard navigation for all interactive elements
- Screen reader compatibility
- Semantic HTML and ARIA labels
- Color contrast ratios >4.5:1

## Deployment (Production Readiness Checklist)

- [ ] Environment variables configured
- [ ] MongoDB connection string with credentials
- [ ] JWT secret changed from development default
- [ ] HTTPS/TLS enabled
- [ ] CORS configured for production domains
- [ ] Rate limiting enabled
- [ ] Logging configured (Application Insights, etc.)
- [ ] Health checks enabled (T108)
- [ ] Docker images built and tested
- [ ] Database indexes created
- [ ] Seed data removed or replaced with production data
- [ ] Security scanning completed (Snyk - T104)
- [ ] Code quality gates passed (SonarCloud - T105)
- [ ] Load testing completed (K6 - T102)
- [ ] Accessibility audit passed (axe-core - T099)

## Contributing

1. Follow the existing code style and conventions
2. Write tests for new features
3. Ensure all tests pass before submitting
4. Update documentation as needed
5. See [CLAUDE.md](CLAUDE.md) for AI assistant guidelines

## License

[Add license information]

## Support

For issues and questions:
- Feature Spec: [specs/001-quotation-display/spec.md](specs/001-quotation-display/spec.md)
- Developer Guide: [specs/001-quotation-display/quickstart.md](specs/001-quotation-display/quickstart.md)
- API Spec: [specs/001-quotation-display/contracts/api-spec.yaml](specs/001-quotation-display/contracts/api-spec.yaml)