import { useEffect } from 'react';
import type { Quotation } from '../../types/quotation';
import './SurpriseModal.css';

interface Props {
  quotation: Quotation | null;
  loading: boolean;
  onClose: () => void;
  onTryAgain: () => void;
}

export const SurpriseModal: React.FC<Props> = ({ quotation, loading, onClose, onTryAgain }) => {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [onClose]);

  return (
    <div className="surprise-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="surprise-modal" onClick={(e) => e.stopPropagation()}>
        {loading ? (
          <div className="surprise-loading">Finding a quote for you…</div>
        ) : quotation ? (
          <>
            <blockquote className="surprise-text">"{quotation.text}"</blockquote>
            <p className="surprise-author">— {quotation.author.name}</p>
            {quotation.source.title && (
              <p className="surprise-source">{quotation.source.title}</p>
            )}
            {quotation.tags.length > 0 && (
              <div className="surprise-tags">
                {quotation.tags.map((t) => <span key={t} className="surprise-tag">{t}</span>)}
              </div>
            )}
          </>
        ) : (
          <p className="surprise-loading">No quotes found.</p>
        )}
        <div className="surprise-actions">
          <button className="surprise-btn-again" onClick={onTryAgain} disabled={loading}>
            Try Another
          </button>
          <button className="surprise-btn-close" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
