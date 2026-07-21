import { CalendarDays, MapPin, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import type { Reservation } from '../types';

const formatDate = (value: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(value));

export const HostReservations = () => {
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const [busyId, setBusyId] = useState<number | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    reservationService.getForHost(controller.signal).then(setReservations)
      .catch((requestError: unknown) => { if (!controller.signal.aborted) setError(toApiError(requestError).message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [retry]);

  const cancel = async (reservation: Reservation) => {
    const reason = window.prompt('Motivo de cancelación para el turista:');
    if (!reason?.trim()) return;
    setBusyId(reservation.id); setError(null);
    try {
      const updated = await reservationService.cancelByHost(reservation.id, reason.trim());
      setReservations((current) => current.map((item) => item.id === updated.id ? updated : item));
      setSuccess('Reserva cancelada; los cupos fueron liberados.');
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally { setBusyId(null); }
  };

  return <div className="container management-page animate-fade-in">
    <header className="page-heading"><span className="page-heading__eyebrow">Panel de anfitrión</span><h1>Reservas recibidas</h1>
      <p>Consulta únicamente reservas de tus experiencias y gestiona cancelaciones.</p></header>
    {success && <Alert tone="success">{success}</Alert>}{error && <Alert tone="error">{error}</Alert>}
    {loading ? <p role="status">Cargando reservas…</p> : error && reservations.length === 0
      ? <ErrorState description={error} onRetry={() => { setLoading(true); setRetry((value) => value + 1); }} />
      : reservations.length === 0 ? <EmptyState title="Sin reservas recibidas" description="Las nuevas reservas aparecerán aquí." />
        : <div className="management-list">{reservations.map((reservation) => {
          const active = reservation.status === 'PendingPayment' || reservation.status === 'Confirmed';
          return <article className="management-card surface-panel" key={reservation.id}>
            <div className="management-card__header"><div><span className="management-card__reference">Reserva #{reservation.id}</span>
              <h2>{reservation.experienceTitle}</h2><p><MapPin size={16} /> {reservation.experienceLocation}</p></div>
              <StatusBadge tone={active ? 'warning' : reservation.status.startsWith('Cancelled') ? 'error' : 'success'}>{reservation.status}</StatusBadge></div>
            <dl className="management-card__facts"><div><dt><CalendarDays size={16} /> Horario</dt><dd>{formatDate(reservation.startsAt)}</dd></div>
              <div><dt><UsersRound size={16} /> Personas</dt><dd>{reservation.quantity}</dd></div></dl>
            {active && <div className="management-actions"><Button variant="danger" onClick={() => void cancel(reservation)}
              isLoading={busyId === reservation.id}>Cancelar por anfitrión</Button></div>}
          </article>;
        })}</div>}
  </div>;
};

export default HostReservations;
