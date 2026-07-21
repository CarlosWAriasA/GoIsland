import { useEffect, useRef } from 'react';

const GOOGLE_SCRIPT_URL = 'https://accounts.google.com/gsi/client';
const isGoogleAuthConfigured = Boolean(import.meta.env.VITE_GOOGLE_CLIENT_ID);

interface GoogleCredentialResponse {
  credential?: string;
}

interface GoogleAccountsId {
  initialize: (options: {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
  }) => void;
  renderButton: (
    parent: HTMLElement,
    options: {
      theme: string;
      size: string;
      shape: string;
      text: string;
      width: number;
    },
  ) => void;
}

declare global {
  interface Window {
    google?: {
      accounts: {
        id: GoogleAccountsId;
      };
    };
  }
}

interface GoogleSignInButtonProps {
  disabled?: boolean;
  onCredential: (credential: string) => void;
  onError: (message: string) => void;
}

const GoogleSignInButton = ({
  disabled = false,
  onCredential,
  onError,
}: GoogleSignInButtonProps) => {
  const buttonRef = useRef<HTMLDivElement>(null);
  const callbackRef = useRef({ disabled, onCredential, onError });

  useEffect(() => {
    callbackRef.current = { disabled, onCredential, onError };
  }, [disabled, onCredential, onError]);

  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
    if (!clientId || !buttonRef.current) return;

    const renderButton = () => {
      if (!window.google || !buttonRef.current) return;

      buttonRef.current.replaceChildren();
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => {
          if (callbackRef.current.disabled) return;
          if (!response.credential) {
            callbackRef.current.onError('Google no devolvió una credencial válida.');
            return;
          }
          callbackRef.current.onCredential(response.credential);
        },
      });
      const availableWidth = Math.floor(buttonRef.current.getBoundingClientRect().width);
      window.google.accounts.id.renderButton(buttonRef.current, {
        theme: 'outline',
        size: 'large',
        shape: 'rectangular',
        text: 'continue_with',
        width: Math.min(320, availableWidth || 320),
      });
    };

    const existingScript = document.querySelector<HTMLScriptElement>(
      `script[src="${GOOGLE_SCRIPT_URL}"]`,
    );
    if (window.google) {
      renderButton();
      return;
    }

    const script = existingScript ?? document.createElement('script');
    script.addEventListener('load', renderButton);
    const handleError = () => callbackRef.current.onError('No fue posible cargar el acceso con Google.');
    script.addEventListener('error', handleError);

    if (!existingScript) {
      script.src = GOOGLE_SCRIPT_URL;
      script.async = true;
      script.defer = true;
      document.head.appendChild(script);
    }

    return () => {
      script.removeEventListener('load', renderButton);
      script.removeEventListener('error', handleError);
    };
  }, []);

  if (!isGoogleAuthConfigured) return null;

  return (
    <div
      className={`google-sign-in${disabled ? ' google-sign-in--disabled' : ''}`}
      aria-busy={disabled}
    >
      <div ref={buttonRef} />
    </div>
  );
};

export default GoogleSignInButton;
