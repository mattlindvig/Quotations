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

export interface Quotation {
  id: string;
  text: string;
  author: Author;
  source: Source;
  tags: string[];
  status: QuotationStatus;
  submittedAt: string;
  reviewedAt?: string;
}

export interface PaginationMetadata {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface PaginatedQuotationsResponse {
  items: Quotation[];
  pagination: PaginationMetadata;
}

export interface ApiResponse<T> {
  data?: T;
  success: boolean;
  errors?: Record<string, string[]>;
}

export interface QuotationFilters {
  page?: number;
  pageSize?: number;
  status?: QuotationStatus;
  authorId?: string;
  sourceType?: SourceType;
  tags?: string[];
}
