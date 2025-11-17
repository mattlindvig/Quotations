import React from 'react';
import type { PaginationMetadata } from '../../types/quotation';
import './PaginationControls.css';

interface PaginationControlsProps {
  pagination: PaginationMetadata;
  onPageChange: (page: number) => void;
  onPrevious: () => void;
  onNext: () => void;
}

/**
 * Pagination controls component
 */
export const PaginationControls: React.FC<PaginationControlsProps> = ({
  pagination,
  onPageChange,
  onPrevious,
  onNext,
}) => {
  const { page, totalPages, hasPrevious, hasNext, totalCount, pageSize } = pagination;

  const startItem = (page - 1) * pageSize + 1;
  const endItem = Math.min(page * pageSize, totalCount);

  // Generate page numbers to display (show current, +/- 2 pages, and first/last)
  const getPageNumbers = (): (number | string)[] => {
    const pages: (number | string)[] = [];
    const maxPagesToShow = 7;

    if (totalPages <= maxPagesToShow) {
      // Show all pages if total is small
      for (let i = 1; i <= totalPages; i++) {
        pages.push(i);
      }
    } else {
      // Always show first page
      pages.push(1);

      // Calculate range around current page
      let start = Math.max(2, page - 1);
      let end = Math.min(totalPages - 1, page + 1);

      // Add ellipsis after first page if needed
      if (start > 2) {
        pages.push('...');
      }

      // Add pages around current
      for (let i = start; i <= end; i++) {
        pages.push(i);
      }

      // Add ellipsis before last page if needed
      if (end < totalPages - 1) {
        pages.push('...');
      }

      // Always show last page
      if (totalPages > 1) {
        pages.push(totalPages);
      }
    }

    return pages;
  };

  const pageNumbers = getPageNumbers();

  return (
    <nav className="pagination-controls" aria-label="Pagination">
      <div className="pagination-info" aria-live="polite">
        Showing {startItem}-{endItem} of {totalCount} quotations
      </div>

      <div className="pagination-buttons">
        <button
          className="pagination-button"
          onClick={onPrevious}
          disabled={!hasPrevious}
          aria-label="Go to previous page"
        >
          Previous
        </button>

        <div className="page-numbers" role="list">
          {pageNumbers.map((pageNum, index) =>
            typeof pageNum === 'number' ? (
              <button
                key={pageNum}
                className={`page-number ${pageNum === page ? 'active' : ''}`}
                onClick={() => onPageChange(pageNum)}
                aria-label={`Go to page ${pageNum}`}
                aria-current={pageNum === page ? 'page' : undefined}
                role="listitem"
              >
                {pageNum}
              </button>
            ) : (
              <span key={`ellipsis-${index}`} className="page-ellipsis" role="listitem">
                {pageNum}
              </span>
            )
          )}
        </div>

        <button
          className="pagination-button"
          onClick={onNext}
          disabled={!hasNext}
          aria-label="Go to next page"
        >
          Next
        </button>
      </div>
    </nav>
  );
};
