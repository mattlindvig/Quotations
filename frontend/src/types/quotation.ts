/**
 * TypeScript types for quotation data
 */

export type SourceType = 'book' | 'movie' | 'speech' | 'interview' | 'other';
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

export interface AiReview {
  status: AiReviewStatus;
  modelUsed?: string | null;
  reviewedAt?: string | null;
  quoteAccuracy?: AiScore | null;
  attributionAccuracy?: AiScore | null;
  sourceAccuracy?: AiScore | null;
  summary?: string | null;
  suggestedTags: string[];
  isLikelyAuthentic?: boolean | null;
  authenticityReasoning?: string | null;
  approximateEra?: string | null;
  knownVariants?: string[];
}

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

export type PaginatedQuotationsResponse = PaginatedResponse<Quotation>;

export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  errors?: Record<string, string[]>;
}

export type QuotationSortBy = 'newest' | 'oldest' | 'author' | 'year';

export interface QuotationFilters {
  page?: number;
  pageSize?: number;
  status?: QuotationStatus;
  authorId?: string;
  authorName?: string;
  sourceType?: SourceType;
  tags?: string[];
  sortBy?: QuotationSortBy;
}
