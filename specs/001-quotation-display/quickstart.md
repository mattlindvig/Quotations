# Quickstart Guide: Quotation Display and Management Application

**Branch**: `001-quotation-display`
**Date**: 2025-10-28

## Overview

This guide helps developers set up the local development environment and understand the project workflow for the Quotation Display and Management Application.

## Prerequisites

### Backend
- .NET SDK 8.0+ ([download](https://dotnet.microsoft.com/download))
- Docker Desktop (for MongoDB Testcontainers in tests)
- IDE: Visual Studio 2022, JetBrains Rider, or VS Code with C# extension

### Frontend
- Node.js 18+ and npm 9+ ([download](https://nodejs.org/))
- IDE: VS Code with recommended extensions (see below)

### Database
- MongoDB 6.0+ (local install or Docker container)

## Project Setup

### 1. Clone Repository

```bash
git clone <repository-url>
cd Quotations
git checkout 001-quotation-display
```

### 2. Backend Setup

```bash
cd backend

# Restore NuGet packages
dotnet restore

# Set user secrets for development (MongoDB connection string)
cd Quotations.Api
dotnet user-secrets init
dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "MongoDB:DatabaseName" "quotations_dev"
dotnet user-secrets set "Jwt:Secret" "your-256-bit-secret-key-change-in-production"
dotnet user-secrets set "Jwt:Issuer" "https://localhost:5000"
dotnet user-secrets set "Jwt:Audience" "https://localhost:3000"

# Build the project
dotnet build

# Run database migrations / seed initial data
dotnet run --project Quotations.Api --seed-data

# Start API
dotnet run --project Quotations.Api
# API will be available at https://localhost:5000
```

### 3. Frontend Setup

```bash
cd frontend

# Install dependencies
npm install

# Create .env file for development
cat > .env.local << EOF
VITE_API_BASE_URL=https://localhost:5000/v1
VITE_APP_NAME=Quotations
EOF

# Start development server
npm run dev
# Frontend will be available at http://localhost:3000
```

### 4. MongoDB Setup (Docker)

```bash
# Start MongoDB container
docker run -d \
  --name quotations-mongodb \
  -p 27017:27017 \
  -e MONGO_INITDB_DATABASE=quotations_dev \
  mongo:6.0

# Verify MongoDB is running
docker ps | grep quotations-mongodb
```

## Development Workflow

### Running Tests

**Backend Tests**:
```bash
cd backend

# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test project
dotnet test Quotations.Tests/Quotations.Tests.csproj

# Watch mode (auto-run on file changes)
dotnet watch test
```

**Frontend Tests**:
```bash
cd frontend

# Run unit tests
npm test

# Run with coverage
npm run test:coverage

# Watch mode
npm test -- --watch

# UI mode (Vitest UI)
npm test -- --ui
```

### Code Quality Checks

**Backend Linting**:
```bash
cd backend

# Run .NET analyzers (built into dotnet build)
dotnet build /p:TreatWarningsAsErrors=true

# Format code
dotnet format
```

**Frontend Linting**:
```bash
cd frontend

# Run ESLint
npm run lint

# Fix auto-fixable issues
npm run lint:fix

# Format with Prettier
npm run format

# Type check
npm run type-check
```

### Database Operations

**Seed Sample Data**:
```bash
# Backend includes seed command
dotnet run --project backend/Quotations.Api --seed-data
```

**MongoDB Shell Access**:
```bash
# Access MongoDB shell
docker exec -it quotations-mongodb mongosh

# In mongosh:
use quotations_dev

# View collections
show collections

# Query quotations
db.quotations.find({ status: "approved" }).limit(5)

# Check indexes
db.quotations.getIndexes()
```

**Reset Database**:
```bash
# Drop database and re-seed
docker exec quotations-mongodb mongosh quotations_dev --eval "db.dropDatabase()"
dotnet run --project backend/Quotations.Api --seed-data
```

## Project Structure Reference

```
backend/
├── Quotations.Api/              # ASP.NET Core Web API
│   ├── Controllers/             # API endpoints
│   ├── Models/                  # Domain models and DTOs
│   ├── Services/                # Business logic
│   ├── Repositories/            # Data access (MongoDB)
│   ├── Validators/              # FluentValidation validators
│   ├── Middleware/              # Auth, error handling
│   └── Program.cs               # App configuration
├── Quotations.Tests/            # Test project
└── Quotations.sln               # Solution file

frontend/
├── src/
│   ├── components/              # React components
│   │   ├── quotations/          # Quotation display components
│   │   ├── forms/               # Form components
│   │   └── common/              # Shared UI components
│   ├── pages/                   # Page components (routes)
│   ├── services/                # API client
│   ├── hooks/                   # Custom React hooks
│   ├── types/                   # TypeScript types
│   └── App.tsx                  # Root component
├── tests/                       # Vitest tests
└── package.json
```

## Common Development Tasks

### Adding a New API Endpoint

1. Define endpoint in [api-spec.yaml](contracts/api-spec.yaml)
2. Create DTO models in `backend/Quotations.Api/Models/`
3. Add controller action in `backend/Quotations.Api/Controllers/`
4. Implement service logic in `backend/Quotations.Api/Services/`
5. Add repository method if needed
6. Write tests in `backend/Quotations.Tests/Integration/`
7. Update frontend API client in `frontend/src/services/`

### Adding a New React Component

1. Create component file in appropriate `components/` subdirectory
2. Add TypeScript types in `frontend/src/types/`
3. Write unit test in `frontend/tests/unit/`
4. Add Storybook story (if using Storybook)
5. Ensure accessibility (run `axe` audits)
6. Update related pages/components to use it

### Database Schema Changes

1. Update data model in [data-model.md](data-model.md)
2. Modify C# models in `backend/Quotations.Api/Models/`
3. Update repository queries if needed
4. Create migration script (if breaking change)
5. Update seed data
6. Test with existing data (backward compatibility)

## Environment Variables

### Backend (`appsettings.Development.json` or User Secrets)

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "quotations_dev"
  },
  "Jwt": {
    "Secret": "your-256-bit-secret-key-minimum-32-characters-long",
    "Issuer": "https://localhost:5000",
    "Audience": "https://localhost:3000",
    "ExpirationMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

### Frontend (`.env.local`)

```bash
VITE_API_BASE_URL=https://localhost:5000/v1
VITE_APP_NAME=Quotations
VITE_ENABLE_MOCKS=false         # Set to true for API mocking (MSW)
```

## Recommended VS Code Extensions

Frontend Development:
- ESLint (`dbaeumer.vscode-eslint`)
- Prettier (`esbenp.prettier-vscode`)
- TypeScript Vue Plugin (`Vue.volar`)
- Tailwind CSS IntelliSense (if using Tailwind)
- axe Accessibility Linter (`deque-systems.vscode-axe-linter`)

Backend Development:
- C# (`ms-dotnettools.csharp`)
- C# Dev Kit (`ms-dotnettools.csdevkit`)
- MongoDB for VS Code (`mongodb.mongodb-vscode`)

## Troubleshooting

### Backend Issues

**"MongoDB connection failed"**:
- Ensure MongoDB container is running: `docker ps`
- Check connection string in user secrets
- Verify port 27017 is not blocked

**"JWT token validation failed"**:
- Ensure JWT secret is configured
- Check token expiration time
- Verify Issuer/Audience match between backend and frontend

### Frontend Issues

**"API request failed (CORS)"**:
- Ensure backend CORS policy allows `http://localhost:3000`
- Check backend is running on correct port
- Verify `VITE_API_BASE_URL` in `.env.local`

**"Module not found"**:
- Run `npm install` to install dependencies
- Clear node_modules and reinstall: `rm -rf node_modules && npm install`

### Test Issues

**"Testcontainers failed to start"**:
- Ensure Docker Desktop is running
- Check Docker daemon is accessible
- On Linux, ensure user is in docker group

## Next Steps

1. Review the [API specification](contracts/api-spec.yaml)
2. Read the [data model documentation](data-model.md)
3. Explore the [research findings](research.md)
4. Start implementing User Story 1 (Browse Quotations) - see `/speckit.tasks`

## Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core/)
- [React Documentation](https://react.dev/)
- [MongoDB C# Driver](https://www.mongodb.com/docs/drivers/csharp/)
- [Vitest Documentation](https://vitest.dev/)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)