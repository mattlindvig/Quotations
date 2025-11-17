import React from 'react';
import type { Quotation } from '../../types/quotation';
import './QuotationCard.css';

interface QuotationCardProps {
  quotation: Quotation;
}

/**
 * Card component for displaying a single quotation with metadata
 */
export const QuotationCard: React.FC<QuotationCardProps> = ({ quotation }) => {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
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
          <span className="author-name">{quotation.author.name}</span>
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
              <span key={tag} className="tag" role="listitem">
                {tag}
              </span>
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
        </div>
      </div>
    </article>
  );
};
