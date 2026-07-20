import axios from 'axios';
import { ArrowLeft, CalendarDays, MapPin, ReceiptText, TicketCheck, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { reservationService } from '../services/reservationService';
import type { Experience, Reservation } from '../types';

const formatPrice = (price: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
}).format(price);

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'long',
  timeStyle: 'short',
}).format(new Date(date));

const getStatusTone = (status: string): 'warning' | 'success' | 'error' | 'info' => {
  if (status === 'Confirmed' || status === 'Paid') return 'success';
  if (status === 'Cancelled' || status === 'Rejected') return 'error';
  return status === 'Pending' ? 'warning' : 'info';
};

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
  experience: Experience | null;
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
  const loading = isValidId && result?.requestKey !== requestKey;
  const currentResult = result?.requestKey === requestKey ? result : null;
  const created = location.state?.created === true;

  useEffect(() => {
    if (!isValidId) return;
    const controller = new AbortController();

    reservationService.getById(parsedId, controller.signal)
      .then(async (reservation) => {
        let experience: Experience | null = null;
        try {
          experience = await experienceService.getExperience(reservation.experienceId, controller.signal);
        } catch (requestError: unknown) {
          if (axios.isCancel(requestError)) throw requestError;
        }

        if (!controller.signal.aborted) {
          setResult({ requestKey, reservation, experience, error: null, notFound: false });
        }
      })
      .catch((requestError: unknown) => {
        if (axios.isCancel(requestError)) return;
        const apiError = toApiError(requestError, 'No fue posible cargar la reserva.');
        setResult({
          requestKey,
          reservation: null,
          experience: null,
          error: apiError.status === 404 ? null : apiError.message,
          notFound: apiError.status === 404,
        });
      });

    return () => controller.abort();
  }, [isValidId, parsedId, requestKey]);

  if (loading) return <ReservationDetailSkeleton />;

  if (!isValidId || currentResult?.notFound) {
    return (
      <div className="container reservation-detail-state animate-fade-in">
        <EmptyState
          title="Reserva no disponible"
          description="La reserva no existe o no pertenece a tu cuenta."
          action={<Link className="button-link button-link--outline" to="/reservations">Volver a mis reservas</Link>}
        />
      </div>
    );
  }

  if (currentResult?.error || !currentResult?.reservation) {
    return (
      <div className="container reservation-detail-state animate-fade-in">
        <ErrorState
          description={currentResult?.error || 'No fue posible cargar la reserva.'}
          onRetry={() => setRetryCount((current) => current + 1)}
        />
      </div>
    );
  }

  const { reservation, experience } = currentResult;

  return (
    <div className="container reservation-detail animate-fade-in">
      <Link className="reservation-detail__back" to="/reservations">
        <ArrowLeft size={18} aria-hidden="true" /> Volver a mis reservas
      </Link>

      {created && (
        <Alert tone="success">
          Reserva creada correctamente con estado <strong>{reservation.status}</strong>.
        </Alert>
      )}

      <header className="reservation-detail__header">
        <div>
          <span className="page-heading__eyebrow">Reserva #{reservation.id}</span>
          <h1>Detalle de reserva</h1>
        </div>
        <StatusBadge tone={getStatusTone(reservation.status)}>{reservation.status}</StatusBadge>
      </header>

      <div className="reservation-detail__layout">
        <section className="surface-panel reservation-detail__experience" aria-labelledby="reserved-experience-title">
          <div className="reservation-detail__icon" aria-hidden="true"><TicketCheck /></div>
          <div>
            <span>Experiencia reservada</span>
            <h2 id="reserved-experience-title">
              {experience?.title || `Experiencia #${reservation.experienceId}`}
            </h2>
            {experience && (
              <>
                <p><MapPin size={17} aria-hidden="true" /> {experience.location}</p>
                <Link to={`/experiences/${experience.id}`}>Ver experiencia</Link>
              </>
            )}
          </div>
        </section>

        <section className="surface-panel reservation-detail__summary" aria-labelledby="reservation-summary-title">
          <h2 id="reservation-summary-title">Resumen</h2>
          <dl>
            <div>
              <dt><UsersRound size={18} aria-hidden="true" /> Cantidad de personas</dt>
              <dd>{reservation.quantity}</dd>
            </div>
            <div>
              <dt><ReceiptText size={18} aria-hidden="true" /> Total</dt>
              <dd>{formatPrice(reservation.totalAmount)}</dd>
            </div>
            <div>
              <dt><CalendarDays size={18} aria-hidden="true" /> Fecha de creación</dt>
              <dd>{formatDate(reservation.reservationDate)}</dd>
            </div>
          </dl>
        </section>
      </div>

      <Alert tone="info">
        Estado actual: <strong>{reservation.status}</strong>. No se muestra pago confirmado ni correo enviado.
      </Alert>
    </div>
  );
};

export default ReservationDetail;
