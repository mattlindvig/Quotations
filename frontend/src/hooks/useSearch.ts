import { useState, useCallback } from 'react';
import { apiClient } from '../services/apiClient';
import type {
  Quotation,
  PaginatedQuotationsResponse,
  ApiResponse,
} from '../types/quotation';

interface UseSearchResult {
  searchQuery: string;
  searchResults: Quotation[];
  searchLoading: boolean;
  searchError: string | null;
  pagination: PaginatedQuotationsResponse['pagination'] | null;
  performSearch: (query: string, page?: number, pageSize?: number) => Promise<void>;
  clearSearch: () => void;
  goToPage: (page: number) => Promise<void>;
}

/**
 * Custom hook for search state management
 */
export function useSearch(): UseSearchResult {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<Quotation[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [pagination, setPagination] = useState<PaginatedQuotationsResponse['pagination'] | null>(
    null
  );
  const [currentPage, setCurrentPage] = useState(1);
  const [currentPageSize, setCurrentPageSize] = useState(20);

  const performSearch = useCallback(async (query: string, page = 1, pageSize = 20) => {
    if (!query.trim()) {
      setSearchResults([]);
      setPagination(null);
      setSearchQuery('');
      return;
    }

    setSearchQuery(query);
    setSearchLoading(true);
    setSearchError(null);
    setCurrentPage(page);
    setCurrentPageSize(pageSize);

    try {
      const params = new URLSearchParams({
        q: query,
        page: page.toString(),
        pageSize: pageSize.toString(),
      });

      const response = await apiClient.get<ApiResponse<PaginatedQuotationsResponse>>(
        `/api/v1/quotations/search?${params.toString()}`
      );

      if (response.data.success && response.data.data) {
        setSearchResults(response.data.data.items);
        setPagination(response.data.data.pagination);
      } else {
        setSearchError('Failed to search quotations');
        setSearchResults([]);
        setPagination(null);
      }
    } catch (err) {
      setSearchError(
        err instanceof Error ? err.message : 'An error occurred while searching quotations'
      );
      setSearchResults([]);
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
    setCurrentPage(1);
  }, []);

  const goToPage = useCallback(
    async (page: number) => {
      if (searchQuery) {
        await performSearch(searchQuery, page, currentPageSize);
      }
    },
    [searchQuery, currentPageSize, performSearch]
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
