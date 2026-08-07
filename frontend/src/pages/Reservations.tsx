import { CalendarDays, MapPin, MapPinned, Pencil, ReceiptText, TicketCheck, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { getReservationStatusLabel, getReservationStatusTone } from '../utils/reservationStatus';
import { buildGoogleMapsUrl } from '../utils/navigation';
import type { Reservation, ReservationStatus } from '../types';

const PAGE_SIZE = 12;
const RESERVATION_STATUSES: ReservationStatus[] = [
  'PendingPayment',
  'Expired',
  'Confirmed',
  'CancelledByTourist',
  'CancelledByHost',
  'Completed',
  'RefundPending',
  'Refunded',
];

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
  requestKey: string;
  reservations: Reservation[];
  totalItems: number;
  totalPages: number;
  error: string | null;
}

export const Reservations = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryString = searchParams.toString();
  const requestedPage = Number(searchParams.get('page'));
  const currentPage = Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  const statusValue = searchParams.get('status') as ReservationStatus | null;
  const currentStatus = statusValue && RESERVATION_STATUSES.includes(statusValue)
    ? statusValue
    : undefined;
  const currentQuery = searchParams.get('q')?.slice(0, 160).trim() || '';
  const [queryState, setQueryState] = useState({ source: queryString, value: currentQuery });
  const queryDraft = queryState.source === queryString ? queryState.value : currentQuery;
  const [retryCount, setRetryCount] = useState(0);
  const [result, setResult] = useState<ReservationsResult | null>(null);
  const requestKey = `${queryString}:${retryCount}`;
  const loading = result?.requestKey !== requestKey;
  const reservations = result?.requestKey === requestKey ? result.reservations : [];
  const totalItems = result?.requestKey === requestKey ? result.totalItems : 0;
  const totalPages = result?.requestKey === requestKey ? result.totalPages : 0;
  const error = result?.requestKey === requestKey ? result.error : null;

  const updateParams = (values: { query?: string; status?: ReservationStatus; page?: number }) => {
    const next = new URLSearchParams();
    const query = values.query?.trim();
    if (query) next.set('q', query);
    if (values.status) next.set('status', values.status);
    if ((values.page ?? 1) > 1) next.set('page', String(values.page));
    setSearchParams(next);
  };

  useEffect(() => {
    const controller = new AbortController();

    reservationService.getMy({
      query: currentQuery || undefined,
      status: currentStatus,
      page: currentPage,
      pageSize: PAGE_SIZE,
    }, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setResult({
            requestKey,
            reservations: data.items,
            totalItems: data.totalItems,
            totalPages: data.totalPages,
            error: null,
          });
        }
      })
      .catch((requestError: unknown) => {
        if (controller.signal.aborted) return;
        setResult({
          requestKey,
          reservations: [],
          totalItems: 0,
          totalPages: 0,
          error: toApiError(requestError, 'No fue posible cargar tus reservas.').message,
        });
      });

    return () => controller.abort();
  }, [currentPage, currentQuery, currentStatus, requestKey]);

  const currentScope = searchParams.get('scope') || 'all';
  const displayedReservations = reservations.filter((reservation) => {
    const isPast = new Date(reservation.endsAt) <= new Date()
      || ['Completed', 'CancelledByTourist', 'CancelledByHost', 'Expired', 'Refunded'].includes(reservation.status);
    if (currentScope === 'upcoming') return !isPast;
    if (currentScope === 'past') return isPast;
    return true;
  });

  return (
    <div className="container reservations-page animate-fade-in">
      <header className="page-heading reservations-page__heading">
        <span className="page-heading__eyebrow">Tu actividad</span>
        <h1>Mis reservas</h1>
        <p>Consulta las reservas creadas con tu cuenta y su estado real.</p>
      </header>

      <form
        className="management-list-toolbar surface-panel"
        role="search"
        onSubmit={(event) => {
          event.preventDefault();
          updateParams({ query: queryDraft, status: currentStatus, page: 1 });
        }}
      >
        <Input
          label="Buscar reservas"
          placeholder="Experiencia o ubicación"
          value={queryDraft}
          maxLength={160}
          onChange={(event) => setQueryState({ source: queryString, value: event.target.value })}
        />
        <SelectField
          label="Período"
          value={currentScope}
          onChange={(event) => {
            const next = new URLSearchParams(searchParams);
            if (event.target.value && event.target.value !== 'all') {
              next.set('scope', event.target.value);
            } else {
              next.delete('scope');
            }
            next.set('page', '1');
            setSearchParams(next);
          }}
        >
          <option value="all">Todas las fechas</option>
          <option value="upcoming">Próximas</option>
          <option value="past">Pasadas</option>
        </SelectField>
        <SelectField
          label="Estado"
          value={currentStatus ?? ''}
          onChange={(event) => updateParams({
            query: queryDraft,
            status: event.target.value as ReservationStatus || undefined,
            page: 1,
          })}
        >
          <option value="">Todos los estados</option>
          {RESERVATION_STATUSES.map((status) => (
            <option value={status} key={status}>{getReservationStatusLabel(status)}</option>
          ))}
        </SelectField>
        <Button type="submit" variant="outline">Buscar</Button>
      </form>

      <section aria-busy={loading}>
        <p className="visually-hidden" role="status" aria-live="polite">
          {loading
            ? 'Cargando tus reservas.'
            : error
              ? 'No fue posible cargar tus reservas.'
              : `${totalItems} ${totalItems === 1 ? 'reserva disponible' : 'reservas disponibles'}.`}
        </p>
        {!loading && !error && displayedReservations.length > 0 && (
          <p className="reservations-page__count">
            {displayedReservations.length} {displayedReservations.length === 1 ? 'reserva' : 'reservas'}
          </p>
        )}

        {loading ? (
          <ReservationsSkeleton />
        ) : error ? (
          <ErrorState description={error} onRetry={() => setRetryCount((current) => current + 1)} />
        ) : displayedReservations.length === 0 ? (
          <EmptyState
            title="No se encontraron reservas"
            description={currentScope !== 'all' ? "No tienes reservas en este período." : "Explora el catálogo y crea una reserva cuando encuentres una experiencia para ti."}
            action={<Link className="button-link button-link--outline" to="/experiences">Explorar experiencias</Link>}
          />
        ) : (
          <div className="reservation-list">
            {displayedReservations.map((reservation) => {
              const isSelfGuided = reservation.schedulingMode === 'SelfGuided';
              const canCompleteVisit = isSelfGuided
                && reservation.status === 'Confirmed' && new Date(reservation.endsAt) <= new Date();
              const canEditVisit = isSelfGuided
                && reservation.status === 'Confirmed' && new Date(reservation.startsAt) > new Date();
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

                  <div className="reservation-card__secondary-links">
                    <Link
                      className="reservation-card__link"
                      to={`/experiences/${reservation.experienceSlug || reservation.experienceId}`}
                      state={{ from: `/reservations${queryString ? `?${queryString}` : ''}` }}
                    >
                      Ver experiencia
                    </Link>
                    <a
                      className="reservation-card__link"
                      href={buildGoogleMapsUrl(reservation.latitude, reservation.longitude, reservation.experienceLocation)}
                      target="_blank"
                      rel="noreferrer"
                    >
                      <MapPinned size={16} aria-hidden="true" /> Ver ubicación
                    </a>
                    <Link
                      className="reservation-card__link"
                      to={`/reservations/${reservation.id}`}
                      state={canEditVisit ? { focusEdit: true } : undefined}
                    >
                      {canCompleteVisit
                        ? <><TicketCheck size={17} aria-hidden="true" /> Marcar realizada y reseñar</>
                        : canEditVisit
                          ? <><Pencil size={17} aria-hidden="true" /> Editar reserva</>
                          : <><TicketCheck size={17} aria-hidden="true" /> Ver detalle</>}
                    </Link>
                  </div>
                </article>
              );
            })}
          </div>
        )}
        {!loading && !error && totalPages > 1 && (
          <nav className="catalog-pagination" aria-label="Páginas de reservas">
            <Button
              variant="outline"
              disabled={currentPage <= 1}
              onClick={() => updateParams({ query: currentQuery, status: currentStatus, page: currentPage - 1 })}
            >Anterior</Button>
            <span>Página {currentPage} de {totalPages}</span>
            <Button
              variant="outline"
              disabled={currentPage >= totalPages}
              onClick={() => updateParams({ query: currentQuery, status: currentStatus, page: currentPage + 1 })}
            >Siguiente</Button>
          </nav>
        )}
      </section>
    </div>
  );
};

export default Reservations;
