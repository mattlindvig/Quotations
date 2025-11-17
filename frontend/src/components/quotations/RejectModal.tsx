import React, { useState } from 'react';
import './RejectModal.css';

interface RejectModalProps {
  onReject: (reason: string) => void;
  onCancel: () => void;
  isLoading: boolean;
}

/**
 * Reject Modal - modal dialog for rejecting a quotation with a reason
 */
export const RejectModal: React.FC<RejectModalProps> = ({ onReject, onCancel, isLoading }) => {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!reason.trim()) {
      setError('Rejection reason is required');
      return;
    }

    if (reason.trim().length < 10) {
      setError('Rejection reason must be at least 10 characters');
      return;
    }

    if (reason.length > 1000) {
      setError('Rejection reason must be 1000 characters or less');
      return;
    }

    onReject(reason.trim());
  };

  const handleReasonChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setReason(e.target.value);
    setError('');
  };

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Reject Quotation</h2>
          <button className="modal-close" onClick={onCancel} aria-label="Close modal">
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            <p className="modal-description">
              Please provide a clear reason for rejecting this quotation. This will help the
              submitter understand what needs to be improved.
            </p>

            <div className="form-group">
              <label htmlFor="rejectionReason" className="form-label">
                Rejection Reason *
              </label>
              <textarea
                id="rejectionReason"
                className={`form-textarea ${error ? 'error' : ''}`}
                value={reason}
                onChange={handleReasonChange}
                rows={5}
                placeholder="e.g., Incorrect attribution, duplicate submission, inappropriate content..."
                disabled={isLoading}
                required
              />
              <div className="character-count">
                {reason.length}/1000 characters {reason.trim().length > 0 && `(min 10)`}
              </div>
              {error && <span className="form-error">{error}</span>}
            </div>
          </div>

          <div className="modal-actions">
            <button
              type="button"
              onClick={onCancel}
              className="button-secondary"
              disabled={isLoading}
            >
              Cancel
            </button>
            <button type="submit" className="button-reject-confirm" disabled={isLoading}>
              {isLoading ? 'Rejecting...' : 'Reject Quotation'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};