import React, { useState } from 'react';
import { useAuth } from '../hooks/useAuth';
import { toApiError } from '../services/apiError';
import Input from '../components/Input';
import Button from '../components/Button';

export const Profile: React.FC = () => {
  const { user, updateUser, isLoading } = useAuth();

  const [fullName, setFullName] = useState<string>(user?.fullName || '');
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSuccess(null);
    setError(null);
    setFieldError(null);

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
      setSuccess('¡Perfil actualizado con éxito!');
      setTimeout(() => setSuccess(null), 4000);
    } catch (err: unknown) {
      console.error(err);
      setError(toApiError(err, 'Error al actualizar el perfil. Inténtalo de nuevo.').message);
    }
  };

  const getRoleLabel = (role: string) => {
    switch (role) {
      case 'Admin':
      case 'Administrator':
        return 'Administrador';
      case 'Host':
        return 'Anfitrión (Proveedor)';
      default:
        return 'Turista (Viajero)';
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/D';
    const date = new Date(dateString);
    return date.toLocaleDateString('es-ES', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  return (
    <div style={{ padding: '40px 0' }} className="animate-fade-in">
      <div style={{
        maxWidth: '800px',
        margin: '0 auto',
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
        gap: '30px',
      }}>
        <div className="glass-panel" style={{
          padding: '40px',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          textAlign: 'center',
        }}>
          <div style={{
            width: '90px',
            height: '90px',
            background: 'var(--color-primary)',
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: '2.5rem',
            fontWeight: 800,
            color: '#fff',
            fontFamily: "'Poppins', sans-serif",
            marginBottom: '20px',
          }}>
            {user?.fullName.charAt(0).toUpperCase()}
          </div>

          <h2 style={{ fontSize: '1.6rem', marginBottom: '4px' }}>{user?.fullName}</h2>
          <span style={{
            fontSize: '0.8rem',
            color: 'var(--color-primary)',
            background: '#EAF1FB',
            border: '1px solid var(--color-primary)',
            padding: '4px 12px',
            borderRadius: '8px',
            fontWeight: 600,
            textTransform: 'uppercase',
            letterSpacing: '0.05em',
            marginBottom: '24px',
          }}>
            {getRoleLabel(user?.role || '')}
          </span>

          <div style={{
            width: '100%',
            borderTop: '1px solid rgba(255, 255, 255, 0.06)',
            paddingTop: '20px',
            textAlign: 'left',
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}>
            <div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'block' }}>
                Correo Electrónico
              </span>
              <span style={{ fontSize: '0.95rem', color: 'var(--text-primary)', fontWeight: 500 }}>
                {user?.email}
              </span>
            </div>

            <div>
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', display: 'block' }}>
                Miembro Desde
              </span>
              <span style={{ fontSize: '0.95rem', color: 'var(--text-primary)', fontWeight: 500 }}>
                {formatDate(user?.createdAt)}
              </span>
            </div>
          </div>
        </div>

        <div className="glass-panel" style={{ padding: '40px' }}>
          <h3 style={{ fontSize: '1.4rem', marginBottom: '8px' }}>Configuración de Cuenta</h3>
          <p style={{ marginBottom: '24px' }}>Actualiza tus datos de contacto personales.</p>

          {success && (
            <div style={{
              background: 'rgba(16, 185, 129, 0.08)',
              border: '1px solid rgba(16, 185, 129, 0.2)',
              padding: '12px 16px',
              borderRadius: '8px',
              color: '#34d399',
              fontSize: '0.85rem',
              marginBottom: '20px',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              fontWeight: 500,
            }}>
              <svg style={{ width: '16px', height: '16px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              {success}
            </div>
          )}

          {error && (
            <div style={{
              background: 'rgba(239, 68, 68, 0.08)',
              border: '1px solid rgba(239, 68, 68, 0.2)',
              padding: '12px 16px',
              borderRadius: '8px',
              color: '#f87171',
              fontSize: '0.85rem',
              marginBottom: '20px',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              fontWeight: 500,
            }}>
              <svg style={{ width: '16px', height: '16px' }} fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <Input
              label="Nombre Completo"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              error={fieldError || undefined}
              icon={
                <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
              }
            />

            <Input
              label="Correo Electrónico (No modificable)"
              type="email"
              value={user?.email || ''}
              disabled
              style={{ opacity: 0.65 }}
              icon={
                <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              }
            />

            <Button
              type="submit"
              variant="primary"
              isLoading={isLoading}
              style={{ width: '100%', marginTop: '10px' }}
            >
              Guardar Cambios
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
};
export default Profile;
