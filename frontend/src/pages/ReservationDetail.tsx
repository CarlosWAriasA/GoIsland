import axios from 'axios';
import { ArrowLeft, CalendarDays, CreditCard, MapPin, ReceiptText, ShieldCheck, TicketCheck, UsersRound } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import TextAreaField from '../components/TextAreaField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import ToastFeedback from '../components/ToastFeedback';
import ReservationExpirationCountdown from '../components/ReservationExpirationCountdown';
import StripePaymentForm from '../components/StripePaymentForm';
import { useAuth } from '../hooks/useAuth';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { paymentService } from '../services/paymentService';
import { reservationService } from '../services/reservationService';
import { getPaymentStatusLabel, getPaymentStatusTone } from '../utils/paymentStatus';
import { getReservationStatusLabel, getReservationStatusTone } from '../utils/reservationStatus';
import { reviewService } from '../services/reviewService';
import type { ExperienceSchedule, Payment, PaymentCheckout, Reservation, Review } from '../types';

const formatPrice = (price: number, currency = 'USD') => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency }).format(price);

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
  payments: Payment[];
  error: string | null;
  notFound: boolean;
  review: Review | null;
}

type PaymentAction = 'pay' | 'refund';

export const ReservationDetail = () => {
  const { id } = useParams();
  const location = useLocation();
  const parsedId = Number(id);
  const isValidId = Number.isInteger(parsedId) && parsedId > 0;
  const [retryCount, setRetryCount] = useState(0);
  const requestKey = `${parsedId}::${retryCount}`;
  const { user } = useAuth();
  const [result, setResult] = useState<ReservationDetailResult | null>(null);
  const [selectedScheduleId, setSelectedScheduleId] = useState('');
  const [busyAction, setBusyAction] = useState<'cancel' | 'reschedule' | 'review' | PaymentAction | null>(null);
  const [pendingConfirmation, setPendingConfirmation] = useState<'cancel' | 'deleteReview' | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [refundReason, setRefundReason] = useState('');
  const [rating, setRating] = useState('5');
  const [reviewComment, setReviewComment] = useState('');
  const [stripeCheckout, setStripeCheckout] = useState<PaymentCheckout | null>(null);
  const [awaitingPaymentId, setAwaitingPaymentId] = useState<number | null>(null);
  const loading = isValidId && result?.requestKey !== requestKey;
  const currentResult = result?.requestKey === requestKey ? result : null;
  const created = location.state?.created === true;
  const refreshAfterExpiration = useCallback(() => {
    setRetryCount((current) => current + 1);
  }, []);

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
        const payments = await paymentService.getForReservation(parsedId, controller.signal);
        const reviews = reservation.status === 'Completed'
          ? await reviewService.forExperience(
            reservation.experienceId,
            controller.signal,
            reservation.id,
          )
          : null;
        const review = reviews?.items.find((item) => item.reservationId === reservation.id) ?? null;
        if (!controller.signal.aborted) {
          setResult({ requestKey, reservation, schedules, payments, review, error: null, notFound: false });
          setSelectedScheduleId(String(schedules.find((item) => item.id !== reservation.scheduleId)?.id ?? ''));
          setRating(String(review?.rating ?? 5));
          setReviewComment(review?.comment ?? '');
          const latestPayment = payments[0];
          if (new URLSearchParams(location.search).get('payment') === 'return'
              && latestPayment?.provider === 'Stripe'
              && latestPayment.status === 'Pending') {
            setAwaitingPaymentId(latestPayment.id);
          }
        }
      })
      .catch((requestError: unknown) => {
        if (axios.isCancel(requestError)) return;
        const apiError = toApiError(requestError, 'No fue posible cargar la reserva.');
        setResult({
          requestKey, reservation: null, schedules: [], payments: [], review: null,
          error: apiError.status === 404 ? null : apiError.message,
          notFound: apiError.status === 404,
        });
      });
    return () => controller.abort();
  }, [isValidId, location.search, parsedId, requestKey]);

  useEffect(() => {
    if (awaitingPaymentId === null) return;

    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    let attempts = 0;
    const poll = async () => {
      try {
        const payment = await paymentService.getById(awaitingPaymentId, controller.signal);
        if (payment.status !== 'Pending') {
          setAwaitingPaymentId(null);
          setStripeCheckout(null);
          setActionMessage(payment.status === 'Paid'
            ? 'Pago confirmado. Tu reserva está lista.'
            : 'El pago no pudo completarse. Puedes intentarlo nuevamente.');
          setRetryCount((current) => current + 1);
          return;
        }
        attempts += 1;
        if (attempts < 20) timer = setTimeout(() => void poll(), 1500);
        else setActionMessage('Seguimos verificando el pago. Puedes actualizar esta página en unos momentos.');
      } catch (requestError: unknown) {
        if (!axios.isCancel(requestError)) {
          setActionError(toApiError(requestError, 'No pudimos verificar el pago.').message);
        }
      }
    };
    void poll();
    return () => {
      controller.abort();
      if (timer) clearTimeout(timer);
    };
  }, [awaitingPaymentId]);

  const replaceReservation = (reservation: Reservation) => {
    setResult((current) => current?.requestKey === requestKey
      ? { ...current, reservation }
      : current);
  };

  const cancelReservation = async () => {
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

  const runPaymentAction = async (action: PaymentAction, execute: () => Promise<unknown>, message: string) => {
    setBusyAction(action);
    setActionError(null);
    setActionMessage(null);
    try {
      await execute();
      setActionMessage(message);
    } catch (error: unknown) {
      setActionError(toApiError(error, 'No fue posible procesar el pago.').message);
    } finally {
      setRetryCount((current) => current + 1);
      setBusyAction(null);
    }
  };

  const startPayment = async () => {
    setBusyAction('pay');
    setActionError(null);
    setActionMessage(null);
    try {
      const pending = currentResult?.payments[0]?.status === 'Pending'
        ? currentResult.payments[0]
        : null;
      const checkout = pending
        ? await paymentService.resumeCheckout(pending.id)
        : await paymentService.createCheckout(parsedId);
      setResult((current) => current?.requestKey === requestKey
        ? {
            ...current,
            payments: [
              checkout.payment,
              ...current.payments.filter((payment) => payment.id !== checkout.payment.id),
            ],
          }
        : current);

      if (checkout.payment.provider === 'Mock') {
        await paymentService.confirmMock(checkout.payment.id);
        setActionMessage('Pago completado. Tu reserva quedó confirmada.');
        setRetryCount((current) => current + 1);
      } else if (checkout.payment.provider === 'Stripe' && checkout.clientSecret) {
        setStripeCheckout(checkout);
      } else {
        setActionError('El pago de prueba no está disponible en este momento.');
      }
    } catch (requestError: unknown) {
      setActionError(toApiError(requestError, 'No fue posible preparar el pago.').message);
    } finally {
      setBusyAction(null);
    }
  };

  const refundPayment = (paymentId: number) => {
    const reason = refundReason.trim();
    if (reason.length < 3) {
      setActionError('Indica el motivo del reembolso (mínimo 3 caracteres).');
      return;
    }
    void runPaymentAction(
      'refund',
      () => paymentService.refund(paymentId, reason),
      'El reembolso quedó registrado y la reserva pasó a estado reembolsado.',
    );
  };

  const saveReview = async () => {
    const comment = reviewComment.trim();
    if (comment.length < 10) { setActionError('La reseña debe tener al menos 10 caracteres.'); return; }
    setBusyAction('review');
    setActionError(null);
    try {
      const input = { rating: Number(rating), comment };
      const review = currentResult?.review
        ? await reviewService.update(currentResult.review.id, input)
        : await reviewService.create(parsedId, input);
      setResult((current) => current?.requestKey === requestKey ? { ...current, review } : current);
      setActionMessage('Tu reseña verificada fue guardada.');
    } catch (requestError: unknown) {
      setActionError(toApiError(requestError, 'No fue posible guardar la reseña.').message);
    } finally { setBusyAction(null); }
  };

  const deleteReview = async () => {
    if (!currentResult?.review) return;
    setBusyAction('review');
    try {
      await reviewService.remove(currentResult.review.id);
      setResult((current) => current?.requestKey === requestKey ? { ...current, review: null } : current);
      setReviewComment('');
      setActionMessage('Tu reseña fue eliminada.');
    } catch (requestError: unknown) {
      setActionError(toApiError(requestError, 'No fue posible eliminar la reseña.').message);
    } finally { setBusyAction(null); }
  };

  const confirmPendingAction = async () => {
    const action = pendingConfirmation;
    if (!action) return;
    if (action === 'cancel') await cancelReservation();
    else await deleteReview();
    setPendingConfirmation(null);
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

  const { reservation, schedules, payments } = currentResult;
  const active = reservation.status === 'PendingPayment' || reservation.status === 'Confirmed';
  const isOwner = user?.id === reservation.userId;
  const isAdmin = user?.role === 'Admin';
  const latestPayment = payments[0] ?? null;
  const currentPayment = stripeCheckout?.payment ?? latestPayment;
  const showPaymentSection = (isOwner || isAdmin)
    && reservation.status !== 'Expired'
    && (currentPayment !== null || reservation.status === 'PendingPayment');

  return (
    <div className="container reservation-detail animate-fade-in">
      <Link className="reservation-detail__back" to="/reservations"><ArrowLeft size={18} /> Volver a mis reservas</Link>
      <ToastFeedback
        message={created ? `Reserva creada. Estado actual: ${getReservationStatusLabel(reservation.status)}.` : null}
        tone="success"
      />
      <ToastFeedback message={actionMessage} tone="success" />
      <ToastFeedback message={actionError} tone="error" />
      {reservation.status === 'PendingPayment' && reservation.expiresAt && (
        <ReservationExpirationCountdown
          expiresAt={reservation.expiresAt}
          onExpired={refreshAfterExpiration}
        />
      )}
      {reservation.status === 'Expired' && (
        <Alert tone="info">
          El tiempo para pagar terminó y los cupos fueron liberados.{' '}
          <Link to={`/experiences/${reservation.experienceSlug || reservation.experienceId}`}>Consultar nuevas fechas</Link>
        </Alert>
      )}

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
            <Link to={`/experiences/${reservation.experienceSlug || reservation.experienceId}`}>Ver experiencia</Link>
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

      {showPaymentSection && (
        <section className="surface-panel reservation-payment" aria-labelledby="reservation-payment-title">
          <div className="reservation-payment__heading">
            <h2 id="reservation-payment-title">Pago de la reserva</h2>
            {currentPayment && (
              <div className="reservation-payment__badges">
                <StatusBadge tone={getPaymentStatusTone(currentPayment.status)}>
                  {getPaymentStatusLabel(currentPayment.status)}
                </StatusBadge>
              </div>
            )}
          </div>

          {!currentPayment && reservation.status === 'PendingPayment' && (
            isOwner && (
              <Button onClick={startPayment} isLoading={busyAction === 'pay'} disabled={busyAction !== null}>
                <CreditCard size={18} /> Pagar
              </Button>
            )
          )}

          {currentPayment?.status === 'Failed' && reservation.status === 'PendingPayment' && (
            <>
              <Alert tone="error">
                No pudimos completar el pago anterior. Puedes intentarlo nuevamente.
              </Alert>
              {isOwner && (
                <Button onClick={startPayment} isLoading={busyAction === 'pay'} disabled={busyAction !== null}>
                  <CreditCard size={18} /> Pagar
                </Button>
              )}
            </>
          )}

          {currentPayment?.status === 'Pending' && (
            <>
              <dl className="reservation-payment__breakdown">
                <div><dt>Subtotal</dt><dd>{formatPrice(currentPayment.subtotalAmount, currentPayment.currency)}</dd></div>
                <div><dt>Cargo por servicio</dt><dd>{formatPrice(currentPayment.serviceFeeAmount, currentPayment.currency)}</dd></div>
                <div className="reservation-payment__total"><dt>Total a pagar</dt><dd>{formatPrice(currentPayment.totalAmount, currentPayment.currency)}</dd></div>
              </dl>
              {currentPayment.failureCode && (
                <Alert tone="error">Revisa los datos de pago e inténtalo nuevamente.</Alert>
              )}
              <p className="reservation-payment__note">
                El pago está listo para completarse.
              </p>
              {isOwner && stripeCheckout?.clientSecret ? (
                <StripePaymentForm
                  clientSecret={stripeCheckout.clientSecret}
                  reservationId={reservation.id}
                  onCancel={() => setStripeCheckout(null)}
                  onSubmitted={() => {
                    setStripeCheckout(null);
                    setAwaitingPaymentId(currentPayment.id);
                    setActionMessage('Estamos confirmando tu pago.');
                  }}
                />
              ) : isOwner && awaitingPaymentId !== currentPayment.id && (
                <Button onClick={startPayment} isLoading={busyAction === 'pay'} disabled={busyAction !== null}>
                  <CreditCard size={18} /> Pagar
                </Button>
              )}
            </>
          )}

          {currentPayment?.status === 'Paid' && (
            <>
              <dl className="reservation-payment__breakdown">
                <div><dt>Total pagado</dt><dd>{formatPrice(currentPayment.totalAmount, currentPayment.currency)}</dd></div>
                <div><dt>Fecha de pago</dt><dd>{currentPayment.paidAt ? formatDate(currentPayment.paidAt) : '—'}</dd></div>
                <div><dt>Número de operación</dt><dd>{currentPayment.providerPaymentId ?? '—'}</dd></div>
              </dl>
              {isAdmin && (
                <div className="reservation-payment__refund">
                  <Input
                    label="Motivo del reembolso"
                    value={refundReason}
                    onChange={(event) => setRefundReason(event.target.value)}
                    hint="Este motivo quedará guardado para futuras consultas (mínimo 3 caracteres)."
                    required
                  />
                  <Button variant="danger" onClick={() => refundPayment(currentPayment.id)}
                    isLoading={busyAction === 'refund'} disabled={busyAction !== null}>
                    <ShieldCheck size={18} /> Reembolsar pago
                  </Button>
                </div>
              )}
            </>
          )}

          {currentPayment?.status === 'Refunded' && (
            <dl className="reservation-payment__breakdown">
              <div><dt>Monto reembolsado</dt><dd>{formatPrice(currentPayment.refundedAmount ?? currentPayment.totalAmount, currentPayment.currency)}</dd></div>
              <div><dt>Número de operación</dt><dd>{currentPayment.providerPaymentId ?? '—'}</dd></div>
            </dl>
          )}
        </section>
      )}

      {active && (
        <section className="surface-panel reservation-actions" aria-labelledby="reservation-actions-title">
          <h2 id="reservation-actions-title">Gestionar reserva</h2>
          {schedules.some((schedule) => schedule.id !== reservation.scheduleId) && (
            <div className="reservation-actions__reschedule">
              <SelectField label="Nuevo horario" value={selectedScheduleId} required
                onChange={(event) => setSelectedScheduleId(event.target.value)}>
                {schedules.filter((schedule) => schedule.id !== reservation.scheduleId).map((schedule) => (
                  <option key={schedule.id} value={schedule.id}>{formatDate(schedule.startsAt)} · {schedule.availableSpots} cupos</option>
                ))}
              </SelectField>
              <Button onClick={() => void rescheduleReservation()} isLoading={busyAction === 'reschedule'}
                disabled={!selectedScheduleId || busyAction !== null}>Reprogramar</Button>
            </div>
          )}
          <Button variant="danger" onClick={() => setPendingConfirmation('cancel')}
            isLoading={busyAction === 'cancel'} disabled={busyAction !== null}>Cancelar reserva</Button>
        </section>
      )}

      {reservation.status === 'Completed' && isOwner && (
        <section className="surface-panel review-form" aria-labelledby="review-form-title">
          <h2 id="review-form-title">{currentResult.review ? 'Editar tu reseña' : 'Comparte tu experiencia'}</h2>
          <p>Esta opinion queda vinculada a una reserva completada.</p>
          <SelectField label="Calificación" value={rating} required onChange={(event) => setRating(event.target.value)}>
            <option value="5">5 · Excelente</option><option value="4">4 · Muy buena</option>
            <option value="3">3 · Buena</option><option value="2">2 · Regular</option><option value="1">1 · Mala</option>
          </SelectField>
          <TextAreaField label="Comentario" value={reviewComment} maxLength={1000} required
            hint={`${reviewComment.length}/1000; minimo 10 caracteres.`}
            onChange={(event) => setReviewComment(event.target.value)} />
          <div className="management-actions">
            <Button onClick={() => void saveReview()} isLoading={busyAction === 'review'}>
              Guardar reseña
            </Button>
            {currentResult.review && <Button variant="danger" onClick={() => setPendingConfirmation('deleteReview')}>Eliminar reseña</Button>}
          </div>
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
      <ConfirmDialog
        open={pendingConfirmation !== null}
        title={pendingConfirmation === 'cancel' ? 'Cancelar reserva' : 'Eliminar reseña'}
        message={pendingConfirmation === 'cancel'
          ? '¿Quieres cancelar esta reserva? Los cupos quedarán disponibles nuevamente.'
          : '¿Quieres eliminar tu reseña? Esta acción no se puede deshacer.'}
        confirmLabel={pendingConfirmation === 'cancel' ? 'Cancelar reserva' : 'Eliminar reseña'}
        isConfirming={busyAction !== null}
        onClose={() => setPendingConfirmation(null)}
        onConfirm={() => void confirmPendingAction()}
      />
    </div>
  );
};

export default ReservationDetail;
