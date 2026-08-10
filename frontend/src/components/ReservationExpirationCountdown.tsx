import { useEffect, useState } from 'react';
import Alert from './Alert';

interface ReservationExpirationCountdownProps {
  expiresAt: string;
  onExpired: () => void;
}

const remainingSeconds = (expiresAt: string) => Math.max(
  0,
  Math.ceil((new Date(expiresAt).getTime() - Date.now()) / 1000),
);

const formatRemaining = (seconds: number) => {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const remainder = seconds % 60;
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`
    : `${minutes}:${String(remainder).padStart(2, '0')}`;
};

export const ReservationExpirationCountdown = ({
  expiresAt,
  onExpired,
}: ReservationExpirationCountdownProps) => {
  const [remaining, setRemaining] = useState(() => remainingSeconds(expiresAt));

  useEffect(() => {
    let notified = false;
    const update = () => {
      const next = remainingSeconds(expiresAt);
      setRemaining(next);
      if (next === 0 && !notified) {
        notified = true;
        onExpired();
      }
    };
    update();
    const interval = window.setInterval(update, 1000);
    return () => window.clearInterval(interval);
  }, [expiresAt, onExpired]);

  return (
    <div className="reservation-expiration" aria-live="polite">
      <Alert tone="warning">
        {remaining > 0 ? (
          <>Completa el pago en <strong>{formatRemaining(remaining)}</strong> para conservar tus cupos.</>
        ) : (
          <>El tiempo para pagar terminó.</>
        )}
      </Alert>
    </div>
  );
};

export default ReservationExpirationCountdown;
