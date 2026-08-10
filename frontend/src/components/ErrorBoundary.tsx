import React, { Component } from 'react';
import type { ReactNode } from 'react';
import { TriangleAlert } from 'lucide-react';
import Button from './Button';

interface Props {
  children: ReactNode;
  // Al cambiar de ruta el error deja de ser relevante: sin esto una sola pantalla rota dejaba la
  // aplicación bloqueada y ningún enlace del menú volvía a mostrar contenido.
  resetKey?: string;
}

interface State {
  hasError: boolean;
  staleModule: boolean;
  resetKey?: string;
}

const reloadFlag = 'goisland.chunk-reload';

// Un solo intento por pestaña dejaba a quien no la cierra nunca sin recuperación automática: el
// segundo despliegue del día ya le mostraba el error. Se permiten unos pocos intentos seguidos,
// suficientes para tomar la versión publicada sin dejar la pestaña recargándose en bucle si el
// fallo resulta ser permanente, y el recuento caduca para que un fallo posterior empiece de cero.
const maxAutomaticReloads = 2;
const reloadWindowMs = 10 * 60_000;

// Tras un despliegue nuevo los fragmentos que la pestaña abierta todavía pide dejan de existir y
// cualquier página falla al cargarse. Recargar basta para tomar la versión publicada.
const isStaleModuleError = (error: Error): boolean => error.name === 'ChunkLoadError'
  || /dynamically imported module|importing a module script failed|failed to fetch dynamically/i
    .test(error.message);

const readReloadAttempts = (): { count: number; at: number } => {
  try {
    const stored = JSON.parse(window.sessionStorage.getItem(reloadFlag) ?? '') as unknown;
    if (stored && typeof stored === 'object') {
      const { count, at } = stored as { count?: unknown; at?: unknown };
      if (typeof count === 'number' && typeof at === 'number') return { count, at };
    }
  } catch {
    // Una marca ilegible equivale a no haber recargado todavía.
  }
  return { count: 0, at: 0 };
};

const reloadForStaleModule = (): boolean => {
  const attempts = readReloadAttempts();
  const expired = Date.now() - attempts.at > reloadWindowMs;
  const count = expired ? 0 : attempts.count;
  if (count >= maxAutomaticReloads) return false;

  window.sessionStorage.setItem(reloadFlag, JSON.stringify({ count: count + 1, at: Date.now() }));
  window.location.reload();
  return true;
};

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    staleModule: false,
  };

  public static getDerivedStateFromError(error: Error): Partial<State> {
    return { hasError: true, staleModule: isStaleModuleError(error) };
  }

  public static getDerivedStateFromProps(props: Props, state: State): Partial<State> | null {
    if (props.resetKey === state.resetKey) return null;
    return { hasError: false, staleModule: false, resetKey: props.resetKey };
  }

  public componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    console.error('ErrorBoundary caught an error:', error, errorInfo);

    if (isStaleModuleError(error)) reloadForStaleModule();
  }

  // Un fragmento que no se pudo descargar queda memorizado como fallido, así que volver a
  // renderizar la misma pantalla repite el error: la única salida es pedir el documento de nuevo.
  private handleRetry = () => {
    if (this.state.staleModule) {
      window.sessionStorage.removeItem(reloadFlag);
      window.location.reload();
      return;
    }

    this.setState({ hasError: false });
  };

  public render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="container error-boundary animate-fade-in">
          <div className="result-state surface-panel" role="alert">
            <TriangleAlert size={42} aria-hidden="true" />
            <h2>
              {this.state.staleModule
                ? 'No pudimos terminar de cargar esta página'
                : 'No pudimos mostrar esta página'}
            </h2>
            <p>
              {this.state.staleModule
                ? 'Puede que haya una versión más reciente de GoIsland o que la conexión se haya interrumpido. Vuelve a cargar la página para continuar.'
                : 'Ocurrió un problema al cargar el contenido. Puedes intentarlo de nuevo o volver al inicio.'}
            </p>
            <div className="error-boundary__actions">
              <Button onClick={this.handleRetry}>
                {this.state.staleModule ? 'Volver a cargar' : 'Reintentar'}
              </Button>
              <a className="button-link button-link--outline" href="/">Ir al inicio</a>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
