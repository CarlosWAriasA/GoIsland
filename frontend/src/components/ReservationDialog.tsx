import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useLocation, useNavigate } from 'react-router-dom';
import Alert from './Alert';
import Button from './Button';
import Dialog from './Dialog';
import Input from './Input';
import SelectField from './SelectField';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { reservationService } from '../services/reservationService';
import { experienceKeys, reservationKeys } from '../queries/queryKeys';
import { getDefaultDateTimeLocal, getMinDateTimeLocal } from '../utils/dateTimeLocal';
import type { Experience, ExperienceSchedule } from '../types';

interface ReservationDialogProps {
  experience: Experience;
  schedules: ExperienceSchedule[];
  onClose: () => void;
}

const formatPrice = (price: number) => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(price);

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
  const [fieldError, setFieldError] = useState<string>();
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
  const total = Number.isInteger(parsedQuantity) && parsedQuantity > 0
    ? experience.price * parsedQuantity : 0;

  const validate = () => {
    if (!Number.isInteger(parsedQuantity) || parsedQuantity < 1) {
      setFieldError('La cantidad debe ser mayor que cero.');
      return false;
    }

    if (isSelfGuided) {
      if (!startsAtLocal) {
        setFieldError('Selecciona una fecha y hora para tu visita.');
        return false;
      }
      const selectedDate = new Date(startsAtLocal);
      if (isNaN(selectedDate.getTime()) || selectedDate <= new Date()) {
        setFieldError('La fecha y hora de la visita debe ser en el futuro.');
        return false;
      }
    } else {
      if (!selectedSchedule) {
        setFieldError('Selecciona un horario disponible.');
        return false;
      }
      if (!selectedSchedule.isUnlimitedCapacity && parsedQuantity > selectedSchedule.availableSpots) {
        setFieldError('El horario no tiene suficientes cupos disponibles.');
        return false;
      }
    }

    setFieldError(undefined);
    return true;
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (requestInFlight.current || !validate()) return;
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
      setFieldError(getFieldError(apiError, 'StartsAtLocal') || getFieldError(apiError, 'Quantity') || getFieldError(apiError, 'ScheduleId'));
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
          <Button type="submit" variant="primary" form="reservation-form" isLoading={isSubmitting} disabled={!isSelfGuided && !selectedSchedule}>
            {isSelfGuided
              ? 'Confirmar agendado'
              : experience.price === 0
                ? 'Confirmar reserva gratis'
                : 'Crear reserva pendiente de pago'}
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
            onChange={(event) => { setStartsAtLocal(event.target.value); setFieldError(undefined); }}
            error={fieldError && startsAtLocal ? undefined : fieldError}
            required
          />
        ) : (
          <SelectField
            label="Fecha y horario" value={scheduleId}
            onChange={(event) => { setScheduleId(event.target.value); setFieldError(undefined); }}
            error={!selectedSchedule ? fieldError : undefined}
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
            ? selectedSchedule.availableSpots
            : undefined}
          step="1" inputMode="numeric"
          value={quantity} onChange={(event) => setQuantity(event.target.value)}
          error={!isSelfGuided && selectedSchedule ? fieldError : isSelfGuided ? fieldError : undefined}
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
          <div><dt>Precio por persona</dt><dd>{formatPrice(experience.price)}</dd></div>
          <div><dt>Cantidad</dt><dd>{parsedQuantity > 0 ? parsedQuantity : '—'}</dd></div>
          <div className="reservation-form__total"><dt>Total</dt><dd>{formatPrice(total)}</dd></div>
        </dl>

        {!isSelfGuided && (
          <Alert tone="info">
            {experience.price === 0
              ? <>Esta experiencia es gratis; la reserva quedará <strong>confirmada inmediatamente</strong>.</>
              : <>La reserva quedará <strong>Pendiente de pago</strong>. Todavía no implica pago ni confirmación.</>}
          </Alert>
        )}
      </form>
    </Dialog>
  );
};

export default ReservationDialog;
