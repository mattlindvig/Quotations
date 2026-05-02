import React, { useState } from 'react';
import { apiClient } from '../../services/apiClient';
import { DuplicateChecker } from './DuplicateChecker';
import { RejectModal } from './RejectModal';
import type { Quotation, AiReview, AiScore, ApiResponse } from '../../types/quotation';
import './ReviewCard.css';

interface ReviewCardProps {
  quotation: Quotation;
  onApprove: (id: string) => void;
  onReject: (id: string) => void;
}

function ScoreBar({ score }: { score: number }) {
  const pct = Math.round((score / 10) * 100);
  const color = score >= 7 ? '#22c55e' : score >= 4 ? '#f59e0b' : '#ef4444';
  return (
    <div className="ai-score-bar-wrap">
      <div className="ai-score-bar" style={{ width: `${pct}%`, background: color }} />
      <span className="ai-score-label">{score}/10</span>
    </div>
  );
}

function ScoreRow({ label, score }: { label: string; score: AiScore }) {
  return (
    <div className="ai-score-row">
      <div className="ai-score-header">
        <span className="ai-score-dimension">{label}</span>
        <ScoreBar score={score.score} />
      </div>
      <p className="ai-score-reasoning">{score.reasoning}</p>
      {score.suggestedValue && (
        <div className="ai-suggestion">
          <span className="ai-suggestion-label">{score.wasAiFilled ? 'AI identified:' : 'Suggested fix:'}</span>
          <span className="ai-suggestion-value">{score.suggestedValue}</span>
        </div>
      )}
      {score.citations && score.citations.length > 0 && (
        <ul className="ai-citations">
          {score.citations.map((c, i) => (
            <li key={i} className="ai-citation">{c}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

function AiReviewPanel({ aiReview }: { aiReview: AiReview }) {
  const [expanded, setExpanded] = useState(false);

  const statusConfig: Record<string, { label: string; className: string }> = {
    notreviewed: { label: 'Not Reviewed', className: 'ai-status-not-reviewed' },
    pending: { label: 'Pending', className: 'ai-status-pending' },
    inprogress: { label: 'In Progress', className: 'ai-status-in-progress' },
    reviewed: { label: 'Reviewed', className: 'ai-status-reviewed' },
    failed: { label: 'Failed', className: 'ai-status-failed' },
  };

  const { label, className } = statusConfig[aiReview.status] ?? { label: aiReview.status, className: '' };
  const hasScores = aiReview.status === 'reviewed' && aiReview.quoteAccuracy;

  return (
    <div className="ai-review-panel">
      <div className="ai-review-header" onClick={() => hasScores && setExpanded(!expanded)}>
        <div className="ai-review-title">
          <svg className="ai-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10" />
            <path d="M12 8v4l3 3" strokeLinecap="round" />
          </svg>
          <span>AI Analysis</span>
          <span className={`ai-status-badge ${className}`}>{label}</span>
        </div>
        {hasScores && (
          <button className="ai-expand-toggle" aria-expanded={expanded}>
            {expanded ? '▲ Hide' : '▼ Show'}
          </button>
        )}
      </div>

      {aiReview.modelUsed && (
        <div className="ai-meta">
          Model: {aiReview.modelUsed}
          {aiReview.reviewedAt && (
            <span> · {new Date(aiReview.reviewedAt).toLocaleString()}</span>
          )}
        </div>
      )}

      {hasScores && expanded && (
        <div className="ai-scores">
          {aiReview.summary && <p className="ai-summary">{aiReview.summary}</p>}
          {aiReview.quoteAccuracy && <ScoreRow label="Quote Accuracy" score={aiReview.quoteAccuracy} />}
          {aiReview.attributionAccuracy && <ScoreRow label="Attribution" score={aiReview.attributionAccuracy} />}
          {aiReview.sourceAccuracy && <ScoreRow label="Source" score={aiReview.sourceAccuracy} />}
          {aiReview.suggestedTags && aiReview.suggestedTags.length > 0 && (
            <div className="ai-suggested-tags">
              <span className="ai-suggested-tags-label">AI suggested tags:</span>
              <div className="ai-tags-list">
                {aiReview.suggestedTags.map((tag) => (
                  <span key={tag} className="ai-tag">{tag}</span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export const ReviewCard: React.FC<ReviewCardProps> = ({ quotation, onApprove, onReject }) => {
  const [showDuplicates, setShowDuplicates] = useState(false);
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleApprove = async () => {
    if (!confirm('Are you sure you want to approve this quotation?')) return;

    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.post<ApiResponse<any>>(
        `/review/${quotation.id}/approve`,
        {}
      );

      if (response.success) {
        onApprove(quotation.id);
      }
    } catch (err: any) {
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
        `/review/${quotation.id}/reject`,
        { rejectionReason: reason }
      );

      if (response.success) {
        setShowRejectModal(false);
        onReject(quotation.id);
      }
    } catch (err: any) {
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
                  <span key={tag} className="tag">{tag}</span>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {quotation.aiReview && <AiReviewPanel aiReview={quotation.aiReview} />}

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
