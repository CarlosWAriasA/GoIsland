import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  ArrowRight,
  Backpack,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock,
  Compass,
  Globe,
  Info,
  MapPin,
  Maximize2,
  Navigation,
  Ship,
  TicketCheck,
  TreePine,
  Utensils,
  UsersRound,
  Waves,
  X,
  XCircle,
} from 'lucide-react';
import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import ReservationDialog from '../components/ReservationDialog';
import Skeleton from '../components/Skeleton';
import { useAuth } from '../hooks/useAuth';
import { usePageMetadata } from '../hooks/usePageMetadata';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import RatingStars from '../components/RatingStars';
import { getCancellationPolicyLabel, getDifficultyLabel } from '../utils/experienceLabels';
import { reviewService } from '../services/reviewService';
import { resolveApiAssetUrl } from '../services/api';
import { formatLocationLabel } from '../services/googleMapsService';
import { experienceKeys, queryRefresh } from '../queries/queryKeys';
import { getReturnPath } from '../utils/navigation';

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
  const { identifier: routeIdentifier } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { isAuthenticated } = useAuth();
  const identifier = routeIdentifier?.trim() ?? '';
  const isValidIdentifier = identifier.length > 0 && identifier.length <= 180;
  const requestedReturnPath = getReturnPath(location.state, '/experiences');
  const returnLabel = /^\/reservations\/\d+/.test(requestedReturnPath)
    ? 'Volver a la reserva'
    : requestedReturnPath.startsWith('/experiences') ? 'Volver al catálogo' : 'Volver';
  const [reservationOpen, setReservationOpen] = useState(false);
  const detailQuery = useQuery({
    queryKey: experienceKeys.detail(identifier),
    queryFn: ({ signal }) => experienceService.getExperience(identifier, signal),
    enabled: isValidIdentifier,
  });
  const experience = detailQuery.data ?? null;
  const availabilityQuery = useQuery({
    queryKey: experienceKeys.availability(experience?.id ?? 0),
    queryFn: ({ signal }) => experienceService.getAvailability(experience!.id, undefined, signal),
    enabled: experience !== null,
    staleTime: 10_000,
    refetchInterval: queryRefresh.availability,
    refetchOnMount: 'always',
  });
  const reviewsQuery = useQuery({
    queryKey: experienceKeys.reviews(experience?.id ?? 0),
    queryFn: ({ signal }) => reviewService.forExperience(experience!.id, signal),
    enabled: experience !== null,
    select: (data) => data.items,
  });
  const schedules = availabilityQuery.data ?? [];
  const reviews = reviewsQuery.data ?? [];
  const requestError = (!detailQuery.data ? detailQuery.error : null)
    ?? (!availabilityQuery.data ? availabilityQuery.error : null)
    ?? (!reviewsQuery.data ? reviewsQuery.error : null);
  const apiError = requestError
    ? toApiError(requestError, 'No fue posible cargar esta experiencia.')
    : null;
  const loading = isValidIdentifier && (
    detailQuery.isPending
    || (experience !== null && (availabilityQuery.isPending || reviewsQuery.isPending))
  );
  const error = apiError?.status === 404 ? null : apiError?.message ?? null;
  const notFound = !isValidIdentifier || apiError?.status === 404;

  const metadata = useMemo(() => {
    if (!experience) return undefined;
    const coverImage = experience.images.find((image) => image.isCover) ?? experience.images[0];
    const description = (experience.shortDescription || experience.description).slice(0, 160);
    const path = `/experiences/${experience.slug}`;
    const siteOrigin = (import.meta.env.VITE_SITE_URL || window.location.origin).replace(/\/$/, '');
    const canonical = `${siteOrigin}${path}`;
    return {
      title: `${experience.title} | GoIsland`,
      description,
      path,
      image: coverImage ? resolveApiAssetUrl(coverImage.url) : undefined,
      structuredData: {
        '@context': 'https://schema.org',
        '@type': 'TouristTrip',
        '@id': canonical,
        name: experience.title,
        description,
        url: canonical,
        image: coverImage ? resolveApiAssetUrl(coverImage.url) : undefined,
        touristType: experience.category,
        itinerary: experience.location,
      },
    };
  }, [experience]);
  usePageMetadata(metadata);

  useEffect(() => {
    if (experience && /^\d+$/.test(identifier)) {
      queryClient.setQueryData(experienceKeys.detail(experience.slug), experience);
      navigate(`/experiences/${experience.slug}`, {
        replace: true,
        state: location.state,
      });
    }
  }, [experience, identifier, location.state, navigate, queryClient]);

  const allImages = useMemo(() => {
    if (!experience || experience.images.length === 0) return [];
    return [...experience.images].sort((a, b) => (b.isCover ? 1 : 0) - (a.isCover ? 1 : 0));
  }, [experience]);

  const detailMapPoints = useMemo(() => {
    if (!experience || experience.latitude === null || experience.longitude === null) return [];
    return [{
      id: experience.id,
      title: experience.title,
      latitude: experience.latitude,
      longitude: experience.longitude,
    }];
  }, [experience]);

  const [activeImageIndex, setActiveImageIndex] = useState(0);
  const [userHasInteracted, setUserHasInteracted] = useState(false);
  const [isLightboxOpen, setIsLightboxOpen] = useState(false);

  useEffect(() => {
    if (userHasInteracted || allImages.length <= 1) return;
    const interval = setInterval(() => {
      setActiveImageIndex((prev) => (prev + 1) % allImages.length);
    }, 4000);
    return () => clearInterval(interval);
  }, [userHasInteracted, allImages.length]);

  const handlePrevImage = useCallback(() => {
    if (allImages.length <= 1) return;
    setUserHasInteracted(true);
    setActiveImageIndex((prev) => (prev - 1 + allImages.length) % allImages.length);
  }, [allImages.length]);

  const handleNextImage = useCallback(() => {
    if (allImages.length <= 1) return;
    setUserHasInteracted(true);
    setActiveImageIndex((prev) => (prev + 1) % allImages.length);
  }, [allImages.length]);

  const handleSelectImage = (index: number) => {
    setUserHasInteracted(true);
    setActiveImageIndex(index);
  };

  useEffect(() => {
    if (!isLightboxOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setIsLightboxOpen(false);
      } else if (e.key === 'ArrowLeft') {
        handlePrevImage();
      } else if (e.key === 'ArrowRight') {
        handleNextImage();
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = '';
    };
  }, [isLightboxOpen, handleNextImage, handlePrevImage]);

  if (loading) return <DetailSkeleton />;

  if (notFound) {
    return (
      <div className="container experience-detail-state animate-fade-in">
        <EmptyState
          title="Experiencia no disponible"
          description="Esta experiencia ya no está disponible."
          action={<Link className="button-link button-link--outline" to={requestedReturnPath}>{returnLabel}</Link>}
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
          onRetry={() => {
            if (!experience) {
              void detailQuery.refetch();
              return;
            }
            void Promise.all([availabilityQuery.refetch(), reviewsQuery.refetch()]);
          }}
        />
        <Link className="experience-detail-state__back" to={requestedReturnPath}>{returnLabel}</Link>
      </div>
    );
  }

  const isSelfGuided = experience.schedulingMode === 'SelfGuided';
  const nextSchedule = schedules[0];
  const canReserve = isSelfGuided || Boolean(nextSchedule
    && (nextSchedule.isUnlimitedCapacity || nextSchedule.availableSpots > 0));

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

  return (
    <article className="container experience-detail animate-fade-in">
      <Link className="experience-detail__back" to={requestedReturnPath}>
        <ArrowLeft size={18} aria-hidden="true" /> {returnLabel}
      </Link>

      <div className="experience-detail__hero-grid">
        <div className="experience-detail__gallery">
          <div className="experience-carousel">
            <div className="experience-carousel__main">
              {allImages.length > 0 ? (
                <div
                  className="experience-carousel__track"
                  style={{ transform: `translateX(-${activeImageIndex * 100}%)` }}
                >
                  {allImages.map((img) => (
                    <img
                      key={img.id}
                      src={resolveApiAssetUrl(img.url)}
                      alt={img.altText || experience.title}
                      className="experience-carousel__slide"
                    />
                  ))}
                </div>
              ) : (
                <div
                  className={`experience-detail__placeholder experience-detail__placeholder--${getCategorySlug(experience.category)}`}
                  role="img"
                  aria-label={`Imagen de ambiente de la categoría ${experience.category}`}
                >
                  {getCategoryIcon(experience.category)}
                </div>
              )}
              <span className="experience-carousel__badge">{experience.category}</span>

              {allImages.length > 0 && (
                <button
                  type="button"
                  className="experience-carousel__expand"
                  onClick={() => {
                    setUserHasInteracted(true);
                    setIsLightboxOpen(true);
                  }}
                  aria-label="Ver imagen en tamaño completo"
                  title="Ver imagen en tamaño completo"
                >
                  <Maximize2 size={18} aria-hidden="true" />
                </button>
              )}

              {allImages.length > 1 && (
                <>
                  <button
                    type="button"
                    className="experience-carousel__control experience-carousel__control--prev"
                    onClick={handlePrevImage}
                    aria-label="Imagen anterior"
                  >
                    <ChevronLeft size={22} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="experience-carousel__control experience-carousel__control--next"
                    onClick={handleNextImage}
                    aria-label="Siguiente imagen"
                  >
                    <ChevronRight size={22} aria-hidden="true" />
                  </button>
                  <div className="experience-carousel__indicators">
                    {allImages.map((img, idx) => (
                      <button
                        key={img.id}
                        type="button"
                        className={`experience-carousel__indicator ${idx === activeImageIndex ? 'is-active' : ''}`}
                        onClick={() => handleSelectImage(idx)}
                        aria-label={`Foto ${idx + 1}`}
                      />
                    ))}
                  </div>
                </>
              )}
            </div>

            {allImages.length > 1 && (
              <div className="experience-detail__thumbnails" aria-label="Galería de la experiencia">
                {allImages.map((image, index) => (
                  <button
                    type="button"
                    key={image.id}
                    className={`experience-detail__thumbnail ${index === activeImageIndex ? 'is-active' : ''}`}
                    onClick={() => handleSelectImage(index)}
                  >
                    <img
                      src={resolveApiAssetUrl(image.url)}
                      alt={image.altText || `Foto ${index + 1} de ${experience.title}`}
                    />
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        <aside className="experience-detail__summary surface-panel" aria-labelledby="experience-summary-title">
          <h2 id="experience-summary-title">Información</h2>
          <div className="experience-detail__price">
            <span>Precio por persona</span>
            <strong>{formatPrice(experience.price)}</strong>
          </div>
          {isSelfGuided ? null : nextSchedule ? (
            <>
              <dl className="experience-detail__facts">
                <div>
                  <dt><TicketCheck size={18} aria-hidden="true" /> Próxima fecha</dt>
                  <dd>{formatDate(nextSchedule.startsAt)}</dd>
                </div>
                <div>
                  <dt><UsersRound size={18} aria-hidden="true" /> Cupos</dt>
                  <dd>{nextSchedule.isUnlimitedCapacity ? 'Sin límite' : nextSchedule.availableSpots}</dd>
                </div>
                <div>
                  <dt><CalendarDays size={18} aria-hidden="true" /> Fechas disponibles</dt>
                  <dd>{schedules.length}</dd>
                </div>
              </dl>
              <Alert tone={canReserve ? 'info' : 'warning'}>
                {nextSchedule.isUnlimitedCapacity
                  ? `${schedules.length === 1 ? '1 fecha disponible' : `${schedules.length} fechas disponibles`} · Sin límite de cupos`
                  : canReserve
                    ? `${nextSchedule.availableSpots} cupos en la próxima fecha.`
                    : 'La próxima fecha está completa.'}
              </Alert>
            </>
          ) : null}
          {canReserve && (
            <>
              <Button
                className="experience-detail__reserve"
                variant="primary"
                fullWidth
                onClick={handleReserve}
              >
                <TicketCheck size={18} aria-hidden="true" /> {isSelfGuided ? 'Agendar visita' : 'Reservar'}
              </Button>
              {!isSelfGuided && (
                <p className="experience-detail__reservation-note">
                  {experience.price === 0
                    ? 'Confirmación inmediata.'
                    : 'Después de reservar, podrás completar el pago.'}
                </p>
              )}
            </>
          )}
        </aside>
      </div>

      <div className="experience-detail__full-content">
        <header className="experience-detail__header">
          <h1>{experience.title}</h1>
          <div className="experience-detail__location">
            <MapPin size={18} aria-hidden="true" />
            <span>{formatLocationLabel(experience.location)}</span>
          </div>
        </header>

        <section className="experience-detail__description" aria-labelledby="experience-description-title">
          <h2 id="experience-description-title">Sobre esta experiencia</h2>
          {experience.shortDescription && <p><strong>{experience.shortDescription}</strong></p>}
          <p>{experience.description}</p>
        </section>

        {(hasBeforeGoing || experience.whatToBring.length > 0 || hasIncludedInformation || hasUsefulInformation) && (
          <div className="experience-detail__catalog-grid">
            {hasBeforeGoing && (
              <div className="experience-info-card">
                <div className="experience-info-card__header">
                  <div className="experience-info-card__badge experience-info-card__badge--blue">
                    <Info size={20} aria-hidden="true" />
                  </div>
                  <h2>Antes de ir</h2>
                </div>
                <div className="experience-info-card__content">
                  {experience.meetingPointInstructions && (
                    <p className="experience-info-card__row">
                      <strong>Punto de encuentro:</strong> {experience.meetingPointInstructions}
                    </p>
                  )}
                  {experience.pickupInformation && (
                    <p className="experience-info-card__row">
                      <strong>Recogida:</strong> {experience.pickupInformation}
                    </p>
                  )}
                  {experience.guestRequirements && (
                    <p className="experience-info-card__row">{experience.guestRequirements}</p>
                  )}
                  {experience.minimumAge !== null && (
                    <p className="experience-info-card__row">
                      <strong>Edad mínima:</strong> {experience.minimumAge} años
                    </p>
                  )}
                </div>
              </div>
            )}
            {experience.whatToBring.length > 0 && (
              <div className="experience-info-card">
                <div className="experience-info-card__header">
                  <div className="experience-info-card__badge experience-info-card__badge--emerald">
                    <Backpack size={20} aria-hidden="true" />
                  </div>
                  <h2>Qué llevar</h2>
                </div>
                <ul className="experience-info-card__list">
                  {experience.whatToBring.map((item) => (
                    <li key={item}>
                      <span className="experience-info-card__bullet" /> {item}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {hasIncludedInformation && (
              <div className="experience-info-card">
                {experience.whatIsIncluded.length > 0 && (
                  <>
                    <div className="experience-info-card__header">
                      <div className="experience-info-card__badge experience-info-card__badge--green">
                        <CheckCircle2 size={20} aria-hidden="true" />
                      </div>
                      <h2>Incluido</h2>
                    </div>
                    <ul className="experience-info-card__list">
                      {experience.whatIsIncluded.map((item) => (
                        <li key={item}>
                          <CheckCircle2 size={16} className="text-green" aria-hidden="true" /> {item}
                        </li>
                      ))}
                    </ul>
                  </>
                )}
                {experience.whatIsNotIncluded.length > 0 && (
                  <>
                    <div className={`experience-info-card__header ${experience.whatIsIncluded.length > 0 ? 'experience-info-card__header--sub' : ''}`}>
                      <div className="experience-info-card__badge experience-info-card__badge--amber">
                        <XCircle size={20} aria-hidden="true" />
                      </div>
                      <h2>No incluido</h2>
                    </div>
                    <ul className="experience-info-card__list">
                      {experience.whatIsNotIncluded.map((item) => (
                        <li key={item}>
                          <XCircle size={16} className="text-amber" aria-hidden="true" /> {item}
                        </li>
                      ))}
                    </ul>
                  </>
                )}
              </div>
            )}
            {hasUsefulInformation && (
              <div className="experience-info-card">
                <div className="experience-info-card__header">
                  <div className="experience-info-card__badge experience-info-card__badge--purple">
                    <Globe size={20} aria-hidden="true" />
                  </div>
                  <h2>Información útil</h2>
                </div>
                <div className="experience-info-card__content">
                  {experience.languages.length > 0 && (
                    <p className="experience-info-card__row">
                      <strong>Idiomas:</strong> {experience.languages.join(', ')}
                    </p>
                  )}
                  {experience.difficulty && (
                    <p className="experience-info-card__row">
                      <strong>Dificultad:</strong> {getDifficultyLabel(experience.difficulty)}
                    </p>
                  )}
                  {experience.accessibilityInformation && (
                    <p className="experience-info-card__row">
                      <strong>Accesibilidad:</strong> {experience.accessibilityInformation}
                    </p>
                  )}
                  {experience.cancellationPolicy && (
                    <p className="experience-info-card__row">
                      <strong>Política de cancelación:</strong> {getCancellationPolicyLabel(experience.cancellationPolicy)}
                    </p>
                  )}
                </div>
              </div>
            )}
          </div>
        )}

        {experience.itinerary.length > 0 && (
          <section className="experience-detail__itinerary-section" aria-labelledby="experience-itinerary-title">
            <div className="experience-detail__itinerary-header">
              <h2 id="experience-itinerary-title">Itinerario de la experiencia</h2>
              <span className="experience-detail__itinerary-count">
                {experience.itinerary.length} {experience.itinerary.length === 1 ? 'parada' : 'paradas'}
              </span>
            </div>
            <div className="itinerary-cards-flow">
              {experience.itinerary.map((item, index) => (
                <div key={item.id ?? item.sortOrder ?? index} className="itinerary-card-step">
                  <div className="itinerary-card">
                    <div className="itinerary-card__top">
                      <span className="itinerary-card__number">{index + 1}</span>
                      <div className="itinerary-card__badges">
                        <span className="itinerary-card__duration">
                          <Clock size={13} aria-hidden="true" /> {item.durationMinutes} min
                        </span>
                        {item.location && (
                          <span className="itinerary-card__location">
                            <MapPin size={13} aria-hidden="true" /> {item.location}
                          </span>
                        )}
                      </div>
                    </div>
                    <h3 className="itinerary-card__title">{item.title}</h3>
                    <p className="itinerary-card__description">{item.description}</p>
                  </div>
                  {index < experience.itinerary.length - 1 && (
                    <div className="itinerary-card-step__arrow" aria-hidden="true">
                      <ArrowRight size={20} />
                    </div>
                  )}
                </div>
              ))}
            </div>
          </section>
        )}

        {experience.latitude !== null && experience.longitude !== null && (
          <section className="experience-detail__map" aria-labelledby="experience-map-title">
            <h2 id="experience-map-title">Dónde se realiza</h2>
            <p>{formatLocationLabel(experience.location)}</p>
            <a
              className="button-link button-link--outline experience-detail__map-action"
              href={experience.latitude !== null && experience.longitude !== null
                ? `https://www.google.com/maps/search/?api=1&query=${experience.latitude},${experience.longitude}`
                : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(experience.location)}`}
              target="_blank"
              rel="noreferrer"
            >
              <Navigation size={17} aria-hidden="true" /> Ir al lugar
            </a>
            <Suspense fallback={<Skeleton className="experience-detail__map-loading" />}>
              <ExperienceMap
                points={detailMapPoints}
                label={`Ubicación de ${experience.title}`}
              />
            </Suspense>
          </section>
        )}

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
              Todavía no hay reseñas.
            </p>
          ) : (
            <ol>{reviews.map((review) => <li key={review.id} className="review-card">
              <div><strong>{review.authorName}</strong><RatingStars value={review.rating} /></div>
              <p>{review.comment}</p><small>{formatDate(review.createdAt)}</small>
            </li>)}</ol>
          )}
        </section>
      </div>
      {reservationOpen && (
        <ReservationDialog
          experience={experience}
          schedules={schedules}
          onClose={() => setReservationOpen(false)}
        />
      )}

      {isLightboxOpen && allImages.length > 0 && (
        <div
          className="experience-lightbox animate-fade-in"
          onClick={() => setIsLightboxOpen(false)}
          role="dialog"
          aria-modal="true"
          aria-label="Visualizador de imágenes a pantalla completa"
        >
          <div className="experience-lightbox__container" onClick={(e) => e.stopPropagation()}>
            <div className="experience-lightbox__header">
              <span className="experience-lightbox__count">
                Foto {activeImageIndex + 1} de {allImages.length}
              </span>
              <button
                type="button"
                className="experience-lightbox__close"
                onClick={() => setIsLightboxOpen(false)}
                aria-label="Cerrar vista ampliada"
              >
                <X size={22} aria-hidden="true" />
              </button>
            </div>

            <div className="experience-lightbox__stage">
              <img
                src={resolveApiAssetUrl(allImages[activeImageIndex].url)}
                alt={allImages[activeImageIndex].altText || experience.title}
                className="experience-lightbox__image"
              />

              {allImages.length > 1 && (
                <>
                  <button
                    type="button"
                    className="experience-lightbox__control experience-lightbox__control--prev"
                    onClick={handlePrevImage}
                    aria-label="Imagen anterior"
                  >
                    <ChevronLeft size={28} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="experience-lightbox__control experience-lightbox__control--next"
                    onClick={handleNextImage}
                    aria-label="Siguiente imagen"
                  >
                    <ChevronRight size={28} aria-hidden="true" />
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </article>
  );
};

export default ExperienceDetail;
