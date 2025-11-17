import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import Header from './components/layout/Header';
import Footer from './components/layout/Footer';
import { BrowsePage } from './pages/Browse/BrowsePage';
import { SubmitPage } from './pages/Submit/SubmitPage';
import { MySubmissionsPage } from './pages/MySubmissions/MySubmissionsPage';
import { ReviewQueuePage } from './pages/Review/ReviewQueuePage';
import './App.css';

function App() {
  return (
    <AuthProvider>
      <Router>
        <div className="app">
          <Header />
          <main>
            <Routes>
              <Route path="/" element={<BrowsePage />} />
              <Route path="/browse" element={<BrowsePage />} />
              <Route path="/submit" element={<SubmitPage />} />
              <Route path="/my-submissions" element={<MySubmissionsPage />} />
              <Route path="/review" element={<ReviewQueuePage />} />
            </Routes>
          </main>
          <Footer />
        </div>
      </Router>
    </AuthProvider>
  );
}

export default App;
