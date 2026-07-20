import React, { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { toApiError } from '../services/apiError';
import Input from '../components/Input';
import Button from '../components/Button';
import Logo from '../components/Logo';
import { toast } from 'react-hot-toast';

export const Register: React.FC = () => {
  const { register, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();

  const [fullName, setFullName] = useState<string>('');
  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [confirmPassword, setConfirmPassword] = useState<string>('');
  const [fieldErrors, setFieldErrors] = useState<{
    fullName?: string;
    email?: string;
    password?: string;
    confirmPassword?: string;
  }>({});

  // Redirige si el usuario ya tiene sesión iniciada
  useEffect(() => {
    if (isAuthenticated) {
      navigate('/experiences');
    }
  }, [isAuthenticated, navigate]);

  const validate = (): boolean => {
    const errors: typeof fieldErrors = {};
    let isValid = true;

    if (!fullName.trim()) {
      errors.fullName = 'El nombre completo es obligatorio.';
      isValid = false;
    } else if (fullName.trim().length < 2 || fullName.trim().length > 120) {
      errors.fullName = 'El nombre completo debe tener entre 2 y 120 caracteres.';
      isValid = false;
    }

    if (!email) {
      errors.email = 'El correo electrónico es obligatorio.';
      isValid = false;
    } else if (!/\S+@\S+\.\S+/.test(email)) {
      errors.email = 'Introduce un formato de correo electrónico válido.';
      isValid = false;
    }

    if (!password) {
      errors.password = 'La contraseña es obligatoria.';
      isValid = false;
    } else if (password.length < 6 || password.length > 100) {
      errors.password = 'La contraseña debe tener entre 6 y 100 caracteres.';
      isValid = false;
    }

    if (password !== confirmPassword) {
      errors.confirmPassword = 'Las contraseñas no coinciden.';
      isValid = false;
    }

    setFieldErrors(errors);
    return isValid;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validate()) return;

    try {
      await register({
        fullName: fullName.trim(),
        email,
        password,
      });
      toast.success("¡Cuenta creada! Bienvenido a GoIsland 🌴");
      navigate('/experiences');
    } catch (err: unknown) {
      console.error(err);
      toast.error(toApiError(
        err,
        'Error al registrar usuario. Inténtalo de nuevo más tarde.',
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
          background-image: url("https://images.unsplash.com/photo-1527631746610-bca00a040d60?auto=format&fit=crop&w=1200&q=80");
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
          <p style={{ marginTop: '12px' }}>Únete a GoIsland para agendar tus viajes y experiencias.</p>
        </div>



        <form onSubmit={handleSubmit}>
          <Input
            label="Nombre Completo"
            placeholder="Juan Pérez"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            error={fieldErrors.fullName}
            icon={
              <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
              </svg>
            }
          />

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

          <Input
            label="Confirmar Contraseña"
            type="password"
            placeholder="••••••••"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            error={fieldErrors.confirmPassword}
            icon={
              <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
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
            Registrarse
          </Button>
        </form>

        <div style={{
          textAlign: 'center',
          marginTop: '24px',
          fontSize: '0.9rem',
          color: 'var(--text-secondary)',
        }}>
          ¿Ya tienes una cuenta?{' '}
          <Link to="/login" style={{ fontWeight: 600 }}>
            Inicia sesión aquí
          </Link>
        </div>
      </div>
    </div>
  );
};
export default Register;
