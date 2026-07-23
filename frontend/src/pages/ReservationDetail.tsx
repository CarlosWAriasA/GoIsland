import axios from 'axios';
import { ArrowLeft, CalendarDays, MapPin, ReceiptText, TicketCheck, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { reservationService } from '../services/reservationService';
import { getReservationStatusLabel, getReservationStatusTone } from '../utils/reservationStatus';
import type { ExperienceSchedule, Reservation } from '../types';

const formatPrice = (price: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency', currency: 'USD',
}).format(price);

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'long', timeStyle: 'short',
}).format(new Date(date));

const ReservationDetailSkeleton = () => (
  <div className="container reservation-detail reservation-detail--loading" role="status" aria-busy="true">
    <span className="visually-hidden">Cargando detalle de la reserva.</span>
    <Skeleton className="experience-skeleton__line experience-skeleton__line--short" />
    <Skeleton className="reservation-detail-skeleton__panel" />
  </div>
);

interface ReservationDetailResult {
  requestKey: string;
  reservation: Reservation | null;
  schedules: ExperienceSchedule[];
  error: string | null;
  notFound: boolean;
}

export const ReservationDetail = () => {
  const { id } = useParams();
  const location = useLocation();
  const parsedId = Number(id);
  const isValidId = Number.isInteger(parsedId) && parsedId > 0;
  const [retryCount, setRetryCount] = useState(0);
  const requestKey = `${parsedId}::${retryCount}`;
  const [result, setResult] = useState<ReservationDetailResult | null>(null);
  const [selectedScheduleId, setSelectedScheduleId] = useState('');
  const [busyAction, setBusyAction] = useState<'cancel' | 'reschedule' | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const loading = isValidId && result?.requestKey !== requestKey;
  const currentResult = result?.requestKey === requestKey ? result : null;
  const created = location.state?.created === true;

  useEffect(() => {
    if (!isValidId) return;
    const controller = new AbortController();
    reservationService.getById(parsedId, controller.signal)
      .then(async (reservation) => {
        let schedules: ExperienceSchedule[] = [];
        if (reservation.status === 'PendingPayment' || reservation.status === 'Confirmed') {
          schedules = await experienceService.getAvailability(
            reservation.experienceId,
            undefined,
            controller.signal,
          );
        }
        if (!controller.signal.aborted) {
          setResult({ requestKey, reservation, schedules, error: null, notFound: false });
          setSelectedScheduleId(String(schedules.find((item) => item.id !== reservation.scheduleId)?.id ?? ''));
        }
      })
      .catch((requestError: unknown) => {
        if (axios.isCancel(requestError)) return;
        const apiError = toApiError(requestError, 'No fue posible cargar la reserva.');
        setResult({
          requestKey, reservation: null, schedules: [],
          error: apiError.status === 404 ? null : apiError.message,
          notFound: apiError.status === 404,
        });
      });
    return () => controller.abort();
  }, [isValidId, parsedId, requestKey]);

  const replaceReservation = (reservation: Reservation) => {
    setResult((current) => current?.requestKey === requestKey
      ? { ...current, reservation }
      : current);
  };

  const cancelReservation = async () => {
    if (!window.confirm('¿Cancelar esta reserva y liberar sus cupos?')) return;
    setBusyAction('cancel');
    setActionError(null);
    try {
      replaceReservation(await reservationService.cancel(parsedId));
      setActionMessage('La reserva fue cancelada y los cupos quedaron liberados.');
    } catch (error: unknown) {
      setActionError(toApiError(error, 'No fue posible cancelar la reserva.').message);
    } finally {
      setBusyAction(null);
    }
  };

  const rescheduleReservation = async () => {
    const scheduleId = Number(selectedScheduleId);
    if (!Number.isInteger(scheduleId) || scheduleId < 1) return;
    setBusyAction('reschedule');
    setActionError(null);
    try {
      const updated = await reservationService.reschedule(parsedId, scheduleId);
      replaceReservation(updated);
      setActionMessage('La reserva fue reprogramada y los cupos se movieron al nuevo horario.');
      setRetryCount((current) => current + 1);
    } catch (error: unknown) {
      setActionError(toApiError(error, 'No fue posible reprogramar la reserva.').message);
    } finally {
      setBusyAction(null);
    }
  };

  if (loading) return <ReservationDetailSkeleton />;
  if (!isValidId || currentResult?.notFound) {
    return (
      <div className="container reservation-detail-state animate-fade-in">
        <EmptyState title="Reserva no disponible" description="La reserva no existe o no pertenece a tu cuenta."
          action={<Link className="button-link button-link--outline" to="/reservations">Volver a mis reservas</Link>} />
      </div>
    );
  }
  if (currentResult?.error || !currentResult?.reservation) {
    return <div className="container reservation-detail-state"><ErrorState
      description={currentResult?.error || 'No fue posible cargar la reserva.'}
      onRetry={() => setRetryCount((current) => current + 1)} /></div>;
  }

  const { reservation, schedules } = currentResult;
  const active = reservation.status === 'PendingPayment' || reservation.status === 'Confirmed';

  return (
    <div className="container reservation-detail animate-fade-in">
      <Link className="reservation-detail__back" to="/reservations"><ArrowLeft size={18} /> Volver a mis reservas</Link>
      {created && <Alert tone="success">Reserva creada. Estado actual: <strong>{getReservationStatusLabel(reservation.status)}</strong>.</Alert>}
      {actionMessage && <Alert tone="success">{actionMessage}</Alert>}
      {actionError && <Alert tone="error">{actionError}</Alert>}

      <header className="reservation-detail__header">
        <div><span className="page-heading__eyebrow">Reserva #{reservation.id}</span><h1>Detalle de reserva</h1></div>
        <StatusBadge tone={getReservationStatusTone(reservation.status)}>{getReservationStatusLabel(reservation.status)}</StatusBadge>
      </header>

      <div className="reservation-detail__layout">
        <section className="surface-panel reservation-detail__experience" aria-labelledby="reserved-experience-title">
          <div className="reservation-detail__icon" aria-hidden="true"><TicketCheck /></div>
          <div>
            <span>Experiencia reservada</span><h2 id="reserved-experience-title">{reservation.experienceTitle}</h2>
            <p><MapPin size={17} /> {reservation.experienceLocation}</p>
            <Link to={`/experiences/${reservation.experienceId}`}>Ver experiencia</Link>
          </div>
        </section>
        <section className="surface-panel reservation-detail__summary" aria-labelledby="reservation-summary-title">
          <h2 id="reservation-summary-title">Resumen</h2>
          <dl>
            <div><dt><CalendarDays size={18} /> Horario</dt><dd>{formatDate(reservation.startsAt)}</dd></div>
            <div><dt><UsersRound size={18} /> Personas</dt><dd>{reservation.quantity}</dd></div>
            <div><dt><ReceiptText size={18} /> Total</dt><dd>{formatPrice(reservation.totalAmount)}</dd></div>
          </dl>
        </section>
      </div>

      {active && (
        <section className="surface-panel reservation-actions" aria-labelledby="reservation-actions-title">
          <h2 id="reservation-actions-title">Gestionar reserva</h2>
          {schedules.some((schedule) => schedule.id !== reservation.scheduleId) && (
            <div className="reservation-actions__reschedule">
              <SelectField label="Nuevo horario" value={selectedScheduleId}
                onChange={(event) => setSelectedScheduleId(event.target.value)}>
                {schedules.filter((schedule) => schedule.id !== reservation.scheduleId).map((schedule) => (
                  <option key={schedule.id} value={schedule.id}>{formatDate(schedule.startsAt)} · {schedule.availableSpots} cupos</option>
                ))}
              </SelectField>
              <Button onClick={() => void rescheduleReservation()} isLoading={busyAction === 'reschedule'}
                disabled={!selectedScheduleId || busyAction !== null}>Reprogramar</Button>
            </div>
          )}
          <Button variant="danger" onClick={() => void cancelReservation()}
            isLoading={busyAction === 'cancel'} disabled={busyAction !== null}>Cancelar reserva</Button>
        </section>
      )}

      <section className="surface-panel reservation-history" aria-labelledby="reservation-history-title">
        <h2 id="reservation-history-title">Historial</h2>
        <ol>
          {reservation.statusHistory.map((item, index) => (
            <li key={`${item.createdAt}-${index}`}>
              <strong>{getReservationStatusLabel(item.toStatus)}</strong> · {formatDate(item.createdAt)}
              {item.reason && <span>{item.reason}</span>}
            </li>
          ))}
        </ol>
      </section>
    </div>
  );
};

export default ReservationDetail;
