import React from 'react';
import { useNavigate } from 'react-router-dom';
import { SubmissionForm } from '../../components/forms/SubmissionForm';
import './SubmitPage.css';

/**
 * Submit page - allows users to submit new quotations
 */
export const SubmitPage: React.FC = () => {
  const navigate = useNavigate();

  const handleSuccess = () => {
    // Navigate to browse page after success message is shown
    navigate('/');
  };

  const handleCancel = () => {
    // Navigate back to browse page
    navigate('/');
  };

  return (
    <div className="submit-page">
      <div className="submit-header">
        <h1>Submit a Quotation</h1>
        <p className="submit-description">
          Share your favorite quotations with the community. All submissions are reviewed before
          being published to ensure quality and accuracy.
        </p>
      </div>

      <SubmissionForm onSuccess={handleSuccess} onCancel={handleCancel} />
    </div>
  );
};