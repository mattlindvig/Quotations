import React, { useState, useEffect, useRef, useCallback } from 'react';
import { apiClient } from '../../services/apiClient';
import type { SourceType, ApiResponse } from '../../types/quotation';
import './FilterPanel.css';

interface Tag {
  tag: string;
  count: number;
}

interface FilterPanelProps {
  onFilterChange: (filters: {
    authorName?: string;
    sourceType?: SourceType;
    sourceTitle?: string;
    tags?: string[];
  }) => void;
  initialFilters?: {
    authorName?: string;
    sourceType?: SourceType;
    sourceTitle?: string;
    tags?: string[];
  };
}

const MAX_SUGGESTIONS = 8;

export const FilterPanel: React.FC<FilterPanelProps> = ({
  onFilterChange,
  initialFilters = {},
}) => {
  const [authorNames, setAuthorNames] = useState<string[]>([]);
  const [tags, setTags] = useState<Tag[]>([]);

  // Autocomplete state
  const [inputValue, setInputValue] = useState(initialFilters.authorName || '');
  const [selectedAuthorName, setSelectedAuthorName] = useState(initialFilters.authorName || '');
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const autocompleteRef = useRef<HTMLDivElement>(null);

  const [selectedSourceType, setSelectedSourceType] = useState(initialFilters.sourceType || '');
  const [selectedSourceTitle, setSelectedSourceTitle] = useState(initialFilters.sourceTitle || '');
  const [selectedTags, setSelectedTags] = useState<string[]>(initialFilters.tags || []);
  const [isExpanded, setIsExpanded] = useState(
    !!(initialFilters.authorName || initialFilters.sourceType || initialFilters.tags?.length)
  );

  const sourceTypes: SourceType[] = ['book', 'movie', 'speech', 'interview', 'other'];

  const suggestions = inputValue.trim().length > 0
    ? authorNames
        .filter((n) => n.toLowerCase().includes(inputValue.toLowerCase()))
        .slice(0, MAX_SUGGESTIONS)
    : [];

  // Sync state when initialFilters change externally (e.g. clicking an author name on a card)
  useEffect(() => {
    setSelectedAuthorName(initialFilters.authorName || '');
    setInputValue(initialFilters.authorName || '');
  }, [initialFilters.authorName]);

  useEffect(() => {
    setSelectedSourceType(initialFilters.sourceType || '');
  }, [initialFilters.sourceType]);

  useEffect(() => {
    setSelectedSourceTitle(initialFilters.sourceTitle || '');
  }, [initialFilters.sourceTitle]);

  useEffect(() => {
    setSelectedTags(initialFilters.tags || []);
  }, [initialFilters.tags]);

  // Fetch author names once on mount
  useEffect(() => {
    apiClient.get<ApiResponse<string[]>>('/quotations/authors?limit=500')
      .then((res) => { if (res.success && res.data) setAuthorNames(res.data); })
      .catch(console.error);
  }, []);

  // Re-fetch tags whenever the author or source type selection changes
  useEffect(() => {
    const params = new URLSearchParams({ limit: '50' });
    if (selectedAuthorName) params.append('authorName', selectedAuthorName);
    if (selectedSourceType) params.append('sourceType', selectedSourceType);

    apiClient.get<ApiResponse<Tag[]>>(`/tags?${params.toString()}`)
      .then((res) => { if (res.success && res.data) setTags(res.data); })
      .catch(console.error);
  }, [selectedAuthorName, selectedSourceType]);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (autocompleteRef.current && !autocompleteRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        // If user typed but didn't select, revert input to last confirmed selection
        setInputValue(selectedAuthorName);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [selectedAuthorName]);

  // Apply filters when confirmed selections change
  useEffect(() => {
    const filters: { authorName?: string; sourceType?: SourceType; sourceTitle?: string; tags?: string[] } = {};
    if (selectedAuthorName) filters.authorName = selectedAuthorName;
    if (selectedSourceType) filters.sourceType = selectedSourceType as SourceType;
    if (selectedSourceTitle) filters.sourceTitle = selectedSourceTitle;
    if (selectedTags.length > 0) filters.tags = selectedTags;
    onFilterChange(filters);
  }, [selectedAuthorName, selectedSourceType, selectedSourceTitle, selectedTags, onFilterChange]);

  const selectAuthor = useCallback((name: string) => {
    setSelectedAuthorName(name);
    setInputValue(name);
    setIsOpen(false);
    setHighlightedIndex(-1);
  }, []);

  const clearAuthor = useCallback(() => {
    setSelectedAuthorName('');
    setInputValue('');
    setIsOpen(false);
    setHighlightedIndex(-1);
  }, []);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setInputValue(val);
    setSelectedAuthorName(''); // clear confirmed selection while typing
    setHighlightedIndex(-1);
    setIsOpen(val.trim().length > 0);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen || suggestions.length === 0) {
      if (e.key === 'Escape') clearAuthor();
      return;
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlightedIndex((i) => Math.min(i + 1, suggestions.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (highlightedIndex >= 0) selectAuthor(suggestions[highlightedIndex]);
    } else if (e.key === 'Escape') {
      setIsOpen(false);
      setInputValue(selectedAuthorName);
    }
  };

  const handleTagToggle = (tag: string) => {
    setSelectedTags((prev) =>
      prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
    );
  };

  const handleClearFilters = () => {
    clearAuthor();
    setSelectedSourceType('');
    setSelectedSourceTitle('');
    setSelectedTags([]);
  };

  const hasActiveFilters = selectedAuthorName || selectedSourceType || selectedSourceTitle || selectedTags.length > 0;

  return (
    <div className="filter-panel">
      <div className="filter-header">
        <button
          className="filter-toggle"
          onClick={() => setIsExpanded(!isExpanded)}
          aria-expanded={isExpanded}
          aria-controls="filter-content"
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
            <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
          </svg>
          <span>Filters</span>
          {hasActiveFilters && (
            <span className="filter-badge">
              {[selectedAuthorName, selectedSourceType, selectedSourceTitle, ...selectedTags].filter(Boolean).length}
            </span>
          )}
        </button>

        {hasActiveFilters && (
          <button className="clear-filters-button" onClick={handleClearFilters}>
            Clear all
          </button>
        )}
      </div>

      <div
        id="filter-content"
        className={`filter-content${isExpanded ? '' : ' filter-content--collapsed'}`}
      >
        {/* Author autocomplete */}
        <div className="filter-group">
          <label htmlFor="author-filter" className="filter-label">Author</label>
          <div className="author-autocomplete" ref={autocompleteRef}>
            <div className="author-input-wrap">
              <input
                id="author-filter"
                type="text"
                className="author-input"
                placeholder="Search authors…"
                value={inputValue}
                onChange={handleInputChange}
                onKeyDown={handleKeyDown}
                onFocus={() => {
                  if (inputValue.trim().length > 0 && suggestions.length > 0) setIsOpen(true);
                }}
                autoComplete="off"
                role="combobox"
                aria-expanded={isOpen}
                aria-autocomplete="list"
                aria-controls="author-listbox"
                aria-activedescendant={
                  highlightedIndex >= 0 ? `author-option-${highlightedIndex}` : undefined
                }
              />
              {inputValue && (
                <button
                  className="author-clear-btn"
                  onClick={clearAuthor}
                  aria-label="Clear author"
                  tabIndex={-1}
                >
                  ×
                </button>
              )}
            </div>

            {isOpen && suggestions.length > 0 && (
              <ul
                id="author-listbox"
                className="author-suggestions"
                role="listbox"
                aria-label="Author suggestions"
              >
                {suggestions.map((name, i) => (
                  <li
                    key={name}
                    id={`author-option-${i}`}
                    className={`author-suggestion-item${i === highlightedIndex ? ' highlighted' : ''}`}
                    role="option"
                    aria-selected={i === highlightedIndex}
                    onMouseDown={(e) => {
                      e.preventDefault(); // keep focus on input
                      selectAuthor(name);
                    }}
                    onMouseEnter={() => setHighlightedIndex(i)}
                  >
                    {name}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Source type filter */}
        <div className="filter-group">
          <label htmlFor="source-type-filter" className="filter-label">Source Type</label>
          <select
            id="source-type-filter"
            className="filter-select"
            value={selectedSourceType}
            onChange={(e) => setSelectedSourceType(e.target.value)}
          >
            <option value="">All types</option>
            {sourceTypes.map((type) => (
              <option key={type} value={type}>
                {type.charAt(0).toUpperCase() + type.slice(1)}
              </option>
            ))}
          </select>
        </div>

        {/* Active source title filter */}
        {selectedSourceTitle && (
          <div className="filter-group">
            <label className="filter-label">Source</label>
            <div className="active-source-filter">
              <span className="active-source-title">{selectedSourceTitle}</span>
              <button
                className="author-clear-btn"
                onClick={() => setSelectedSourceTitle('')}
                aria-label="Clear source filter"
              >
                ×
              </button>
            </div>
          </div>
        )}

        {/* Tags filter */}
        {tags.length > 0 && (
          <div className="filter-group">
            <label className="filter-label">Tags</label>
            <div className="tags-container">
              {tags.slice(0, 15).map(({ tag, count }) => (
                <button
                  key={tag}
                  className={`tag-filter-button ${selectedTags.includes(tag) ? 'active' : ''}`}
                  onClick={() => handleTagToggle(tag)}
                  aria-pressed={selectedTags.includes(tag)}
                >
                  {tag} <span className="tag-count">({count})</span>
                </button>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
