import React, { useState, useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getRandomBatch } from '../../services/quotationService';
import type { Quotation, SourceType } from '../../types/quotation';
import './RandomPage.css';

const SOURCE_FILTERS: { value: SourceType | ''; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'book', label: 'Books' },
  { value: 'movie', label: 'Movies' },
  { value: 'speech', label: 'Speeches' },
  { value: 'interview', label: 'Interviews' },
  { value: 'other', label: 'Other' },
];

const QUICK_TAGS = [
  'humor', 'wisdom', 'motivation', 'love', 'life',
  'success', 'philosophy', 'history', 'science', 'art',
];

export const RandomPage: React.FC = () => {
  const [quotes, setQuotes] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(false);
  const [sourceType, setSourceType] = useState<SourceType | ''>('');
  const [activeTags, setActiveTags] = useState<string[]>([]);
  const navigate = useNavigate();

  const fetchRandom = useCallback(async () => {
    setLoading(true);
    try {
      const results = await getRandomBatch(
        12,
        sourceType || undefined,
        activeTags.length > 0 ? activeTags : undefined
      );
      setQuotes(results);
    } catch {
      setQuotes([]);
    } finally {
      setLoading(false);
    }
  }, [sourceType, activeTags]);

  // Fetch whenever filters change
  useEffect(() => {
    fetchRandom();
  }, [fetchRandom]);

  const toggleTag = useCallback((tag: string) => {
    setActiveTags((prev) =>
      prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
    );
  }, []);

  return (
    <div className="random-page">
      <div className="random-header">
        <h1 className="random-title">Random Quotes</h1>
        <p className="random-subtitle">Discover something new every time.</p>
      </div>

      <div className="random-controls">
        <div className="random-filter-section">
          <span className="random-filter-label">Source</span>
          <div className="source-chips">
            {SOURCE_FILTERS.map((sf) => (
              <button
                key={sf.value}
                className={`source-chip${sourceType === sf.value ? ' source-chip--active' : ''}`}
                onClick={() => setSourceType(sf.value)}
              >
                {sf.label}
              </button>
            ))}
          </div>
        </div>

        <div className="random-filter-section">
          <span className="random-filter-label">Tags</span>
          <div className="tag-chips">
            {QUICK_TAGS.map((tag) => (
              <button
                key={tag}
                className={`tag-chip${activeTags.includes(tag) ? ' tag-chip--active' : ''}`}
                onClick={() => toggleTag(tag)}
              >
                #{tag}
              </button>
            ))}
          </div>
        </div>

        <button
          className="shuffle-btn"
          onClick={fetchRandom}
          disabled={loading}
          aria-label="Shuffle quotes"
        >
          {loading ? 'Shuffling…' : '🔀 Shuffle'}
        </button>
      </div>

      {loading ? (
        <div className="random-grid">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="random-card-skeleton" />
          ))}
        </div>
      ) : quotes.length === 0 ? (
        <div className="random-empty">
          <p>No quotes found with those filters.</p>
          <button className="shuffle-btn" onClick={fetchRandom}>Try Again</button>
        </div>
      ) : (
        <div className="random-grid">
          {quotes.map((quote) => (
            <div
              key={quote.id}
              className="random-card"
              onClick={() => navigate(`/quote/${quote.id}`)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => e.key === 'Enter' && navigate(`/quote/${quote.id}`)}
            >
              <blockquote className="random-card-text">"{quote.text}"</blockquote>
              <div className="random-card-meta">
                <span className="random-card-author">— {quote.author.name}</span>
                {quote.source.title && (
                  <span className="random-card-source">{quote.source.title}</span>
                )}
              </div>
              {quote.tags.length > 0 && (
                <div className="random-card-tags">
                  {quote.tags.slice(0, 3).map((tag) => (
                    <span key={tag} className="random-card-tag">#{tag}</span>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
