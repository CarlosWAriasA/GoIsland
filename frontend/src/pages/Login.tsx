import { LockKeyhole, Mail } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { toast } from 'react-hot-toast';
import Button from '../components/Button';
import Input from '../components/Input';
import AuthMosaic from '../components/AuthMosaic';
import Logo from '../components/Logo';
import GoogleSignInButton from '../components/GoogleSignInButton';
import ToastFeedback from '../components/ToastFeedback';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { isGoogleAuthConfigured } from '../services/googleAuthConfig';

export const Login = () => {
  const { login, loginWithGoogle, isAuthenticated, isLoading, sessionExpired } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);

  const loggedOut = location.state?.loggedOut === true;
  const redirectMessage = !loggedOut && typeof location.state?.message === 'string'
    ? location.state.message
    : null;

  const requestedPath = typeof location.state?.from === 'string'
    && location.state.from.startsWith('/')
    && !location.state.from.startsWith('//')
    ? location.state.from
    : '/experiences';

  useEffect(() => {
    if (isAuthenticated && !loggedOut) navigate(requestedPath, { replace: true });
  }, [isAuthenticated, loggedOut, navigate, requestedPath]);

  const validate = () => {
    const errors: { email?: string; password?: string } = {};
    if (!email) errors.email = 'El correo electrónico es obligatorio.';
    else if (!/\S+@\S+\.\S+/.test(email)) errors.email = 'Introduce un formato de correo válido.';
    if (!password) errors.password = 'La contraseña es obligatoria.';
    else if (password.length < 6) errors.password = 'La contraseña debe tener al menos 6 caracteres.';
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    if (!validate()) return;

    try {
      await login({ email, password });
      toast.success('Sesión iniciada correctamente');
      navigate(requestedPath, { replace: true });
    } catch (error: unknown) {
      const apiError = toApiError(
        error,
        'Error al iniciar sesión. Por favor, verifica tus credenciales.',
      );
      setFieldErrors({
        email: getFieldError(apiError, 'Email'),
        password: getFieldError(apiError, 'Password'),
      });
      setFormError(apiError.message);
    }
  };

  const handleGoogleCredential = async (credential: string) => {
    setFormError(null);
    try {
      await loginWithGoogle(credential);
      toast.success('Sesión iniciada con Google.');
      navigate(requestedPath, { replace: true });
    } catch (error: unknown) {
      setFormError(toApiError(error, 'No fue posible iniciar sesión con Google.').message);
    }
  };

  return (
    <div className="auth-page animate-fade-in">
      <AuthMosaic />
      <section className="auth-card surface-panel" aria-labelledby="login-title">
        <header className="auth-card__header">
          <span className="auth-card__brand"><Logo iconOnly fontSize="2.4rem" /></span>
          <h1 id="login-title">Bienvenido de vuelta</h1>
          <p>Ingresa tus datos para acceder a tu cuenta.</p>
        </header>

        <ToastFeedback message={redirectMessage} tone="info" />
        <ToastFeedback
          message={!loggedOut && !redirectMessage && sessionExpired
            ? 'Tu sesión expiró. Inicia sesión nuevamente para continuar.'
            : null}
          tone="warning"
        />
        <ToastFeedback message={formError} tone="error" />

        {isGoogleAuthConfigured && (
          <>
            <GoogleSignInButton
              disabled={isLoading}
              onCredential={(credential) => void handleGoogleCredential(credential)}
              onError={setFormError}
            />
            <div className="auth-divider"><span>o continúa con correo</span></div>
          </>
        )}

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <Input
            label="Correo electrónico"
            type="email"
            autoComplete="email"
            placeholder="tu@correo.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            error={fieldErrors.email}
            icon={<Mail size={18} />}
            required
          />

          <Input
            label="Contraseña"
            type="password"
            autoComplete="current-password"
            placeholder="••••••••"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            error={fieldErrors.password}
            icon={<LockKeyhole size={18} />}
            required
          />

          <div className="auth-form__options">
            <Link to="/forgot-password" tabIndex={0}>
              ¿Olvidaste tu contraseña?
            </Link>
          </div>

          <Button type="submit" isLoading={isLoading} fullWidth>
            Iniciar sesión
          </Button>
        </form>

        <footer className="auth-card__footer">
          <p>
            ¿No tienes una cuenta? <Link to="/register">Crear cuenta</Link>
          </p>
        </footer>
      </section>
    </div>
  );
};

export default Login;
