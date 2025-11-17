import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

const Header = () => {
  const { isAuthenticated, user, logout, hasRole } = useAuth();

  return (
    <header
      style={{
        padding: '1rem 2rem',
        backgroundColor: '#f8f9fa',
        borderBottom: '1px solid #dee2e6',
      }}
    >
      <nav style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', gap: '2rem', alignItems: 'center' }}>
          <Link to="/" style={{ fontSize: '1.5rem', fontWeight: 'bold', textDecoration: 'none' }}>
            Quotations
          </Link>
          <Link to="/" style={{ textDecoration: 'none' }}>
            Browse
          </Link>
          <Link to="/submit" style={{ textDecoration: 'none' }}>
            Submit
          </Link>
          {isAuthenticated && (
            <Link to="/my-submissions" style={{ textDecoration: 'none' }}>
              My Submissions
            </Link>
          )}
          {(hasRole('Reviewer') || hasRole('Admin')) && (
            <Link to="/review" style={{ textDecoration: 'none' }}>
              Review
            </Link>
          )}
        </div>
        <div>
          {isAuthenticated ? (
            <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
              <span>Welcome, {user?.displayName}</span>
              <button onClick={logout}>Logout</button>
            </div>
          ) : (
            <Link to="/login" style={{ textDecoration: 'none' }}>
              Login
            </Link>
          )}
        </div>
      </nav>
    </header>
  );
};

export default Header;
