import React, { useState, useEffect } from 'react';
import { apiClient } from '../../services/apiClient';
import type { SourceType, ApiResponse } from '../../types/quotation';
import './FilterPanel.css';

interface Author {
  id: string;
  name: string;
}

interface Source {
  id: string;
  title: string;
  type: SourceType;
}

interface Tag {
  tag: string;
  count: number;
}

interface FilterPanelProps {
  onFilterChange: (filters: {
    authorId?: string;
    sourceType?: SourceType;
    tags?: string[];
  }) => void;
  initialFilters?: {
    authorId?: string;
    sourceType?: SourceType;
    tags?: string[];
  };
}

/**
 * Filter panel component for quotations
 * Allows filtering by author, source type, and tags
 */
export const FilterPanel: React.FC<FilterPanelProps> = ({
  onFilterChange,
  initialFilters = {},
}) => {
  const [authors, setAuthors] = useState<Author[]>([]);
  const [tags, setTags] = useState<Tag[]>([]);
  const [selectedAuthorId, setSelectedAuthorId] = useState(initialFilters.authorId || '');
  const [selectedSourceType, setSelectedSourceType] = useState(initialFilters.sourceType || '');
  const [selectedTags, setSelectedTags] = useState<string[]>(initialFilters.tags || []);
  const [isExpanded, setIsExpanded] = useState(false);

  const sourceTypes: SourceType[] = ['book', 'movie', 'speech', 'interview', 'other'];

  // Fetch authors and tags on mount
  useEffect(() => {
    const fetchMetadata = async () => {
      try {
        const [authorsResponse, tagsResponse] = await Promise.all([
          apiClient.get<ApiResponse<Author[]>>('/api/v1/authors?limit=100'),
          apiClient.get<ApiResponse<Tag[]>>('/api/v1/tags?limit=50'),
        ]);

        if (authorsResponse.data.success && authorsResponse.data.data) {
          setAuthors(authorsResponse.data.data);
        }

        if (tagsResponse.data.success && tagsResponse.data.data) {
          setTags(tagsResponse.data.data);
        }
      } catch (error) {
        console.error('Failed to fetch metadata:', error);
      }
    };

    fetchMetadata();
  }, []);

  // Apply filters when selections change
  useEffect(() => {
    const filters: { authorId?: string; sourceType?: SourceType; tags?: string[] } = {};

    if (selectedAuthorId) filters.authorId = selectedAuthorId;
    if (selectedSourceType) filters.sourceType = selectedSourceType as SourceType;
    if (selectedTags.length > 0) filters.tags = selectedTags;

    onFilterChange(filters);
  }, [selectedAuthorId, selectedSourceType, selectedTags, onFilterChange]);

  const handleTagToggle = (tag: string) => {
    setSelectedTags((prev) =>
      prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
    );
  };

  const handleClearFilters = () => {
    setSelectedAuthorId('');
    setSelectedSourceType('');
    setSelectedTags([]);
  };

  const hasActiveFilters = selectedAuthorId || selectedSourceType || selectedTags.length > 0;

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
          {hasActiveFilters && <span className="filter-badge">{[selectedAuthorId, selectedSourceType, ...selectedTags].filter(Boolean).length}</span>}
        </button>

        {hasActiveFilters && (
          <button className="clear-filters-button" onClick={handleClearFilters}>
            Clear all
          </button>
        )}
      </div>

      {isExpanded && (
        <div id="filter-content" className="filter-content">
          {/* Author filter */}
          <div className="filter-group">
            <label htmlFor="author-filter" className="filter-label">
              Author
            </label>
            <select
              id="author-filter"
              className="filter-select"
              value={selectedAuthorId}
              onChange={(e) => setSelectedAuthorId(e.target.value)}
            >
              <option value="">All authors</option>
              {authors.map((author) => (
                <option key={author.id} value={author.id}>
                  {author.name}
                </option>
              ))}
            </select>
          </div>

          {/* Source type filter */}
          <div className="filter-group">
            <label htmlFor="source-type-filter" className="filter-label">
              Source Type
            </label>
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
      )}
    </div>
  );
};
