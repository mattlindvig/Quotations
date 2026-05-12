import React, { useState, useCallback, useEffect } from 'react';
import { getRandomBatch } from '../../services/quotationService';
import { QuotationCard } from '../../components/quotations/QuotationCard';
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
        <div className="random-list">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="random-card-skeleton" />
          ))}
        </div>
      ) : quotes.length === 0 ? (
        <div className="random-empty">
          <p>No quotes found with those filters.</p>
          <button className="shuffle-btn" onClick={fetchRandom}>Try Again</button>
        </div>
      ) : (
        <div className="random-list">
          {quotes.map((quote) => (
            <QuotationCard key={quote.id} quotation={quote} />
          ))}
        </div>
      )}
    </div>
  );
};
