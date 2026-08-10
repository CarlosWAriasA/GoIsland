import type { ReactNode } from 'react';
import Button from './Button';
import Dialog from './Dialog';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  isConfirming?: boolean;
  confirmVariant?: 'primary' | 'accent' | 'secondary' | 'danger';
  onConfirm: () => void;
  onClose: () => void;
}

export const ConfirmDialog = ({
  open,
  title,
  message,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  isConfirming = false,
  confirmVariant = 'danger',
  onConfirm,
  onClose,
}: ConfirmDialogProps) => (
  <Dialog
    open={open}
    title={title}
    onClose={onClose}
    closeDisabled={isConfirming}
    footer={(
      <>
        <Button variant="outline" onClick={onClose} disabled={isConfirming}>{cancelLabel}</Button>
        <Button variant={confirmVariant} onClick={onConfirm} isLoading={isConfirming}>{confirmLabel}</Button>
      </>
    )}
  >
    <p>{message}</p>
  </Dialog>
);

export default ConfirmDialog;
