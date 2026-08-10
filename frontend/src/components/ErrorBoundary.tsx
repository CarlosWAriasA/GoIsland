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
  resetKey?: string;
}

const reloadFlag = 'goisland.chunk-reload';

// Tras un despliegue nuevo los fragmentos que la pestaña abierta todavía pide dejan de existir y
// cualquier página falla al cargarse. Recargar una vez basta para tomar la versión publicada.
const isStaleModuleError = (error: Error): boolean => error.name === 'ChunkLoadError'
  || /dynamically imported module|importing a module script failed|failed to fetch dynamically/i
    .test(error.message);

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
  };

  public static getDerivedStateFromError(): Partial<State> {
    return { hasError: true };
  }

  public static getDerivedStateFromProps(props: Props, state: State): Partial<State> | null {
    if (props.resetKey === state.resetKey) return null;
    return { hasError: false, resetKey: props.resetKey };
  }

  // Una carga correcta demuestra que la pestaña ya tiene la versión publicada, así que se libera
  // el seguro y una caída posterior podrá recargar de nuevo.
  public componentDidMount(): void {
    if (!this.state.hasError) window.sessionStorage.removeItem(reloadFlag);
  }

  public componentDidUpdate(): void {
    if (!this.state.hasError) window.sessionStorage.removeItem(reloadFlag);
  }

  public componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    console.error('ErrorBoundary caught an error:', error, errorInfo);

    if (isStaleModuleError(error) && !window.sessionStorage.getItem(reloadFlag)) {
      window.sessionStorage.setItem(reloadFlag, 'true');
      window.location.reload();
    }
  }

  private handleRetry = () => {
    window.sessionStorage.removeItem(reloadFlag);
    this.setState({ hasError: false });
  };

  public render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="container error-boundary animate-fade-in">
          <div className="result-state surface-panel" role="alert">
            <TriangleAlert size={42} aria-hidden="true" />
            <h2>No pudimos mostrar esta página</h2>
            <p>
              Ocurrió un problema al cargar el contenido. Puedes intentarlo de nuevo o volver al inicio.
            </p>
            <div className="error-boundary__actions">
              <Button onClick={this.handleRetry}>Reintentar</Button>
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
