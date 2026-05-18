import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../../services/apiClient';
import './ResetPasswordPage.css';

export const ResetPasswordPage: React.FC = () => {
  const token = new URLSearchParams(window.location.hash.slice(1)).get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (password.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }
    if (password !== confirm) {
      setError('Passwords do not match.');
      return;
    }

    setIsSubmitting(true);
    try {
      await apiClient.post('/auth/reset-password', { token, newPassword: password });
      setSuccess(true);
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { errors?: { general?: string[] } } } })
        ?.response?.data?.errors?.general?.[0];
      setError(detail ?? 'This reset link is invalid or has expired.');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!token) {
    return (
      <div className="rp-page">
        <div className="rp-card">
          <h1 className="rp-title">Invalid link</h1>
          <p className="rp-description">This password reset link is missing a token. Please request a new one.</p>
          <Link to="/forgot-password" className="rp-link">Request a new reset link</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="rp-page">
      <div className="rp-card">
        <h1 className="rp-title">Choose a new password</h1>

        {success ? (
          <div>
            <p className="rp-success-msg">Your password has been reset. You can now sign in with your new password.</p>
            <Link to="/login" className="rp-link">Go to sign in</Link>
          </div>
        ) : (
          <>
            {error && <p className="rp-error" role="alert">{error}</p>}
            <form onSubmit={handleSubmit} className="rp-form" noValidate>
              <div className="form-group">
                <label htmlFor="rp-password">New password <span className="rp-hint">(min 8 chars, upper, lower, number/symbol)</span></label>
                <input
                  id="rp-password"
                  type="password"
                  autoComplete="new-password"
                  required
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="rp-confirm">Confirm password</label>
                <input
                  id="rp-confirm"
                  type="password"
                  autoComplete="new-password"
                  required
                  value={confirm}
                  onChange={e => setConfirm(e.target.value)}
                />
              </div>
              <button type="submit" className="rp-submit" disabled={isSubmitting || !password || !confirm}>
                {isSubmitting ? 'Resetting…' : 'Reset password'}
              </button>
            </form>
          </>
        )}
      </div>
    </div>
  );
};
