import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../../services/apiClient';
import './VerifyEmailPage.css';

type Status = 'verifying' | 'success' | 'error';

export const VerifyEmailPage: React.FC = () => {
  const token = new URLSearchParams(window.location.hash.slice(1)).get('token') ?? '';
  const [status, setStatus] = useState<Status>('verifying');

  useEffect(() => {
    if (!token) {
      setStatus('error');
      return;
    }

    apiClient.post('/auth/verify-email', { token })
      .then(() => setStatus('success'))
      .catch(() => setStatus('error'));
  }, [token]);

  return (
    <div className="ve-page">
      <div className="ve-card">
        {status === 'verifying' && (
          <p className="ve-message">Verifying your email…</p>
        )}

        {status === 'success' && (
          <>
            <h1 className="ve-title">Email verified</h1>
            <p className="ve-message">Your email address has been verified. Thanks for confirming!</p>
            <Link to="/" className="ve-link">Go to the app</Link>
          </>
        )}

        {status === 'error' && (
          <>
            <h1 className="ve-title">Verification failed</h1>
            <p className="ve-message">This verification link is invalid or has expired. You can request a new one from the sign-in page.</p>
            <Link to="/login" className="ve-link">Back to sign in</Link>
          </>
        )}
      </div>
    </div>
  );
};
