import { useEffect } from 'react';
import { toast } from 'react-hot-toast';

type ToastFeedbackTone = 'success' | 'error' | 'info' | 'warning';

interface ToastFeedbackProps {
  message: string | null | undefined;
  tone: ToastFeedbackTone;
}

export const ToastFeedback = ({ message, tone }: ToastFeedbackProps) => {
  useEffect(() => {
    if (!message) return;

    const options = { id: `feedback-${tone}-${message}` };
    if (tone === 'success') {
      toast.success(message, options);
    } else if (tone === 'error') {
      toast.error(message, options);
    } else if (tone === 'warning') {
      toast(message, { ...options, icon: '⚠️' });
    } else {
      toast(message, options);
    }
  }, [message, tone]);

  return null;
};

export default ToastFeedback;
