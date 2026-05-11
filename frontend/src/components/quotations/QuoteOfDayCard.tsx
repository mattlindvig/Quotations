import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getQuoteOfTheDay } from '../../services/quotationService';
import type { Quotation } from '../../types/quotation';
import './QuoteOfDayCard.css';

export const QuoteOfDayCard: React.FC = () => {
  const [quote, setQuote] = useState<Quotation | null>(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    getQuoteOfTheDay()
      .then(setQuote)
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return <div className="qotd-skeleton" aria-label="Loading quote of the day" />;
  }

  if (!quote) return null;

  return (
    <div
      className="qotd-card"
      onClick={() => navigate(`/quote/${quote.id}`)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && navigate(`/quote/${quote.id}`)}
      title="View full quote"
    >
      <div className="qotd-label">✦ Quote of the Day</div>
      <blockquote className="qotd-text">"{quote.text}"</blockquote>
      <div className="qotd-meta">
        <span className="qotd-author">— {quote.author.name}</span>
        {quote.source.title && (
          <span className="qotd-source">{quote.source.title}</span>
        )}
      </div>
    </div>
  );
};
