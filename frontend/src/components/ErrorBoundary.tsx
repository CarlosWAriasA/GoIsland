import React, { Component } from 'react';
import type { ReactNode } from 'react';
import { TriangleAlert } from 'lucide-react';
import Button from './Button';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
  };

  public static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  public componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    console.error('ErrorBoundary caught an error:', error, errorInfo);
  }

  private handleRetry = () => {
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
