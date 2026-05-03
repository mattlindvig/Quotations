import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../../services/apiClient';
import { QuotationCard } from '../../components/quotations/QuotationCard';
import type { Quotation, PaginatedResponse, ApiResponse } from '../../types/quotation';
import './FavoritesPage.css';

export const FavoritesPage: React.FC = () => {
  const navigate = useNavigate();
  const [favorites, setFavorites] = useState<Quotation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 20,
    totalCount: 0,
  });

  useEffect(() => {
    fetchFavorites();
  }, [pagination.page]);

  const fetchFavorites = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await apiClient.get<ApiResponse<PaginatedResponse<Quotation>>>(
        `/favorites?page=${pagination.page}&pageSize=${pagination.pageSize}`
      );

      if (response.success && response.data) {
        setFavorites(response.data.items);
        setPagination((prev) => ({
          ...prev,
          totalCount: response.data!.pagination.totalCount,
        }));
      }
    } catch (err: any) {
      if (err.response?.status === 401) {
        setError('Please log in to view your favorites.');
      } else {
        setError('Failed to load favorites. Please try again later.');
      }
    } finally {
      setLoading(false);
    }
  };

  const handlePageChange = (newPage: number) => {
    setPagination((prev) => ({ ...prev, page: newPage }));
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const totalPages = Math.ceil(pagination.totalCount / pagination.pageSize);

  if (loading && favorites.length === 0) {
    return (
      <div className="favorites-page">
        <div className="loading-container">
          <div className="spinner" role="status" aria-label="Loading favorites"></div>
          <p>Loading your favorites...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="favorites-page">
        <div className="error-container">
          <p className="error-message">{error}</p>
          <button onClick={() => navigate('/')} className="button-primary">
            Go to Home
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="favorites-page">
      <div className="favorites-header">
        <h1>My Favorites</h1>
        <p className="favorites-description">
          Quotations you've saved. Browse and revisit your collection.
        </p>
        <button onClick={() => navigate('/')} className="button-primary">
          Browse More Quotes
        </button>
      </div>

      {favorites.length === 0 ? (
        <div className="empty-state">
          <svg
            className="empty-icon"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"
            />
          </svg>
          <h2>No Favorites Yet</h2>
          <p>Save quotes you love by clicking the ♡ Save button on any quotation.</p>
          <button onClick={() => navigate('/')} className="button-primary">
            Find Quotes to Save
          </button>
        </div>
      ) : (
        <>
          <div className="favorites-list">
            {favorites.map((quotation) => (
              <QuotationCard key={quotation.id} quotation={quotation} />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="pagination">
              <button
                className="pagination-button"
                onClick={() => handlePageChange(pagination.page - 1)}
                disabled={pagination.page === 1 || loading}
              >
                Previous
              </button>
              <span className="pagination-info">
                Page {pagination.page} of {totalPages}
              </span>
              <button
                className="pagination-button"
                onClick={() => handlePageChange(pagination.page + 1)}
                disabled={pagination.page >= totalPages || loading}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
};
