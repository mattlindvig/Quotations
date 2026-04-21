import React, { useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import { QuotationCard } from './QuotationCard';
import type { Quotation } from '../../types/quotation';
import './QuotationList.css';

interface QuotationListProps {
  quotations: Quotation[];
  loading?: boolean;
  virtualized?: boolean;
}

export const QuotationList: React.FC<QuotationListProps> = ({
  quotations,
  loading = false,
  virtualized = false,
}) => {
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

  if (virtualized) {
    return <VirtualizedList quotations={quotations} />;
  }

  return (
    <section
      className="quotation-list-container"
      aria-label="Quotations list"
      role="region"
    >
      <div className="quotation-list">
        {quotations.map((quotation) => (
          <QuotationCard key={quotation.id} quotation={quotation} />
        ))}
      </div>
    </section>
  );
};

// Estimated card height in pixels — used by the virtualizer to size the container.
// Actual heights are measured after first render and corrected automatically.
const ESTIMATED_CARD_HEIGHT = 200;

const VirtualizedList: React.FC<{ quotations: Quotation[] }> = ({ quotations }) => {
  const parentRef = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: quotations.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => ESTIMATED_CARD_HEIGHT,
    overscan: 5,
  });

  return (
    <section
      ref={parentRef}
      className="quotation-list-container quotation-list-virtual-scroll"
      aria-label="Quotations list"
      role="region"
    >
      <div
        className="quotation-list"
        style={{ height: `${virtualizer.getTotalSize()}px`, position: 'relative' }}
      >
        {virtualizer.getVirtualItems().map((virtualItem) => (
          <div
            key={virtualItem.key}
            data-index={virtualItem.index}
            ref={virtualizer.measureElement}
            style={{
              position: 'absolute',
              top: 0,
              left: 0,
              width: '100%',
              transform: `translateY(${virtualItem.start}px)`,
            }}
          >
            <QuotationCard quotation={quotations[virtualItem.index]} />
          </div>
        ))}
      </div>
    </section>
  );
};
