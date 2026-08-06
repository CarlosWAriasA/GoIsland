import { LockKeyhole, Mail, ShieldCheck, UserRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
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
import { getPasswordPolicyError, PASSWORD_POLICY_HINT } from '../utils/passwordPolicy';

interface RegisterErrors {
  fullName?: string;
  email?: string;
  password?: string;
  confirmPassword?: string;
}

export const Register = () => {
  const { register, loginWithGoogle, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<RegisterErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (isAuthenticated) navigate('/experiences');
  }, [isAuthenticated, navigate]);

  const validate = () => {
    const errors: RegisterErrors = {};
    const trimmedName = fullName.trim();
    if (!trimmedName) errors.fullName = 'El nombre completo es obligatorio.';
    else if (trimmedName.length < 2 || trimmedName.length > 120) {
      errors.fullName = 'El nombre completo debe tener entre 2 y 120 caracteres.';
    }
    if (!email) errors.email = 'El correo electrónico es obligatorio.';
    else if (!/\S+@\S+\.\S+/.test(email)) errors.email = 'Introduce un correo electrónico válido.';
    if (!password) errors.password = 'La contraseña es obligatoria.';
    else errors.password = getPasswordPolicyError(password);
    if (!confirmPassword) errors.confirmPassword = 'Confirma tu contraseña.';
    else if (password !== confirmPassword) errors.confirmPassword = 'Las contraseñas no coinciden.';
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    if (!validate()) return;

    try {
      await register({ fullName: fullName.trim(), email, password });
      toast.success('Cuenta creada. Bienvenido a GoIsland.');
      navigate('/experiences');
    } catch (error: unknown) {
      const apiError = toApiError(
        error,
        'Error al registrar usuario. Inténtalo de nuevo más tarde.',
      );
      setFieldErrors((current) => ({
        ...current,
        fullName: getFieldError(apiError, 'FullName'),
        email: getFieldError(apiError, 'Email'),
        password: getFieldError(apiError, 'Password'),
      }));
      setFormError(apiError.message);
    }
  };

  const handleGoogleCredential = async (credential: string) => {
    setFormError(null);
    try {
      await loginWithGoogle(credential);
      toast.success('Cuenta creada con Google. Bienvenido a GoIsland.');
      navigate('/experiences');
    } catch (error: unknown) {
      setFormError(toApiError(error, 'No fue posible continuar con Google.').message);
    }
  };

  return (
    <div className="auth-page animate-fade-in">
      <AuthMosaic />
      <section className="auth-card surface-panel" aria-labelledby="register-title">
        <header className="auth-card__header">
          <span className="auth-card__brand"><Logo iconOnly fontSize="2.4rem" /></span>
          <h1 id="register-title">Crea tu cuenta</h1>
          <p>Regístrate como turista para descubrir experiencias dominicanas.</p>
        </header>

        <ToastFeedback message={formError} tone="error" />

        {isGoogleAuthConfigured && (
          <>
            <GoogleSignInButton
              disabled={isLoading}
              onCredential={(credential) => void handleGoogleCredential(credential)}
              onError={setFormError}
            />
            <div className="auth-divider"><span>o regístrate con correo</span></div>
          </>
        )}

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <Input
            label="Nombre completo"
            autoComplete="name"
            placeholder="Juan Pérez"
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
            error={fieldErrors.fullName}
            icon={<UserRound size={18} />}
            required
          />
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
            autoComplete="new-password"
            placeholder="••••••••"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            error={fieldErrors.password}
            hint={fieldErrors.password ? undefined : PASSWORD_POLICY_HINT}
            icon={<LockKeyhole size={18} />}
            required
          />
          <Input
            label="Confirmar contraseña"
            type="password"
            autoComplete="new-password"
            placeholder="••••••••"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            error={fieldErrors.confirmPassword}
            icon={<ShieldCheck size={18} />}
            required
          />
          <Button type="submit" fullWidth isLoading={isLoading}>Crear cuenta</Button>
        </form>

        <p className="auth-card__footer">
          ¿Ya tienes una cuenta? <Link to="/login">Inicia sesión</Link>
        </p>
      </section>
    </div>
  );
};

export default Register;
