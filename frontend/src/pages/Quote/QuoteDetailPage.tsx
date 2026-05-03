import React, { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { QuotationCard } from '../../components/quotations/QuotationCard';
import apiClient from '../../services/apiClient';
import type { Quotation, ApiResponse } from '../../types/quotation';
import './QuoteDetailPage.css';

export const QuoteDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [quotation, setQuotation] = useState<Quotation | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    setError(null);
    apiClient
      .get<ApiResponse<Quotation>>(`/quotations/${id}`)
      .then((res) => {
        if (res.success && res.data) {
          setQuotation(res.data);
        } else {
          setError('Quotation not found.');
        }
      })
      .catch(() => setError('Failed to load quotation.'))
      .finally(() => setLoading(false));
  }, [id]);

  return (
    <div className="quote-detail-page">
      <div className="quote-detail-nav">
        <Link to="/browse" className="back-link">← Browse all quotes</Link>
      </div>

      {loading && (
        <div className="quote-detail-loading" aria-label="Loading">
          <div className="loading-spinner" />
        </div>
      )}

      {error && (
        <div className="quote-detail-error" role="alert">
          <p>{error}</p>
          <Link to="/browse">Browse all quotes</Link>
        </div>
      )}

      {quotation && !loading && (
        <div className="quote-detail-content">
          <QuotationCard quotation={quotation} />
        </div>
      )}
    </div>
  );
};
