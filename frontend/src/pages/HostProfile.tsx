import { BriefcaseBusiness, Phone, UserRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import TextAreaField from '../components/TextAreaField';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { hostService } from '../services/hostService';
import type { ApiError } from '../services/apiError';
import type { HostProfile as HostProfileType, HostProfileRequest } from '../types';
import { getModerationLabel, getModerationTone } from '../utils/moderationStatus';

const emptyForm: HostProfileRequest = {
  displayName: '',
  description: '',
  phoneNumber: '',
};

const toForm = (profile: HostProfileType): HostProfileRequest => ({
  displayName: profile.displayName,
  description: profile.description,
  phoneNumber: profile.phoneNumber,
});

export const HostProfile = () => {
  const { user, refreshUser } = useAuth();
  const [profile, setProfile] = useState<HostProfileType | null>(null);
  const [form, setForm] = useState<HostProfileRequest>(emptyForm);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [requestError, setRequestError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  const retryLoad = () => {
    setLoading(true);
    setLoadError(null);
    setRetryCount((current) => current + 1);
  };

  useEffect(() => {
    const controller = new AbortController();
    hostService.getMine(controller.signal)
      .then(async (current) => {
        setProfile(current);
        setForm(toForm(current));
        setLoadError(null);
        if (current.verificationStatus === 'Approved' && user?.role !== 'Host') {
          await refreshUser();
        }
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const apiError = toApiError(error);
        if (apiError.status === 404) {
          setProfile(null);
          setForm(emptyForm);
        } else {
          setLoadError(apiError.message);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [refreshUser, retryCount, user?.role]);

  const updateField = (field: keyof HostProfileRequest, value: string) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setRequestError(null);
    setSuccess(null);
    setSubmitting(true);
    try {
      const isApplication = !profile || profile.verificationStatus === 'Rejected';
      const updated = isApplication
        ? await hostService.apply(form)
        : await hostService.updateMine(form);
      setProfile(updated);
      setForm(toForm(updated));
      setSuccess(isApplication
        ? 'Tu solicitud fue enviada y está pendiente de revisión.'
        : 'Datos del perfil guardados.');
    } catch (error: unknown) {
      setRequestError(toApiError(error));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="container management-page" role="status">
        <Skeleton className="management-skeleton" />
        <span className="visually-hidden">Cargando perfil de anfitrión...</span>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="container management-page">
        <ErrorState description={loadError} onRetry={retryLoad} />
      </div>
    );
  }

  const canResubmit = profile?.verificationStatus === 'Rejected';

  return (
    <div className="container management-page animate-fade-in">
      <header className="page-heading">
        <span className="page-heading__eyebrow">Comunidad local</span>
        <h1>{profile ? 'Mi perfil de anfitrión' : 'Conviértete en anfitrión'}</h1>
        <p>Comparte experiencias auténticas después de una revisión administrativa.</p>
      </header>

      {profile && (
        <section className="management-summary surface-panel" aria-labelledby="host-status-title">
          <div>
            <span className="management-summary__eyebrow">Estado de verificación</span>
            <h2 id="host-status-title">{profile.displayName}</h2>
          </div>
          <StatusBadge tone={getModerationTone(profile.verificationStatus)}>
            {getModerationLabel(profile.verificationStatus)}
          </StatusBadge>
          {profile.rejectionReason && (
            <Alert tone="error"><strong>Motivo:</strong> {profile.rejectionReason}</Alert>
          )}
          {profile.verificationStatus === 'Approved' && (
            <Alert tone="success">
              Tu perfil está aprobado. Ya puedes administrar tus experiencias.
              {' '}<Link to="/host/experiences">Ir a mis experiencias</Link>
            </Alert>
          )}
          {profile.verificationStatus === 'Pending' && (
            <Alert tone="warning">La solicitud está en revisión; todavía no puedes publicar.</Alert>
          )}
          {profile.verificationStatus === 'Suspended' && (
            <Alert tone="error">El perfil está suspendido y no puede crear ni modificar experiencias.</Alert>
          )}
        </section>
      )}

      <section className="management-form surface-panel" aria-labelledby="host-form-title">
        <div className="management-form__heading">
          <BriefcaseBusiness aria-hidden="true" />
          <div>
            <h2 id="host-form-title">
              {!profile || canResubmit ? 'Datos de la solicitud' : 'Datos públicos'}
            </h2>
            <p>No envíes documentos de identidad ni información financiera.</p>
          </div>
        </div>

        {success && <Alert tone="success">{success}</Alert>}
        {requestError && <Alert tone="error">{requestError.message}</Alert>}

        <form onSubmit={handleSubmit} noValidate>
          <Input
            label="Nombre público"
            value={form.displayName}
            onChange={(event) => updateField('displayName', event.target.value)}
            error={requestError ? getFieldError(requestError, 'DisplayName') : undefined}
            icon={<UserRound size={18} />}
            maxLength={120}
            required
          />
          <TextAreaField
            label="Descripción de tu propuesta"
            value={form.description}
            onChange={(event) => updateField('description', event.target.value)}
            error={requestError ? getFieldError(requestError, 'Description') : undefined}
            hint="Describe tu experiencia local, enfoque y forma de recibir visitantes."
            rows={6}
            maxLength={1000}
            required
          />
          <Input
            label="Teléfono de contacto"
            type="tel"
            autoComplete="tel"
            value={form.phoneNumber}
            onChange={(event) => updateField('phoneNumber', event.target.value)}
            error={requestError ? getFieldError(requestError, 'PhoneNumber') : undefined}
            icon={<Phone size={18} />}
            maxLength={30}
            required
          />
          <Button type="submit" isLoading={submitting}>
            {!profile || canResubmit ? 'Enviar solicitud' : 'Guardar datos'}
          </Button>
        </form>
      </section>
    </div>
  );
};

export default HostProfile;
