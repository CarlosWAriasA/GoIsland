import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import Alert from './Alert';
import Button from './Button';
import Dialog from './Dialog';
import Input from './Input';
import SelectField from './SelectField';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { reservationService } from '../services/reservationService';
import type { Experience, ExperienceSchedule } from '../types';

interface ReservationDialogProps {
  experience: Experience;
  schedules: ExperienceSchedule[];
  onClose: () => void;
  onSchedulesUpdate: (schedules: ExperienceSchedule[]) => void;
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
  experience, schedules, onClose, onSchedulesUpdate,
}: ReservationDialogProps) => {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const requestInFlight = useRef(false);
  const [scheduleId, setScheduleId] = useState(String(schedules[0]?.id ?? ''));
  const [quantity, setQuantity] = useState('1');
  const [fieldError, setFieldError] = useState<string>();
  const [requestError, setRequestError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const selectedSchedule = schedules.find((schedule) => schedule.id === Number(scheduleId));
  const parsedQuantity = Number(quantity);
  const total = Number.isInteger(parsedQuantity) && parsedQuantity > 0
    ? experience.price * parsedQuantity : 0;

  const validate = () => {
    if (!selectedSchedule) {
      setFieldError('Selecciona un horario disponible.');
      return false;
    }
    if (!Number.isInteger(parsedQuantity) || parsedQuantity < 1) {
      setFieldError('La cantidad debe ser mayor que cero.');
      return false;
    }
    if (!selectedSchedule.isUnlimitedCapacity && parsedQuantity > selectedSchedule.availableSpots) {
      setFieldError('El horario no tiene suficientes cupos disponibles.');
      return false;
    }
    setFieldError(undefined);
    return true;
  };

  const refreshAvailability = async () => {
    try {
      onSchedulesUpdate(await experienceService.getAvailability(experience.id));
    } catch {
      // El error de reserva conserva prioridad.
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (requestInFlight.current || !validate() || !selectedSchedule) return;
    if (!isAuthenticated) {
      navigate('/login', { state: { from: location.pathname, message: 'Inicia sesión para reservar.' } });
      return;
    }

    requestInFlight.current = true;
    setIsSubmitting(true);
    setRequestError(null);
    try {
      const reservation = await reservationService.create({
        scheduleId: selectedSchedule.id,
        quantity: parsedQuantity,
      });
      navigate(`/reservations/${reservation.id}`, { replace: true, state: { created: true } });
    } catch (error: unknown) {
      const apiError = toApiError(error, 'No fue posible crear la reserva.');
      setFieldError(getFieldError(apiError, 'Quantity') || getFieldError(apiError, 'ScheduleId'));
      setRequestError(apiError.message);
      if (apiError.status === 409) await refreshAvailability();
    } finally {
      requestInFlight.current = false;
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      open title="Revisar reserva" onClose={onClose} closeDisabled={isSubmitting}
      footer={(
        <>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>Volver</Button>
          <Button type="submit" variant="primary" form="reservation-form" isLoading={isSubmitting} disabled={!selectedSchedule}>
            {experience.price === 0 ? 'Confirmar reserva gratis' : 'Crear reserva pendiente de pago'}
          </Button>
        </>
      )}
    >
      <form id="reservation-form" className="reservation-form" onSubmit={handleSubmit} noValidate>
        {requestError && <Alert tone="error">{requestError}</Alert>}
        <div className="reservation-form__experience">
          <span>Experiencia</span><strong>{experience.title}</strong><small>{experience.location}</small>
        </div>
        <SelectField
          label="Fecha y horario" value={scheduleId}
          onChange={(event) => { setScheduleId(event.target.value); setFieldError(undefined); }}
          error={!selectedSchedule ? fieldError : undefined}
        >
          {schedules.map((schedule) => (
            <option key={schedule.id} value={schedule.id}>
              {formatSchedule(schedule.startsAt, schedule.endsAt)} · {schedule.isUnlimitedCapacity ? 'Sin límite' : `${schedule.availableSpots} cupos`}
            </option>
          ))}
        </SelectField>
        <Input
          label="Cantidad de personas" type="number" min="1"
          max={selectedSchedule && !selectedSchedule.isUnlimitedCapacity
            ? selectedSchedule.availableSpots
            : undefined}
          step="1" inputMode="numeric"
          value={quantity} onChange={(event) => setQuantity(event.target.value)}
          error={selectedSchedule ? fieldError : undefined}
          hint={selectedSchedule
            ? selectedSchedule.isUnlimitedCapacity
              ? 'Este horario no tiene límite de personas'
              : `${selectedSchedule.availableSpots} cupos en este horario`
            : undefined}
          disabled={!selectedSchedule}
        />
        <dl className="reservation-form__summary">
          <div><dt>Precio por persona</dt><dd>{formatPrice(experience.price)}</dd></div>
          <div><dt>Cantidad</dt><dd>{parsedQuantity > 0 ? parsedQuantity : '—'}</dd></div>
          <div className="reservation-form__total"><dt>Total</dt><dd>{formatPrice(total)}</dd></div>
        </dl>
        <Alert tone="info">
          {experience.price === 0
            ? <>Esta experiencia es gratis; la reserva quedará <strong>confirmada inmediatamente</strong>.</>
            : <>La reserva quedará <strong>Pendiente de pago</strong>. Todavía no implica pago ni confirmación.</>}
        </Alert>
      </form>
    </Dialog>
  );
};

export default ReservationDialog;
