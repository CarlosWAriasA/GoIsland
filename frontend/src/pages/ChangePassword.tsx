import { KeyRound, LockKeyhole, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import Input from '../components/Input';
import { getFieldError, toApiError } from '../services/apiError';
import { authService } from '../services/authService';

interface ChangePasswordErrors {
  currentPassword?: string;
  newPassword?: string;
  confirmPassword?: string;
}

export const ChangePassword = () => {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<ChangePasswordErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const validate = () => {
    const errors: ChangePasswordErrors = {};
    if (!currentPassword) errors.currentPassword = 'La contrasena actual es obligatoria.';
    if (!newPassword) errors.newPassword = 'La nueva contrasena es obligatoria.';
    else if (newPassword.length < 6 || newPassword.length > 100) {
      errors.newPassword = 'La nueva contrasena debe tener entre 6 y 100 caracteres.';
    }
    if (!confirmPassword) errors.confirmPassword = 'La confirmacion de la contrasena es obligatoria.';
    else if (newPassword !== confirmPassword) {
      errors.confirmPassword = 'La confirmacion no coincide con la nueva contrasena.';
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
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
      setSuccess('Contraseña actualizada correctamente.');
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

        {success && <Alert tone="success">{success}</Alert>}
        {error && <Alert tone="error">{error}</Alert>}

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <Input
            label="Contraseña actual"
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.target.value)}
            error={fieldErrors.currentPassword}
            icon={<LockKeyhole size={18} />}
          />
          <Input
            label="Nueva contraseña"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            error={fieldErrors.newPassword}
            icon={<LockKeyhole size={18} />}
          />
          <Input
            label="Confirmar nueva contraseña"
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            error={fieldErrors.confirmPassword}
            icon={<ShieldCheck size={18} />}
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
