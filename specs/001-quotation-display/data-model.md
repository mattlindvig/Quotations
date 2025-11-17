# Data Model: Quotation Display and Management Application

**Phase**: 1 (Design)
**Date**: 2025-10-28
**Branch**: `001-quotation-display`

## Overview

This document defines the data entities, their relationships, validation rules, and state transitions for the quotation management system. The model supports MongoDB document storage with embedded and referenced relationships as appropriate.

## Core Entities

### 1. Quotation

Represents a single quotation with all metadata, review status, and audit trail.

**MongoDB Collection**: `quotations`

**Schema**:
```typescript
interface Quotation {
  _id: ObjectId                    // MongoDB unique identifier
  text: string                     // The quotation text
  author: AuthorReference          // Reference to author
  source: SourceReference          // Reference to source
  tags: string[]                   // Array of tag names
  status: QuotationStatus          // Enum: pending, approved, rejected
  submittedBy: UserReference | null // User who submitted (null for seeded data)
  submittedAt: Date                // Submission timestamp
  reviewedBy: UserReference | null // Reviewer who processed
  reviewedAt: Date | null          // Review timestamp
  rejectionReason: string | null   // Reason for rejection (if rejected)
  createdAt: Date                  // Document creation timestamp
  updatedAt: Date                  // Last update timestamp
}

type QuotationStatus = 'pending' | 'approved' | 'rejected'

interface AuthorReference {
  id: ObjectId                     // Reference to Author document
  name: string                     // Denormalized for display performance
}

interface SourceReference {
  id: ObjectId                     // Reference to Source document
  title: string                    // Denormalized for display performance
  type: SourceType                 // Denormalized for filtering
}

type SourceType = 'book' | 'movie' | 'speech' | 'interview' | 'other'

interface UserReference {
  id: string                       // ASP.NET Identity user ID (GUID)
  username: string                 // Denormalized for display
}
```

**Validation Rules**:
- `text`: Required, 1-5000 characters, trimmed
- `author.name`: Required, 1-200 characters
- `source.title`: Required, 1-300 characters
- `source.type`: Required, must be valid SourceType
- `tags`: Optional, 0-20 tags max, each tag 1-50 characters
- `status`: Required, defaults to 'pending' for submissions
- `submittedAt`: Required, auto-set to current timestamp
- `reviewedAt`: Required when status is 'approved' or 'rejected'
- `rejectionReason`: Required when status is 'rejected', 1-500 characters

**Indexes**:
```javascript
// Text search (quote, author, source)
db.quotations.createIndex({
  text: "text",
  "author.name": "text",
  "source.title": "text"
}, { weights: { text: 10, "author.name": 5, "source.title": 3 } })

// Filter and sort
db.quotations.createIndex({
  status: 1,
  "author.id": 1,
  "source.type": 1,
  submittedAt: -1
})

// Tag filtering
db.quotations.createIndex({ tags: 1 })

// Review queue
db.quotations.createIndex({ status: 1, submittedAt: 1 })
```

**State Transitions**:
```
[New Submission] → pending
pending → approved (by reviewer)
pending → rejected (by reviewer)
approved → (no transitions)
rejected → (no transitions)
```

**Design Decisions**:
- **Denormalization**: Author name, source title/type embedded for read performance (avoids joins)
- **References**: Author and Source IDs stored for normalization updates
- **Tags**: Array of strings (not references) for simplicity; tag management via distinct query
- **Audit Trail**: `reviewedBy`, `reviewedAt`, `rejectionReason` for review accountability

---

### 2. Author

Represents a person who said or wrote a quotation.

**MongoDB Collection**: `authors`

**Schema**:
```typescript
interface Author {
  _id: ObjectId                    // MongoDB unique identifier
  name: string                     // Full name (primary display)
  lifespan: string | null          // e.g., "1809-1865" (optional)
  occupation: string | null        // e.g., "Philosopher, Writer" (optional)
  biography: string | null         // Short bio (optional)
  quotationCount: number           // Denormalized count for performance
  createdAt: Date                  // Document creation timestamp
  updatedAt: Date                  // Last update timestamp
}
```

**Validation Rules**:
- `name`: Required, unique, 1-200 characters
- `lifespan`: Optional, regex pattern: `\d{4}-\d{4}` or `\d{4}-present`
- `occupation`: Optional, 1-200 characters
- `biography`: Optional, 1-2000 characters
- `quotationCount`: Auto-calculated, non-negative integer

**Indexes**:
```javascript
db.authors.createIndex({ name: 1 }, { unique: true })
db.authors.createIndex({ name: "text" })
```

**Relationships**:
- One author → many quotations (referenced in Quotation.author.id)

---

### 3. Source

Represents the origin of a quotation (book, movie, speech, etc.).

**MongoDB Collection**: `sources`

**Schema**:
```typescript
interface Source {
  _id: ObjectId                    // MongoDB unique identifier
  title: string                    // Source title
  type: SourceType                 // book, movie, speech, interview, other
  year: number | null              // Publication/release year (optional)
  additionalInfo: string | null    // Publisher, director, etc. (optional)
  quotationCount: number           // Denormalized count for performance
  createdAt: Date                  // Document creation timestamp
  updatedAt: Date                  // Last update timestamp
}
```

**Validation Rules**:
- `title`: Required, 1-300 characters
- `type`: Required, must be valid SourceType enum
- `year`: Optional, 1000-2100 (reasonable year range)
- `additionalInfo`: Optional, 1-500 characters
- `quotationCount`: Auto-calculated, non-negative integer

**Indexes**:
```javascript
db.sources.createIndex({ title: 1, type: 1 }, { unique: true })
db.sources.createIndex({ title: "text" })
db.sources.createIndex({ type: 1 })
```

**Relationships**:
- One source → many quotations (referenced in Quotation.source.id)

---

### 4. User

Represents application users (submitters, reviewers, admins).

**ASP.NET Identity Tables**: Managed by ASP.NET Core Identity framework

**Key Fields** (from `AspNetUsers` table):
```csharp
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; }       // User's display name
    public DateTime CreatedAt { get; set; }        // Account creation date
    public bool IsActive { get; set; }             // Account enabled/disabled
    public int SubmissionCount { get; set; }       // Denormalized count
}
```

**Roles** (via `AspNetRoles` and `AspNetUserRoles`):
- `User`: Can submit quotations
- `Reviewer`: Can review/approve/reject quotations
- `Admin`: Full access (manage users, roles, settings)

**Validation Rules**:
- `Email`: Required, unique, valid email format
- `UserName`: Required, unique, 3-50 characters, alphanumeric + underscore
- `DisplayName`: Required, 1-100 characters
- `Password`: ASP.NET Identity complexity requirements (8+ chars, uppercase, lowercase, digit, symbol)

**Design Decisions**:
- ASP.NET Identity provides battle-tested user management
- JWT tokens contain userId + roles for authorization
- MongoDB quotations reference userId (not full user object)

---

### 5. Tag

Tags are not a separate collection; they exist as values within the Quotation.tags array.

**Tag Management**:
- Distinct query on `quotations.tags` returns all unique tags
- Tag usage count calculated via aggregation pipeline
- No formal Tag entity needed (folksonomy approach per spec assumptions)

**Tag Autocomplete Query**:
```javascript
db.quotations.aggregate([
  { $unwind: "$tags" },
  { $group: { _id: "$tags", count: { $sum: 1 } } },
  { $sort: { count: -1 } },
  { $limit: 100 }
])
```

---

## Relationships Diagram

```
┌──────────┐          ┌─────────────┐          ┌────────┐
│  Author  │◄────────┤  Quotation  ├─────────►│ Source │
└──────────┘  1:M    └─────────────┘   M:1    └────────┘
                            │
                            │ M:M (embedded)
                            ▼
                        [Tags Array]

                            │
                            │ M:1 (references)
                            ▼
                      ┌──────────┐
                      │   User   │
                      │(Identity)│
                      └──────────┘
```

**Relationship Details**:
- **Author ↔ Quotation**: One-to-many; author embedded in quotation for read performance
- **Source ↔ Quotation**: Many-to-one; source embedded in quotation for read performance
- **Quotation ↔ Tags**: Many-to-many; tags stored as string array (no Tag entity)
- **User ↔ Quotation**: Many-to-one; user reference (submittedBy, reviewedBy) for audit

---

## Data Integrity Rules

### Referential Integrity

1. **Quotation → Author**:
   - When author name changes, update all quotations with matching author.id
   - When author deleted, reject deletion if quotations exist (or cascade delete)

2. **Quotation → Source**:
   - When source title/type changes, update all quotations with matching source.id
   - When source deleted, reject deletion if quotations exist (or cascade delete)

3. **Quotation → User**:
   - User deletion does not delete quotations (preserve content)
   - Set submittedBy/reviewedBy to null or [deleted user] marker

### Duplicate Detection

**Duplicate Criteria** (during review):
- Exact text match (case-insensitive, whitespace normalized)
- Same author
- Same source

**Duplicate Query**:
```javascript
db.quotations.find({
  text: { $regex: /^exact text$/i },
  "author.id": authorId,
  "source.id": sourceId,
  status: "approved"
})
```

**Handling**: Reviewer warned during review; manual decision to approve or reject

---

## Aggregation Patterns

### 1. Search with Filters

```javascript
db.quotations.aggregate([
  // Text search
  { $match: { $text: { $search: "wisdom" } } },
  // Filters
  { $match: {
      status: "approved",
      "author.id": ObjectId("..."),
      "source.type": "book",
      tags: { $in: ["philosophy", "life"] }
  }},
  // Sort by relevance
  { $sort: { score: { $meta: "textScore" }, submittedAt: -1 } },
  // Pagination
  { $skip: 0 },
  { $limit: 20 }
])
```

### 2. Tag Usage Statistics

```javascript
db.quotations.aggregate([
  { $match: { status: "approved" } },
  { $unwind: "$tags" },
  { $group: { _id: "$tags", count: { $sum: 1 } } },
  { $sort: { count: -1 } },
  { $limit: 50 }
])
```

### 3. Author Quotation Count Update

```javascript
db.authors.updateMany({}, [
  {
    $set: {
      quotationCount: {
        $size: {
          $filter: {
            input: { $lookup: { from: "quotations", ... } },
            cond: { $eq: ["$$this.status", "approved"] }
          }
        }
      }
    }
  }
])
```

---

## Performance Considerations

### Read Optimization

- **Denormalization**: Author name, source title/type embedded in quotations
- **Indexes**: Text search, filter combinations, review queue optimized
- **Pagination**: Cursor-based for deep pagination (avoid skip on large offsets)

### Write Optimization

- **Async Updates**: Author/source quotation counts updated asynchronously (background job)
- **Batch Operations**: Bulk updates for denormalized data changes
- **Write Concern**: `w: majority` for quotation submissions (ensure durability)

### Estimated Collection Sizes

| Collection | Initial | Target (1 year) | Document Size |
|------------|---------|-----------------|---------------|
| quotations | 1,000   | 100,000         | ~2 KB avg     |
| authors    | 500     | 5,000           | ~500 bytes    |
| sources    | 300     | 3,000           | ~400 bytes    |
| users      | 50      | 1,000           | ~1 KB         |

**Storage**: ~200 MB at target scale (well within MongoDB free tier limits)

---

## Migration Strategy

### Initial Data Seeding

1. **Authors**: Import from curated list (famous authors, historical figures)
2. **Sources**: Import from curated list (classic books, popular movies)
3. **Quotations**: Import 1,000+ curated quotations (pre-approved status)
4. **Tags**: Auto-generated from quotation imports
5. **Admin User**: Create initial reviewer/admin account

### Schema Evolution

- **Versioning**: Include `schemaVersion` field in documents for migration tracking
- **Backward Compatibility**: Additive changes preferred (new optional fields)
- **Migration Scripts**: C# console app for complex schema changes

---

## Summary

The data model balances normalization (separate Author/Source entities) with denormalization (embedded references in Quotation) for optimal read performance. MongoDB's flexible schema supports the folksonomy tag approach while maintaining referential integrity through application logic. Indexes are strategically placed to support search, filter, and review workflows within constitutional performance requirements (< 200ms p95 latency).