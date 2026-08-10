import { Mail, UserRound } from 'lucide-react';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import Button from '../components/Button';
import Input from '../components/Input';
import StatusBadge from '../components/StatusBadge';
import ToastFeedback from '../components/ToastFeedback';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { reservationKeys } from '../queries/queryKeys';

const getRoleLabel = (role: string) => {
  if (role === 'Host') return 'Anfitrión';
  return 'Turista';
};

const formatDate = (dateString?: string) => {
  if (!dateString) return 'No disponible';
  return new Date(dateString).toLocaleDateString('es-DO', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
};

export const Profile = () => {
  const { user, authenticationMethod, updateUser, isLoading } = useAuth();
  // Solo interesa el total, así que se pide la página mínima.
  const activityQuery = useQuery({
    queryKey: [...reservationKeys.all, 'count'],
    queryFn: ({ signal }) => reservationService.getMy({ pageSize: 1 }, signal),
  });
  const [fullName, setFullName] = useState(user?.fullName || '');
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>();

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSuccess(null);
    setError(null);
    setFieldError(undefined);

    const trimmedName = fullName.trim();
    if (!trimmedName) {
      setFieldError('El nombre completo es obligatorio.');
      return;
    }
    if (trimmedName.length < 2 || trimmedName.length > 120) {
      setFieldError('El nombre completo debe tener entre 2 y 120 caracteres.');
      return;
    }

    try {
      await updateUser(trimmedName);
      setSuccess('Perfil actualizado correctamente.');
    } catch (requestError: unknown) {
      const apiError = toApiError(requestError, 'Error al actualizar el perfil. Inténtalo de nuevo.');
      setFieldError(getFieldError(apiError, 'FullName'));
      setError(apiError.message);
    }
  };

  if (!user) return null;

  return (
    <div className="account-section">
      <div className="account-section__heading">
        <h2>Perfil</h2>
        <p>Así te identifican los anfitriones cuando reservas con ellos.</p>
      </div>

      <div className="profile-grid">
        <section className="profile-summary surface-panel" aria-labelledby="profile-summary-title">
          <div className="profile-avatar" aria-hidden="true">
            {user.fullName ? user.fullName.charAt(0).toUpperCase() : ''}
          </div>
          <h3 id="profile-summary-title">{user?.fullName}</h3>
          <div className="profile-summary__badges">
            <StatusBadge tone="info">{getRoleLabel(user?.role || '')}</StatusBadge>
            {user?.isAdmin && <StatusBadge tone="warning">Administrador</StatusBadge>}
          </div>
          <dl className="profile-details">
            <div>
              <dt>Correo electrónico</dt>
              <dd>{user?.email}</dd>
            </div>
            <div>
              <dt>Miembro desde</dt>
              <dd>{formatDate(user?.createdAt)}</dd>
            </div>
            <div>
              <dt>Reservas creadas</dt>
              <dd>
                {activityQuery.isPending
                  ? '—'
                  : activityQuery.data?.totalItems ?? 0}
              </dd>
            </div>
          </dl>
          {authenticationMethod === 'Google' || user?.hasPassword === false ? (
            <div className="profile-login-method">
              <span aria-hidden="true">G</span>
              <strong>Acceso con Google</strong>
            </div>
          ) : (
            <Link className="profile-password-link" to="/account/password">
              Cambiar contraseña
            </Link>
          )}
        </section>

        <section className="profile-form surface-panel" aria-labelledby="profile-form-title">
          <h3 id="profile-form-title">Datos personales</h3>
          <ToastFeedback message={success} tone="success" />
          <ToastFeedback message={error} tone="error" />

          <form onSubmit={handleSubmit} noValidate>
            <Input
              label="Nombre completo"
              autoComplete="name"
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              error={fieldError}
              icon={<UserRound size={18} />}
              required
            />
            <Input
              label="Correo electrónico"
              type="email"
              value={user?.email || ''}
              disabled
              icon={<Mail size={18} />}
            />
            <Button type="submit" fullWidth isLoading={isLoading}>Guardar cambios</Button>
          </form>
        </section>
      </div>
    </div>
  );
};

export default Profile;
