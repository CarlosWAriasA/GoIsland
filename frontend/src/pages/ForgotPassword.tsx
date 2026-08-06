import { Mail } from 'lucide-react';
import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import Button from '../components/Button';
import Input from '../components/Input';
import AuthMosaic from '../components/AuthMosaic';
import Logo from '../components/Logo';
import ToastFeedback from '../components/ToastFeedback';
import { getFieldError, toApiError } from '../services/apiError';
import { authService } from '../services/authService';

export const ForgotPassword = () => {
  const [email, setEmail] = useState('');
  const [fieldError, setFieldError] = useState<string>();
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFieldError(undefined);
    setMessage(null);
    setError(null);

    if (!email.trim()) {
      setFieldError('El correo electronico es obligatorio.');
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await authService.forgotPassword({ email: email.trim() });
      setMessage(response.message);
    } catch (requestError: unknown) {
      const apiError = toApiError(requestError);
      setFieldError(getFieldError(apiError, 'Email'));
      setError(apiError.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="auth-page animate-fade-in">
      <AuthMosaic />
      <section className="auth-card surface-panel" aria-labelledby="forgot-password-title">
        <header className="auth-card__header">
          <span className="auth-card__brand"><Logo iconOnly fontSize="2.4rem" /></span>
          <h1 id="forgot-password-title">Recupera tu contraseña</h1>
          <p>Te enviaremos un enlace si el correo pertenece a una cuenta registrada.</p>
        </header>

        <ToastFeedback message={message} tone="success" />
        <ToastFeedback message={error} tone="error" />

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <Input
            label="Correo electrónico"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            error={fieldError}
            icon={<Mail size={18} />}
            required
          />
          <Button type="submit" fullWidth isLoading={isSubmitting}>Enviar instrucciones</Button>
        </form>

        <p className="auth-card__footer">
          <Link to="/login">Volver a iniciar sesión</Link>
        </p>
      </section>
    </div>
  );
};

export default ForgotPassword;
