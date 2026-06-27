/**
 * TypeScript types for quotation data
 */

export type SourceType =
  | 'book' | 'movie' | 'television' | 'speech' | 'interview' | 'poem'
  | 'song' | 'play' | 'musical' | 'videogame' | 'comic' | 'article'
  | 'letter' | 'podcast' | 'documentary' | 'scripture' | 'proverb'
  | 'memoir' | 'standup' | 'organization' | 'other';
export type QuotationStatus = 'pending' | 'approved' | 'rejected';

export interface Author {
  id: string;
  name: string;
  lifespan?: string;
  occupation?: string;
}

export interface Source {
  id: string;
  title: string;
  type: SourceType;
  year?: number;
}

export type AiReviewStatus = 'notreviewed' | 'pending' | 'inprogress' | 'reviewed' | 'failed';

export interface AiScore {
  score: number;
  reasoning: string;
  suggestedValue?: string | null;
  wasAiFilled: boolean;
  citations: string[];
}

// Full AI review — returned by GET /quotations/:id
export interface AiReview {
  status: AiReviewStatus;
  modelUsed?: string | null;
  reviewedAt?: string | null;
  quoteAccuracy?: AiScore | null;
  attributionAccuracy?: AiScore | null;
  sourceAccuracy?: AiScore | null;
  summary?: string | null;
  suggestedTags?: string[];
  isLikelyAuthentic?: boolean | null;
  authenticityReasoning?: string | null;
  correctAttribution?: string | null;
  approximateEra?: string | null;
  knownVariants?: string[];
  language?: string | null;
  qualityScore?: number | null;
  mood?: string | null;
}

// Slim AI review — returned by list/search/random-batch endpoints.
export interface AiReviewSummary {
  status: AiReviewStatus;
  modelUsed?: string | null;
  reviewedAt?: string | null;
  summary?: string | null;
  isLikelyAuthentic?: boolean | null;
  approximateEra?: string | null;
  language?: string | null;
  qualityScore?: number | null;
  mood?: string | null;
}

// Slim quotation returned by list/search/random-batch/favorites endpoints
export interface QuotationSummary {
  id: string;
  text: string;
  author: Author;
  source: Source;
  tags: string[];
  status: QuotationStatus;
  submittedAt: string;
  reviewedAt?: string;
  potentialDuplicateIds?: string[];
  aiReview?: AiReviewSummary;
}

// Full quotation returned by GET /quotations/:id and review-queue endpoints
export interface Quotation {
  id: string;
  text: string;
  author: Author;
  source: Source;
  tags: string[];
  status: QuotationStatus;
  submittedAt: string;
  reviewedAt?: string;
  submittedBy?: { id: string; username: string };
  potentialDuplicateIds?: string[];
  aiReview?: AiReview;
}

export interface PaginationMetadata {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface PaginatedResponse<T> {
  items: T[];
  pagination: PaginationMetadata;
}

export type PaginatedQuotationsResponse = PaginatedResponse<QuotationSummary>;

export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  errors?: Record<string, string[]>;
}

export type QuotationSortBy = 'newest' | 'oldest' | 'author';

export interface QuotationFilters {
  page?: number;
  pageSize?: number;
  status?: QuotationStatus;
  authorId?: string;
  authorName?: string;
  sourceType?: SourceType;
  sourceTitle?: string;
  tags?: string[];
  sortBy?: QuotationSortBy;
  yearFrom?: string;
  yearTo?: string;
}
