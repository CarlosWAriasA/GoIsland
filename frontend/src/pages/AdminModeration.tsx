import {
  BadgeCheck,
  BriefcaseBusiness,
  Clock3,
  EyeOff,
  ImageIcon,
  Images,
  MapPin,
  ShieldCheck,
  Tag,
  UserRound,
  UserRoundX,
  UsersRound,
} from 'lucide-react';
import { lazy, Suspense, useEffect, useState } from 'react';
import Alert from '../components/Alert';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import Dialog from '../components/Dialog';
import PromptDialog from '../components/PromptDialog';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import ToastFeedback from '../components/ToastFeedback';
import { resolveApiAssetUrl } from '../services/api';
import { toApiError } from '../services/apiError';
import { hostService } from '../services/hostService';
import { reviewService } from '../services/reviewService';
import { formatLocationLabel } from '../services/googleMapsService';
import type {
  ExperienceApprovalStatus,
  HostProfile,
  HostVerificationStatus,
  ManagedExperience,
  Review,
  ReviewModerationStatus,
} from '../types';
import { getModerationLabel, getModerationTone } from '../utils/moderationStatus';
import { getCancellationPolicyLabel, getDifficultyLabel } from '../utils/experienceLabels';
import { getReviewModerationLabel, getReviewModerationTone } from '../utils/reviewModerationStatus';

const ExperienceMap = lazy(() => import('../components/ExperienceMap'));

type HostAction = 'approve' | 'reject' | 'suspend';
type ExperienceAction = HostAction;
type HostFilter = HostVerificationStatus | 'All';
type ExperienceFilter = ExperienceApprovalStatus | 'All';
const PAGE_SIZE = 10;

const actionPastParticiple: Record<HostAction, string> = {
  approve: 'aprobada',
  reject: 'rechazada',
  suspend: 'suspendida',
};

const formatPrice = (price: number) => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(price);

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium',
  timeStyle: 'short',
}).format(new Date(date));

interface ModerationExperienceDetailProps {
  experience: ManagedExperience;
}

const ModerationExperienceDetail = ({ experience }: ModerationExperienceDetailProps) => {
  const coverImage = experience.images.find((image) => image.isCover) ?? experience.images[0];
  const galleryImages = experience.images.filter((image) => image.id !== coverImage?.id);
  const hasBeforeGoing = Boolean(
    experience.meetingPointInstructions
    || experience.pickupInformation
    || experience.guestRequirements
    || experience.minimumAge !== null,
  );
  const hasIncludedInformation = experience.whatIsIncluded.length > 0
    || experience.whatIsNotIncluded.length > 0;
  const hasUsefulInformation = experience.languages.length > 0
    || Boolean(experience.difficulty)
    || Boolean(experience.accessibilityInformation);

  return (
    <article className="moderation-experience-detail">
      <div className="experience-detail__layout">
        <div className="experience-detail__main">
          <div className="experience-detail__gallery">
            <div
              className={`experience-detail__placeholder${coverImage ? ' experience-detail__placeholder--image' : ''}`}
              role="img"
              aria-label={coverImage?.altText || `Sin foto de portada para ${experience.title}`}
              style={coverImage
                ? { backgroundImage: `url("${resolveApiAssetUrl(coverImage.url)}")` }
                : undefined}
            >
              {!coverImage && <ImageIcon size={38} aria-hidden="true" />}
              <span>{experience.category || 'Sin categoría'}</span>
            </div>
            {galleryImages.length > 0 && (
              <div className="experience-detail__thumbnails" aria-label="Galería de la experiencia">
                {galleryImages.map((image) => (
                  <img
                    key={image.id}
                    src={resolveApiAssetUrl(image.url)}
                    alt={image.altText || `Foto de ${experience.title}`}
                  />
                ))}
              </div>
            )}
          </div>

          <header className="experience-detail__header">
            <div className="moderation-experience-detail__status">
              <StatusBadge tone={getModerationTone(experience.approvalStatus)}>
                {getModerationLabel(experience.approvalStatus)}
              </StatusBadge>
              <span>Experiencia enviada a revisión</span>
            </div>
            <h2>{experience.title}</h2>
            <div className="experience-detail__location">
              <MapPin size={18} aria-hidden="true" />
              <span>{experience.location ? formatLocationLabel(experience.location) : 'Lugar por definir'}</span>
            </div>
          </header>

          <section className="experience-detail__description" aria-labelledby="moderation-description-title">
            <h2 id="moderation-description-title">Sobre esta experiencia</h2>
            {experience.shortDescription && <p><strong>{experience.shortDescription}</strong></p>}
            <p>{experience.description}</p>
          </section>

          {(hasBeforeGoing || experience.whatToBring.length > 0 || hasIncludedInformation || hasUsefulInformation) && (
            <div className="experience-detail__catalog-grid">
              {hasBeforeGoing && (
                <section>
                  <h2>Antes de ir</h2>
                  {experience.meetingPointInstructions && (
                    <p><strong>Punto de encuentro:</strong> {experience.meetingPointInstructions}</p>
                  )}
                  {experience.pickupInformation && <p><strong>Recogida:</strong> {experience.pickupInformation}</p>}
                  {experience.guestRequirements && <p>{experience.guestRequirements}</p>}
                  {experience.minimumAge !== null && <p>Edad mínima: {experience.minimumAge} años</p>}
                </section>
              )}
              {experience.whatToBring.length > 0 && (
                <section>
                  <h2>Qué llevar</h2>
                  <ul>{experience.whatToBring.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}</ul>
                </section>
              )}
              {hasIncludedInformation && (
                <section>
                  {experience.whatIsIncluded.length > 0 && (
                    <>
                      <h2>Incluido</h2>
                      <ul>{experience.whatIsIncluded.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}</ul>
                    </>
                  )}
                  {experience.whatIsNotIncluded.length > 0 && (
                    <>
                      <h3>No incluido</h3>
                      <ul>{experience.whatIsNotIncluded.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}</ul>
                    </>
                  )}
                </section>
              )}
              {hasUsefulInformation && (
                <section>
                  <h2>Información útil</h2>
                  {experience.languages.length > 0 && <p>Idiomas: {experience.languages.join(', ')}</p>}
                  {experience.difficulty && <p>Dificultad: {getDifficultyLabel(experience.difficulty)}</p>}
                  <p>Cancelación: {getCancellationPolicyLabel(experience.cancellationPolicy)}</p>
                  {experience.accessibilityInformation && <p>Accesibilidad: {experience.accessibilityInformation}</p>}
                </section>
              )}
            </div>
          )}

          {experience.itinerary.length > 0 && (
            <section className="moderation-experience-detail__itinerary" aria-labelledby="moderation-itinerary-title">
              <h2 id="moderation-itinerary-title">Itinerario</h2>
              <ol className="experience-detail__itinerary">
                {experience.itinerary.map((item, index) => (
                  <li key={item.id ?? item.sortOrder ?? index}>
                    <strong>{item.title}</strong>
                    <p>{item.description}</p>
                    <small>
                      {item.durationMinutes > 0 ? `${item.durationMinutes} minutos` : 'Sin duración'}
                      {item.location ? ` · ${item.location}` : ''}
                    </small>
                  </li>
                ))}
              </ol>
            </section>
          )}

          {experience.tags.length > 0 && (
            <section className="moderation-experience-detail__tags" aria-labelledby="moderation-tags-title">
              <h2 id="moderation-tags-title"><Tag size={19} aria-hidden="true" /> Etiquetas</h2>
              <ul>{experience.tags.map((tag) => <li key={tag}>{tag}</li>)}</ul>
            </section>
          )}

          {experience.latitude !== null && experience.longitude !== null && (
            <section className="experience-detail__map" aria-labelledby="moderation-map-title">
              <h2 id="moderation-map-title">Dónde se realiza</h2>
              <p>{formatLocationLabel(experience.location)}</p>
              <Suspense fallback={<Skeleton className="experience-detail__map-loading" />}>
                <ExperienceMap
                  points={[{
                    id: experience.id,
                    title: experience.title,
                    latitude: experience.latitude,
                    longitude: experience.longitude,
                  }]}
                  label={`Ubicación de ${experience.title}`}
                />
              </Suspense>
            </section>
          )}
        </div>

        <aside className="experience-detail__summary surface-panel" aria-labelledby="moderation-summary-title">
          <h2 id="moderation-summary-title">Información para revisar</h2>
          <div className="experience-detail__price">
            <span>Precio por persona</span>
            <strong>{formatPrice(experience.price)}</strong>
          </div>
          <dl className="experience-detail__facts">
            <div>
              <dt><UserRound size={18} aria-hidden="true" /> Anfitrión</dt>
              <dd>{experience.hostName}</dd>
            </div>
            <div>
              <dt><UsersRound size={18} aria-hidden="true" /> Capacidad</dt>
              <dd>{experience.isUnlimitedCapacity ? 'Sin límite' : `${experience.capacity} personas`}</dd>
            </div>
            {experience.durationMinutes !== null && (
              <div>
                <dt><Clock3 size={18} aria-hidden="true" /> Duración estimada</dt>
                <dd>{experience.durationMinutes} min</dd>
              </div>
            )}
            <div>
              <dt><Images size={18} aria-hidden="true" /> Fotos</dt>
              <dd>{experience.images.length}</dd>
            </div>
          </dl>
          <div className="moderation-experience-detail__dates">
            <span>Última actualización</span>
            <strong>{formatDate(experience.updatedAt)}</strong>
          </div>
          {experience.rejectionReason && <Alert tone="error">{experience.rejectionReason}</Alert>}
        </aside>
      </div>
    </article>
  );
};

type ReviewFilter = ReviewModerationStatus | 'All';

type PendingReasonAction =
  | { scope: 'host'; target: HostProfile; action: HostAction }
  | { scope: 'experience'; target: ManagedExperience; action: ExperienceAction };

export const AdminModeration = () => {
  const [applications, setApplications] = useState<HostProfile[]>([]);
  const [experiences, setExperiences] = useState<ManagedExperience[]>([]);
  const [hostFilter, setHostFilter] = useState<HostFilter>('Pending');
  const [experienceFilter, setExperienceFilter] = useState<ExperienceFilter>('PendingReview');
  const [hostQuery, setHostQuery] = useState('');
  const [experienceQuery, setExperienceQuery] = useState('');
  const [hostPage, setHostPage] = useState(1);
  const [hostTotalPages, setHostTotalPages] = useState(0);
  const [experiencePage, setExperiencePage] = useState(1);
  const [experienceTotalPages, setExperienceTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const [pendingReasonAction, setPendingReasonAction] = useState<PendingReasonAction | null>(null);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [reviewFilter, setReviewFilter] = useState<ReviewFilter>('Visible');
  const [reviewQuery, setReviewQuery] = useState('');
  const [reviewPage, setReviewPage] = useState(1);
  const [reviewTotalPages, setReviewTotalPages] = useState(0);
  const [pendingHideReview, setPendingHideReview] = useState<Review | null>(null);
  const [pendingApprovalAction, setPendingApprovalAction] = useState<PendingReasonAction | null>(null);
  const [selectedExperience, setSelectedExperience] = useState<ManagedExperience | null>(null);

  const retryLoad = () => {
    setLoading(true);
    setError(null);
    setRetryCount((current) => current + 1);
  };

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      hostService.getApplications({
        query: hostQuery.trim() || undefined,
        status: hostFilter === 'All' ? undefined : hostFilter,
        page: hostPage,
        pageSize: PAGE_SIZE,
      }, controller.signal),
      hostService.getExperiencesForAdmin({
        query: experienceQuery.trim() || undefined,
        status: experienceFilter === 'All' ? undefined : experienceFilter,
        page: experiencePage,
        pageSize: PAGE_SIZE,
      }, controller.signal),
      reviewService.forAdmin({
        query: reviewQuery.trim() || undefined,
        status: reviewFilter === 'All' ? undefined : reviewFilter,
        page: reviewPage,
        pageSize: PAGE_SIZE,
      }, controller.signal),
    ])
      .then(([hostData, experienceData, reviewData]) => {
        setApplications(hostData.items);
        setHostTotalPages(hostData.totalPages);
        setExperiences(experienceData.items);
        setExperienceTotalPages(experienceData.totalPages);
        setReviews(reviewData.items);
        setReviewTotalPages(reviewData.totalPages);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(requestError).message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [experienceFilter, experiencePage, experienceQuery, hostFilter, hostPage, hostQuery,
    retryCount, reviewFilter, reviewPage, reviewQuery]);

  const hideReview = async (review: Review, reason: string) => {
    setBusyKey(`review-${review.id}`);
    setError(null);
    setSuccess(null);
    try {
      const updated = await reviewService.hide(review.id, reason);
      setReviews((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess(`La reseña de ${review.authorName} fue ocultada.`);
      setRetryCount((current) => current + 1);
    } catch (requestError: unknown) {
      setError(toApiError(requestError, 'No fue posible ocultar la reseña.').message);
    } finally {
      setBusyKey(null);
    }
  };

  const confirmHideReview = async (reason: string) => {
    if (!pendingHideReview) return;
    await hideReview(pendingHideReview, reason);
    setPendingHideReview(null);
  };

  const decideHost = async (profile: HostProfile, action: HostAction, reason?: string) => {
    setBusyKey(`host-${profile.id}`);
    setError(null);
    setSuccess(null);
    try {
      const updated = await hostService.decideApplication(profile.id, action, reason);
      setApplications((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess(`La solicitud de ${profile.displayName} fue ${actionPastParticiple[action]}.`);
      setRetryCount((current) => current + 1);
      return true;
    } catch (requestError: unknown) {
      setError(toApiError(requestError).message);
      return false;
    } finally {
      setBusyKey(null);
    }
  };

  const decideExperience = async (experience: ManagedExperience, action: ExperienceAction, reason?: string) => {
    setBusyKey(`experience-${experience.id}`);
    setError(null);
    setSuccess(null);
    try {
      const updated = await hostService.decideExperience(experience.id, action, reason);
      setExperiences((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSelectedExperience((current) => current?.id === updated.id ? updated : current);
      setSuccess(`La experiencia “${experience.title}” fue ${actionPastParticiple[action]}.`);
      setRetryCount((current) => current + 1);
      return true;
    } catch (requestError: unknown) {
      setError(toApiError(requestError).message);
      return false;
    } finally {
      setBusyKey(null);
    }
  };

  const requestHostDecision = (profile: HostProfile, action: HostAction) => {
    if (action === 'approve') {
      setPendingApprovalAction({ scope: 'host', target: profile, action });
      return;
    }
    setPendingReasonAction({ scope: 'host', target: profile, action });
  };

  const requestExperienceDecision = (experience: ManagedExperience, action: ExperienceAction) => {
    if (action === 'approve') {
      setPendingApprovalAction({ scope: 'experience', target: experience, action });
      return;
    }
    setPendingReasonAction({ scope: 'experience', target: experience, action });
  };

  const confirmReasonAction = async (reason: string) => {
    if (!pendingReasonAction) return;
    const succeeded = pendingReasonAction.scope === 'host'
      ? await decideHost(pendingReasonAction.target, pendingReasonAction.action, reason)
      : await decideExperience(pendingReasonAction.target, pendingReasonAction.action, reason);
    if (succeeded) setPendingReasonAction(null);
  };

  const confirmApprovalAction = async () => {
    if (!pendingApprovalAction) return;
    const succeeded = pendingApprovalAction.scope === 'host'
      ? await decideHost(pendingApprovalAction.target, 'approve')
      : await decideExperience(pendingApprovalAction.target, 'approve');
    if (succeeded) setPendingApprovalAction(null);
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
        <p>Revisa las solicitudes de anfitrión y las experiencias antes de publicarlas.</p>
      </header>

      <ToastFeedback message={success} tone="success" />
      <ToastFeedback message={error} tone="error" />

      <section className="moderation-section" aria-labelledby="host-moderation-title">
        <div className="moderation-section__heading">
          <div>
            <span className="page-heading__eyebrow">Verificación de identidad</span>
            <h2 id="host-moderation-title">Solicitudes de anfitrión</h2>
          </div>
          <div className="moderation-section__filters">
            <Input
              label="Buscar solicitudes"
              placeholder="Nombre o correo"
              value={hostQuery}
              maxLength={160}
              onChange={(event) => {
                setHostQuery(event.target.value);
                setHostPage(1);
              }}
            />
            <SelectField
              label="Filtrar solicitudes"
              value={hostFilter}
              onChange={(event) => {
                setHostFilter(event.target.value as HostFilter);
                setHostPage(1);
              }}
            >
              <option value="Pending">Pendientes</option>
              <option value="Approved">Aprobadas</option>
              <option value="Rejected">Rechazadas</option>
              <option value="Suspended">Suspendidas</option>
              <option value="All">Todas</option>
            </SelectField>
          </div>
        </div>

        {applications.length === 0 ? (
          <EmptyState title="No hay solicitudes en este estado" description="Cambia el filtro para consultar el historial." />
        ) : (
          <div className="operations-list" aria-label="Solicitudes de anfitrión">
            {applications.map((profile) => (
              <article className="operations-row operations-row--hosts" key={profile.id}>
                <div className="operations-row__main">
                  <div className="operations-row__primary">
                    <span className="operations-row__reference">Solicitud de anfitrión</span>
                    <h3>{profile.displayName}</h3>
                    <small>{profile.userFullName}</small>
                  </div>
                  <div className="operations-row__cell">
                    <span>Contacto</span>
                    <strong>{profile.userEmail}</strong>
                    <small>{profile.phoneNumber}</small>
                  </div>
                  <div className="operations-row__cell">
                    <span>Enviada</span>
                    <strong>{new Date(profile.submittedAt).toLocaleDateString('es-DO')}</strong>
                  </div>
                  <StatusBadge tone={getModerationTone(profile.verificationStatus)}>
                    {getModerationLabel(profile.verificationStatus)}
                  </StatusBadge>
                  <div className="operations-row__actions">
                    {profile.verificationStatus === 'Pending' && (
                      <>
                        <Button
                          size="sm"
                          onClick={() => requestHostDecision(profile, 'approve')}
                          isLoading={busyKey === `host-${profile.id}`}
                        ><BadgeCheck size={16} aria-hidden="true" />Aprobar</Button>
                        <Button
                          size="sm"
                          variant="danger"
                          onClick={() => requestHostDecision(profile, 'reject')}
                          disabled={busyKey === `host-${profile.id}`}
                        ><UserRoundX size={16} aria-hidden="true" />Rechazar</Button>
                      </>
                    )}
                    {profile.verificationStatus === 'Approved' && (
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => requestHostDecision(profile, 'suspend')}
                        isLoading={busyKey === `host-${profile.id}`}
                      ><UserRoundX size={16} aria-hidden="true" />Suspender</Button>
                    )}
                  </div>
                </div>
                <details className="operations-row__details">
                  <summary>Ver presentación</summary>
                  <p>{profile.description}</p>
                  {profile.rejectionReason && <Alert tone="error">{profile.rejectionReason}</Alert>}
                </details>
              </article>
            ))}
          </div>
        )}
        {hostTotalPages > 1 && (
          <nav className="catalog-pagination" aria-label="Páginas de solicitudes">
            <Button variant="outline" disabled={hostPage <= 1} onClick={() => setHostPage(hostPage - 1)}>Anterior</Button>
            <span>Página {hostPage} de {hostTotalPages}</span>
            <Button variant="outline" disabled={hostPage >= hostTotalPages} onClick={() => setHostPage(hostPage + 1)}>Siguiente</Button>
          </nav>
        )}
      </section>

      <section className="moderation-section" aria-labelledby="experience-moderation-title">
        <div className="moderation-section__heading">
          <div>
            <span className="page-heading__eyebrow">Revisión de contenido</span>
            <h2 id="experience-moderation-title">Experiencias</h2>
          </div>
          <div className="moderation-section__filters">
            <Input
              label="Buscar experiencias"
              placeholder="Título, lugar o anfitrión"
              value={experienceQuery}
              maxLength={160}
              onChange={(event) => {
                setExperienceQuery(event.target.value);
                setExperiencePage(1);
              }}
            />
            <SelectField
              label="Filtrar experiencias"
              value={experienceFilter}
              onChange={(event) => {
                setExperienceFilter(event.target.value as ExperienceFilter);
                setExperiencePage(1);
              }}
            >
              <option value="PendingReview">En revisión</option>
              <option value="Draft">Borradores</option>
              <option value="Approved">Aprobadas</option>
              <option value="Rejected">Rechazadas</option>
              <option value="Suspended">Suspendidas</option>
              <option value="All">Todas</option>
            </SelectField>
          </div>
        </div>

        {experiences.length === 0 ? (
          <EmptyState title="No hay experiencias en este estado" description="Cambia el filtro para consultar el historial." />
        ) : (
          <div className="operations-list" aria-label="Experiencias en moderación">
            {experiences.map((experience) => (
              <article className="operations-row operations-row--experiences" key={experience.id}>
                <div className="operations-row__main">
                  <div className="operations-row__primary">
                    <span className="operations-row__reference">Experiencia para moderar</span>
                    <h3>{experience.title}</h3>
                    <small><MapPin size={14} aria-hidden="true" />{formatLocationLabel(experience.location)}</small>
                  </div>
                  <div className="operations-row__cell">
                    <span>Anfitrión</span>
                    <strong>{experience.hostName}</strong>
                    <small>{experience.category}</small>
                  </div>
                  <div className="operations-row__cell">
                    <span>Precio y cupos</span>
                    <strong>USD {experience.price.toFixed(2)}</strong>
                    <small>{experience.isUnlimitedCapacity ? 'Sin límite' : `${experience.capacity} personas`}</small>
                  </div>
                  <StatusBadge tone={getModerationTone(experience.approvalStatus)}>
                    {getModerationLabel(experience.approvalStatus)}
                  </StatusBadge>
                  <div className="operations-row__actions">
                    {experience.approvalStatus === 'PendingReview' && (
                      <>
                        <Button
                          size="sm"
                          onClick={() => requestExperienceDecision(experience, 'approve')}
                          isLoading={busyKey === `experience-${experience.id}`}
                        ><ShieldCheck size={16} aria-hidden="true" />Aprobar</Button>
                        <Button
                          size="sm"
                          variant="danger"
                          onClick={() => requestExperienceDecision(experience, 'reject')}
                          disabled={busyKey === `experience-${experience.id}`}
                        ><BriefcaseBusiness size={16} aria-hidden="true" />Rechazar</Button>
                      </>
                    )}
                    {experience.approvalStatus === 'Approved' && (
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => requestExperienceDecision(experience, 'suspend')}
                        isLoading={busyKey === `experience-${experience.id}`}
                      >Suspender</Button>
                    )}
                  </div>
                </div>
                <div className="operations-row__details">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setSelectedExperience(experience)}
                  >
                    <ImageIcon size={16} aria-hidden="true" /> Ver información completa
                  </Button>
                  {experience.rejectionReason && <Alert tone="error">{experience.rejectionReason}</Alert>}
                </div>
              </article>
            ))}
          </div>
        )}
        {experienceTotalPages > 1 && (
          <nav className="catalog-pagination" aria-label="Páginas de experiencias">
            <Button variant="outline" disabled={experiencePage <= 1} onClick={() => setExperiencePage(experiencePage - 1)}>Anterior</Button>
            <span>Página {experiencePage} de {experienceTotalPages}</span>
            <Button variant="outline" disabled={experiencePage >= experienceTotalPages} onClick={() => setExperiencePage(experiencePage + 1)}>Siguiente</Button>
          </nav>
        )}
      </section>

      <section className="moderation-section" aria-labelledby="review-moderation-title">
        <div className="moderation-section__heading">
          <div>
            <span className="page-heading__eyebrow">Contenido de la comunidad</span>
            <h2 id="review-moderation-title">Reseñas publicadas</h2>
          </div>
          <div className="moderation-section__filters">
            <Input
              label="Buscar reseñas"
              placeholder="Autor o comentario"
              value={reviewQuery}
              maxLength={160}
              onChange={(event) => {
                setReviewQuery(event.target.value);
                setReviewPage(1);
              }}
            />
            <SelectField
              label="Filtrar reseñas"
              value={reviewFilter}
              onChange={(event) => {
                setReviewFilter(event.target.value as ReviewFilter);
                setReviewPage(1);
              }}
            >
              <option value="Visible">Publicadas</option>
              <option value="Reported">Reportadas</option>
              <option value="Hidden">Ocultas</option>
              <option value="Deleted">Eliminadas</option>
              <option value="All">Todas</option>
            </SelectField>
          </div>
        </div>

        {reviews.length === 0 ? (
          <EmptyState
            title="No hay reseñas en este estado"
            description="Cambia el filtro para revisar el resto de las reseñas."
          />
        ) : (
          <div className="operations-list" aria-label="Reseñas publicadas">
            {reviews.map((review) => (
              <article className="operations-row operations-row--reviews" key={review.id}>
                <div className="operations-row__main">
                  <div className="operations-row__primary">
                    <span className="operations-row__reference">Reseña #{review.id}</span>
                    <h3>{review.authorName}</h3>
                    <small>Reserva #{review.reservationId} · Experiencia #{review.experienceId}</small>
                  </div>
                  <div className="operations-row__cell">
                    <span>Calificación</span>
                    <strong>{review.rating} de 5</strong>
                    <small>{formatDate(review.createdAt)}</small>
                  </div>
                  <StatusBadge tone={getReviewModerationTone(review.moderationStatus)}>
                    {getReviewModerationLabel(review.moderationStatus)}
                  </StatusBadge>
                  <div className="operations-row__actions">
                    {review.moderationStatus !== 'Hidden' && review.moderationStatus !== 'Deleted' && (
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => setPendingHideReview(review)}
                        isLoading={busyKey === `review-${review.id}`}
                      ><EyeOff size={16} aria-hidden="true" />Ocultar</Button>
                    )}
                  </div>
                </div>
                <div className="operations-row__details">
                  <p className="operations-row__comment">{review.comment}</p>
                </div>
              </article>
            ))}
          </div>
        )}
        {reviewTotalPages > 1 && (
          <nav className="catalog-pagination" aria-label="Páginas de reseñas">
            <Button variant="outline" disabled={reviewPage <= 1} onClick={() => setReviewPage(reviewPage - 1)}>Anterior</Button>
            <span>Página {reviewPage} de {reviewTotalPages}</span>
            <Button variant="outline" disabled={reviewPage >= reviewTotalPages} onClick={() => setReviewPage(reviewPage + 1)}>Siguiente</Button>
          </nav>
        )}
      </section>
      <Dialog
        open={selectedExperience !== null}
        title={selectedExperience ? `Revisar: ${selectedExperience.title}` : 'Revisar experiencia'}
        className="moderation-experience-dialog"
        closeDisabled={busyKey !== null}
        onClose={() => setSelectedExperience(null)}
        footer={selectedExperience && (
          <>
            <Button variant="outline" onClick={() => setSelectedExperience(null)} disabled={busyKey !== null}>
              Volver
            </Button>
            {selectedExperience.approvalStatus === 'PendingReview' && (
              <>
                <Button
                  onClick={() => {
                    const experience = selectedExperience;
                    setSelectedExperience(null);
                    requestExperienceDecision(experience, 'approve');
                  }}
                  isLoading={busyKey === `experience-${selectedExperience.id}`}
                >
                  <ShieldCheck size={17} aria-hidden="true" /> Aprobar
                </Button>
                <Button
                  variant="danger"
                  onClick={() => {
                    const experience = selectedExperience;
                    setSelectedExperience(null);
                    requestExperienceDecision(experience, 'reject');
                  }}
                  disabled={busyKey !== null}
                >
                  <BriefcaseBusiness size={17} aria-hidden="true" /> Rechazar
                </Button>
              </>
            )}
            {selectedExperience.approvalStatus === 'Approved' && (
              <Button
                variant="danger"
                onClick={() => {
                  const experience = selectedExperience;
                  setSelectedExperience(null);
                  requestExperienceDecision(experience, 'suspend');
                }}
                disabled={busyKey !== null}
              >
                Suspender
              </Button>
            )}
          </>
        )}
      >
        {selectedExperience && <ModerationExperienceDetail experience={selectedExperience} />}
      </Dialog>
      <PromptDialog
        open={pendingHideReview !== null}
        title="Ocultar reseña"
        description={pendingHideReview
          ? `La reseña de ${pendingHideReview.authorName} dejará de mostrarse en el catálogo.`
          : undefined}
        label="Motivo"
        placeholder="Explica por qué se oculta (mínimo 3 caracteres)"
        confirmLabel="Ocultar reseña"
        isConfirming={busyKey !== null}
        onClose={() => setPendingHideReview(null)}
        onConfirm={confirmHideReview}
      />
      <PromptDialog
        open={pendingReasonAction !== null}
        title={pendingReasonAction?.action === 'reject'
          ? 'Indicar motivo del rechazo'
          : 'Indicar motivo de la suspensión'}
        description={pendingReasonAction?.scope === 'host'
          ? `Explica la decisión para ${pendingReasonAction.target.displayName}.`
          : pendingReasonAction
            ? `Explica la decisión para ${pendingReasonAction.target.title}.`
            : undefined}
        label="Motivo"
        placeholder="Escribe un motivo claro"
        confirmLabel="Guardar decisión"
        isConfirming={busyKey !== null}
        onClose={() => setPendingReasonAction(null)}
        onConfirm={confirmReasonAction}
      />
      <ConfirmDialog
        open={pendingApprovalAction !== null}
        title={pendingApprovalAction?.scope === 'host' ? 'Aprobar anfitrión' : 'Publicar experiencia'}
        message={pendingApprovalAction?.scope === 'host'
          ? `¿Confirmas que ${pendingApprovalAction.target.displayName} puede publicar y gestionar experiencias?`
          : pendingApprovalAction
            ? `¿Confirmas que “${pendingApprovalAction.target.title}” está lista para aparecer en el catálogo?`
            : ''}
        confirmLabel={pendingApprovalAction?.scope === 'host' ? 'Aprobar anfitrión' : 'Publicar experiencia'}
        confirmVariant="primary"
        isConfirming={busyKey !== null}
        onClose={() => setPendingApprovalAction(null)}
        onConfirm={() => void confirmApprovalAction()}
      />
    </div>
  );
};

export default AdminModeration;
