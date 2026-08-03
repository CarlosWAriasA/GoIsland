import { CalendarDays, CheckCheck, MapPin, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { getReservationStatusLabel, getReservationStatusTone } from '../utils/reservationStatus';
import type { Reservation } from '../types';

const formatDate = (value: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(value));

const formatCurrency = (value: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency', currency: 'USD',
}).format(value);

export const HostReservations = () => {
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const controller = new AbortController();
    reservationService.getForHost(controller.signal).then((data) => {
      setReservations(data);
      setNow(Date.now());
    })
      .catch((requestError: unknown) => { if (!controller.signal.aborted) setError(toApiError(requestError).message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [retry]);

  const cancel = async (reservation: Reservation) => {
    const reason = window.prompt('Motivo de cancelación para el turista:');
    if (!reason?.trim()) return;
    setBusyId(reservation.id); setError(null); setSuccess(null);
    try {
      const updated = await reservationService.cancelByHost(reservation.id, reason.trim());
      setReservations((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess('Reserva cancelada; los cupos fueron liberados.');
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally { setBusyId(null); }
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
    {success && <Alert tone="success">{success}</Alert>}{error && <Alert tone="error">{error}</Alert>}
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
                {active && <Button size="sm" variant="danger" onClick={() => void cancel(reservation)}
                  disabled={busyId === reservation.id}>Cancelar</Button>}
              </div>
            </div>
          </article>;
        })}</div>}
  </div>;
};

export default HostReservations;
