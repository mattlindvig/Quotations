import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { useAuth } from './contexts/AuthContext';
import { FavoritesProvider } from './contexts/FavoritesContext';
import Header from './components/layout/Header';
import Footer from './components/layout/Footer';
import { BrowsePage } from './pages/Browse/BrowsePage';
import { SubmitPage } from './pages/Submit/SubmitPage';
import { MySubmissionsPage } from './pages/MySubmissions/MySubmissionsPage';
import { FavoritesPage } from './pages/Favorites/FavoritesPage';
import { ReviewQueuePage } from './pages/Review/ReviewQueuePage';
import { LoginPage } from './pages/Login/LoginPage';
import { ForgotPasswordPage } from './pages/ForgotPassword/ForgotPasswordPage';
import { ResetPasswordPage } from './pages/ResetPassword/ResetPasswordPage';
import { VerifyEmailPage } from './pages/VerifyEmail/VerifyEmailPage';
import AiReviewDashboardPage from './pages/AiReviewDashboard/AiReviewDashboardPage';
import { QuoteDetailPage } from './pages/Quote/QuoteDetailPage';
import { RandomPage } from './pages/Random/RandomPage';
import { MisattributedPage } from './pages/Misattributed/MisattributedPage';
import { ChatWidget } from './components/chat/ChatWidget';
import { UserManagementPage } from './pages/Admin/UserManagementPage';
import './App.css';

function BrowsePageWrapper() {
  return <BrowsePage />;
}

function AppContent() {
  const { isLoading } = useAuth();
  if (isLoading) return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>Loading…</div>;
  return (
    <Router>
      <div className="app">
        <Header />
        <main>
          <Routes>
            <Route path="/" element={<BrowsePageWrapper />} />
            <Route path="/browse" element={<BrowsePageWrapper />} />
            <Route path="/quote/:id" element={<QuoteDetailPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route path="/verify-email" element={<VerifyEmailPage />} />
            <Route path="/submit" element={<SubmitPage />} />
            <Route path="/my-submissions" element={<MySubmissionsPage />} />
            <Route path="/favorites" element={<FavoritesPage />} />
            <Route path="/review" element={<ReviewQueuePage />} />
            <Route path="/ai-review" element={<AiReviewDashboardPage />} />
            <Route path="/random" element={<RandomPage />} />
            <Route path="/misattributed" element={<MisattributedPage />} />
            <Route path="/admin/users" element={<UserManagementPage />} />
          </Routes>
        </main>
        <Footer />
        <ChatWidget />
      </div>
    </Router>
  );
}

function App() {
  return (
    <AuthProvider>
      <FavoritesProvider>
        <AppContent />
      </FavoritesProvider>
    </AuthProvider>
  );
}

export default App;
