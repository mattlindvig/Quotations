import React, { useEffect, useState, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuotations } from '../../hooks/useQuotations';
import { useSearch } from '../../hooks/useSearch';
import { useFilters, type QuotationFilters } from '../../hooks/useFilters';
import { QuotationList } from '../../components/quotations/QuotationList';
import { PaginationControls } from '../../components/quotations/PaginationControls';
import { SearchBar } from '../../components/quotations/SearchBar';
import { FilterPanel } from '../../components/quotations/FilterPanel';
import type { SourceType } from '../../types/quotation';
import './BrowsePage.css';

/**
 * Browse page - displays paginated list of approved quotations with search and filter
 * Implements accessibility best practices (ARIA labels, semantic HTML, keyboard navigation)
 * Preserves search/filter state in URL query parameters
 */
export const BrowsePage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeMode, setActiveMode] = useState<'browse' | 'search'>('browse');

  // Extract initial values from URL
  const initialQuery = searchParams.get('q') || '';
  const initialPage = parseInt(searchParams.get('page') || '1');
  const initialAuthorId = searchParams.get('authorId') || undefined;
  const initialSourceType = searchParams.get('sourceType') as SourceType | undefined;
  const initialTags = searchParams.get('tags')?.split(',').filter(Boolean) || undefined;

  const initialFilters: QuotationFilters = {
    authorId: initialAuthorId,
    sourceType: initialSourceType,
    tags: initialTags,
  };

  // Hooks
  const { filters, setFilters } = useFilters(initialFilters);
  const {
    quotations: browseQuotations,
    loading: browseLoading,
    error: browseError,
    pagination: browsePagination,
    fetchQuotations,
    goToPage: browsePage,
    nextPage: browseNextPage,
    previousPage: browsePreviousPage,
  } = useQuotations({ status: 'approved', page: initialPage, pageSize: 20, ...filters });

  const {
    searchQuery,
    searchResults,
    searchLoading,
    searchError,
    pagination: searchPagination,
    performSearch,
    clearSearch,
    goToPage: searchPage,
  } = useSearch();

  // Determine active data
  const quotations = activeMode === 'search' ? searchResults : browseQuotations;
  const loading = activeMode === 'search' ? searchLoading : browseLoading;
  const error = activeMode === 'search' ? searchError : browseError;
  const pagination = activeMode === 'search' ? searchPagination : browsePagination;
  const hasResults = quotations.length > 0;

  // Initialize search from URL on mount
  useEffect(() => {
    if (initialQuery) {
      performSearch(initialQuery, initialPage);
      setActiveMode('search');
    }
  }, []); // Run only on mount

  // Handle search
  const handleSearch = useCallback(
    (query: string) => {
      if (query.trim()) {
        performSearch(query, 1);
        setActiveMode('search');
      } else {
        clearSearch();
        setActiveMode('browse');
        fetchQuotations({ ...filters, page: 1 });
      }
    },
    [performSearch, clearSearch, fetchQuotations, filters]
  );

  // Handle filter changes
  const handleFilterChange = useCallback(
    (newFilters: QuotationFilters) => {
      setFilters(newFilters);
      if (activeMode === 'browse') {
        fetchQuotations({ ...newFilters, page: 1 });
      }
    },
    [setFilters, fetchQuotations, activeMode]
  );

  // Handle pagination
  const handlePageChange = useCallback(
    (page: number) => {
      if (activeMode === 'search') {
        searchPage(page);
      } else {
        browsePage(page);
      }
    },
    [activeMode, searchPage, browsePage]
  );

  const handleNextPage = useCallback(() => {
    if (activeMode === 'browse') {
      browseNextPage();
    } else if (pagination && pagination.hasNext) {
      searchPage(pagination.page + 1);
    }
  }, [activeMode, browseNextPage, searchPage, pagination]);

  const handlePreviousPage = useCallback(() => {
    if (activeMode === 'browse') {
      browsePreviousPage();
    } else if (pagination && pagination.hasPrevious) {
      searchPage(pagination.page - 1);
    }
  }, [activeMode, browsePreviousPage, searchPage, pagination]);

  return (
    <main className="browse-page" id="main-content">
      <div className="browse-header">
        <h1 className="page-title">Browse Quotations</h1>
        <p className="page-description">
          Explore our collection of inspiring quotations from various sources
        </p>
      </div>

      <div className="browse-content">
        <SearchBar onSearch={handleSearch} initialValue={initialQuery} />
        <FilterPanel onFilterChange={handleFilterChange} initialFilters={initialFilters} />

        {/* Result count and mode indicator */}
        {!loading && hasResults && (
          <div className="results-info" aria-live="polite">
            {activeMode === 'search' && (
              <p className="search-mode-indicator">
                Showing search results for: <strong>"{searchQuery}"</strong>
              </p>
            )}
            {pagination && (
              <p className="result-count">
                {pagination.totalCount} {pagination.totalCount === 1 ? 'quotation' : 'quotations'} found
              </p>
            )}
          </div>
        )}

        {/* Empty state */}
        {!loading && !hasResults && !error && (
          <div className="empty-state" role="status">
            <p>No quotations found matching your criteria.</p>
            {(searchQuery || filters.authorId || filters.sourceType || filters.tags?.length) && (
              <button
                className="clear-all-button"
                onClick={() => {
                  clearSearch();
                  setFilters({});
                  setActiveMode('browse');
                  fetchQuotations({ page: 1 });
                }}
              >
                Clear search and filters
              </button>
            )}
          </div>
        )}

        {error && (
          <div className="error-message" role="alert" aria-live="assertive">
            <svg
              className="error-icon"
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="currentColor"
              aria-hidden="true"
            >
              <path
                fillRule="evenodd"
                d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"
                clipRule="evenodd"
              />
            </svg>
            <div className="error-content">
              <h2 className="error-title">Error Loading Quotations</h2>
              <p className="error-text">{error}</p>
              <button
                className="retry-button"
                onClick={() => window.location.reload()}
                aria-label="Retry loading quotations"
              >
                Retry
              </button>
            </div>
          </div>
        )}

        {!error && hasResults && (
          <>
            <QuotationList quotations={quotations} loading={loading} />

            {!loading && pagination && (
              <PaginationControls
                pagination={pagination}
                onPageChange={handlePageChange}
                onPrevious={handlePreviousPage}
                onNext={handleNextPage}
              />
            )}
          </>
        )}
      </div>

      {/* Skip to content link for accessibility */}
      <a href="#main-content" className="skip-link">
        Skip to main content
      </a>
    </main>
  );
};
