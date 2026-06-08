import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { QuotationSummary, AiReview, AiScore, ApiResponse, Quotation } from '../../types/quotation';
import { useAuth } from '../../contexts/AuthContext';
import { useFavorites } from '../../contexts/FavoritesContext';
import { apiClient } from '../../services/apiClient';
import './QuotationCard.css';

interface QuotationCardProps {
  quotation: QuotationSummary;
}

const SOURCE_TYPE_LABELS: Record<string, string> = {
  Book: 'Book',
  Movie: 'Film',
  Television: 'TV',
  Speech: 'Speech',
  Interview: 'Interview',
  Poem: 'Poem',
  Organization: 'Organization',
  Other: 'Other',
};

export const QuotationCard: React.FC<QuotationCardProps> = ({ quotation }) => {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { isFavorited, toggleFavorite } = useFavorites();
  const [copied, setCopied] = useState(false);
  const [linkCopied, setLinkCopied] = useState(false);
  const [showAiPanel, setShowAiPanel] = useState(false);
  const [fullAiReview, setFullAiReview] = useState<AiReview | null>(null);
  const [aiLoading, setAiLoading] = useState(false);

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  const handleAuthorClick = () => {
    navigate(`/browse?authorName=${encodeURIComponent(quotation.author.name)}`);
  };

  const handleSourceClick = () => {
    navigate(`/browse?sourceTitle=${encodeURIComponent(quotation.source.title)}`);
  };

  const handleTagClick = (tag: string) => {
    navigate(`/browse?tags=${encodeURIComponent(tag)}`);
  };

  const handleShare = async () => {
    const url = `${window.location.origin}/quote/${quotation.id}`;
    try {
      await navigator.clipboard.writeText(url);
    } catch {
      const textarea = document.createElement('textarea');
      textarea.value = url;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
    }
    setLinkCopied(true);
    setTimeout(() => setLinkCopied(false), 2000);
  };

  const handleCopy = async () => {
    const text = `"${quotation.text}" — ${quotation.author.name}`;
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      const textarea = document.createElement('textarea');
      textarea.value = text;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleToggleAiPanel = async () => {
    const next = !showAiPanel;
    setShowAiPanel(next);

    // Fetch full AI detail on first open (list response only has scores, not reasoning)
    if (next && !fullAiReview) {
      setAiLoading(true);
      try {
        const res = await apiClient.get<ApiResponse<Quotation>>(`/quotations/${quotation.id}`);
        if (res.data?.aiReview) {
          setFullAiReview(res.data.aiReview);
        }
      } catch {
        // Silently fall back — panel will show whatever data is in the summary
      } finally {
        setAiLoading(false);
      }
    }
  };

  return (
    <article
      className="quotation-card"
      aria-label={`Quotation by ${quotation.author.name}`}
    >
      <blockquote className="quotation-text">
        <p>"{quotation.text}"</p>
      </blockquote>

      <div className="quotation-metadata">
        <div className="quotation-author">
          <button
            className="author-name author-link"
            onClick={handleAuthorClick}
            title={`Browse quotes by ${quotation.author.name}`}
          >
            {quotation.author.name}
          </button>
          {quotation.author.lifespan && (
            <span className="author-lifespan" aria-label="Author lifespan">
              ({quotation.author.lifespan})
            </span>
          )}
          {quotation.author.occupation && (
            <span className="author-occupation">{quotation.author.occupation}</span>
          )}
        </div>

        <div className="quotation-source">
          <span className="source-type" aria-label="Source type">
            {SOURCE_TYPE_LABELS[quotation.source.type] ?? quotation.source.type}
          </span>
          {quotation.source.title && (
            <>
              <span className="source-from">from</span>
              <button
                className="source-title source-link"
                onClick={handleSourceClick}
                title={`Browse quotes from ${quotation.source.title}`}
              >
                {quotation.source.title}
              </button>
            </>
          )}
          {quotation.source.year && (
            <span className="source-year" aria-label="Publication year">
              ({quotation.source.year})
            </span>
          )}
        </div>

        {quotation.tags.length > 0 && (
          <div className="quotation-tags" role="list" aria-label="Tags">
            {quotation.tags.map((tag) => (
              <button
                key={tag}
                className="tag tag-link"
                role="listitem"
                onClick={() => handleTagClick(tag)}
                title={`Browse ${tag} quotes`}
              >
                {tag}
              </button>
            ))}
          </div>
        )}

        <div className="quotation-footer">
          <time
            className="submitted-date"
            dateTime={quotation.submittedAt}
            aria-label="Submitted on"
          >
            Submitted: {formatDate(quotation.submittedAt)}
          </time>

          <div className="card-actions">
            {isAuthenticated && (
              <button
                className={`card-action-btn favorite-btn${isFavorited(quotation.id) ? ' favorited' : ''}`}
                onClick={() => toggleFavorite(quotation.id)}
                title={isFavorited(quotation.id) ? 'Remove from favorites' : 'Add to favorites'}
                aria-pressed={isFavorited(quotation.id)}
                aria-label={isFavorited(quotation.id) ? 'Remove from favorites' : 'Add to favorites'}
              >
                <svg viewBox="0 0 24 24" fill={isFavorited(quotation.id) ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                </svg>
              </button>
            )}
            <button
              className={`card-action-btn share-btn${linkCopied ? ' copied' : ''}`}
              onClick={handleShare}
              title={linkCopied ? 'Link copied!' : 'Copy link'}
              aria-label="Copy link to this quote"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                {linkCopied
                  ? <polyline points="20 6 9 17 4 12" />
                  : <><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" /><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" /></>
                }
              </svg>
            </button>
            <button
              className={`card-action-btn copy-btn${copied ? ' copied' : ''}`}
              onClick={handleCopy}
              title={copied ? 'Copied!' : 'Copy quote text'}
              aria-label="Copy quote text"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                {copied
                  ? <polyline points="20 6 9 17 4 12" />
                  : <><rect x="9" y="9" width="13" height="13" rx="2" ry="2" /><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" /></>
                }
              </svg>
            </button>
            {quotation.aiReview?.status === 'reviewed' && (
              <button
                className={`card-action-btn ai-eval-btn${showAiPanel ? ' active' : ''}`}
                onClick={handleToggleAiPanel}
                title={showAiPanel ? 'Hide AI evaluation' : 'Show AI evaluation'}
                aria-label="Toggle AI evaluation"
                aria-pressed={showAiPanel}
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M12 2l1.5 4.5L18 8l-4.5 1.5L12 14l-1.5-4.5L6 8l4.5-1.5z" />
                  <path d="M19 14l.75 2.25L22 17l-2.25.75L19 20l-.75-2.25L16 17l2.25-.75z" />
                </svg>
              </button>
            )}
          </div>
        </div>
      </div>

      {showAiPanel && (
        aiLoading
          ? <div className="ai-eval-panel ai-eval-loading">Loading AI evaluation…</div>
          : fullAiReview
            ? <AiEvaluationPanel aiReview={fullAiReview} />
            : null
      )}
    </article>
  );
};

function ScorePill({ score }: { score: number }) {
  const color = score >= 8 ? '#198754' : score >= 5 ? '#fd7e14' : '#dc3545';
  return <span style={{ fontWeight: 700, color }}>{score}/10</span>;
}

function ScoreBlock({ label, data }: { label: string; data: AiScore | null | undefined }) {
  if (!data) return null;
  return (
    <div className="ai-score-block">
      <div className="ai-score-header">
        <span className="ai-score-label">{label}</span>
        <ScorePill score={data.score} />
      </div>
      {data.reasoning && <p className="ai-score-reasoning">{data.reasoning}</p>}
      {data.suggestedValue && (
        <div className="ai-suggestion">
          <span className="ai-suggestion-label">Suggestion: </span>
          {data.suggestedValue}
        </div>
      )}
    </div>
  );
}

function AiEvaluationPanel({ aiReview }: { aiReview: AiReview }) {
  return (
    <div className="ai-eval-panel">
      <div className="ai-eval-header">
        <span className="ai-eval-title">AI Evaluation</span>
        {aiReview.modelUsed && (
          <span className="ai-eval-model">{aiReview.modelUsed}</span>
        )}
      </div>

      {aiReview.summary && (
        <p className="ai-eval-summary">{aiReview.summary}</p>
      )}

      {(aiReview.isLikelyAuthentic !== null && aiReview.isLikelyAuthentic !== undefined) && (
        <div className="ai-authenticity">
          <span className={`ai-authenticity-badge ${aiReview.isLikelyAuthentic ? 'authentic' : 'misattributed'}`}>
            {aiReview.isLikelyAuthentic ? 'Likely Authentic' : 'Possibly Misattributed'}
          </span>
          {aiReview.approximateEra && (
            <span className="ai-era">{aiReview.approximateEra}</span>
          )}
        </div>
      )}

      {aiReview.authenticityReasoning && (
        <p className="ai-authenticity-reasoning">{aiReview.authenticityReasoning}</p>
      )}

      <div className="ai-scores">
        <ScoreBlock label="Quote Accuracy"  data={aiReview.quoteAccuracy} />
        <ScoreBlock label="Attribution"     data={aiReview.attributionAccuracy} />
        <ScoreBlock label="Source"          data={aiReview.sourceAccuracy} />
      </div>

      {aiReview.knownVariants && aiReview.knownVariants.length > 0 && (
        <div className="ai-variants">
          <div className="ai-variants-label">Known Variants</div>
          <ul className="ai-variants-list">
            {aiReview.knownVariants.map((v, i) => (
              <li key={i}>"{v}"</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
