import React, { useEffect, useState, useCallback, useRef } from 'react';
import { useSearchParams, useLocation } from 'react-router-dom';
import { useQuotations } from '../../hooks/useQuotations';
import { useSearch } from '../../hooks/useSearch';
import { useFilters, type QuotationFilters } from '../../hooks/useFilters';
import { QuotationList } from '../../components/quotations/QuotationList';
import { SearchBar, type SearchBarHandle } from '../../components/quotations/SearchBar';
import { FilterPanel } from '../../components/quotations/FilterPanel';
import { QuoteOfDayCard } from '../../components/quotations/QuoteOfDayCard';
import type { SourceType, QuotationSortBy } from '../../types/quotation';
import './BrowsePage.css';

export const BrowsePage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const location = useLocation();
  const [activeMode, setActiveMode] = useState<'browse' | 'search'>('browse');
  const [sortBy, setSortBy] = useState<QuotationSortBy>(
    (searchParams.get('sortBy') as QuotationSortBy | null) || 'newest'
  );

  const initialQuery = searchParams.get('q') || '';
  const initialPage = parseInt(searchParams.get('page') || '1');
  const initialAuthorName = searchParams.get('authorName') || undefined;
  const initialSourceType = searchParams.get('sourceType') as SourceType | undefined;
  const initialSourceTitle = searchParams.get('sourceTitle') || undefined;
  const initialTags = searchParams.get('tags')?.split(',').filter(Boolean) || undefined;

  const initialFilters: QuotationFilters = {
    authorName: initialAuthorName,
    sourceType: initialSourceType,
    sourceTitle: initialSourceTitle,
    tags: initialTags,
  };

  const { filters, setFilters, clearFilters } = useFilters(initialFilters);
  const {
    quotations: browseQuotations,
    loading: browseLoading,
    error: browseError,
    pagination: browsePagination,
    fetchQuotations,
    nextPage: browseNextPage,
  } = useQuotations({ status: 'approved', page: initialPage, pageSize: 20, sortBy, ...filters });

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

  const loadingRef = useRef(loading);
  const paginationRef = useRef(pagination);
  loadingRef.current = loading;
  paginationRef.current = pagination;

  const handleNextPage = useCallback(() => {
    if (activeMode === 'browse') browseNextPage();
    else if (paginationRef.current?.hasNext)
      searchPage((paginationRef.current.page ?? 1) + 1);
  }, [activeMode, browseNextPage, searchPage]);

  const sentinelRef = useRef<HTMLDivElement>(null);
  const searchBarRef = useRef<SearchBarHandle>(null);
  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel) return;
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !loadingRef.current && paginationRef.current?.hasNext) {
          handleNextPage();
        }
      },
      { rootMargin: '400px' }
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [handleNextPage]);

  useEffect(() => {
    if (initialQuery) {
      performSearch(initialQuery, initialPage);
      setActiveMode('search');
    }
  }, []);

  useEffect(() => {
    if (!(location.state as any)?.resetFilters) return;
    clearFilters();
    setSortBy('newest');
    setActiveMode('browse');
    clearSearch();
    setSearchParams({}, { replace: true });
    fetchQuotations({ status: 'approved', page: 1, pageSize: 20, sortBy: 'newest' });
  }, [(location.state as any)?.resetFilters]);

  const updateUrl = useCallback(
    (query: string, newFilters: QuotationFilters, newSortBy: QuotationSortBy) => {
      const params: Record<string, string> = {};
      if (query) params.q = query;
      if (newFilters.authorName) params.authorName = newFilters.authorName;
      if (newFilters.sourceType) params.sourceType = newFilters.sourceType;
      if (newFilters.sourceTitle) params.sourceTitle = newFilters.sourceTitle;
      if (newFilters.tags?.length) params.tags = newFilters.tags.join(',');
      if (newSortBy !== 'newest') params.sortBy = newSortBy;
      setSearchParams(params, { replace: true });
    },
    [setSearchParams]
  );

  const handleSearch = useCallback(
    (query: string) => {
      if (query.trim()) {
        performSearch(query, 1, 20, {
          authorName: filters.authorName,
          sourceType: filters.sourceType,
          tags: filters.tags,
        });
        setActiveMode('search');
        updateUrl(query, filters, sortBy);
      } else {
        clearSearch();
        setActiveMode('browse');
        fetchQuotations({ ...filters, page: 1 });
        updateUrl('', filters, sortBy);
      }
    },
    [performSearch, clearSearch, fetchQuotations, filters, sortBy, updateUrl]
  );

  const handleFilterChange = useCallback(
    (newFilters: QuotationFilters) => {
      setFilters(newFilters);
      updateUrl(activeMode === 'search' ? searchQuery : '', newFilters, sortBy);
      if (activeMode === 'search' && searchQuery) {
        performSearch(searchQuery, 1, 20, {
          authorName: newFilters.authorName,
          sourceType: newFilters.sourceType,
          tags: newFilters.tags,
        });
      } else if (activeMode === 'browse') {
        fetchQuotations({
          status: 'approved',
          page: 1,
          pageSize: 20,
          authorName: undefined,
          sourceType: undefined,
          tags: undefined,
          ...newFilters,
          sortBy,
        });
      }
    },
    [setFilters, fetchQuotations, activeMode, searchQuery, sortBy, updateUrl, performSearch]
  );

  const handleSortChange = useCallback(
    (newSort: QuotationSortBy) => {
      setSortBy(newSort);
      updateUrl(activeMode === 'search' ? searchQuery : '', filters, newSort);
      if (activeMode === 'browse') {
        fetchQuotations({ status: 'approved', page: 1, pageSize: 20, ...filters, sortBy: newSort });
      }
    },
    [activeMode, filters, searchQuery, fetchQuotations, updateUrl]
  );

  return (
    <main className="browse-page" id="main-content">
      <a href="#main-content" className="skip-link">Skip to main content</a>

      <div className="browse-layout">
        {/* Left sidebar — filters + quote of the day */}
        <aside className="browse-sidebar">
          <FilterPanel
            onFilterChange={handleFilterChange}
            initialFilters={initialFilters}
            activeSearch={activeMode === 'search' ? searchQuery : undefined}
            onClearSearch={() => {
              clearSearch();
              setActiveMode('browse');
              fetchQuotations({ status: 'approved', page: 1, pageSize: 20, ...filters, sortBy });
              updateUrl('', filters, sortBy);
              searchBarRef.current?.clear();
            }}
          />
          <QuoteOfDayCard />
        </aside>

        {/* Main content */}
        <div className="browse-main">
          <div className="browse-toolbar">
            <SearchBar ref={searchBarRef} onSearch={handleSearch} initialValue={initialQuery} />
          </div>

          <div className="sort-bar">
            {!loading && hasResults && pagination && (
              <p className="result-count">
                {activeMode === 'search'
                  ? <>Results for: <strong>"{searchQuery}"</strong> — {pagination.totalCount.toLocaleString()} found</>
                  : <>{pagination.totalCount.toLocaleString()} {pagination.totalCount === 1 ? 'quotation' : 'quotations'}</>
                }
              </p>
            )}
            <select
              className="sort-select"
              value={sortBy}
              onChange={(e) => handleSortChange(e.target.value as QuotationSortBy)}
              aria-label="Sort quotations"
            >
              <option value="newest">Newest first</option>
              <option value="oldest">Oldest first</option>
              <option value="author">Author (A–Z)</option>
            </select>
          </div>

          {!loading && !hasResults && !error && (
            <div className="empty-state" role="status">
              <p>No quotations found matching your criteria.</p>
              {(searchQuery || filters.authorName || filters.sourceType || filters.tags?.length) && (
                <button
                  className="clear-all-button"
                  onClick={() => {
                    clearSearch();
                    setFilters({});
                    setActiveMode('browse');
                    fetchQuotations({ page: 1 });
                    setSearchParams({}, { replace: true });
                    searchBarRef.current?.clear();
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

          {!error && (
            <>
              <QuotationList quotations={quotations} loading={loading && !hasResults} />

              {loading && hasResults && (
                <div className="load-more-spinner" aria-label="Loading more quotations">
                  <div className="loading-spinner" />
                </div>
              )}

              <div ref={sentinelRef} style={{ height: 1 }} />

              {!loading && pagination && !pagination.hasNext && hasResults && (
                <p className="end-of-results">All {pagination.totalCount.toLocaleString()} quotations loaded</p>
              )}
            </>
          )}
        </div>

      </div>
    </main>
  );
};
