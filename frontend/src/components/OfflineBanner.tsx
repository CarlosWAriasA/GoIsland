import { WifiOff } from 'lucide-react';
import { useEffect, useState } from 'react';

// Aviso global de estado "sin conexión". Solo observa el estado del navegador
// (navigator.onLine) y no realiza ninguna llamada a la API.
export const OfflineBanner = () => {
  const [offline, setOffline] = useState(() => typeof navigator !== 'undefined' && !navigator.onLine);

  useEffect(() => {
    const goOnline = () => setOffline(false);
    const goOffline = () => setOffline(true);
    window.addEventListener('online', goOnline);
    window.addEventListener('offline', goOffline);
    return () => {
      window.removeEventListener('online', goOnline);
      window.removeEventListener('offline', goOffline);
    };
  }, []);

  if (!offline) return null;

  return (
    <div className="offline-banner" role="status" aria-live="polite">
      <WifiOff size={17} aria-hidden="true" />
      <span>Sin conexión. Revisa tu internet; los cambios no se enviarán hasta que vuelvas a estar en línea.</span>
    </div>
  );
};

export default OfflineBanner;
