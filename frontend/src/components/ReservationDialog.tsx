import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import Alert from './Alert';
import Button from './Button';
import Dialog from './Dialog';
import Input from './Input';
import { useAuth } from '../hooks/useAuth';
import { getFieldError, toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { reservationService } from '../services/reservationService';
import type { Experience } from '../types';

interface ReservationDialogProps {
  experience: Experience;
  onClose: () => void;
  onExperienceUpdate: (experience: Experience) => void;
}

const formatPrice = (price: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
}).format(price);

export const ReservationDialog = ({
  experience,
  onClose,
  onExperienceUpdate,
}: ReservationDialogProps) => {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const requestInFlight = useRef(false);
  const [quantity, setQuantity] = useState('1');
  const [fieldError, setFieldError] = useState<string>();
  const [requestError, setRequestError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const parsedQuantity = Number(quantity);
  const total = Number.isInteger(parsedQuantity) && parsedQuantity > 0
    ? experience.price * parsedQuantity
    : 0;

  const validate = () => {
    if (!Number.isInteger(parsedQuantity) || parsedQuantity < 1) {
      setFieldError('La cantidad debe ser mayor que cero.');
      return false;
    }
    if (parsedQuantity > experience.availableSpots) {
      setFieldError('La experiencia no tiene suficientes cupos disponibles.');
      return false;
    }
    setFieldError(undefined);
    return true;
  };

  const refreshAvailability = async () => {
    try {
      const currentExperience = await experienceService.getExperience(experience.id);
      onExperienceUpdate(currentExperience);
    } catch {
      // El error original de reserva sigue siendo el mensaje principal.
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (requestInFlight.current || !validate()) return;

    if (!isAuthenticated) {
      navigate('/login', {
        state: {
          from: `${location.pathname}${location.search}`,
          message: 'Inicia sesión para reservar esta experiencia.',
        },
      });
      return;
    }

    requestInFlight.current = true;
    setIsSubmitting(true);
    setRequestError(null);

    try {
      const reservation = await reservationService.create({
        experienceId: experience.id,
        quantity: parsedQuantity,
      });
      navigate(`/reservations/${reservation.id}`, { replace: true, state: { created: true } });
    } catch (error: unknown) {
      const apiError = toApiError(error, 'No fue posible crear la reserva.');
      setFieldError(getFieldError(apiError, 'Quantity') || getFieldError(apiError, 'ExperienceId'));
      setRequestError(apiError.message);

      if (apiError.status === 409) await refreshAvailability();
      if (apiError.status === 401) {
        navigate('/login', {
          state: {
            from: `${location.pathname}${location.search}`,
            message: 'Tu sesión expiró. Inicia sesión nuevamente para reservar.',
          },
        });
      }
    } finally {
      requestInFlight.current = false;
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      open
      title="Revisar reserva"
      onClose={onClose}
      closeDisabled={isSubmitting}
      footer={(
        <>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>Cancelar</Button>
          <Button
            type="submit"
            form="reservation-form"
            isLoading={isSubmitting}
            disabled={experience.availableSpots === 0}
          >
            Crear reserva pendiente
          </Button>
        </>
      )}
    >
      <form id="reservation-form" className="reservation-form" onSubmit={handleSubmit} noValidate>
        {requestError && <Alert tone="error">{requestError}</Alert>}
        {experience.availableSpots === 0 && (
          <Alert tone="warning">Actualmente no quedan cupos para esta experiencia.</Alert>
        )}

        <div className="reservation-form__experience">
          <span>Experiencia</span>
          <strong>{experience.title}</strong>
          <small>{experience.location}</small>
        </div>

        <Input
          label="Cantidad de personas"
          type="number"
          min="1"
          max={experience.availableSpots}
          step="1"
          inputMode="numeric"
          value={quantity}
          onChange={(event) => setQuantity(event.target.value)}
          error={fieldError}
          hint={`${experience.availableSpots} cupos disponibles`}
          disabled={experience.availableSpots === 0}
        />

        <dl className="reservation-form__summary">
          <div>
            <dt>Precio por persona</dt>
            <dd>{formatPrice(experience.price)}</dd>
          </div>
          <div>
            <dt>Cantidad</dt>
            <dd>{Number.isInteger(parsedQuantity) && parsedQuantity > 0 ? parsedQuantity : '—'}</dd>
          </div>
          <div className="reservation-form__total">
            <dt>Total estimado</dt>
            <dd>{formatPrice(total)}</dd>
          </div>
        </dl>

        <Alert tone="info">
          La reserva se creará con estado <strong>Pending</strong>. No implica pago ni confirmación final.
        </Alert>
      </form>
    </Dialog>
  );
};

export default ReservationDialog;
