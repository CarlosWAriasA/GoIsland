import React from 'react';
import { BrowserRouter } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import Navbar from './components/Navbar';
import Footer from './components/Footer';
import AppRoutes from './routes/AppRoutes';

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <AuthProvider>
        <>
          <div style={{
            display: 'flex',
            flexDirection: 'column',
            minHeight: '100vh',
          }}>
            <Navbar />

            <main className="container" style={{
              flex: '1 0 auto',
              paddingBottom: '40px',
            }}>
              <AppRoutes />
            </main>

            <Footer />
          </div>
          <Toaster
            position="top-right"
            toastOptions={{
              duration: 4000,
              style: {
                background: '#FFFFFF',
                color: '#333333',
                fontFamily: "'Poppins', sans-serif",
                fontSize: '0.9rem',
                borderRadius: '8px',
                border: '1px solid var(--color-border)',
                boxShadow: '0 4px 12px rgba(0, 0, 0, 0.08)',
                padding: '12px 16px',
              },
              success: {
                iconTheme: {
                  primary: '#0056B3',
                  secondary: '#FFFFFF',
                },
              },
              error: {
                iconTheme: {
                  primary: '#EF4444',
                  secondary: '#FFFFFF',
                },
              },
            }}
          />
        </>
      </AuthProvider>
    </BrowserRouter>
  );
};

export default App;
