import { CalendarDays, CheckCheck, MapPin, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import PromptDialog from '../components/PromptDialog';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import ToastFeedback from '../components/ToastFeedback';
import { toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { getReservationStatusLabel, getReservationStatusTone } from '../utils/reservationStatus';
import type { Reservation, ReservationStatus } from '../types';

const PAGE_SIZE = 15;
const RESERVATION_STATUSES: ReservationStatus[] = [
  'PendingPayment', 'Expired', 'Confirmed', 'CancelledByTourist', 'CancelledByHost',
  'Completed', 'RefundPending', 'Refunded',
];

const formatDate = (value: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(value));

const formatCurrency = (value: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency', currency: 'USD',
}).format(value);

export const HostReservations = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedPage = Number(searchParams.get('page'));
  const currentPage = Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  const statusValue = searchParams.get('status') as ReservationStatus | null;
  const currentStatus = statusValue && RESERVATION_STATUSES.includes(statusValue) ? statusValue : undefined;
  const currentQuery = searchParams.get('q')?.slice(0, 160).trim() || '';
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const queryString = searchParams.toString();
  const [queryState, setQueryState] = useState({ source: queryString, value: currentQuery });
  const queryDraft = queryState.source === queryString ? queryState.value : currentQuery;
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const [reservationToCancel, setReservationToCancel] = useState<Reservation | null>(null);

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
    queueMicrotask(() => {
      if (!controller.signal.aborted) setLoading(true);
    });
    reservationService.getForHost({
      query: currentQuery || undefined,
      status: currentStatus,
      page: currentPage,
      pageSize: PAGE_SIZE,
    }, controller.signal).then((data) => {
      setReservations(data.items);
      setTotalItems(data.totalItems);
      setTotalPages(data.totalPages);
      setNow(Date.now());
      setError(null);
    })
      .catch((requestError: unknown) => { if (!controller.signal.aborted) setError(toApiError(requestError).message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [currentPage, currentQuery, currentStatus, retry]);

  const cancel = async (reservation: Reservation, reason: string) => {
    setBusyId(reservation.id); setError(null); setSuccess(null);
    try {
      const updated = await reservationService.cancelByHost(reservation.id, reason.trim());
      setReservations((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess('Reserva cancelada; los cupos fueron liberados.');
      setRetry((value) => value + 1);
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally {
      setBusyId(null);
      setReservationToCancel(null);
    }
  };

  const complete = async (reservation: Reservation) => {
    setBusyId(reservation.id); setError(null); setSuccess(null);
    try {
      const updated = await reservationService.completeByHost(reservation.id);
      setReservations((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess('Reserva completada. El turista ya puede dejar su reseña.');
    } catch (requestError: unknown) {
      setError(toApiError(requestError, 'No fue posible completar la reserva.').message);
    } finally { setBusyId(null); }
  };

  return <div className="container management-page animate-fade-in">
    <header className="page-heading"><span className="page-heading__eyebrow">Panel de anfitrión</span><h1>Reservas recibidas</h1>
      <p>Gestiona las reservas de tus experiencias: márcalas como completadas al terminar o cancélalas si hace falta.</p></header>
    <ToastFeedback message={success} tone="success" />
    <ToastFeedback message={error} tone="error" />
    <form className="management-list-toolbar surface-panel" role="search" onSubmit={(event) => {
      event.preventDefault();
      updateParams({ query: queryDraft, status: currentStatus, page: 1 });
    }}>
      <Input label="Buscar reservas" placeholder="Experiencia o ubicación" value={queryDraft}
        maxLength={160} onChange={(event) => setQueryState({ source: queryString, value: event.target.value })} />
      <SelectField label="Estado" value={currentStatus ?? ''} onChange={(event) => updateParams({
        query: queryDraft,
        status: event.target.value as ReservationStatus || undefined,
        page: 1,
      })}>
        <option value="">Todos los estados</option>
        {RESERVATION_STATUSES.map((status) => <option value={status} key={status}>{getReservationStatusLabel(status)}</option>)}
      </SelectField>
      <Button type="submit" variant="outline">Buscar</Button>
    </form>
    {loading ? (
      <div className="operations-list" role="status">
        {[1, 2, 3].map((item) => <Skeleton key={item} className="operations-row operations-row--loading" />)}
        <span className="visually-hidden">Cargando reservas recibidas…</span>
      </div>
    ) : error && reservations.length === 0
      ? <ErrorState description={error} onRetry={() => { setLoading(true); setRetry((value) => value + 1); }} />
      : reservations.length === 0 ? <EmptyState title="Sin reservas recibidas" description="Las nuevas reservas aparecerán aquí." />
        : <div className="operations-list" aria-label="Reservas recibidas">{reservations.map((reservation) => {
          const active = reservation.status === 'PendingPayment' || reservation.status === 'Confirmed';
          const canComplete = reservation.status === 'Confirmed'
            && new Date(reservation.endsAt).getTime() <= now;
          return <article className="operations-row operations-row--reservations" key={reservation.id}>
            <div className="operations-row__main">
              <div className="operations-row__primary">
                <span className="operations-row__reference">Reserva #{reservation.id} · Turista #{reservation.userId}</span>
                <h2>{reservation.experienceTitle}</h2>
                <small><MapPin size={14} aria-hidden="true" />{reservation.experienceLocation}</small>
              </div>
              <div className="operations-row__cell">
                <span><CalendarDays size={14} aria-hidden="true" />Horario</span>
                <strong>{formatDate(reservation.startsAt)}</strong>
              </div>
              <div className="operations-row__cell">
                <span><UsersRound size={14} aria-hidden="true" />Personas</span>
                <strong>{reservation.quantity}</strong>
                <small>{formatCurrency(reservation.totalAmount)}</small>
              </div>
              <StatusBadge tone={getReservationStatusTone(reservation.status)}>
                {getReservationStatusLabel(reservation.status)}
              </StatusBadge>
              <div className="operations-row__actions">
                {canComplete && <Button size="sm" onClick={() => void complete(reservation)}
                  isLoading={busyId === reservation.id}>
                  <CheckCheck size={15} aria-hidden="true" />Completar
                </Button>}
                {active && <Button size="sm" variant="danger" onClick={() => setReservationToCancel(reservation)}
                  isLoading={busyId === reservation.id}>Cancelar</Button>}
              </div>
            </div>
          </article>;
        })}</div>}
    {!loading && !error && totalItems > 0 && <p className="management-list-total">
      {totalItems} {totalItems === 1 ? 'reserva' : 'reservas'}
    </p>}
    {!loading && !error && totalPages > 1 && <nav className="catalog-pagination" aria-label="Páginas de reservas recibidas">
      <Button variant="outline" disabled={currentPage <= 1}
        onClick={() => updateParams({ query: currentQuery, status: currentStatus, page: currentPage - 1 })}>Anterior</Button>
      <span>Página {currentPage} de {totalPages}</span>
      <Button variant="outline" disabled={currentPage >= totalPages}
        onClick={() => updateParams({ query: currentQuery, status: currentStatus, page: currentPage + 1 })}>Siguiente</Button>
    </nav>}
    <PromptDialog
      open={reservationToCancel !== null}
      title="Cancelar reserva"
      description="El turista recibirá este motivo junto con la actualización de la reserva."
      label="Motivo de cancelación"
      placeholder="Escribe el motivo para el turista"
      confirmLabel="Cancelar reserva"
      isConfirming={busyId !== null}
      onClose={() => setReservationToCancel(null)}
      onConfirm={async (reason) => {
        if (reservationToCancel) await cancel(reservationToCancel, reason);
      }}
    />
  </div>;
};

export default HostReservations;
