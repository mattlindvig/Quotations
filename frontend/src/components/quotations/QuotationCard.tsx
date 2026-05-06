import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Quotation, AiScore } from '../../types/quotation';
import { useAuth } from '../../contexts/AuthContext';
import { useFavorites } from '../../contexts/FavoritesContext';
import './QuotationCard.css';

interface QuotationCardProps {
  quotation: Quotation;
}

export const QuotationCard: React.FC<QuotationCardProps> = ({ quotation }) => {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const { isFavorited, toggleFavorite } = useFavorites();
  const [copied, setCopied] = useState(false);
  const [linkCopied, setLinkCopied] = useState(false);
  const [showAiPanel, setShowAiPanel] = useState(false);

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
      // Fallback for browsers without clipboard API
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
            {quotation.source.type}
          </span>
          <span className="source-title">{quotation.source.title}</span>
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
              >
                {isFavorited(quotation.id) ? '♥ Saved' : '♡ Save'}
              </button>
            )}
            <button
              className="card-action-btn share-btn"
              onClick={handleShare}
              title="Copy link to this quote"
            >
              {linkCopied ? 'Link copied!' : 'Share'}
            </button>
            <button
              className="card-action-btn copy-btn"
              onClick={handleCopy}
              title="Copy quote to clipboard"
            >
              {copied ? 'Copied!' : 'Copy'}
            </button>
            {quotation.aiReview?.status === 'reviewed' && (
              <button
                className={`card-action-btn ai-eval-btn${showAiPanel ? ' active' : ''}`}
                onClick={() => setShowAiPanel(v => !v)}
                title="Show AI evaluation"
              >
                {showAiPanel ? 'Hide AI' : 'AI Eval'}
              </button>
            )}
          </div>
        </div>
      </div>

      {showAiPanel && quotation.aiReview && (
        <AiEvaluationPanel aiReview={quotation.aiReview} />
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

function AiEvaluationPanel({ aiReview }: { aiReview: NonNullable<Quotation['aiReview']> }) {
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
