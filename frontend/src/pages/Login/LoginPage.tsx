import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import './LoginPage.css';

type Tab = 'login' | 'register';

export const LoginPage: React.FC = () => {
  const { login, register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string })?.from ?? '/';

  const [tab, setTab] = useState<Tab>('login');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [loginForm, setLoginForm] = useState({ username: '', password: '' });
  const [registerForm, setRegisterForm] = useState({
    username: '',
    email: '',
    password: '',
    displayName: '',
  });

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsSubmitting(true);
    try {
      await login(loginForm.username, loginForm.password);
      navigate(from, { replace: true });
    } catch {
      setError('Invalid username or password.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (registerForm.password.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }
    setIsSubmitting(true);
    try {
      await register(
        registerForm.username,
        registerForm.email,
        registerForm.password,
        registerForm.displayName
      );
      navigate(from, { replace: true });
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : 'Registration failed. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <h1 className="login-title">Quotations</h1>

        <div className="login-tabs" role="tablist">
          <button
            role="tab"
            aria-selected={tab === 'login'}
            className={`login-tab ${tab === 'login' ? 'active' : ''}`}
            onClick={() => { setTab('login'); setError(''); }}
          >
            Sign in
          </button>
          <button
            role="tab"
            aria-selected={tab === 'register'}
            className={`login-tab ${tab === 'register' ? 'active' : ''}`}
            onClick={() => { setTab('register'); setError(''); }}
          >
            Create account
          </button>
        </div>

        {error && (
          <p className="login-error" role="alert">{error}</p>
        )}

        {tab === 'login' ? (
          <form onSubmit={handleLogin} className="login-form" noValidate>
            <div className="form-group">
              <label htmlFor="login-username">Username</label>
              <input
                id="login-username"
                type="text"
                autoComplete="username"
                required
                value={loginForm.username}
                onChange={e => setLoginForm(f => ({ ...f, username: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label htmlFor="login-password">Password</label>
              <input
                id="login-password"
                type="password"
                autoComplete="current-password"
                required
                value={loginForm.password}
                onChange={e => setLoginForm(f => ({ ...f, password: e.target.value }))}
              />
            </div>
            <button type="submit" className="login-submit" disabled={isSubmitting}>
              {isSubmitting ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        ) : (
          <form onSubmit={handleRegister} className="login-form" noValidate>
            <div className="form-group">
              <label htmlFor="reg-username">Username</label>
              <input
                id="reg-username"
                type="text"
                autoComplete="username"
                required
                minLength={3}
                maxLength={50}
                value={registerForm.username}
                onChange={e => setRegisterForm(f => ({ ...f, username: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label htmlFor="reg-email">Email</label>
              <input
                id="reg-email"
                type="email"
                autoComplete="email"
                required
                value={registerForm.email}
                onChange={e => setRegisterForm(f => ({ ...f, email: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label htmlFor="reg-displayname">Display name <span className="optional">(optional)</span></label>
              <input
                id="reg-displayname"
                type="text"
                autoComplete="name"
                value={registerForm.displayName}
                onChange={e => setRegisterForm(f => ({ ...f, displayName: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label htmlFor="reg-password">Password <span className="optional">(min 8 characters)</span></label>
              <input
                id="reg-password"
                type="password"
                autoComplete="new-password"
                required
                minLength={8}
                value={registerForm.password}
                onChange={e => setRegisterForm(f => ({ ...f, password: e.target.value }))}
              />
            </div>
            <button type="submit" className="login-submit" disabled={isSubmitting}>
              {isSubmitting ? 'Creating account…' : 'Create account'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
