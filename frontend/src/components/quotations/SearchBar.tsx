import React, { useState, useEffect, useCallback, useRef, forwardRef, useImperativeHandle } from 'react';
import './SearchBar.css';

interface SearchBarProps {
  onSearch: (query: string) => void;
  initialValue?: string;
  placeholder?: string;
  debounceMs?: number;
}

export interface SearchBarHandle {
  clear: () => void;
}

export const SearchBar = forwardRef<SearchBarHandle, SearchBarProps>(({
  onSearch,
  initialValue = '',
  placeholder = 'Search quotations...',
  debounceMs = 200,
}, ref) => {
  const [searchValue, setSearchValue] = useState(initialValue);
  const isMountRef = useRef(true);

  useImperativeHandle(ref, () => ({
    clear: () => setSearchValue(''),
  }));

  // Debounced search effect
  useEffect(() => {
    if (isMountRef.current) {
      isMountRef.current = false;
      return;
    }
    const id = setTimeout(() => onSearch(searchValue.trim()), debounceMs);
    return () => clearTimeout(id);
  }, [searchValue]); // intentionally omit onSearch and debounceMs from deps — they don't change the intent

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
});

SearchBar.displayName = 'SearchBar';
