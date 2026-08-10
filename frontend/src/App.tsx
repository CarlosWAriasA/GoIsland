import React from 'react';
import { useEffect } from 'react';
import { BrowserRouter, useLocation, useNavigationType } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import Navbar from './components/Navbar';
import Footer from './components/Footer';
import OfflineBanner from './components/OfflineBanner';
import ErrorBoundary from './components/ErrorBoundary';
import AppRoutes from './routes/AppRoutes';

const RouteFocusManager = () => {
  const { pathname } = useLocation();
  const navigationType = useNavigationType();

  useEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      document.getElementById('main-content')?.focus({ preventScroll: true });
      if (navigationType !== 'POP') window.scrollTo({ top: 0, behavior: 'auto' });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [navigationType, pathname]);

  return null;
};

// La ruta activa vive dentro del router, así que el límite de error se monta aquí para poder
// olvidar el fallo en cuanto la persona navega a otra pantalla.
const RoutedContent = () => {
  const { pathname } = useLocation();

  return (
    <ErrorBoundary resetKey={pathname}>
      <RouteFocusManager />
      <AppRoutes />
    </ErrorBoundary>
  );
};

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <AuthProvider>
        <>
          <a className="skip-link" href="#main-content">
            Saltar al contenido principal
          </a>
          <div className="app-shell">
            <Navbar />
            <OfflineBanner />

            <main id="main-content" tabIndex={-1} className="app-main">
              <RoutedContent />
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
