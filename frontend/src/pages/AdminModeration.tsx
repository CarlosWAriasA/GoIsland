import { BadgeCheck, BriefcaseBusiness, MapPin, ShieldCheck, UserRoundX } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { hostService } from '../services/hostService';
import type {
  ExperienceApprovalStatus,
  HostProfile,
  HostVerificationStatus,
  ManagedExperience,
} from '../types';
import { getModerationLabel, getModerationTone } from '../utils/moderationStatus';

type HostAction = 'approve' | 'reject' | 'suspend';
type ExperienceAction = HostAction;
type HostFilter = HostVerificationStatus | 'All';
type ExperienceFilter = ExperienceApprovalStatus | 'All';

const actionPastParticiple: Record<HostAction, string> = {
  approve: 'aprobada',
  reject: 'rechazada',
  suspend: 'suspendida',
};

const requireReason = (action: HostAction) => {
  if (action === 'approve') return undefined;
  const label = action === 'reject' ? 'rechazo' : 'suspensión';
  const reason = window.prompt(`Indica el motivo del ${label}:`)?.trim();
  return reason || null;
};

export const AdminModeration = () => {
  const [applications, setApplications] = useState<HostProfile[]>([]);
  const [experiences, setExperiences] = useState<ManagedExperience[]>([]);
  const [hostFilter, setHostFilter] = useState<HostFilter>('Pending');
  const [experienceFilter, setExperienceFilter] = useState<ExperienceFilter>('PendingReview');
  const [loading, setLoading] = useState(true);
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  const retryLoad = () => {
    setLoading(true);
    setError(null);
    setRetryCount((current) => current + 1);
  };

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      hostService.getApplications(undefined, controller.signal),
      hostService.getExperiencesForAdmin(undefined, controller.signal),
    ])
      .then(([hostData, experienceData]) => {
        setApplications(hostData);
        setExperiences(experienceData);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(requestError).message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [retryCount]);

  const visibleApplications = useMemo(() => applications.filter(
    (item) => hostFilter === 'All' || item.verificationStatus === hostFilter,
  ), [applications, hostFilter]);
  const visibleExperiences = useMemo(() => experiences.filter(
    (item) => experienceFilter === 'All' || item.approvalStatus === experienceFilter,
  ), [experienceFilter, experiences]);

  const decideHost = async (profile: HostProfile, action: HostAction) => {
    const reason = requireReason(action);
    if (reason === null) return;
    setBusyKey(`host-${profile.id}`);
    setError(null);
    setSuccess(null);
    try {
      const updated = await hostService.decideApplication(profile.id, action, reason);
      setApplications((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess(`La solicitud de ${profile.displayName} fue ${actionPastParticiple[action]}.`);
    } catch (requestError: unknown) {
      setError(toApiError(requestError).message);
    } finally {
      setBusyKey(null);
    }
  };

  const decideExperience = async (experience: ManagedExperience, action: ExperienceAction) => {
    const reason = requireReason(action);
    if (reason === null) return;
    setBusyKey(`experience-${experience.id}`);
    setError(null);
    setSuccess(null);
    try {
      const updated = await hostService.decideExperience(experience.id, action, reason);
      setExperiences((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess(`La experiencia “${experience.title}” fue ${actionPastParticiple[action]}.`);
    } catch (requestError: unknown) {
      setError(toApiError(requestError).message);
    } finally {
      setBusyKey(null);
    }
  };

  if (loading) {
    return (
      <div className="container management-page" role="status">
        <Skeleton className="management-skeleton" />
        <Skeleton className="management-skeleton" />
        <span className="visually-hidden">Cargando moderación...</span>
      </div>
    );
  }

  if (error && applications.length === 0 && experiences.length === 0) {
    return (
      <div className="container management-page">
        <ErrorState description={error} onRetry={retryLoad} />
      </div>
    );
  }

  return (
    <div className="container management-page animate-fade-in">
      <header className="page-heading">
        <span className="page-heading__eyebrow">Administración</span>
        <h1>Moderación</h1>
        <p>Valida identidad operativa, propiedad y contenido antes de publicar.</p>
      </header>

      {success && <Alert tone="success">{success}</Alert>}
      {error && <Alert tone="error">{error}</Alert>}

      <section className="moderation-section" aria-labelledby="host-moderation-title">
        <div className="moderation-section__heading">
          <div>
            <span className="page-heading__eyebrow">Verificación de identidad</span>
            <h2 id="host-moderation-title">Solicitudes de anfitrión</h2>
          </div>
          <SelectField
            label="Filtrar solicitudes"
            value={hostFilter}
            onChange={(event) => setHostFilter(event.target.value as HostFilter)}
          >
            <option value="Pending">Pendientes</option>
            <option value="Approved">Aprobadas</option>
            <option value="Rejected">Rechazadas</option>
            <option value="Suspended">Suspendidas</option>
            <option value="All">Todas</option>
          </SelectField>
        </div>

        {visibleApplications.length === 0 ? (
          <EmptyState title="No hay solicitudes en este estado" description="Cambia el filtro para consultar el historial." />
        ) : (
          <div className="management-list">
            {visibleApplications.map((profile) => (
              <article className="management-card surface-panel" key={profile.id}>
                <div className="management-card__header">
                  <div>
                    <span className="management-card__reference">Solicitud #{profile.id}</span>
                    <h3>{profile.displayName}</h3>
                    <p>{profile.userFullName} · {profile.userEmail}</p>
                  </div>
                  <StatusBadge tone={getModerationTone(profile.verificationStatus)}>
                    {getModerationLabel(profile.verificationStatus)}
                  </StatusBadge>
                </div>
                <p className="management-card__description">{profile.description}</p>
                <dl className="management-card__facts">
                  <div><dt>Teléfono</dt><dd>{profile.phoneNumber}</dd></div>
                  <div><dt>Usuario</dt><dd>#{profile.userId}</dd></div>
                  <div><dt>Enviada</dt><dd>{new Date(profile.submittedAt).toLocaleDateString('es-DO')}</dd></div>
                </dl>
                {profile.rejectionReason && <Alert tone="error">{profile.rejectionReason}</Alert>}
                <div className="management-actions">
                  {profile.verificationStatus === 'Pending' && (
                    <>
                      <Button
                        onClick={() => void decideHost(profile, 'approve')}
                        isLoading={busyKey === `host-${profile.id}`}
                      ><BadgeCheck size={17} aria-hidden="true" />Aprobar</Button>
                      <Button
                        variant="danger"
                        onClick={() => void decideHost(profile, 'reject')}
                        disabled={busyKey === `host-${profile.id}`}
                      ><UserRoundX size={17} aria-hidden="true" />Rechazar</Button>
                    </>
                  )}
                  {profile.verificationStatus === 'Approved' && (
                    <Button
                      variant="danger"
                      onClick={() => void decideHost(profile, 'suspend')}
                      isLoading={busyKey === `host-${profile.id}`}
                    ><UserRoundX size={17} aria-hidden="true" />Suspender</Button>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="moderation-section" aria-labelledby="experience-moderation-title">
        <div className="moderation-section__heading">
          <div>
            <span className="page-heading__eyebrow">Revisión de contenido</span>
            <h2 id="experience-moderation-title">Experiencias</h2>
          </div>
          <SelectField
            label="Filtrar experiencias"
            value={experienceFilter}
            onChange={(event) => setExperienceFilter(event.target.value as ExperienceFilter)}
          >
            <option value="PendingReview">En revisión</option>
            <option value="Draft">Borradores</option>
            <option value="Approved">Aprobadas</option>
            <option value="Rejected">Rechazadas</option>
            <option value="Suspended">Suspendidas</option>
            <option value="All">Todas</option>
          </SelectField>
        </div>

        {visibleExperiences.length === 0 ? (
          <EmptyState title="No hay experiencias en este estado" description="Cambia el filtro para consultar el historial." />
        ) : (
          <div className="management-list">
            {visibleExperiences.map((experience) => (
              <article className="management-card surface-panel" key={experience.id}>
                <div className="management-card__header">
                  <div>
                    <span className="management-card__reference">Experiencia #{experience.id}</span>
                    <h3>{experience.title}</h3>
                    <p><MapPin size={16} aria-hidden="true" />{experience.location} · {experience.hostName}</p>
                  </div>
                  <StatusBadge tone={getModerationTone(experience.approvalStatus)}>
                    {getModerationLabel(experience.approvalStatus)}
                  </StatusBadge>
                </div>
                <p className="management-card__description">{experience.description}</p>
                <dl className="management-card__facts">
                  <div><dt>Categoría</dt><dd>{experience.category}</dd></div>
                  <div><dt>Precio</dt><dd>USD {experience.price.toFixed(2)}</dd></div>
                  <div><dt>Capacidad</dt><dd>{experience.capacity}</dd></div>
                </dl>
                {experience.rejectionReason && <Alert tone="error">{experience.rejectionReason}</Alert>}
                <div className="management-actions">
                  {experience.approvalStatus === 'PendingReview' && (
                    <>
                      <Button
                        onClick={() => void decideExperience(experience, 'approve')}
                        isLoading={busyKey === `experience-${experience.id}`}
                      ><ShieldCheck size={17} aria-hidden="true" />Aprobar</Button>
                      <Button
                        variant="danger"
                        onClick={() => void decideExperience(experience, 'reject')}
                        disabled={busyKey === `experience-${experience.id}`}
                      ><BriefcaseBusiness size={17} aria-hidden="true" />Rechazar</Button>
                    </>
                  )}
                  {experience.approvalStatus === 'Approved' && (
                    <Button
                      variant="danger"
                      onClick={() => void decideExperience(experience, 'suspend')}
                      isLoading={busyKey === `experience-${experience.id}`}
                    >Suspender publicación</Button>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
};

export default AdminModeration;
