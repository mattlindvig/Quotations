import React, { useState, useEffect } from 'react';
import { apiClient } from '../../services/apiClient';
import { DuplicateChecker } from './DuplicateChecker';
import { RejectModal } from './RejectModal';
import type { Quotation, ApiResponse } from '../../types/quotation';
import './ReviewCard.css';

interface ReviewCardProps {
  quotation: Quotation;
  onApprove: (id: string) => void;
  onReject: (id: string) => void;
}

/**
 * Review Card - displays a quotation pending review with action buttons
 */
export const ReviewCard: React.FC<ReviewCardProps> = ({ quotation, onApprove, onReject }) => {
  const [showDuplicates, setShowDuplicates] = useState(false);
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleApprove = async () => {
    if (!confirm('Are you sure you want to approve this quotation?')) {
      return;
    }

    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.post<ApiResponse<any>>(
        `/api/v1/review/${quotation.id}/approve`,
        {}
      );

      if (response.data.success) {
        onApprove(quotation.id);
      }
    } catch (err: any) {
      console.error('Error approving quotation:', err);
      setError(err.response?.data?.errors?.general?.[0] || 'Failed to approve quotation');
    } finally {
      setLoading(false);
    }
  };

  const handleReject = async (reason: string) => {
    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.post<ApiResponse<any>>(
        `/api/v1/review/${quotation.id}/reject`,
        {
          rejectionReason: reason,
        }
      );

      if (response.data.success) {
        setShowRejectModal(false);
        onReject(quotation.id);
      }
    } catch (err: any) {
      console.error('Error rejecting quotation:', err);
      setError(err.response?.data?.errors?.general?.[0] || 'Failed to reject quotation');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="review-card">
      <div className="review-card-header">
        <div className="submission-info">
          <span className="submission-date">
            Submitted {new Date(quotation.submittedAt).toLocaleDateString()}
          </span>
          {quotation.submittedBy && (
            <span className="submitter">by {quotation.submittedBy.username}</span>
          )}
        </div>
        <button
          className="duplicate-toggle"
          onClick={() => setShowDuplicates(!showDuplicates)}
          aria-expanded={showDuplicates}
        >
          {showDuplicates ? 'Hide' : 'Check'} Duplicates
        </button>
      </div>

      {showDuplicates && <DuplicateChecker quotationId={quotation.id} />}

      <div className="review-card-content">
        <blockquote className="quotation-text">"{quotation.text}"</blockquote>

        <div className="quotation-metadata">
          <div className="metadata-item">
            <span className="metadata-label">Author:</span>
            <span className="metadata-value">{quotation.author.name}</span>
          </div>
          <div className="metadata-item">
            <span className="metadata-label">Source:</span>
            <span className="metadata-value">
              {quotation.source.title} ({quotation.source.type})
            </span>
          </div>
          {quotation.tags && quotation.tags.length > 0 && (
            <div className="metadata-item">
              <span className="metadata-label">Tags:</span>
              <div className="tags-list">
                {quotation.tags.map((tag) => (
                  <span key={tag} className="tag">
                    {tag}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {error && <div className="review-error">{error}</div>}

      <div className="review-actions">
        <button
          className="button-reject"
          onClick={() => setShowRejectModal(true)}
          disabled={loading}
        >
          Reject
        </button>
        <button className="button-approve" onClick={handleApprove} disabled={loading}>
          {loading ? 'Processing...' : 'Approve'}
        </button>
      </div>

      {showRejectModal && (
        <RejectModal
          onReject={handleReject}
          onCancel={() => setShowRejectModal(false)}
          isLoading={loading}
        />
      )}
    </div>
  );
};