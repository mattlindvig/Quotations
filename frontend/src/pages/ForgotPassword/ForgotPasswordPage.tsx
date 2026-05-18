import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../../services/apiClient';
import './ForgotPasswordPage.css';

export const ForgotPasswordPage: React.FC = () => {
  const [email, setEmail] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await apiClient.post('/auth/forgot-password', { email });
    } catch {
      // Swallow errors — we show the same message regardless
    } finally {
      setSubmitted(true);
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fp-page">
      <div className="fp-card">
        <h1 className="fp-title">Forgot password</h1>

        {submitted ? (
          <div className="fp-success">
            <p>If an account exists for <strong>{email}</strong>, we've sent a password reset link. Check your inbox.</p>
            <Link to="/login" className="fp-back">Back to sign in</Link>
          </div>
        ) : (
          <>
            <p className="fp-description">Enter your email address and we'll send you a link to reset your password.</p>
            <form onSubmit={handleSubmit} className="fp-form" noValidate>
              <div className="form-group">
                <label htmlFor="fp-email">Email address</label>
                <input
                  id="fp-email"
                  type="email"
                  autoComplete="email"
                  required
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                />
              </div>
              <button type="submit" className="fp-submit" disabled={isSubmitting || !email}>
                {isSubmitting ? 'Sending…' : 'Send reset link'}
              </button>
            </form>
            <Link to="/login" className="fp-back">Back to sign in</Link>
          </>
        )}
      </div>
    </div>
  );
};
