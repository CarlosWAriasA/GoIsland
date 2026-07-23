import React from 'react';
import type { MouseEvent } from 'react';
import { BrowserRouter } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import Navbar from './components/Navbar';
import Footer from './components/Footer';
import OfflineBanner from './components/OfflineBanner';
import AppRoutes from './routes/AppRoutes';

const focusMainContent = (event: MouseEvent<HTMLAnchorElement>) => {
  event.preventDefault();
  const mainContent = document.getElementById('main-content');
  mainContent?.focus();
  mainContent?.scrollIntoView();
  window.history.replaceState(null, '', '#main-content');
};

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <AuthProvider>
        <>
          <a className="skip-link" href="#main-content" onClick={focusMainContent}>
            Saltar al contenido principal
          </a>
          <div className="app-shell">
            <Navbar />
            <OfflineBanner />

            <main id="main-content" tabIndex={-1} className="app-main">
              <AppRoutes />
            </main>

            <Footer />
          </div>
          <Toaster
            position="top-right"
            toastOptions={{
              duration: 4000,
              className: 'app-toast',
            }}
          />
        </>
      </AuthProvider>
    </BrowserRouter>
  );
};

export default App;
