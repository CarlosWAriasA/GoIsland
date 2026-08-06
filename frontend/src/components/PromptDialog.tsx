import { useId, useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import Button from './Button';
import Dialog from './Dialog';
import TextAreaField from './TextAreaField';

interface PromptDialogProps {
  open: boolean;
  title: string;
  label: string;
  description?: ReactNode;
  placeholder?: string;
  initialValue?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  isConfirming?: boolean;
  onConfirm: (value: string) => void | Promise<void>;
  onClose: () => void;
}

export const PromptDialog = ({
  open,
  title,
  label,
  description,
  placeholder,
  initialValue = '',
  confirmLabel = 'Continuar',
  cancelLabel = 'Cancelar',
  isConfirming = false,
  onConfirm,
  onClose,
}: PromptDialogProps) => {
  const formId = useId();
  const [error, setError] = useState<string>();

  const close = () => {
    setError(undefined);
    onClose();
  };

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const formData = new FormData(event.currentTarget);
    const normalizedValue = String(formData.get('promptValue') ?? '').trim();
    if (!normalizedValue) {
      setError('Escribe un motivo para continuar.');
      return;
    }
    setError(undefined);
    void onConfirm(normalizedValue);
  };

  return (
    <Dialog
      open={open}
      title={title}
      onClose={close}
      closeDisabled={isConfirming}
      footer={(
        <>
          <Button variant="outline" onClick={close} disabled={isConfirming}>{cancelLabel}</Button>
          <Button type="submit" form={formId} isLoading={isConfirming}>{confirmLabel}</Button>
        </>
      )}
    >
      <form id={formId} onSubmit={submit} noValidate>
        {description && <p>{description}</p>}
        <TextAreaField
          label={label}
          name="promptValue"
          defaultValue={initialValue}
          placeholder={placeholder}
          error={error}
          rows={4}
          autoFocus
          required
        />
      </form>
    </Dialog>
  );
};

export default PromptDialog;
