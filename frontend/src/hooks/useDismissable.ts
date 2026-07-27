import { useEffect, useRef } from 'react';
import type { RefObject } from 'react';

export const useDismissable = <T extends HTMLElement>(open: boolean, onDismiss: () => void): RefObject<T | null> => {
  const containerRef = useRef<T>(null);

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent | TouchEvent) => {
      const container = containerRef.current;
      if (container && !container.contains(event.target as Node)) onDismiss();
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      onDismiss();
      containerRef.current?.querySelector<HTMLElement>('[data-dismiss-focus]')?.focus();
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('touchstart', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('touchstart', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open, onDismiss]);

  return containerRef;
};

export default useDismissable;
