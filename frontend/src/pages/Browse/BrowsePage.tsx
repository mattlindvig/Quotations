import React, { useEffect, useState, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuotations } from '../../hooks/useQuotations';
import { useSearch } from '../../hooks/useSearch';
import { useFilters, type QuotationFilters } from '../../hooks/useFilters';
import { QuotationList } from '../../components/quotations/QuotationList';
import { PaginationControls } from '../../components/quotations/PaginationControls';
import { SearchBar } from '../../components/quotations/SearchBar';
import { FilterPanel } from '../../components/quotations/FilterPanel';
import { SurpriseModal } from '../../components/quotations/SurpriseModal';
import apiClient from '../../services/apiClient';
import type { Quotation, SourceType, ApiResponse } from '../../types/quotation';
import './BrowsePage.css';

export const BrowsePage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const [activeMode, setActiveMode] = useState<'browse' | 'search'>('browse');
  const [surpriseQuote, setSurpriseQuote] = useState<Quotation | null>(null);
  const [surpriseLoading, setSurpriseLoading] = useState(false);
  const [showSurprise, setShowSurprise] = useState(false);

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

  const quotations = activeMode === 'search' ? searchResults : browseQuotations;
  const loading = activeMode === 'search' ? searchLoading : browseLoading;
  const error = activeMode === 'search' ? searchError : browseError;
  const pagination = activeMode === 'search' ? searchPagination : browsePagination;
  const hasResults = quotations.length > 0;

  useEffect(() => {
    if (initialQuery) {
      performSearch(initialQuery, initialPage);
      setActiveMode('search');
    }
  }, []);

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

  const handleFilterChange = useCallback(
    (newFilters: QuotationFilters) => {
      setFilters(newFilters);
      if (activeMode === 'browse') {
        // Explicitly reset every filter field first so cleared selections don't
        // persist in the ref that useQuotations merges against.
        fetchQuotations({
          status: 'approved',
          page: 1,
          pageSize: 20,
          authorId: undefined,
          sourceType: undefined,
          tags: undefined,
          ...newFilters,
        });
      }
    },
    [setFilters, fetchQuotations, activeMode]
  );

  const handlePageChange = useCallback(
    (page: number) => {
      if (activeMode === 'search') searchPage(page);
      else browsePage(page);
    },
    [activeMode, searchPage, browsePage]
  );

  const handleNextPage = useCallback(() => {
    if (activeMode === 'browse') browseNextPage();
    else if (pagination?.hasNext) searchPage(pagination.page + 1);
  }, [activeMode, browseNextPage, searchPage, pagination]);

  const handlePreviousPage = useCallback(() => {
    if (activeMode === 'browse') browsePreviousPage();
    else if (pagination?.hasPrevious) searchPage(pagination.page - 1);
  }, [activeMode, browsePreviousPage, searchPage, pagination]);

  const fetchSurprise = useCallback(async () => {
    setSurpriseLoading(true);
    try {
      const res = await apiClient.get<ApiResponse<Quotation>>('/quotations/random');
      setSurpriseQuote(res.data ?? null);
    } catch {
      setSurpriseQuote(null);
    } finally {
      setSurpriseLoading(false);
    }
  }, []);

  const handleSurprise = useCallback(async () => {
    setShowSurprise(true);
    await fetchSurprise();
  }, [fetchSurprise]);

  return (
    <main className="browse-page" id="main-content">
      <a href="#main-content" className="skip-link">Skip to main content</a>

      <div className="browse-layout">
        {/* Sidebar — filters */}
        <aside className="browse-sidebar">
          <FilterPanel onFilterChange={handleFilterChange} initialFilters={initialFilters} />
        </aside>

        {/* Main content */}
        <div className="browse-main">
          <div className="browse-toolbar">
            <SearchBar onSearch={handleSearch} initialValue={initialQuery} />
            <button className="surprise-btn" onClick={handleSurprise} title="Show me a random quote">
              ✨ Surprise Me
            </button>
          </div>

          {!loading && hasResults && (
            <div className="results-info" aria-live="polite">
              {activeMode === 'search' && (
                <p className="search-mode-indicator">
                  Results for: <strong>"{searchQuery}"</strong>
                </p>
              )}
              {pagination && (
                <p className="result-count">
                  {pagination.totalCount.toLocaleString()} {pagination.totalCount === 1 ? 'quotation' : 'quotations'}
                </p>
              )}
            </div>
          )}

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
            <div className="error-message" role="alert">
              <div className="error-content">
                <h2 className="error-title">Error Loading Quotations</h2>
                <p className="error-text">{error}</p>
                <button className="retry-button" onClick={() => window.location.reload()}>
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
      </div>

      {showSurprise && (
        <SurpriseModal
          quotation={surpriseQuote}
          loading={surpriseLoading}
          onClose={() => { setShowSurprise(false); setSurpriseQuote(null); }}
          onTryAgain={fetchSurprise}
        />
      )}
    </main>
  );
};
