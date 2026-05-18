import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { SubmissionForm } from '../../components/forms/SubmissionForm';
import './SubmitPage.css';

export const SubmitPage: React.FC = () => {
  const navigate = useNavigate();
  const { isAuthenticated, isLoading, hasRole } = useAuth();

  useEffect(() => {
    if (!isLoading && (!isAuthenticated || !hasRole('Admin'))) {
      navigate('/', { replace: true });
    }
  }, [isLoading, isAuthenticated, hasRole, navigate]);

  if (isLoading || !hasRole('Admin')) return null;

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