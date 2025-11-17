import { useState, useCallback } from 'react';
import type { SourceType } from '../types/quotation';

export interface QuotationFilters {
  authorId?: string;
  sourceType?: SourceType;
  tags?: string[];
}

interface UseFiltersResult {
  filters: QuotationFilters;
  setFilters: (filters: QuotationFilters) => void;
  updateFilter: (key: keyof QuotationFilters, value: string | string[] | undefined) => void;
  clearFilters: () => void;
  hasActiveFilters: boolean;
}

/**
 * Custom hook for filter state management
 */
export function useFilters(initialFilters: QuotationFilters = {}): UseFiltersResult {
  const [filters, setFiltersState] = useState<QuotationFilters>(initialFilters);

  const setFilters = useCallback((newFilters: QuotationFilters) => {
    setFiltersState(newFilters);
  }, []);

  const updateFilter = useCallback(
    (key: keyof QuotationFilters, value: string | string[] | undefined) => {
      setFiltersState((prev) => {
        const updated = { ...prev };

        if (value === undefined || value === '' || (Array.isArray(value) && value.length === 0)) {
          delete updated[key];
        } else {
          updated[key] = value as any;
        }

        return updated;
      });
    },
    []
  );

  const clearFilters = useCallback(() => {
    setFiltersState({});
  }, []);

  const hasActiveFilters = Object.keys(filters).length > 0;

  return {
    filters,
    setFilters,
    updateFilter,
    clearFilters,
    hasActiveFilters,
  };
}
