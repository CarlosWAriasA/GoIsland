import { TriangleAlert } from 'lucide-react';
import Button from './Button';

interface ErrorStateProps {
  title?: string;
  description: string;
  onRetry?: () => void;
}

export const ErrorState = ({
  title = 'No pudimos completar la solicitud',
  description,
  onRetry,
}: ErrorStateProps) => (
  <div className="result-state surface-panel" role="alert">
    <TriangleAlert size={42} aria-hidden="true" />
    <h3>{title}</h3>
    <p>{description}</p>
    {onRetry && <Button variant="outline" onClick={onRetry}>Reintentar</Button>}
  </div>
);

export default ErrorState;
