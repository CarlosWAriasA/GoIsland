import { Mail, UserRound } from 'lucide-react';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import Input from '../components/Input';
import StatusBadge from '../components/StatusBadge';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';

const getRoleLabel = (role: string) => {
  if (role === 'Admin' || role === 'Administrator') return 'Administrador';
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
  const { user, updateUser, isLoading } = useAuth();
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

  return (
    <div className="container profile-page animate-fade-in">
      <header className="page-heading">
        <span className="page-heading__eyebrow">Tu cuenta</span>
        <h1>Mi perfil</h1>
        <p>Consulta tu identidad y mantén actualizado tu nombre.</p>
      </header>

      <div className="profile-grid">
        <section className="profile-summary surface-panel" aria-labelledby="profile-summary-title">
          <div className="profile-avatar" aria-hidden="true">
            {user?.fullName.charAt(0).toUpperCase()}
          </div>
          <h2 id="profile-summary-title">{user?.fullName}</h2>
          <StatusBadge tone="info">{getRoleLabel(user?.role || '')}</StatusBadge>
          <dl className="profile-details">
            <div>
              <dt>Correo electrónico</dt>
              <dd>{user?.email}</dd>
            </div>
            <div>
              <dt>Miembro desde</dt>
              <dd>{formatDate(user?.createdAt)}</dd>
            </div>
          </dl>
          <Link className="profile-password-link" to="/account/password">
            Cambiar contraseña
          </Link>
        </section>

        <section className="profile-form surface-panel" aria-labelledby="profile-form-title">
          <h2 id="profile-form-title">Datos personales</h2>
          <p>El correo identifica tu cuenta y no puede modificarse.</p>
          {success && <Alert tone="success">{success}</Alert>}
          {error && <Alert tone="error">{error}</Alert>}

          <form onSubmit={handleSubmit} noValidate>
            <Input
              label="Nombre completo"
              autoComplete="name"
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              error={fieldError}
              icon={<UserRound size={18} />}
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
