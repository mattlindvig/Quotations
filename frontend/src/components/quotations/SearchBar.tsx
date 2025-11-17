import React, { useState, useEffect, useCallback } from 'react';
import './SearchBar.css';

interface SearchBarProps {
  onSearch: (query: string) => void;
  initialValue?: string;
  placeholder?: string;
  debounceMs?: number;
}

/**
 * Search bar component with debouncing
 * Debounces user input to avoid excessive API calls
 */
export const SearchBar: React.FC<SearchBarProps> = ({
  onSearch,
  initialValue = '',
  placeholder = 'Search quotations...',
  debounceMs = 500,
}) => {
  const [searchValue, setSearchValue] = useState(initialValue);

  // Debounced search effect
  useEffect(() => {
    const timeoutId = setTimeout(() => {
      if (searchValue.trim() !== initialValue) {
        onSearch(searchValue.trim());
      }
    }, debounceMs);

    return () => clearTimeout(timeoutId);
  }, [searchValue, debounceMs, onSearch, initialValue]);

  const handleClear = useCallback(() => {
    setSearchValue('');
    onSearch('');
  }, [onSearch]);

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      onSearch(searchValue.trim());
    },
    [searchValue, onSearch]
  );

  return (
    <form className="search-bar" onSubmit={handleSubmit} role="search">
      <div className="search-input-container">
        <svg
          className="search-icon"
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <circle cx="11" cy="11" r="8" />
          <path d="m21 21-4.35-4.35" />
        </svg>

        <input
          type="search"
          className="search-input"
          value={searchValue}
          onChange={(e) => setSearchValue(e.target.value)}
          placeholder={placeholder}
          aria-label="Search quotations"
        />

        {searchValue && (
          <button
            type="button"
            className="search-clear-button"
            onClick={handleClear}
            aria-label="Clear search"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        )}
      </div>
    </form>
  );
};
