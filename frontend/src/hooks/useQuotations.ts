import { useState, useEffect, useCallback, useRef } from 'react';
import { apiClient } from '../services/apiClient';
import type {
  Quotation,
  PaginatedQuotationsResponse,
  QuotationFilters,
  ApiResponse,
} from '../types/quotation';

interface UseQuotationsResult {
  quotations: Quotation[];
  loading: boolean;
  error: string | null;
  pagination: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPrevious: boolean;
    hasNext: boolean;
  } | null;
  fetchQuotations: (filters?: QuotationFilters) => Promise<void>;
  goToPage: (page: number) => Promise<void>;
  nextPage: () => Promise<void>;
  previousPage: () => Promise<void>;
}

/**
 * Custom hook for fetching and managing quotations
 */
export function useQuotations(initialFilters?: QuotationFilters): UseQuotationsResult {
  const [quotations, setQuotations] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pagination, setPagination] = useState<UseQuotationsResult['pagination']>(null);
  const currentFiltersRef = useRef<QuotationFilters>(initialFilters || { page: 1, pageSize: 20 });

  const fetchQuotations = useCallback(async (filters?: QuotationFilters) => {
    const mergedFilters = { ...currentFiltersRef.current, ...filters };
    currentFiltersRef.current = mergedFilters;

    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams();

      if (mergedFilters.page) params.append('page', mergedFilters.page.toString());
      if (mergedFilters.pageSize) params.append('pageSize', mergedFilters.pageSize.toString());
      if (mergedFilters.status) params.append('status', mergedFilters.status);
      if (mergedFilters.authorId) params.append('authorId', mergedFilters.authorId);
      if (mergedFilters.sourceType) params.append('sourceType', mergedFilters.sourceType);
      if (mergedFilters.tags && mergedFilters.tags.length > 0) {
        params.append('tags', mergedFilters.tags.join(','));
      }

      const response = await apiClient.get<ApiResponse<PaginatedQuotationsResponse>>(
        `/api/v1/quotations?${params.toString()}`
      );

      if (response.success && response.data) {
        setQuotations(response.data.items);
        setPagination(response.data.pagination);
      } else {
        setError('Failed to fetch quotations');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred while fetching quotations');
      setQuotations([]);
      setPagination(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const goToPage = useCallback(
    async (page: number) => {
      await fetchQuotations({ page });
    },
    [fetchQuotations]
  );

  const nextPage = useCallback(async () => {
    if (pagination && pagination.hasNext) {
      await goToPage(pagination.page + 1);
    }
  }, [pagination, goToPage]);

  const previousPage = useCallback(async () => {
    if (pagination && pagination.hasPrevious) {
      await goToPage(pagination.page - 1);
    }
  }, [pagination, goToPage]);

  // Fetch quotations on mount
  useEffect(() => {
    fetchQuotations();
  }, [fetchQuotations]);

  return {
    quotations,
    loading,
    error,
    pagination,
    fetchQuotations,
    goToPage,
    nextPage,
    previousPage,
  };
}
