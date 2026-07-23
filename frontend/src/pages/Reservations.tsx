import { CalendarDays, MapPin, ReceiptText, TicketCheck, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { getReservationStatusLabel, getReservationStatusTone } from '../utils/reservationStatus';
import type { Reservation } from '../types';

const formatPrice = (price: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
}).format(price);

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
}).format(new Date(date));

const ReservationsSkeleton = () => (
  <div className="reservation-list" aria-hidden="true">
    {[1, 2, 3].map((item) => (
      <div className="surface-panel reservation-card reservation-card--loading" key={item} aria-hidden="true">
        <Skeleton className="experience-skeleton__line experience-skeleton__line--short" />
        <Skeleton className="experience-skeleton__line" />
        <Skeleton className="experience-skeleton__line" />
      </div>
    ))}
  </div>
);

interface ReservationsResult {
  requestKey: number;
  reservations: Reservation[];
  error: string | null;
}

export const Reservations = () => {
  const [retryCount, setRetryCount] = useState(0);
  const [result, setResult] = useState<ReservationsResult | null>(null);
  const loading = result?.requestKey !== retryCount;
  const reservations = result?.requestKey === retryCount ? result.reservations : [];
  const error = result?.requestKey === retryCount ? result.error : null;

  useEffect(() => {
    const controller = new AbortController();

    reservationService.getMy(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setResult({
            requestKey: retryCount,
            reservations: data,
            error: null,
          });
        }
      })
      .catch((requestError: unknown) => {
        if (controller.signal.aborted) return;
        setResult({
          requestKey: retryCount,
          reservations: [],
          error: toApiError(requestError, 'No fue posible cargar tus reservas.').message,
        });
      });

    return () => controller.abort();
  }, [retryCount]);

  return (
    <div className="container reservations-page animate-fade-in">
      <header className="page-heading reservations-page__heading">
        <span className="page-heading__eyebrow">Tu actividad</span>
        <h1>Mis reservas</h1>
        <p>Consulta las reservas creadas con tu cuenta y su estado real.</p>
      </header>

      <section aria-busy={loading}>
        <p className="visually-hidden" role="status" aria-live="polite">
          {loading
            ? 'Cargando tus reservas.'
            : error
              ? 'No fue posible cargar tus reservas.'
              : `${reservations.length} ${reservations.length === 1 ? 'reserva disponible' : 'reservas disponibles'}.`}
        </p>
        {!loading && !error && reservations.length > 0 && (
          <p className="reservations-page__count">
            {reservations.length} {reservations.length === 1 ? 'reserva' : 'reservas'}
          </p>
        )}

        {loading ? (
          <ReservationsSkeleton />
        ) : error ? (
          <ErrorState description={error} onRetry={() => setRetryCount((current) => current + 1)} />
        ) : reservations.length === 0 ? (
          <EmptyState
            title="Todavía no tienes reservas"
            description="Explora el catálogo y crea una reserva cuando encuentres una experiencia para ti."
            action={<Link className="button-link button-link--outline" to="/experiences">Explorar experiencias</Link>}
          />
        ) : (
          <div className="reservation-list">
            {reservations.map((reservation) => {
              return (
                <article className="surface-panel reservation-card" key={reservation.id}>
                  <div className="reservation-card__header">
                    <div>
                      <span className="reservation-card__reference">Reserva #{reservation.id}</span>
                      <h2>{reservation.experienceTitle}</h2>
                    </div>
                    <StatusBadge tone={getReservationStatusTone(reservation.status)}>{getReservationStatusLabel(reservation.status)}</StatusBadge>
                  </div>

                  <p className="reservation-card__location">
                    <MapPin size={16} aria-hidden="true" /> {reservation.experienceLocation}
                  </p>

                  <dl className="reservation-card__facts">
                    <div>
                      <dt><UsersRound size={17} aria-hidden="true" /> Personas</dt>
                      <dd>{reservation.quantity}</dd>
                    </div>
                    <div>
                      <dt><ReceiptText size={17} aria-hidden="true" /> Total</dt>
                      <dd>{formatPrice(reservation.totalAmount)}</dd>
                    </div>
                    <div>
                      <dt><CalendarDays size={17} aria-hidden="true" /> Horario</dt>
                      <dd>{formatDate(reservation.startsAt)}</dd>
                    </div>
                  </dl>

                  <Link className="reservation-card__link" to={`/reservations/${reservation.id}`}>
                    <TicketCheck size={17} aria-hidden="true" /> Ver detalle
                  </Link>
                </article>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
};

export default Reservations;
