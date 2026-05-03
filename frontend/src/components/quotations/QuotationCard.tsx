import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Quotation } from '../../types/quotation';
import './QuotationCard.css';

interface QuotationCardProps {
  quotation: Quotation;
}

export const QuotationCard: React.FC<QuotationCardProps> = ({ quotation }) => {
  const navigate = useNavigate();
  const [copied, setCopied] = useState(false);
  const [linkCopied, setLinkCopied] = useState(false);

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
          </div>
        </div>
      </div>
    </article>
  );
};
