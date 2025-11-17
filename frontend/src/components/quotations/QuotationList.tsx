import React from 'react';
import { QuotationCard } from './QuotationCard';
import type { Quotation } from '../../types/quotation';
import './QuotationList.css';

interface QuotationListProps {
  quotations: Quotation[];
  loading?: boolean;
  virtualized?: boolean;
}

/**
 * List component for displaying quotations
 * Note: Virtualization temporarily disabled due to react-window compatibility issues
 */
export const QuotationList: React.FC<QuotationListProps> = ({
  quotations,
  loading = false,
  virtualized = false,
}) => {
  // Render simple list (virtualization disabled for now)
  const quotationsList = (
    <div className="quotation-list">
      {quotations.map((quotation) => (
        <QuotationCard key={quotation.id} quotation={quotation} />
      ))}
    </div>
  );

  if (loading) {
    return (
      <div className="quotation-list-loading" role="status" aria-live="polite">
        <div className="loading-spinner" aria-label="Loading quotations"></div>
        <p>Loading quotations...</p>
      </div>
    );
  }

  if (quotations.length === 0) {
    return (
      <div className="quotation-list-empty" role="status">
        <p>No quotations found.</p>
      </div>
    );
  }

  return (
    <section
      className="quotation-list-container"
      aria-label="Quotations list"
      role="region"
    >
      {quotationsList}
    </section>
  );
};
