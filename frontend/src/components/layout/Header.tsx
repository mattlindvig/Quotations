import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import './Header.css';

const Header = () => {
  const { isAuthenticated, user, logout, hasRole } = useAuth();
  const navigate = useNavigate();

  const goHome = (e: React.MouseEvent) => {
    e.preventDefault();
    navigate('/', { replace: true });
  };

  return (
    <header className="site-header">
      <nav className="site-nav">
        <div className="nav-links">
          <a href="/" className="nav-brand" onClick={goHome}>Quotations</a>
          <a href="/" className="nav-link" onClick={goHome}>Browse</a>
          <Link to="/random" className="nav-link">Random</Link>
          <Link to="/submit" className="nav-link">Submit</Link>
          {isAuthenticated && (
            <Link to="/my-submissions" className="nav-link">My Submissions</Link>
          )}
          {isAuthenticated && (
            <Link to="/favorites" className="nav-link">Favorites</Link>
          )}
          {(hasRole('Reviewer') || hasRole('Admin')) && (
            <Link to="/review" className="nav-link">Review</Link>
          )}
          {(hasRole('Reviewer') || hasRole('Admin')) && (
            <Link to="/ai-review" className="nav-link">AI Status</Link>
          )}
          {hasRole('Admin') && (
            <Link to="/admin/users" className="nav-link">Users</Link>
          )}
        </div>

        <div className="nav-user">
          {isAuthenticated ? (
            <>
              <span className="nav-welcome">Welcome, {user?.displayName}</span>
              <button className="nav-logout" onClick={logout}>Logout</button>
            </>
          ) : (
            <Link to="/login" className="nav-link">Login</Link>
          )}
        </div>
      </nav>
    </header>
  );
};

export default Header;
