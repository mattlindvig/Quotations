import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../../services/apiClient';
import { QuotationCard } from '../../components/quotations/QuotationCard';
import type { Quotation, PaginatedResponse, ApiResponse } from '../../types/quotation';
import './MySubmissionsPage.css';

/**
 * My Submissions page - displays user's submitted quotations
 */
export const MySubmissionsPage: React.FC = () => {
  const navigate = useNavigate();
  const [submissions, setSubmissions] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 20,
    totalCount: 0,
  });

  useEffect(() => {
    fetchSubmissions();
  }, [pagination.page]);

  const fetchSubmissions = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.get<ApiResponse<PaginatedResponse>>(
        `/api/v1/submissions/my?page=${pagination.page}&pageSize=${pagination.pageSize}`
      );

      if (response.data.success && response.data.data) {
        setSubmissions(response.data.data.items);
        setPagination((prev) => ({
          ...prev,
          totalCount: response.data.data!.pagination.totalCount,
        }));
      }
    } catch (err: any) {
      if (err.response?.status === 401) {
        // User not authenticated, redirect to login or show message
        setError('Please log in to view your submissions.');
      } else {
        setError('Failed to load submissions. Please try again later.');
      }
      console.error('Error fetching submissions:', err);
    } finally {
      setLoading(false);
    }
  };

  const handlePageChange = (newPage: number) => {
    setPagination((prev) => ({ ...prev, page: newPage }));
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const getStatusBadgeClass = (status: string) => {
    switch (status.toLowerCase()) {
      case 'approved':
        return 'status-badge status-approved';
      case 'pending':
        return 'status-badge status-pending';
      case 'rejected':
        return 'status-badge status-rejected';
      default:
        return 'status-badge';
    }
  };

  const totalPages = Math.ceil(pagination.totalCount / pagination.pageSize);

  if (loading && submissions.length === 0) {
    return (
      <div className="my-submissions-page">
        <div className="loading-container">
          <div className="spinner" role="status" aria-label="Loading submissions"></div>
          <p>Loading your submissions...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="my-submissions-page">
        <div className="error-container">
          <p className="error-message">{error}</p>
          <button onClick={() => navigate('/')} className="button-primary">
            Go to Home
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="my-submissions-page">
      <div className="submissions-header">
        <h1>My Submissions</h1>
        <p className="submissions-description">
          Track the status of your submitted quotations. Approved submissions will appear in the
          public quotation library.
        </p>
        <button onClick={() => navigate('/submit')} className="button-primary">
          Submit New Quotation
        </button>
      </div>

      {submissions.length === 0 ? (
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
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
          <h2>No Submissions Yet</h2>
          <p>You haven't submitted any quotations. Share your favorite quotes with the community!</p>
          <button onClick={() => navigate('/submit')} className="button-primary">
            Submit Your First Quotation
          </button>
        </div>
      ) : (
        <>
          <div className="submissions-list">
            {submissions.map((quotation) => (
              <div key={quotation.id} className="submission-item">
                <div className="submission-status-header">
                  <span className={getStatusBadgeClass(quotation.status)}>
                    {quotation.status.charAt(0).toUpperCase() + quotation.status.slice(1)}
                  </span>
                  <span className="submission-date">
                    Submitted {new Date(quotation.submittedAt).toLocaleDateString()}
                  </span>
                </div>
                <QuotationCard quotation={quotation} />
              </div>
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