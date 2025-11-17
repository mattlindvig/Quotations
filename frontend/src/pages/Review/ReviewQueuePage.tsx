import React, { useEffect, useState } from 'react';
import { apiClient } from '../../services/apiClient';
import { ReviewCard } from '../../components/quotations/ReviewCard';
import type { Quotation, PaginatedResponse, ApiResponse } from '../../types/quotation';
import './ReviewQueuePage.css';

/**
 * Review Queue page - displays pending quotations for reviewers
 */
export const ReviewQueuePage: React.FC = () => {
  const [quotations, setQuotations] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 20,
    totalCount: 0,
  });

  useEffect(() => {
    fetchPendingQuotations();
  }, [pagination.page]);

  const fetchPendingQuotations = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.get<ApiResponse<PaginatedResponse>>(
        `/api/v1/review/pending?page=${pagination.page}&pageSize=${pagination.pageSize}`
      );

      if (response.data.success && response.data.data) {
        setQuotations(response.data.data.items);
        setPagination((prev) => ({
          ...prev,
          totalCount: response.data.data!.pagination.totalCount,
        }));
      }
    } catch (err: any) {
      if (err.response?.status === 401 || err.response?.status === 403) {
        setError('You do not have permission to access the review queue.');
      } else {
        setError('Failed to load pending quotations. Please try again later.');
      }
      console.error('Error fetching pending quotations:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (id: string) => {
    // Remove from local state optimistically
    setQuotations((prev) => prev.filter((q) => q.id !== id));
    await fetchPendingQuotations(); // Refresh to ensure accuracy
  };

  const handleReject = async (id: string) => {
    // Remove from local state optimistically
    setQuotations((prev) => prev.filter((q) => q.id !== id));
    await fetchPendingQuotations(); // Refresh to ensure accuracy
  };

  const handlePageChange = (newPage: number) => {
    setPagination((prev) => ({ ...prev, page: newPage }));
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const totalPages = Math.ceil(pagination.totalCount / pagination.pageSize);

  if (loading && quotations.length === 0) {
    return (
      <div className="review-queue-page">
        <div className="loading-container">
          <div className="spinner" role="status" aria-label="Loading review queue"></div>
          <p>Loading review queue...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="review-queue-page">
        <div className="error-container">
          <p className="error-message">{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="review-queue-page">
      <div className="review-header">
        <h1>Review Queue</h1>
        <p className="review-description">
          Review pending quotation submissions. Check for accuracy, appropriateness, and potential
          duplicates before approving or rejecting.
        </p>
        <div className="queue-stats">
          <span className="stat">
            <strong>{pagination.totalCount}</strong> pending submission{pagination.totalCount !== 1 ? 's' : ''}
          </span>
        </div>
      </div>

      {quotations.length === 0 ? (
        <div className="empty-state">
          <svg
            className="empty-icon"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <h2>No Pending Submissions</h2>
          <p>All quotations have been reviewed. Check back later for new submissions.</p>
        </div>
      ) : (
        <>
          <div className="review-list">
            {quotations.map((quotation) => (
              <ReviewCard
                key={quotation.id}
                quotation={quotation}
                onApprove={handleApprove}
                onReject={handleReject}
              />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="pagination">
              <button
                className="pagination-button"
                onClick={() => handlePageChange(pagination.page - 1)}
                disabled={pagination.page === 1 || loading}
              >
                Previous
              </button>
              <span className="pagination-info">
                Page {pagination.page} of {totalPages}
              </span>
              <button
                className="pagination-button"
                onClick={() => handlePageChange(pagination.page + 1)}
                disabled={pagination.page >= totalPages || loading}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
};