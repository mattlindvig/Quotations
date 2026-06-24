import React, { useState, useEffect, useRef, useCallback } from 'react';
import { apiClient } from '../../services/apiClient';
import type { SourceType, ApiResponse, QuotationFilters } from '../../types/quotation';
import './FilterPanel.css';

interface Tag {
  tag: string;
  count: number;
}

interface FilterPanelProps {
  onFilterChange: (filters: QuotationFilters) => void;
  initialFilters?: QuotationFilters;
  activeSearch?: string;
  onClearSearch?: () => void;
}

const MAX_AUTHOR_SUGGESTIONS = 8;
const MAX_TAG_SUGGESTIONS = 8;

const CURATED_TAGS = [
  'wisdom', 'courage', 'love', 'friendship',
  'nature', 'art', 'war', 'peace',
  'humor', 'work', 'history', 'family',
];

export const FilterPanel: React.FC<FilterPanelProps> = ({
  onFilterChange,
  initialFilters = {},
  activeSearch,
  onClearSearch,
}) => {
  const [authorNames, setAuthorNames] = useState<string[]>([]);
  const [tags, setTags] = useState<Tag[]>([]);

  // Author autocomplete state
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

  // Tag autocomplete state
  const [tagInputValue, setTagInputValue] = useState('');
  const [tagDropdownOpen, setTagDropdownOpen] = useState(false);
  const [tagDropdownHighlight, setTagDropdownHighlight] = useState(-1);
  const tagAutocompleteRef = useRef<HTMLDivElement>(null);

  const sourceTypes: { value: SourceType; label: string }[] = [
    { value: 'book',         label: 'Book' },
    { value: 'movie',        label: 'Movie' },
    { value: 'television',   label: 'TV Show' },
    { value: 'speech',       label: 'Speech' },
    { value: 'interview',    label: 'Interview' },
    { value: 'poem',         label: 'Poem' },
    { value: 'song',         label: 'Song' },
    { value: 'play',         label: 'Play' },
    { value: 'musical',      label: 'Musical' },
    { value: 'videogame',    label: 'Video Game' },
    { value: 'comic',        label: 'Comic / Graphic Novel' },
    { value: 'article',      label: 'Article / Essay' },
    { value: 'letter',       label: 'Letter / Correspondence' },
    { value: 'podcast',      label: 'Podcast' },
    { value: 'documentary',  label: 'Documentary' },
    { value: 'scripture',    label: 'Scripture / Religious Text' },
    { value: 'proverb',      label: 'Proverb' },
    { value: 'memoir',       label: 'Memoir / Autobiography' },
    { value: 'standup',      label: 'Stand-up Comedy' },
    { value: 'organization', label: 'Organization' },
    { value: 'other',        label: 'Other' },
  ];

  const authorSuggestions = inputValue.trim().length > 0
    ? authorNames
        .filter((n) => n.toLowerCase().includes(inputValue.toLowerCase()))
        .slice(0, MAX_AUTHOR_SUGGESTIONS)
    : [];

  const tagSuggestions = tagInputValue.trim().length > 0
    ? tags
        .filter(({ tag }) =>
          tag.toLowerCase().includes(tagInputValue.trim().toLowerCase()) &&
          !selectedTags.includes(tag)
        )
        .slice(0, MAX_TAG_SUGGESTIONS)
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

  const externalTagsKey = (initialFilters.tags || []).join(',');
  useEffect(() => {
    const newTags = initialFilters.tags || [];
    setSelectedTags(prev => {
      if (prev.join(',') === externalTagsKey) return prev;
      return newTags;
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [externalTagsKey]);

  // Fetch author names once on mount
  useEffect(() => {
    apiClient.get<ApiResponse<string[]>>('/quotations/authors?limit=500')
      .then((res) => { if (res.success && res.data) setAuthorNames(res.data); })
      .catch(console.error);
  }, []);

  // Fetch tags for autocomplete pool and curated chip counts
  useEffect(() => {
    const params = new URLSearchParams({ limit: '500' });
    if (selectedAuthorName) params.append('authorName', selectedAuthorName);
    if (selectedSourceType) params.append('sourceType', selectedSourceType);

    apiClient.get<ApiResponse<Tag[]>>(`/tags?${params.toString()}`)
      .then((res) => { if (res.success && res.data) setTags(res.data); })
      .catch(console.error);
  }, [selectedAuthorName, selectedSourceType]);

  // Close author dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (autocompleteRef.current && !autocompleteRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setInputValue(selectedAuthorName);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [selectedAuthorName]);

  // Close tag dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (tagAutocompleteRef.current && !tagAutocompleteRef.current.contains(e.target as Node)) {
        setTagDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  // Keep a stable ref to the callback so the effect below only re-runs when
  // filter *values* change, not when the parent recreates the callback reference.
  const onFilterChangeRef = useRef(onFilterChange);
  onFilterChangeRef.current = onFilterChange;

  // Apply filters when confirmed selections change
  useEffect(() => {
    const filters: QuotationFilters = {};
    if (selectedAuthorName) filters.authorName = selectedAuthorName;
    if (selectedSourceType) filters.sourceType = selectedSourceType as SourceType;
    if (selectedSourceTitle) filters.sourceTitle = selectedSourceTitle;
    if (selectedTags.length > 0) filters.tags = selectedTags;
    onFilterChangeRef.current(filters);
  }, [selectedAuthorName, selectedSourceType, selectedSourceTitle, selectedTags]);

  // ── Author autocomplete handlers ──
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
    setSelectedAuthorName('');
    setHighlightedIndex(-1);
    setIsOpen(val.trim().length > 0);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen || authorSuggestions.length === 0) {
      if (e.key === 'Escape') clearAuthor();
      return;
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlightedIndex((i) => Math.min(i + 1, authorSuggestions.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (highlightedIndex >= 0) selectAuthor(authorSuggestions[highlightedIndex]);
    } else if (e.key === 'Escape') {
      setIsOpen(false);
      setInputValue(selectedAuthorName);
    }
  };

  // ── Tag handlers ──
  const handleTagToggle = (tag: string) => {
    setSelectedTags((prev) =>
      prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
    );
  };

  const selectTag = useCallback((tag: string) => {
    setSelectedTags((prev) => (prev.includes(tag) ? prev : [...prev, tag]));
    setTagInputValue('');
    setTagDropdownOpen(false);
    setTagDropdownHighlight(-1);
  }, []);

  const handleTagInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setTagInputValue(val);
    setTagDropdownHighlight(-1);
    setTagDropdownOpen(val.trim().length > 0);
  };

  const handleTagKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!tagDropdownOpen || tagSuggestions.length === 0) {
      if (e.key === 'Escape') { setTagInputValue(''); setTagDropdownOpen(false); }
      return;
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setTagDropdownHighlight((i) => Math.min(i + 1, tagSuggestions.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setTagDropdownHighlight((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (tagDropdownHighlight >= 0) selectTag(tagSuggestions[tagDropdownHighlight].tag);
    } else if (e.key === 'Escape') {
      setTagDropdownOpen(false);
      setTagInputValue('');
    }
  };

  // ── Misc handlers ──
  const handleClearFilters = () => {
    clearAuthor();
    setSelectedSourceType('');
    setSelectedSourceTitle('');
    setSelectedTags([]);
    onClearSearch?.();
  };

  const hasActiveFilters = !!(activeSearch || selectedAuthorName || selectedSourceType || selectedSourceTitle || selectedTags.length > 0);

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
              {[activeSearch, selectedAuthorName, selectedSourceType, selectedSourceTitle, ...selectedTags].filter(Boolean).length}
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
                  if (inputValue.trim().length > 0 && authorSuggestions.length > 0) setIsOpen(true);
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

            {isOpen && authorSuggestions.length > 0 && (
              <ul
                id="author-listbox"
                className="author-suggestions"
                role="listbox"
                aria-label="Author suggestions"
              >
                {authorSuggestions.map((name, i) => (
                  <li
                    key={name}
                    id={`author-option-${i}`}
                    className={`author-suggestion-item${i === highlightedIndex ? ' highlighted' : ''}`}
                    role="option"
                    aria-selected={i === highlightedIndex}
                    onMouseDown={(e) => {
                      e.preventDefault();
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
            {sourceTypes.map(({ value, label }) => (
              <option key={value} value={value}>{label}</option>
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

        {/* Tags — two-tier: curated chips + search autocomplete */}
        <div className="filter-group">
          <label className="filter-label">Tags</label>

          {/* Active selected tags as removable chips */}
          {selectedTags.length > 0 && (
            <div className="active-selected-tags">
              {selectedTags.map((tag) => (
                <button
                  key={tag}
                  className="active-tag-chip"
                  onClick={() => handleTagToggle(tag)}
                  aria-label={`Remove tag ${tag}`}
                >
                  {tag} <span className="active-tag-x" aria-hidden="true">×</span>
                </button>
              ))}
            </div>
          )}

          {/* Curated tag chips */}
          <div className="curated-tags">
            {CURATED_TAGS.map((tag) => (
              <button
                key={tag}
                className={`tag-chip${selectedTags.includes(tag) ? ' tag-chip--active' : ''}`}
                onClick={() => handleTagToggle(tag)}
                aria-pressed={selectedTags.includes(tag)}
              >
                {tag}
              </button>
            ))}
          </div>

          {/* Tag search autocomplete */}
          <div className="tag-autocomplete" ref={tagAutocompleteRef}>
            <div className="author-input-wrap">
              <input
                type="text"
                className="author-input"
                placeholder="Search more tags…"
                value={tagInputValue}
                onChange={handleTagInputChange}
                onKeyDown={handleTagKeyDown}
                onFocus={() => {
                  if (tagInputValue.trim().length > 0 && tagSuggestions.length > 0)
                    setTagDropdownOpen(true);
                }}
                autoComplete="off"
              />
              {tagInputValue && (
                <button
                  className="author-clear-btn"
                  onClick={() => { setTagInputValue(''); setTagDropdownOpen(false); }}
                  tabIndex={-1}
                  aria-label="Clear tag search"
                >
                  ×
                </button>
              )}
            </div>
            {tagDropdownOpen && tagSuggestions.length > 0 && (
              <ul className="tag-suggestions" role="listbox" aria-label="Tag suggestions">
                {tagSuggestions.map(({ tag, count }, i) => (
                  <li
                    key={tag}
                    className={`tag-suggestion-item${i === tagDropdownHighlight ? ' highlighted' : ''}`}
                    role="option"
                    aria-selected={i === tagDropdownHighlight}
                    onMouseDown={(e) => { e.preventDefault(); selectTag(tag); }}
                    onMouseEnter={() => setTagDropdownHighlight(i)}
                  >
                    {tag} <span className="tag-count">({count.toLocaleString()})</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
