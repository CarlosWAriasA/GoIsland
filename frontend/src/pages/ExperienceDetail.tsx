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
import { useEffect, useState } from 'react';
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
import type { Experience } from '../types';

const formatPrice = (price: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
}).format(price);

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
}).format(new Date(date));

const getCategoryIcon = (category: string) => {
  const iconProps = { size: 76, 'aria-hidden': true as const };
  switch (category.toLowerCase()) {
    case 'acuático':
    case 'acuatico':
      return <Waves {...iconProps} />;
    case 'cruceros':
      return <Ship {...iconProps} />;
    case 'gastronomía':
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
    error: string | null;
    notFound: boolean;
  } | null>(null);
  const loading = isValidId && result?.requestKey !== requestKey;
  const experience = result?.requestKey === requestKey ? result.experience : null;
  const error = result?.requestKey === requestKey ? result.error : null;
  const notFound = !isValidId || (result?.requestKey === requestKey && result.notFound);

  useEffect(() => {
    if (!isValidId) return;

    const controller = new AbortController();

    experienceService.getExperience(parsedId, controller.signal)
      .then((data) => setResult({ requestKey, experience: data, error: null, notFound: false }))
      .catch((requestError: unknown) => {
        if (axios.isCancel(requestError)) return;
        const apiError = toApiError(requestError, 'No fue posible cargar esta experiencia.');
        setResult({
          requestKey,
          experience: null,
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

  const availabilityTone = experience.availableSpots === 0 ? 'warning' : 'info';
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

  const handleExperienceUpdate = (updatedExperience: Experience) => {
    setResult((current) => current?.requestKey === requestKey
      ? { ...current, experience: updatedExperience }
      : current);
  };

  return (
    <article className="container experience-detail animate-fade-in">
      <Link className="experience-detail__back" to={requestedReturnPath}>
        <ArrowLeft size={18} aria-hidden="true" /> Volver al catálogo
      </Link>

      <div className="experience-detail__layout">
        <div className="experience-detail__main">
          <div className="experience-detail__placeholder" role="img" aria-label="Imagen no disponible">
            {getCategoryIcon(experience.category)}
            <span>{experience.category}</span>
            <small>Imagen no disponible</small>
          </div>

          <header className="experience-detail__header">
            <span className="experience-detail__category">{experience.category}</span>
            <h1>{experience.title}</h1>
            <div className="experience-detail__location">
              <MapPin size={18} aria-hidden="true" />
              <span>{experience.location}</span>
            </div>
          </header>

          <section className="experience-detail__description" aria-labelledby="experience-description-title">
            <h2 id="experience-description-title">Sobre esta experiencia</h2>
            <p>{experience.description}</p>
          </section>
        </div>

        <aside className="experience-detail__summary surface-panel" aria-labelledby="experience-summary-title">
          <h2 id="experience-summary-title">Información</h2>
          <div className="experience-detail__price">
            <span>Precio por persona</span>
            <strong>{formatPrice(experience.price)}</strong>
          </div>
          <dl className="experience-detail__facts">
            <div>
              <dt><TicketCheck size={18} aria-hidden="true" /> Cupos disponibles</dt>
              <dd>{experience.availableSpots}</dd>
            </div>
            <div>
              <dt><UsersRound size={18} aria-hidden="true" /> Capacidad total</dt>
              <dd>{experience.capacity}</dd>
            </div>
            <div>
              <dt><CalendarDays size={18} aria-hidden="true" /> Publicada</dt>
              <dd>{formatDate(experience.createdAt)}</dd>
            </div>
          </dl>
          <Alert tone={availabilityTone}>
            {experience.availableSpots === 0 ? (
              <>Actualmente no quedan cupos.</>
            ) : (
              `${experience.availableSpots} de ${experience.capacity} cupos disponibles.`
            )}
          </Alert>
          <Button
            className="experience-detail__reserve"
            fullWidth
            onClick={handleReserve}
            disabled={experience.availableSpots === 0}
          >
            <TicketCheck size={18} aria-hidden="true" /> Reservar
          </Button>
          <p className="experience-detail__reservation-note">
            La reserva se crea como <strong>Pending</strong>; el pago no está confirmado.
          </p>
        </aside>
      </div>
      {reservationOpen && (
        <ReservationDialog
          experience={experience}
          onClose={() => setReservationOpen(false)}
          onExperienceUpdate={handleExperienceUpdate}
        />
      )}
    </article>
  );
};

export default ExperienceDetail;
