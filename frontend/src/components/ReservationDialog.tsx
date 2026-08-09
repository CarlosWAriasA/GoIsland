import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocation, useNavigate } from 'react-router-dom';
import Alert from './Alert';
import Button from './Button';
import Dialog from './Dialog';
import Input from './Input';
import SelectField from './SelectField';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { experienceService } from '../services/experienceService';
import { experienceKeys, reservationKeys } from '../queries/queryKeys';
import { getDefaultDateTimeLocal, getMinDateTimeLocal } from '../utils/dateTimeLocal';
import type { Experience, ExperienceSchedule } from '../types';
import { isValidReservationQuantity } from '../utils/reservationQuantity';

interface ReservationDialogProps {
  experience: Experience;
  schedules: ExperienceSchedule[];
  onClose: () => void;
}

interface ReservationFieldErrors {
  schedule?: string;
  startsAt?: string;
  quantity?: string;
}

const formatPrice = (price: number, currency = 'USD') => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency }).format(price);

const formatSchedule = (startsAt: string, endsAt: string) => {
  const start = new Date(startsAt);
  const end = new Date(endsAt);
  return `${new Intl.DateTimeFormat('es-DO', {
    dateStyle: 'medium', timeStyle: 'short',
  }).format(start)} – ${new Intl.DateTimeFormat('es-DO', {
    hour: 'numeric', minute: '2-digit',
  }).format(end)}`;
};

export const ReservationDialog = ({
  experience, schedules, onClose,
}: ReservationDialogProps) => {
  const isSelfGuided = experience.schedulingMode === 'SelfGuided';
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const requestInFlight = useRef(false);

  const [scheduleId, setScheduleId] = useState(String(schedules[0]?.id ?? ''));
  const [startsAtLocal, setStartsAtLocal] = useState(getDefaultDateTimeLocal);
  const [quantity, setQuantity] = useState('1');
  const [fieldErrors, setFieldErrors] = useState<ReservationFieldErrors>({});
  const [requestError, setRequestError] = useState<string | null>(null);

  const createReservation = useMutation({
    mutationFn: reservationService.create,
    onSuccess: (_reservation, variables) => {
      queryClient.setQueryData<ExperienceSchedule[]>(
        experienceKeys.availability(experience.id),
        (current) => current?.map((schedule) => schedule.id === variables.scheduleId
          && !schedule.isUnlimitedCapacity
          ? { ...schedule, availableSpots: Math.max(0, schedule.availableSpots - variables.quantity) }
          : schedule),
      );
      void queryClient.invalidateQueries({ queryKey: experienceKeys.all });
      void queryClient.invalidateQueries({ queryKey: reservationKeys.all });
    },
  });

  const createSelfScheduledReservation = useMutation({
    mutationFn: reservationService.createSelfScheduled,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: experienceKeys.all });
      void queryClient.invalidateQueries({ queryKey: reservationKeys.all });
    },
  });

  const isSubmitting = createReservation.isPending || createSelfScheduledReservation.isPending;
  const selectedSchedule = schedules.find((schedule) => schedule.id === Number(scheduleId));
  const parsedQuantity = Number(quantity);
  const hasValidQuantity = isValidReservationQuantity(parsedQuantity);
  const quoteQuery = useQuery({
    queryKey: experienceKeys.paymentQuote(experience.id, parsedQuantity),
    queryFn: ({ signal }) => experienceService.getPaymentQuote(experience.id, parsedQuantity, signal),
    enabled: experience.price > 0 && hasValidQuantity,
    staleTime: 5 * 60_000,
    retry: 1,
  });
  const currency = quoteQuery.data?.currency ?? 'USD';
  const subtotal = experience.price === 0
    ? (hasValidQuantity ? experience.price * parsedQuantity : 0)
    : quoteQuery.data?.subtotalAmount;
  const serviceFee = quoteQuery.data?.serviceFeeAmount;
  const total = experience.price === 0 ? 0 : quoteQuery.data?.totalAmount;

  const validate = () => {
    const errors: ReservationFieldErrors = {};
    if (!hasValidQuantity) {
      errors.quantity = 'La cantidad debe estar entre 1 y 100.';
    }

    if (isSelfGuided) {
      if (!startsAtLocal) {
        errors.startsAt = 'Selecciona una fecha y hora para tu visita.';
      } else {
        const selectedDate = new Date(startsAtLocal);
        if (isNaN(selectedDate.getTime()) || selectedDate <= new Date()) {
          errors.startsAt = 'La fecha y hora de la visita debe ser en el futuro.';
        }
      }
    } else {
      if (!selectedSchedule) {
        errors.schedule = 'Selecciona un horario disponible.';
      } else if (!selectedSchedule.isUnlimitedCapacity && parsedQuantity > selectedSchedule.availableSpots) {
        errors.quantity = 'El horario no tiene suficientes cupos disponibles.';
      }
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (requestInFlight.current || !validate()) return;
    if (experience.price > 0 && !quoteQuery.data) {
      setRequestError('No pudimos calcular el total. Inténtalo nuevamente.');
      return;
    }
    if (!isAuthenticated) {
      navigate('/login', { state: { from: location.pathname, message: isSelfGuided ? 'Inicia sesión para agendar tu visita.' : 'Inicia sesión para reservar.' } });
      return;
    }

    requestInFlight.current = true;
    setRequestError(null);
    try {
      if (isSelfGuided) {
        const reservation = await createSelfScheduledReservation.mutateAsync({
          experienceId: experience.id,
          startsAtLocal,
          quantity: parsedQuantity,
        });
        navigate(`/reservations/${reservation.id}`, { replace: true, state: { created: true } });
      } else {
        if (!selectedSchedule) return;
        const reservation = await createReservation.mutateAsync({
          scheduleId: selectedSchedule.id,
          quantity: parsedQuantity,
        });
        navigate(`/reservations/${reservation.id}`, { replace: true, state: { created: true } });
      }
    } catch (error: unknown) {
      const apiError = toApiError(error, isSelfGuided ? 'No fue posible agendar la visita.' : 'No fue posible crear la reserva.');
      setFieldErrors({
        startsAt: getFieldError(apiError, 'StartsAtLocal'),
        quantity: getFieldError(apiError, 'Quantity'),
        schedule: getFieldError(apiError, 'ScheduleId'),
      });
      setRequestError(apiError.message);
      if (apiError.status === 409 && !isSelfGuided) {
        await queryClient.refetchQueries({
          queryKey: experienceKeys.availability(experience.id),
          type: 'active',
        });
      }
    } finally {
      requestInFlight.current = false;
    }
  };

  return (
    <Dialog
      open title={isSelfGuided ? 'Agendar visita' : 'Revisar reserva'} onClose={onClose} closeDisabled={isSubmitting}
      footer={(
        <>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>Volver</Button>
          <Button
            type="submit"
            variant="primary"
            form="reservation-form"
            isLoading={isSubmitting}
            disabled={(!isSelfGuided && !selectedSchedule)
              || (experience.price > 0 && (!quoteQuery.data || quoteQuery.isFetching))}
          >
            {isSelfGuided
              ? 'Confirmar visita'
              : experience.price === 0
                ? 'Confirmar reserva'
                : 'Crear reserva'}
          </Button>
        </>
      )}
    >
      <form id="reservation-form" className="reservation-form" onSubmit={handleSubmit} noValidate>
        {requestError && <Alert tone="error">{requestError}</Alert>}
        <div className="reservation-form__experience">
          <span>Experiencia</span><strong>{experience.title}</strong><small>{experience.location}</small>
        </div>

        {isSelfGuided ? (
          <Input
            label="Fecha y hora de visita"
            type="datetime-local"
            min={getMinDateTimeLocal()}
            value={startsAtLocal}
            onChange={(event) => { setStartsAtLocal(event.target.value); setFieldErrors((current) => ({ ...current, startsAt: undefined })); }}
            error={fieldErrors.startsAt}
            required
          />
        ) : (
          <SelectField
            label="Fecha y horario" value={scheduleId}
            onChange={(event) => { setScheduleId(event.target.value); setFieldErrors((current) => ({ ...current, schedule: undefined })); }}
            error={fieldErrors.schedule}
            required
          >
            {schedules.map((schedule) => (
              <option key={schedule.id} value={schedule.id}>
                {formatSchedule(schedule.startsAt, schedule.endsAt)} · {schedule.isUnlimitedCapacity ? 'Sin límite' : `${schedule.availableSpots} cupos`}
              </option>
            ))}
          </SelectField>
        )}

        <Input
          label="Cantidad de personas" type="number" min="1"
          max={!isSelfGuided && selectedSchedule && !selectedSchedule.isUnlimitedCapacity
            ? Math.min(100, selectedSchedule.availableSpots)
            : 100}
          step="1" inputMode="numeric"
          value={quantity} onChange={(event) => {
            setQuantity(event.target.value);
            setFieldErrors((current) => ({ ...current, quantity: undefined }));
          }}
          error={fieldErrors.quantity}
          hint={isSelfGuided
            ? 'Esta experiencia autoguiada no tiene límite de cupos'
            : selectedSchedule
              ? selectedSchedule.isUnlimitedCapacity
                ? 'Este horario no tiene límite de personas'
                : `${selectedSchedule.availableSpots} cupos en este horario`
              : undefined}
          disabled={!isSelfGuided && !selectedSchedule}
          required
        />

        <dl className="reservation-form__summary">
          <div><dt>Precio por persona</dt><dd>{formatPrice(experience.price, currency)}</dd></div>
          <div><dt>Cantidad</dt><dd>{parsedQuantity > 0 ? parsedQuantity : '—'}</dd></div>
          <div><dt>Subtotal</dt><dd>{subtotal === undefined ? 'Calculando…' : formatPrice(subtotal, currency)}</dd></div>
          {experience.price > 0 && (
            <div><dt>Cargo por servicio</dt><dd>{serviceFee === undefined ? 'Calculando…' : formatPrice(serviceFee, currency)}</dd></div>
          )}
          <div className="reservation-form__total">
            <dt>Total a pagar</dt><dd>{total === undefined ? 'Calculando…' : formatPrice(total, currency)}</dd>
          </div>
        </dl>

        {experience.price > 0 && quoteQuery.isError && (
          <Alert tone="error">
            No pudimos calcular el total.{' '}
            <button type="button" className="link-button" onClick={() => void quoteQuery.refetch()}>
              Reintentar
            </button>
          </Alert>
        )}

        {!isSelfGuided && experience.price > 0 && (
          <Alert tone="info">Crearás la reserva y podrás completar el pago seguro desde el siguiente paso.</Alert>
        )}

        {!isSelfGuided && experience.price === 0 && (
          <Alert tone="info">Esta experiencia es gratis: tu reserva queda confirmada de inmediato.</Alert>
        )}
      </form>
    </Dialog>
  );
};

export default ReservationDialog;
