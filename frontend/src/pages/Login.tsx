import React, { useState, useEffect } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { toApiError } from '../services/apiError';
import Input from '../components/Input';
import Button from '../components/Button';
import Logo from '../components/Logo';
import { toast } from 'react-hot-toast';

export const Login: React.FC = () => {
  const { login, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});

  // Mensaje opcional recibido desde el estado de navegación de otra ruta
  const redirectMessage = location.state?.message || null;

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/experiences');
    }
  }, [isAuthenticated, navigate]);

  const validate = (): boolean => {
    const errors: { email?: string; password?: string } = {};
    let isValid = true;

    if (!email) {
      errors.email = 'El correo electrónico es obligatorio.';
      isValid = false;
    } else if (!/\S+@\S+\.\S+/.test(email)) {
      errors.email = 'Introduce un formato de correo válido.';
      isValid = false;
    }

    if (!password) {
      errors.password = 'La contraseña es obligatoria.';
      isValid = false;
    } else if (password.length < 6) {
      errors.password = 'La contraseña debe tener al menos 6 caracteres.';
      isValid = false;
    }

    setFieldErrors(errors);
    return isValid;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validate()) return;

    try {
      await login({ email, password });
      toast.success("Sesión iniciada correctamente");
      navigate('/experiences');
    } catch (err: unknown) {
      console.error(err);
      toast.error(toApiError(
        err,
        'Error al iniciar sesión. Por favor, verifica tus credenciales.',
      ).message);
    }
  };

  return (
    <div className="full-screen-bg animate-fade-in">
      <style>{`
        .full-screen-bg {
          width: 100vw;
          min-height: calc(100vh - 70px);
          position: relative;
          left: 50%;
          right: 50%;
          margin-left: -50vw;
          margin-right: -50vw;
          margin-bottom: -40px;
          background-image: url("https://images.unsplash.com/photo-1506929562872-bb421503ef21?auto=format&fit=crop&w=1200&q=80");
          background-size: cover;
          background-position: center;
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 40px 16px;
        }
        .bg-overlay {
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background-color: rgba(0, 30, 60, 0.45);
          z-index: 1;
        }
        .auth-card {
          position: relative;
          z-index: 2;
          width: 100%;
          max-width: 480px;
          background: var(--color-white);
          border-radius: 8px;
          box-shadow: 0 20px 40px rgba(0, 0, 0, 0.25);
          padding: 48px;
        }
        @media (max-width: 480px) {
          .auth-card {
            padding: 32px 20px;
          }
        }
      `}</style>
      <div className="bg-overlay" />
      <div className="auth-card">
        <div style={{ textAlign: 'center', marginBottom: '32px' }}>
          <Logo showUnderline fontSize="2.2rem" style={{ marginBottom: '16px' }} />
          <p style={{ marginTop: '12px' }}>Ingresa tus datos para acceder a tu cuenta.</p>
        </div>

        {redirectMessage && (
          <div style={{
            background: '#EAF1FB',
            border: '1px solid var(--color-primary)',
            padding: '12px 16px',
            borderRadius: '8px',
            color: 'var(--color-primary)',
            fontSize: '0.85rem',
            marginBottom: '20px',
            textAlign: 'center',
            fontWeight: 500,
          }}>
            {redirectMessage}
          </div>
        )}



        <form onSubmit={handleSubmit}>
          <Input
            label="Correo Electrónico"
            type="email"
            placeholder="tu@correo.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={fieldErrors.email}
            icon={
              <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
              </svg>
            }
          />

          <Input
            label="Contraseña"
            type="password"
            placeholder="••••••••"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={fieldErrors.password}
            icon={
              <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
            }
          />

          <Button
            type="submit"
            variant="primary"
            fullWidth
            isLoading={isLoading}
            style={{ marginTop: '10px' }}
          >
            Iniciar Sesión
          </Button>
        </form>

        <div style={{
          textAlign: 'center',
          marginTop: '24px',
          fontSize: '0.9rem',
          color: 'var(--text-secondary)',
        }}>
          ¿No tienes una cuenta?{' '}
          <Link to="/register" style={{ fontWeight: 600 }}>
            Regístrate aquí
          </Link>
        </div>
      </div>
    </div>
  );
};
export default Login;
