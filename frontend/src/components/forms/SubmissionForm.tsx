import React, { useState } from 'react';
import { apiClient } from '../../services/apiClient';
import type { SourceType, ApiResponse } from '../../types/quotation';
import './SubmissionForm.css';

interface SubmissionFormData {
  text: string;
  authorName: string;
  authorLifespan: string;
  authorOccupation: string;
  sourceTitle: string;
  sourceType: SourceType | '';
  sourceYear: string;
  sourceAdditionalInfo: string;
  tags: string[];
}

interface SubmissionFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

export const SubmissionForm: React.FC<SubmissionFormProps> = ({ onSuccess, onCancel }) => {
  const [formData, setFormData] = useState<SubmissionFormData>({
    text: '',
    authorName: '',
    authorLifespan: '',
    authorOccupation: '',
    sourceTitle: '',
    sourceType: '',
    sourceYear: '',
    sourceAdditionalInfo: '',
    tags: [],
  });

  const [tagInput, setTagInput] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  const sourceTypes: SourceType[] = ['book', 'movie', 'speech', 'interview', 'other'];

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    // Clear error for this field
    if (errors[name]) {
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors[name];
        return newErrors;
      });
    }
  };

  const handleAddTag = () => {
    const tag = tagInput.trim();
    if (tag && !formData.tags.includes(tag) && formData.tags.length < 20) {
      setFormData((prev) => ({ ...prev, tags: [...prev.tags, tag] }));
      setTagInput('');
    }
  };

  const handleRemoveTag = (tagToRemove: string) => {
    setFormData((prev) => ({
      ...prev,
      tags: prev.tags.filter((tag) => tag !== tagToRemove),
    }));
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleAddTag();
    }
  };

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.text.trim()) newErrors.text = 'Quotation text is required';
    else if (formData.text.length > 5000)
      newErrors.text = 'Quotation text must be 5000 characters or less';

    if (!formData.authorName.trim()) newErrors.authorName = 'Author name is required';
    else if (formData.authorName.length > 200)
      newErrors.authorName = 'Author name must be 200 characters or less';

    if (!formData.sourceTitle.trim()) newErrors.sourceTitle = 'Source title is required';
    else if (formData.sourceTitle.length > 300)
      newErrors.sourceTitle = 'Source title must be 300 characters or less';

    if (!formData.sourceType) newErrors.sourceType = 'Source type is required';

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validate()) return;

    setIsSubmitting(true);
    setErrors({});

    try {
      const payload = {
        text: formData.text.trim(),
        authorName: formData.authorName.trim(),
        authorLifespan: formData.authorLifespan.trim() || undefined,
        authorOccupation: formData.authorOccupation.trim() || undefined,
        sourceTitle: formData.sourceTitle.trim(),
        sourceType: formData.sourceType,
        sourceYear: formData.sourceYear ? parseInt(formData.sourceYear) : undefined,
        sourceAdditionalInfo: formData.sourceAdditionalInfo.trim() || undefined,
        tags: formData.tags,
      };

      const response = await apiClient.post<ApiResponse<any>>('/api/v1/submissions', payload);

      if (response.data.success) {
        setShowSuccess(true);
        // Reset form
        setFormData({
          text: '',
          authorName: '',
          authorLifespan: '',
          authorOccupation: '',
          sourceTitle: '',
          sourceType: '',
          sourceYear: '',
          sourceAdditionalInfo: '',
          tags: [],
        });
        setTimeout(() => {
          setShowSuccess(false);
          onSuccess?.();
        }, 3000);
      } else if (response.data.errors) {
        const apiErrors: Record<string, string> = {};
        Object.entries(response.data.errors).forEach(([key, messages]) => {
          apiErrors[key] = messages[0];
        });
        setErrors(apiErrors);
      }
    } catch (error: any) {
      if (error.response?.data?.errors) {
        const apiErrors: Record<string, string> = {};
        Object.entries(error.response.data.errors).forEach(([key, messages]: [string, any]) => {
          apiErrors[key] = messages[0];
        });
        setErrors(apiErrors);
      } else {
        setErrors({ general: 'Failed to submit quotation. Please try again.' });
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (showSuccess) {
    return (
      <div className="submission-success" role="alert">
        <svg className="success-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
        <h3>Quotation Submitted Successfully!</h3>
        <p>
          Your quotation has been submitted for review. Our team will review it shortly and it will
          be published once approved.
        </p>
      </div>
    );
  }

  return (
    <form className="submission-form" onSubmit={handleSubmit}>
      {errors.general && <div className="form-error-general">{errors.general}</div>}

      <div className="form-group">
        <label htmlFor="text" className="form-label required">
          Quotation Text
        </label>
        <textarea
          id="text"
          name="text"
          className={`form-textarea ${errors.text ? 'error' : ''}`}
          value={formData.text}
          onChange={handleChange}
          rows={4}
          placeholder="Enter the quotation..."
          required
        />
        {errors.text && <span className="form-error">{errors.text}</span>}
      </div>

      <div className="form-section">
        <h3 className="section-title">Author Information</h3>

        <div className="form-group">
          <label htmlFor="authorName" className="form-label required">
            Author Name
          </label>
          <input
            type="text"
            id="authorName"
            name="authorName"
            className={`form-input ${errors.authorName ? 'error' : ''}`}
            value={formData.authorName}
            onChange={handleChange}
            placeholder="e.g., Albert Einstein"
            required
          />
          {errors.authorName && <span className="form-error">{errors.authorName}</span>}
        </div>

        <div className="form-row">
          <div className="form-group">
            <label htmlFor="authorLifespan" className="form-label">
              Lifespan (Optional)
            </label>
            <input
              type="text"
              id="authorLifespan"
              name="authorLifespan"
              className="form-input"
              value={formData.authorLifespan}
              onChange={handleChange}
              placeholder="e.g., 1879-1955"
            />
          </div>

          <div className="form-group">
            <label htmlFor="authorOccupation" className="form-label">
              Occupation (Optional)
            </label>
            <input
              type="text"
              id="authorOccupation"
              name="authorOccupation"
              className="form-input"
              value={formData.authorOccupation}
              onChange={handleChange}
              placeholder="e.g., Physicist"
            />
          </div>
        </div>
      </div>

      <div className="form-section">
        <h3 className="section-title">Source Information</h3>

        <div className="form-group">
          <label htmlFor="sourceTitle" className="form-label required">
            Source Title
          </label>
          <input
            type="text"
            id="sourceTitle"
            name="sourceTitle"
            className={`form-input ${errors.sourceTitle ? 'error' : ''}`}
            value={formData.sourceTitle}
            onChange={handleChange}
            placeholder="e.g., Relativity: The Special and General Theory"
            required
          />
          {errors.sourceTitle && <span className="form-error">{errors.sourceTitle}</span>}
        </div>

        <div className="form-row">
          <div className="form-group">
            <label htmlFor="sourceType" className="form-label required">
              Source Type
            </label>
            <select
              id="sourceType"
              name="sourceType"
              className={`form-select ${errors.sourceType ? 'error' : ''}`}
              value={formData.sourceType}
              onChange={handleChange}
              required
            >
              <option value="">Select type...</option>
              {sourceTypes.map((type) => (
                <option key={type} value={type}>
                  {type.charAt(0).toUpperCase() + type.slice(1)}
                </option>
              ))}
            </select>
            {errors.sourceType && <span className="form-error">{errors.sourceType}</span>}
          </div>

          <div className="form-group">
            <label htmlFor="sourceYear" className="form-label">
              Year (Optional)
            </label>
            <input
              type="number"
              id="sourceYear"
              name="sourceYear"
              className="form-input"
              value={formData.sourceYear}
              onChange={handleChange}
              placeholder="e.g., 1920"
              min="1000"
              max="2100"
            />
          </div>
        </div>

        <div className="form-group">
          <label htmlFor="sourceAdditionalInfo" className="form-label">
            Additional Info (Optional)
          </label>
          <input
            type="text"
            id="sourceAdditionalInfo"
            name="sourceAdditionalInfo"
            className="form-input"
            value={formData.sourceAdditionalInfo}
            onChange={handleChange}
            placeholder="e.g., Published by Methuen & Co Ltd"
          />
        </div>
      </div>

      <div className="form-section">
        <h3 className="section-title">Tags</h3>

        <div className="form-group">
          <label htmlFor="tagInput" className="form-label">
            Add Tags (Optional)
          </label>
          <div className="tag-input-container">
            <input
              type="text"
              id="tagInput"
              className="form-input"
              value={tagInput}
              onChange={(e) => setTagInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Type a tag and press Enter"
              disabled={formData.tags.length >= 20}
            />
            <button
              type="button"
              onClick={handleAddTag}
              className="tag-add-button"
              disabled={!tagInput.trim() || formData.tags.length >= 20}
            >
              Add
            </button>
          </div>
          <span className="form-hint">Maximum 20 tags, up to 50 characters each</span>
        </div>

        {formData.tags.length > 0 && (
          <div className="tags-list">
            {formData.tags.map((tag) => (
              <span key={tag} className="tag-item">
                {tag}
                <button
                  type="button"
                  onClick={() => handleRemoveTag(tag)}
                  className="tag-remove"
                  aria-label={`Remove tag ${tag}`}
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}
      </div>

      <div className="form-actions">
        {onCancel && (
          <button type="button" onClick={onCancel} className="button-secondary" disabled={isSubmitting}>
            Cancel
          </button>
        )}
        <button type="submit" className="button-primary" disabled={isSubmitting}>
          {isSubmitting ? 'Submitting...' : 'Submit Quotation'}
        </button>
      </div>
    </form>
  );
};