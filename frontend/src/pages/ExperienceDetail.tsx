import axios from 'axios';
import {
  ArrowLeft,
  CalendarDays,
  Compass,
  MapPin,
  Ship,
  TicketCheck,
  TreePine,
  Utensils,
  UsersRound,
  Waves,
} from 'lucide-react';
import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import ReservationDialog from '../components/ReservationDialog';
import Skeleton from '../components/Skeleton';
import { useAuth } from '../hooks/useAuth';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import RatingStars from '../components/RatingStars';
import { getCancellationPolicyLabel, getDifficultyLabel } from '../utils/experienceLabels';
import { reviewService } from '../services/reviewService';
import { resolveApiAssetUrl } from '../services/api';
import type { Experience, ExperienceSchedule, Review } from '../types';

const ExperienceMap = lazy(() => import('../components/ExperienceMap'));

const formatPrice = (price: number) => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(price);

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
}).format(new Date(date));

const getCategorySlug = (category: string) => {
  switch (category.toLowerCase()) {
    case 'acuático':
    case 'acuatico':
      return 'acuatico';
    case 'cruceros':
      return 'cruceros';
    case 'gastronomía':
    case 'gastronomia':
      return 'gastronomia';
    case 'naturaleza':
      return 'naturaleza';
    default:
      return 'default';
  }
};

const getCategoryIcon = (category: string) => {
  const iconProps = { size: 40, 'aria-hidden': true as const };
  switch (getCategorySlug(category)) {
    case 'acuatico':
      return <Waves {...iconProps} />;
    case 'cruceros':
      return <Ship {...iconProps} />;
    case 'gastronomia':
      return <Utensils {...iconProps} />;
    case 'naturaleza':
      return <TreePine {...iconProps} />;
    default:
      return <Compass {...iconProps} />;
  }
};

const DetailSkeleton = () => (
  <div className="container experience-detail experience-detail--loading" role="status" aria-busy="true">
    <span className="visually-hidden">Cargando detalle de la experiencia.</span>
    <Skeleton className="experience-detail-skeleton__hero" />
    <div className="experience-detail-skeleton__content">
      <Skeleton className="experience-skeleton__line experience-skeleton__line--short" />
      <Skeleton className="experience-skeleton__line" />
      <Skeleton className="experience-skeleton__line" />
    </div>
  </div>
);

export const ExperienceDetail = () => {
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const parsedId = Number(id);
  const isValidId = Number.isInteger(parsedId) && parsedId > 0;
  const requestedReturnPath = typeof location.state?.from === 'string'
    && location.state.from.startsWith('/experiences')
    && !location.state.from.startsWith('//')
    ? location.state.from
    : '/experiences';
  const [retryCount, setRetryCount] = useState(0);
  const [reservationOpen, setReservationOpen] = useState(false);
  const requestKey = `${parsedId}::${retryCount}`;
  const [result, setResult] = useState<{
    requestKey: string;
    experience: Experience | null;
    schedules: ExperienceSchedule[];
    reviews: Review[];
    error: string | null;
    notFound: boolean;
  } | null>(null);
  const loading = isValidId && result?.requestKey !== requestKey;
  const experience = result?.requestKey === requestKey ? result.experience : null;
  const schedules = result?.requestKey === requestKey ? result.schedules : [];
  const reviews = result?.requestKey === requestKey ? result.reviews : [];
  const error = result?.requestKey === requestKey ? result.error : null;
  const notFound = !isValidId || (result?.requestKey === requestKey && result.notFound);

  useEffect(() => {
    if (!isValidId) return;

    const controller = new AbortController();

    Promise.all([
      experienceService.getExperience(parsedId, controller.signal),
      experienceService.getAvailability(parsedId, undefined, controller.signal),
      reviewService.forExperience(parsedId, controller.signal),
    ])
      .then(([data, availability, publicReviews]) => setResult({
        requestKey,
        experience: data,
        schedules: availability,
        reviews: publicReviews,
        error: null,
        notFound: false,
      }))
      .catch((requestError: unknown) => {
        if (axios.isCancel(requestError)) return;
        const apiError = toApiError(requestError, 'No fue posible cargar esta experiencia.');
        setResult({
          requestKey,
          experience: null,
          schedules: [],
          reviews: [],
          error: apiError.status === 404 ? null : apiError.message,
          notFound: apiError.status === 404,
        });
      });

    return () => controller.abort();
  }, [isValidId, parsedId, requestKey]);

  if (loading) return <DetailSkeleton />;

  if (notFound) {
    return (
      <div className="container experience-detail-state animate-fade-in">
        <EmptyState
          title="Experiencia no disponible"
          description="La experiencia no existe, no está aprobada o dejó de estar disponible."
          action={<Link className="button-link button-link--outline" to={requestedReturnPath}>Volver al catálogo</Link>}
        />
      </div>
    );
  }

  if (error || !experience) {
    return (
      <div className="container experience-detail-state animate-fade-in">
        <ErrorState
          title="No pudimos cargar la experiencia"
          description={error || 'No fue posible cargar esta experiencia.'}
          onRetry={() => setRetryCount((current) => current + 1)}
        />
        <Link className="experience-detail-state__back" to={requestedReturnPath}>Volver al catálogo</Link>
      </div>
    );
  }

  const nextSchedule = schedules[0];
  const coverImage = experience.images.find((image) => image.isCover) ?? experience.images[0];
  const galleryImages = experience.images.filter((image) => image.id !== coverImage?.id);
  const availabilityTone = nextSchedule ? 'info' : 'warning';
  const hasPreparationInfo = Boolean(experience.meetingPointInstructions)
    || Boolean(experience.pickupInformation)
    || Boolean(experience.guestRequirements)
    || experience.minimumAge !== null;
  const hasUsefulInfo = experience.languages.length > 0
    || Boolean(experience.difficulty)
    || Boolean(experience.accessibilityInformation)
    || Boolean(experience.cancellationPolicy);
  const handleReserve = () => {
    if (!isAuthenticated) {
      navigate('/login', {
        state: {
          from: `${location.pathname}${location.search}`,
          message: 'Inicia sesión para reservar esta experiencia.',
        },
      });
      return;
    }
    setReservationOpen(true);
  };

  const handleSchedulesUpdate = (updatedSchedules: ExperienceSchedule[]) => {
    setResult((current) => current?.requestKey === requestKey
      ? { ...current, schedules: updatedSchedules }
      : current);
  };

  return (
    <article className="container experience-detail animate-fade-in">
      <Link className="experience-detail__back" to={requestedReturnPath}>
        <ArrowLeft size={18} aria-hidden="true" /> Volver al catálogo
      </Link>

      <div className="experience-detail__layout">
        <div className="experience-detail__main">
          <div className="experience-detail__gallery">
            <div
              className={`experience-detail__placeholder experience-detail__placeholder--${coverImage ? 'image' : getCategorySlug(experience.category)}`}
              role="img"
              aria-label={coverImage?.altText || `Imagen de ambiente de la categoría ${experience.category}`}
              style={coverImage
                ? { backgroundImage: `url("${resolveApiAssetUrl(coverImage.url)}")` }
                : undefined}
            >
              {!coverImage && getCategoryIcon(experience.category)}
              <span>{experience.category}</span>
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
            <h1>{experience.title}</h1>
            <div className="experience-detail__location">
              <MapPin size={18} aria-hidden="true" />
              <span>{experience.location}</span>
            </div>
          </header>

          <section className="experience-detail__description" aria-labelledby="experience-description-title">
            <h2 id="experience-description-title">Sobre esta experiencia</h2>
            {experience.shortDescription && <p><strong>{experience.shortDescription}</strong></p>}
            <p>{experience.description}</p>
          </section>
          <div className="experience-detail__catalog-grid">
            {hasPreparationInfo && (
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
                <ul>{experience.whatToBring.map((item) => <li key={item}>{item}</li>)}</ul>
              </section>
            )}
            {(experience.whatIsIncluded.length > 0 || experience.whatIsNotIncluded.length > 0) && (
              <section>
                <h2>Incluido</h2>
                {experience.whatIsIncluded.length > 0 && (
                  <ul>{experience.whatIsIncluded.map((item) => <li key={item}>{item}</li>)}</ul>
                )}
                {experience.whatIsNotIncluded.length > 0 && (
                  <>
                    <h3>No incluido</h3>
                    <ul>{experience.whatIsNotIncluded.map((item) => <li key={item}>{item}</li>)}</ul>
                  </>
                )}
              </section>
            )}
            {hasUsefulInfo && (
              <section>
                <h2>Información útil</h2>
                {experience.languages.length > 0 && <p>Idiomas: {experience.languages.join(', ')}</p>}
                {experience.difficulty && <p>Dificultad: {getDifficultyLabel(experience.difficulty)}</p>}
                {experience.accessibilityInformation && <p>{experience.accessibilityInformation}</p>}
                {experience.cancellationPolicy && (
                  <p>Política de cancelación: {getCancellationPolicyLabel(experience.cancellationPolicy)}</p>
                )}
              </section>
            )}
          </div>
          {experience.itinerary.length > 0 && (
            <section aria-labelledby="experience-itinerary-title">
              <h2 id="experience-itinerary-title">Itinerario</h2>
              <ol className="experience-detail__itinerary">
                {experience.itinerary.map((item) => (
                  <li key={item.id ?? item.sortOrder}>
                    <strong>{item.title}</strong>
                    <p>{item.description}</p>
                    <small>{item.durationMinutes} minutos{item.location ? ` · ${item.location}` : ''}</small>
                  </li>
                ))}
              </ol>
            </section>
          )}
          {experience.latitude !== null && experience.longitude !== null && (
            <section className="experience-detail__map" aria-labelledby="experience-map-title">
              <h2 id="experience-map-title">Dónde se realiza</h2>
              <p>{experience.location}</p>
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

        <aside className="experience-detail__summary surface-panel" aria-labelledby="experience-summary-title">
          <h2 id="experience-summary-title">Información</h2>
          <div className="experience-detail__price">
            <span>Precio por persona</span>
            <strong>{formatPrice(experience.price)}</strong>
          </div>
          <dl className="experience-detail__facts">
            <div>
              <dt><TicketCheck size={18} aria-hidden="true" /> Próximo horario</dt>
              <dd>{nextSchedule ? formatDate(nextSchedule.startsAt) : 'Sin fecha'}</dd>
            </div>
            <div>
              <dt><UsersRound size={18} aria-hidden="true" /> Cupos próximos</dt>
              <dd>{nextSchedule
                ? nextSchedule.isUnlimitedCapacity ? 'Sin límite' : nextSchedule.availableSpots
                : 0}</dd>
            </div>
            <div>
              <dt><CalendarDays size={18} aria-hidden="true" /> Fechas disponibles</dt>
              <dd>{schedules.length}</dd>
            </div>
          </dl>
          <Alert tone={availabilityTone}>
            {!nextSchedule ? (
              <>Actualmente no hay horarios futuros disponibles.</>
            ) : (
              nextSchedule.isUnlimitedCapacity
                ? `La próxima fecha no tiene límite de cupos; puedes elegir entre ${schedules.length}.`
                : `${nextSchedule.availableSpots} cupos en la próxima fecha; puedes elegir entre ${schedules.length}.`
            )}
          </Alert>
          <Button
            className="experience-detail__reserve"
            variant="primary"
            fullWidth
            onClick={handleReserve}
            disabled={!nextSchedule}
          >
            <TicketCheck size={18} aria-hidden="true" /> Reservar
          </Button>
          <p className="experience-detail__reservation-note">
            {experience.price === 0
              ? <>La reserva es gratis y quedará <strong>confirmada inmediatamente</strong>.</>
              : <>La reserva se crea como <strong>Pendiente de pago</strong>; el pago todavía no está confirmado.</>}
          </p>
        </aside>
      </div>
      <section className="surface-panel experience-reviews" aria-labelledby="experience-reviews-title">
        <div className="experience-reviews__heading">
          <h2 id="experience-reviews-title">Reseñas verificadas</h2>
          {experience.averageRating !== null && (
            <p className="experience-reviews__average">
              <RatingStars value={Math.round(experience.averageRating)} />
              <strong>{experience.averageRating.toFixed(1)}</strong>
              <span>
                de 5 · {experience.reviewCount} {experience.reviewCount === 1 ? 'reseña' : 'reseñas'}
              </span>
            </p>
          )}
        </div>
        {reviews.length === 0 ? (
          <p className="experience-reviews__empty">
            Todavía no hay reseñas. Solo quienes completaron la experiencia pueden escribir una.
          </p>
        ) : (
          <ol>{reviews.map((review) => <li key={review.id} className="review-card">
            <div><strong>{review.authorName}</strong><RatingStars value={review.rating} /></div>
            <p>{review.comment}</p><small>{formatDate(review.createdAt)}</small>
          </li>)}</ol>
        )}
      </section>
      {reservationOpen && (
        <ReservationDialog
          experience={experience}
          schedules={schedules}
          onClose={() => setReservationOpen(false)}
          onSchedulesUpdate={handleSchedulesUpdate}
        />
      )}
    </article>
  );
};

export default ExperienceDetail;
