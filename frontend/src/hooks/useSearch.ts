import { useState, useCallback } from 'react';
import { apiClient } from '../services/apiClient';
import type {
  QuotationSummary,
  PaginatedQuotationsResponse,
  ApiResponse,
} from '../types/quotation';

interface SearchFilters {
  authorName?: string;
  sourceType?: string;
  tags?: string[];
  yearFrom?: string;
  yearTo?: string;
}

interface UseSearchResult {
  searchQuery: string;
  searchResults: QuotationSummary[];
  searchLoading: boolean;
  searchError: string | null;
  pagination: PaginatedQuotationsResponse['pagination'] | null;
  performSearch: (query: string, page?: number, pageSize?: number, filters?: SearchFilters) => Promise<void>;
  clearSearch: () => void;
  goToPage: (page: number) => Promise<void>;
}

/**
 * Custom hook for search state management
 */
export function useSearch(): UseSearchResult {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<QuotationSummary[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [pagination, setPagination] = useState<PaginatedQuotationsResponse['pagination'] | null>(
    null
  );
  const [currentPageSize, setCurrentPageSize] = useState(20);
  const [currentFilters, setCurrentFilters] = useState<SearchFilters>({});

  const performSearch = useCallback(async (query: string, page = 1, pageSize = 20, filters: SearchFilters = {}) => {
    if (!query.trim()) {
      setSearchResults([]);
      setPagination(null);
      setSearchQuery('');
      return;
    }

    const isNewQuery = page <= 1;
    setSearchQuery(query);
    setSearchLoading(true);
    setSearchError(null);
    setCurrentPageSize(pageSize);
    setCurrentFilters(filters);

    try {
      const params = new URLSearchParams({
        q: query,
        page: page.toString(),
        pageSize: pageSize.toString(),
      });

      if (filters.authorName) params.set('authorName', filters.authorName);
      if (filters.sourceType) params.set('sourceType', filters.sourceType);
      if (filters.tags && filters.tags.length > 0) {
        filters.tags.forEach((tag) => params.append('tags', tag));
      }
      if (filters.yearFrom) params.set('yearFrom', filters.yearFrom);
      if (filters.yearTo) params.set('yearTo', filters.yearTo);

      const response = await apiClient.get<ApiResponse<PaginatedQuotationsResponse>>(
        `/quotations/search?${params.toString()}`
      );

      if (response.success && response.data) {
        setSearchResults((prev) =>
          isNewQuery ? response.data!.items : [...prev, ...response.data!.items]
        );
        setPagination(response.data.pagination);
      } else {
        setSearchError('Failed to search quotations');
        if (isNewQuery) setSearchResults([]);
        setPagination(null);
      }
    } catch (err) {
      setSearchError(
        err instanceof Error ? err.message : 'An error occurred while searching quotations'
      );
      if (isNewQuery) setSearchResults([]);
      setPagination(null);
    } finally {
      setSearchLoading(false);
    }
  }, []);

  const clearSearch = useCallback(() => {
    setSearchQuery('');
    setSearchResults([]);
    setSearchError(null);
    setPagination(null);
    setCurrentFilters({});
  }, []);

  const goToPage = useCallback(
    async (page: number) => {
      if (searchQuery) {
        await performSearch(searchQuery, page, currentPageSize, currentFilters);
      }
    },
    [searchQuery, currentPageSize, currentFilters, performSearch]
  );

  return {
    searchQuery,
    searchResults,
    searchLoading,
    searchError,
    pagination,
    performSearch,
    clearSearch,
    goToPage,
  };
}
