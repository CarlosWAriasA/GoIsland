import { Elements, PaymentElement, useElements, useStripe } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js/pure';
import { useState, type FormEvent } from 'react';
import Alert from './Alert';
import Button from './Button';

const publishableKey = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY?.trim() ?? '';
const stripePromise = publishableKey.startsWith('pk_test_')
  ? loadStripe(publishableKey)
  : null;

interface StripePaymentFormProps {
  clientSecret: string;
  reservationId: number;
  onSubmitted: (paymentId: string) => void;
  onCancel: () => void;
}

const PaymentForm = ({ reservationId, onSubmitted, onCancel }: StripePaymentFormProps) => {
  const stripe = useStripe();
  const elements = useElements();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!stripe || !elements) return;

    setSubmitting(true);
    setError(null);
    const result = await stripe.confirmPayment({
      elements,
      confirmParams: {
        return_url: `${window.location.origin}/reservations/${reservationId}?payment=return`,
      },
      redirect: 'if_required',
    });

    if (result.error) {
      setError(result.error.message || 'No pudimos completar el pago. Revisa los datos e inténtalo nuevamente.');
      setSubmitting(false);
      return;
    }

    if (result.paymentIntent) onSubmitted(result.paymentIntent.id);
    setSubmitting(false);
  };

  return (
    <form className="stripe-payment-form" onSubmit={(event) => void submit(event)}>
      <Alert tone="info">Pago de prueba. No se realizarán cargos reales.</Alert>
      <PaymentElement options={{ layout: 'tabs' }} />
      {error && <Alert tone="error">{error}</Alert>}
      <div className="stripe-payment-form__actions">
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Volver
        </Button>
        <Button type="submit" isLoading={submitting} disabled={!stripe || !elements || submitting}>
          Completar pago de prueba
        </Button>
      </div>
    </form>
  );
};

export const StripePaymentForm = (props: StripePaymentFormProps) => {
  if (!stripePromise) {
    return (
      <Alert tone="error">
        El pago de prueba no está disponible en este momento. Inténtalo nuevamente más tarde.
      </Alert>
    );
  }

  return (
    <Elements
      stripe={stripePromise}
      options={{
        clientSecret: props.clientSecret,
        appearance: {
          theme: 'stripe',
          variables: {
            colorPrimary: '#0d766e',
            borderRadius: '10px',
            fontFamily: 'Inter, system-ui, sans-serif',
          },
        },
      }}
    >
      <PaymentForm {...props} />
    </Elements>
  );
};

export default StripePaymentForm;
