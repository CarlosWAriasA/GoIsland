import { KeyRound, LockKeyhole, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, Navigate } from 'react-router-dom';
import Button from '../components/Button';
import Input from '../components/Input';
import ToastFeedback from '../components/ToastFeedback';
import { getFieldError, toApiError } from '../services/apiError';
import { authService } from '../services/authService';
import { useAuth } from '../hooks/useAuth';
import { getPasswordPolicyError, PASSWORD_POLICY_HINT } from '../utils/passwordPolicy';

interface ChangePasswordErrors {
  currentPassword?: string;
  newPassword?: string;
  confirmPassword?: string;
}

export const ChangePassword = () => {
  const { user, authenticationMethod } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<ChangePasswordErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (authenticationMethod === 'Google' || user?.hasPassword === false) {
    return <Navigate to="/profile" replace />;
  }

  const validate = () => {
    const errors: ChangePasswordErrors = {};
    if (!currentPassword) errors.currentPassword = 'Escribe tu contraseña actual.';
    if (!newPassword) errors.newPassword = 'Escribe la nueva contraseña.';
    else errors.newPassword = getPasswordPolicyError(newPassword);
    if (!confirmPassword) errors.confirmPassword = 'Repite la nueva contraseña.';
    else if (newPassword !== confirmPassword) {
      errors.confirmPassword = 'Las contraseñas no coinciden.';
    }
    setFieldErrors(errors);
    return Object.values(errors).every((message) => !message);
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setSuccess(null);
    if (!validate()) return;

    setIsSubmitting(true);
    try {
      await authService.changePassword({ currentPassword, newPassword, confirmPassword });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setFieldErrors({});
      setSuccess('Contraseña actualizada.');
    } catch (requestError: unknown) {
      const apiError = toApiError(requestError);
      setFieldErrors({
        currentPassword: getFieldError(apiError, 'CurrentPassword'),
        newPassword: getFieldError(apiError, 'NewPassword'),
        confirmPassword: getFieldError(apiError, 'ConfirmPassword'),
      });
      setError(apiError.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="container account-page animate-fade-in">
      <header className="page-heading">
        <span className="page-heading__eyebrow">Seguridad</span>
        <h1>Cambiar contraseña</h1>
        <p>Actualiza la contraseña utilizada para acceder a tu cuenta.</p>
      </header>

      <section className="account-form surface-panel" aria-labelledby="change-password-form-title">
        <div className="account-form__heading">
          <KeyRound aria-hidden="true" />
          <div>
            <h2 id="change-password-form-title">Credenciales de acceso</h2>
            <p>La nueva contraseña debe ser diferente a la actual.</p>
          </div>
        </div>

        <ToastFeedback message={success} tone="success" />
        <ToastFeedback message={error} tone="error" />

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <Input
            label="Contraseña actual"
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
            error={fieldErrors.currentPassword}
            icon={<LockKeyhole size={18} />}
            required
          />
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
          <div className="account-form__actions">
            <Link className="button-link button-link--outline" to="/profile">Volver al perfil</Link>
            <Button type="submit" isLoading={isSubmitting}>Guardar contraseña</Button>
          </div>
        </form>
      </section>
    </div>
  );
};

export default ChangePassword;
