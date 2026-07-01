import React, { useEffect, useState, useCallback, useRef } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../../services/apiClient';
import type { QuotationSummary, PaginatedQuotationsResponse, ApiResponse } from '../../types/quotation';
import './MisattributedPage.css';

const PAGE_SIZE = 20;

export const MisattributedPage: React.FC = () => {
  const [quotes, setQuotes] = useState<QuotationSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [hasNext, setHasNext] = useState(false);

  const loadingRef = useRef(false);

  const fetchPage = useCallback(async (pageNum: number) => {
    if (loadingRef.current) return;
    loadingRef.current = true;
    setLoading(true);
    setError(null);
    try {
      const res = await apiClient.get<ApiResponse<PaginatedQuotationsResponse>>(
        `/quotations/misattributed?page=${pageNum}&pageSize=${PAGE_SIZE}`
      );
      if (res.success && res.data) {
        setQuotes((prev) => (pageNum === 1 ? res.data!.items : [...prev, ...res.data!.items]));
        setTotalCount(res.data.pagination.totalCount);
        setHasNext(res.data.pagination.hasNext);
        setPage(pageNum);
      } else {
        setError('Failed to load misattributed quotes.');
      }
    } catch {
      setError('Failed to load misattributed quotes.');
    } finally {
      setLoading(false);
      loadingRef.current = false;
    }
  }, []);

  useEffect(() => {
    fetchPage(1);
  }, [fetchPage]);

  return (
    <div className="misattributed-page">
      <header className="misattributed-header">
        <h1>Quotes People Get Wrong</h1>
        <p className="misattributed-intro">
          Famous lines are constantly pinned on the wrong person. These are quotations our AI
          review flagged as likely misattributed — with the attribution it believes is correct.
        </p>
        {!loading && totalCount > 0 && (
          <p className="misattributed-count">{totalCount.toLocaleString()} flagged quotations</p>
        )}
      </header>

      {error && (
        <div className="misattributed-error" role="alert">
          <p>{error}</p>
          <Link to="/browse">Browse all quotes</Link>
        </div>
      )}

      {!error && (
        <ul className="misattributed-list">
          {quotes.map((q) => (
            <li key={q.id} className="misattributed-card">
              <Link to={`/quote/${q.id}`} className="misattributed-quote">
                "{q.text}"
              </Link>
              <div className="misattributed-attributions">
                <span className="misattributed-claimed">
                  <span className="misattributed-label">Commonly attributed to</span>
                  <strong>{q.author.name || 'Unknown'}</strong>
                </span>
                {q.aiReview?.correctAttribution && (
                  <span className="misattributed-correct">
                    <span className="misattributed-label">Likely actually</span>
                    <strong>{q.aiReview.correctAttribution}</strong>
                  </span>
                )}
              </div>
              {q.aiReview?.authenticityReasoning && (
                <p className="misattributed-reasoning">{q.aiReview.authenticityReasoning}</p>
              )}
            </li>
          ))}
        </ul>
      )}

      {loading && (
        <div className="misattributed-loading" aria-label="Loading">
          <div className="loading-spinner" />
        </div>
      )}

      {!loading && !error && hasNext && (
        <div className="misattributed-more">
          <button className="misattributed-more-btn" onClick={() => fetchPage(page + 1)}>
            Load more
          </button>
        </div>
      )}

      {!loading && !error && quotes.length === 0 && (
        <div className="misattributed-empty" role="status">
          <p>No misattributed quotes have been flagged yet.</p>
          <Link to="/browse">Browse all quotes</Link>
        </div>
      )}
    </div>
  );
};

export default MisattributedPage;
