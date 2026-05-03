import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import type { ReactNode } from 'react';
import { apiClient } from '../services/apiClient';
import type { ApiResponse } from '../types/quotation';
import { useAuth } from './AuthContext';

interface FavoritesContextType {
  favoriteIds: Set<string>;
  isFavorited: (quotationId: string) => boolean;
  toggleFavorite: (quotationId: string) => Promise<void>;
  isLoading: boolean;
}

const FavoritesContext = createContext<FavoritesContextType | undefined>(undefined);

export const useFavorites = () => {
  const context = useContext(FavoritesContext);
  if (!context) throw new Error('useFavorites must be used within a FavoritesProvider');
  return context;
};

export const FavoritesProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { isAuthenticated } = useAuth();
  const [favoriteIds, setFavoriteIds] = useState<Set<string>>(new Set());
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!isAuthenticated) {
      setFavoriteIds(new Set());
      return;
    }

    const loadFavoriteIds = async () => {
      setIsLoading(true);
      try {
        const response = await apiClient.get<ApiResponse<string[]>>('/favorites/ids');
        if (response.success && response.data) {
          setFavoriteIds(new Set(response.data));
        }
      } catch {
        // Non-critical — favorites simply won't show as active
      } finally {
        setIsLoading(false);
      }
    };

    loadFavoriteIds();
  }, [isAuthenticated]);

  const isFavorited = useCallback(
    (quotationId: string) => favoriteIds.has(quotationId),
    [favoriteIds]
  );

  const toggleFavorite = useCallback(
    async (quotationId: string) => {
      const wasFavorited = favoriteIds.has(quotationId);

      // Optimistic update
      setFavoriteIds((prev) => {
        const next = new Set(prev);
        if (wasFavorited) {
          next.delete(quotationId);
        } else {
          next.add(quotationId);
        }
        return next;
      });

      try {
        if (wasFavorited) {
          await apiClient.delete(`/favorites/${quotationId}`);
        } else {
          await apiClient.post(`/favorites/${quotationId}`);
        }
      } catch {
        // Roll back on failure
        setFavoriteIds((prev) => {
          const next = new Set(prev);
          if (wasFavorited) {
            next.add(quotationId);
          } else {
            next.delete(quotationId);
          }
          return next;
        });
      }
    },
    [favoriteIds]
  );

  return (
    <FavoritesContext.Provider value={{ favoriteIds, isFavorited, toggleFavorite, isLoading }}>
      {children}
    </FavoritesContext.Provider>
  );
};
