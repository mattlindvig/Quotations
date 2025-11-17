import React, { useEffect, useState } from 'react';
import { apiClient } from '../../services/apiClient';
import type { Quotation, ApiResponse } from '../../types/quotation';
import './DuplicateChecker.css';

interface DuplicateCheckerProps {
  quotationId: string;
}

/**
 * Duplicate Checker - displays potential duplicate quotations
 */
export const DuplicateChecker: React.FC<DuplicateCheckerProps> = ({ quotationId }) => {
  const [duplicates, setDuplicates] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchDuplicates();
  }, [quotationId]);

  const fetchDuplicates = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.get<ApiResponse<Quotation[]>>(
        `/api/v1/review/${quotationId}/duplicates`
      );

      if (response.data.success && response.data.data) {
        setDuplicates(response.data.data);
      }
    } catch (err: any) {
      console.error('Error fetching duplicates:', err);
      setError('Failed to check for duplicates');
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="duplicate-checker">
        <div className="duplicate-loading">
          <div className="spinner-small"></div>
          <span>Checking for duplicates...</span>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="duplicate-checker">
        <div className="duplicate-error">{error}</div>
      </div>
    );
  }

  if (duplicates.length === 0) {
    return (
      <div className="duplicate-checker">
        <div className="no-duplicates">
          <svg
            className="check-icon"
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
          <span>No duplicates found</span>
        </div>
      </div>
    );
  }

  return (
    <div className="duplicate-checker">
      <div className="duplicate-warning">
        <svg
          className="warning-icon"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
          />
        </svg>
        <strong>Potential Duplicates Found ({duplicates.length})</strong>
      </div>

      <div className="duplicates-list">
        {duplicates.map((duplicate) => (
          <div key={duplicate.id} className="duplicate-item">
            <div className="duplicate-text">"{duplicate.text}"</div>
            <div className="duplicate-meta">
              <span className="duplicate-status status-{duplicate.status}">
                {duplicate.status.charAt(0).toUpperCase() + duplicate.status.slice(1)}
              </span>
              <span className="duplicate-date">
                Submitted {new Date(duplicate.submittedAt).toLocaleDateString()}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};