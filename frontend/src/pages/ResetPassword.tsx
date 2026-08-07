import { LockKeyhole, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import Input from '../components/Input';
import AuthMosaic from '../components/AuthMosaic';
import Logo from '../components/Logo';
import ToastFeedback from '../components/ToastFeedback';
import { getFieldError, toApiError } from '../services/apiError';
import { authService } from '../services/authService';
import { getPasswordPolicyError, PASSWORD_POLICY_HINT } from '../utils/passwordPolicy';

interface ResetErrors {
  token?: string;
  newPassword?: string;
  confirmPassword?: string;
}

export const ResetPassword = () => {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token')?.trim() || '';
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<ResetErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const validate = () => {
    const errors: ResetErrors = {};
    if (!token) errors.token = 'El enlace de recuperación está incompleto.';
    if (!newPassword) errors.newPassword = 'La nueva contrasena es obligatoria.';
    else errors.newPassword = getPasswordPolicyError(newPassword);
    if (!confirmPassword) errors.confirmPassword = 'La confirmacion de la contrasena es obligatoria.';
    else if (newPassword !== confirmPassword) {
      errors.confirmPassword = 'La confirmacion no coincide con la nueva contrasena.';
    }
    setFieldErrors(errors);
    return Object.values(errors).every((message) => !message);
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    if (!validate()) return;

    setIsSubmitting(true);
    try {
      await authService.resetPassword({ token, newPassword, confirmPassword });
      setSuccess(true);
      setNewPassword('');
      setConfirmPassword('');
    } catch (requestError: unknown) {
      const apiError = toApiError(requestError);
      setFieldErrors({
        token: getFieldError(apiError, 'Token'),
        newPassword: getFieldError(apiError, 'NewPassword'),
        confirmPassword: getFieldError(apiError, 'ConfirmPassword'),
      });
      setError(apiError.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="auth-page animate-fade-in">
      <AuthMosaic />
      <section className="auth-card surface-panel" aria-labelledby="reset-password-title">
        <header className="auth-card__header">
          <span className="auth-card__brand"><Logo iconOnly fontSize="2.4rem" /></span>
          <h1 id="reset-password-title">Crea una nueva contraseña</h1>
          <p>Elige una contraseña diferente a la utilizada anteriormente.</p>
        </header>

        <ToastFeedback
          message={!token ? 'Este enlace de recuperación no es válido o está incompleto.' : null}
          tone="error"
        />
        <ToastFeedback message={fieldErrors.token} tone="error" />
        <ToastFeedback message={error} tone="error" />
        <ToastFeedback
          message={success ? 'Contraseña actualizada. Ya puedes iniciar sesión.' : null}
          tone="success"
        />
        {success && (
          <p>Ya puedes iniciar sesión con tu nueva contraseña.</p>
        )}

        {!success && (
          <form className="auth-form" onSubmit={handleSubmit} noValidate>
            <Input
              label="Nueva contraseña"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              error={fieldErrors.newPassword}
              hint={fieldErrors.newPassword ? undefined : PASSWORD_POLICY_HINT}
              icon={<LockKeyhole size={18} />}
              required
            />
            <Input
              label="Confirmar nueva contraseña"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              error={fieldErrors.confirmPassword}
              icon={<ShieldCheck size={18} />}
              required
            />
            <Button type="submit" fullWidth isLoading={isSubmitting} disabled={!token}>
              Restablecer contraseña
            </Button>
          </form>
        )}

        <p className="auth-card__footer">
          <Link to="/login">Volver a iniciar sesión</Link>
        </p>
      </section>
    </div>
  );
};

export default ResetPassword;
